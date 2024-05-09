using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Media.Imaging;
using _1RM.Utils;
using Renci.SshNet;
using Renci.SshNet.Sftp;
using Shawn.Utils;

namespace _1RM.Model.Protocol.FileTransmit.Transmitters
{
    public class TransmitterSFtp : ITransmitter
    {
        public readonly string Hostname;
        public readonly int Port;
        public readonly string Username;
        public readonly string Password;
        public readonly string SshKeyPath;
        private SftpClient? _sftp = null;


        public TransmitterSFtp(string host, int port, string username, string key, bool keyIsPassword)
        {
            Hostname = host;
            Port = port;
            Username = username;
            if (keyIsPassword)
            {
                Password = key;
                SshKeyPath = "";
            }
            else
            {
                Password = "";
                SshKeyPath = key;
            }
            InitClient();
        }

        ~TransmitterSFtp()
        {
            lock (this)
            {
                _sftp?.Dispose();
            }
        }

        public void Conn()
        {
            lock (this)
            {
                InitClient();
            }
        }

        public bool IsConnected()
        {
            lock (this)
            {
                return _sftp?.IsConnected == true;
            }
        }

        public ITransmitter Clone()
        {
            if (!string.IsNullOrWhiteSpace(Password))
                return new TransmitterSFtp(Hostname, Port, Username, Password, true);
            else
                return new TransmitterSFtp(Hostname, Port, Username, SshKeyPath, false);
        }

        public RemoteItem? Get(string path)
        {
            if (_sftp == null) return null;
            lock (this)
            {
                return Exists(path) ? SftpFile2RemoteItem(_sftp.Get(path)) : null;
            }
        }

        public List<RemoteItem> ListDirectoryItems(string path)
        {
            if (_sftp == null) return new List<RemoteItem>();
            lock (this)
            {
                var ret = new List<RemoteItem>();
                IEnumerable<ISftpFile> items = new List<SftpFile>();
                items = _sftp.ListDirectory(path);
                if (items == null || !items.Any())
                    return ret;

                items = items.OrderBy(x => x.Name);
                foreach (var item in items)
                {
                    if (item.Name == "." || item.Name == "..")
                        continue;
                    ret.Add(SftpFile2RemoteItem(item));
                }
                return ret;
            }
        }

        public bool Exists(string path)
        {
            lock (this)
            {
                return _sftp?.Exists(path) == true;
            }
        }

        private RemoteItem SftpFile2RemoteItem(ISftpFile item)
        {
            lock (this)
            {
                var fn = item.FullName;
                var newItem = new RemoteItem()
                {
                    Icon = null,
                    IsDirectory = item.IsDirectory,
                    Name = item.Name,
                    FullName = fn,
                    LastUpdate = item.LastWriteTime,
                    ByteSize = (ulong)item.Length,
                };
                if (item.IsDirectory)
                {
                    newItem.Icon = TransmitItemIconCache.GetDictIcon();
                    newItem.ByteSize = 0;
                    newItem.FileType = "folder";
                    if (item.IsSymbolicLink)
                        newItem.Icon = TransmitItemIconCache.GetDictIcon(Environment.GetFolderPath(Environment.SpecialFolder.Favorites));
                }
                else
                {
                    if (item.IsSymbolicLink)
                        newItem.FileType = ".lnk";

                    BitmapSource icon;
                    if (item.Name.IndexOf(".", StringComparison.Ordinal) > 0)
                    {
                        var ext = item.Name.Substring(item.Name.LastIndexOf(".", StringComparison.Ordinal)).ToLower();
                        newItem.FileType = ext;
                        icon = TransmitItemIconCache.GetFileIcon(ext);
                    }
                    else
                    {
                        icon = TransmitItemIconCache.GetFileIcon();
                    }
                    newItem.Icon = icon;
                }
                return newItem;
            }
        }

        public void Delete(string path)
        {
            if (_sftp == null) return;
            lock (this)
            {
                var item = Get(path);
                if (item != null)
                {
                    if (item.IsDirectory)
                    {
                        var sub = _sftp.ListDirectory(path) ?? new List<SftpFile>();
                        foreach (var file in sub)
                        {
                            if (string.IsNullOrWhiteSpace(
                                    file.Name
                                        .Replace('.', ' ')
                                        .Replace('\\', ' ')
                                        .Replace('/', ' ')))
                                continue;
                            Delete((string)file.FullName);
                        }

                        _sftp.DeleteDirectory(path);
                    }
                    else
                        _sftp.Delete(path);
                }
            }
        }

        public void Delete(RemoteItem item)
        {
            lock (this)
            {
                Delete(item.FullName);
            }
        }

        public void CreateDirectory(string path)
        {
            if (_sftp == null) return;
            lock (this)
            {
                if (_sftp.Exists(path) == false)
                    _sftp.CreateDirectory(path);
            }
        }

        public void RenameFile(string path, string newPath)
        {
            if (_sftp == null) return;
            if (path != newPath)
                lock (this)
                {
                    if (_sftp.Exists(path) == true)
                        _sftp.RenameFile(path, newPath);
                }
        }

        public void UploadFile(string localFilePath, string saveToRemotePath, Action<ulong> writeCallBack, CancellationToken cancellationToken)
        {
            var fi = new FileInfo(localFilePath);
            if (fi?.Exists != true)
                return;

            if (_sftp == null) return;
            lock (this)
            {
                try
                {
                    // check parent
                    if (saveToRemotePath.LastIndexOf("/", StringComparison.Ordinal) > 0)
                    {
                        var parent = saveToRemotePath.Substring(0,
                            saveToRemotePath.LastIndexOf("/", StringComparison.Ordinal));
                        if (_sftp.Exists(parent) == false)
                            _sftp.CreateDirectory(parent);
                    }

                    using var fileStream = File.OpenRead(fi.FullName);
                    if (!fileStream.CanRead)
                        return;

                    _sftp.UploadFile(fileStream, saveToRemotePath, obj =>
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            SimpleLogHelper.Debug("SFTP Upload: cancel by CancellationToken");
                            fileStream.Close();
                            fileStream.Dispose();
                        }
                        writeCallBack?.Invoke(obj);
                    });
                }
                catch (Exception)
                {
                    if (cancellationToken.IsCancellationRequested == false)
                        throw;
                }
            }
        }

        public void DownloadFile(string remoteFilePath, string saveToLocalPath, Action<ulong> readCallBack, CancellationToken cancellationToken)
        {
            if (_sftp == null) return;
            lock (this)
            {
                try
                {
                    var fi = new FileInfo(saveToLocalPath);
                    if (fi.Exists)
                        fi.Delete();
                    if (fi?.Directory?.Exists == false)
                        fi.Directory.Create();
                    using var fileStream = File.OpenWrite(saveToLocalPath);
                    if (!fileStream.CanWrite)
                        return;

                    _sftp.DownloadFile(remoteFilePath, fileStream, obj =>
                    {
                        if (cancellationToken.IsCancellationRequested)
                            fileStream.Close();
                        readCallBack?.Invoke(obj);
                    });
                }
                catch (Exception)
                {
                    if (cancellationToken.IsCancellationRequested == false)
                        throw;
                }
            }
        }

        public void Release()
        {
            _sftp?.Disconnect();
            _sftp?.Dispose();
        }

        private void InitClient()
        {
            lock (this)
            {
                if (_sftp?.IsConnected != true)
                {
                    RetryHelper.Try(() =>
                    {
                        _sftp?.Dispose();
                        if (string.IsNullOrEmpty(Password)
                            && string.IsNullOrEmpty(SshKeyPath) == false
                            && File.Exists(SshKeyPath))
                        {
                            try
                            {
                                var connectionInfo = new ConnectionInfo(Hostname, Port, Username, new PrivateKeyAuthenticationMethod(Username, new PrivateKeyFile(SshKeyPath)));
                                _sftp = new SftpClient(connectionInfo);
                            }
                            catch (Exception e)
                            {
                                MsAppCenterHelper.Error(e);
                            }
                        }
                        _sftp ??= new SftpClient(new ConnectionInfo(Hostname, Port, Username, new PasswordAuthenticationMethod(Username, Password)));
                        //_sftp.KeepAliveInterval = new TimeSpan(0, 0, 10);
                        _sftp.Connect();
                    });
                }
            }
        }

        private readonly System.Timers.Timer _timerKeepAlive = new System.Timers.Timer();

        private void CheckMeAlive()
        {
            _timerKeepAlive.Interval = 10 * 1000;
            _timerKeepAlive.AutoReset = false;
            _timerKeepAlive.Elapsed += (sender, args) =>
            {
                InitClient();
                lock (this)
                {
                    _sftp?.ListDirectory("/");
                }
                _timerKeepAlive.Interval = 10 * 1000;
                _timerKeepAlive.Start();
            };
            _timerKeepAlive.Start();
        }
    }
}