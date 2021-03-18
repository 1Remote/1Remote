using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Shawn.Utils;
using Renci.SshNet;
using Renci.SshNet.Sftp;

namespace PRM.Core.Protocol.FileTransmit.Transmitters
{
    public class TransmitterSFtp : ITransmitter
    {
        public readonly string Hostname = "";
        public readonly int Port = 22;
        public readonly string Username = "";
        public readonly string Password = "";
        public readonly string SshKey = "";
        private SftpClient _sftp = null;

        public TransmitterSFtp(string host, int port, string username, string password)
        {
            Hostname = host;
            Port = port;
            Username = username;
            Password = password;
            SshKey = "";
            InitClient();
            //CheckMeAlive();
        }

        public TransmitterSFtp(string host, int port, string username, byte[] ppk)
        {
            Hostname = host;
            Port = port;
            Username = username;
            Password = "";
            SshKey = Encoding.ASCII.GetString(ppk);
            InitClient();
            //CheckMeAlive();
        }

        ~TransmitterSFtp()
        {
            _sftp?.Dispose();
            //_timerKeepAlive.Stop();
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
                return new TransmitterSFtp(Hostname, Port, Username, Password);
            else
                return new TransmitterSFtp(Hostname, Port, Username, Encoding.ASCII.GetBytes(SshKey));
        }

        public RemoteItem Get(string path)
        {
            lock (this)
            {
                return Exists(path) ? SftpFile2RemoteItem(_sftp?.Get(path)) : null;
            }
        }

        public List<RemoteItem> ListDirectoryItems(string path)
        {
            lock (this)
            {
                var ret = new List<RemoteItem>();
                IEnumerable<SftpFile> items = new List<SftpFile>();
                items = _sftp?.ListDirectory(path);
                if (items == null ||
                    !items.Any())
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

        private RemoteItem SftpFile2RemoteItem(SftpFile item)
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

                    var icon = TransmitItemIconCache.GetFileIcon("*");
                    if (item.Name.IndexOf(".", StringComparison.Ordinal) > 0)
                    {
                        var ext = item.Name.Substring(item.Name.LastIndexOf(".", StringComparison.Ordinal)).ToLower();
                        newItem.FileType = ext;
                        icon = TransmitItemIconCache.GetFileIcon(ext);
                    }
                    newItem.Icon = icon;
                }
                return newItem;
            }
        }

        public void Delete(string path)
        {
            lock (this)
            {
                var item = Get(path);
                if (item != null)
                {
                    if (item.IsDirectory)
                    {
                        var sub = _sftp?.ListDirectory(path);
                        foreach (var file in sub)
                        {
                            if (string.IsNullOrWhiteSpace(
                                file.Name
                                    .Replace('.', ' ')
                                    .Replace('\\', ' ')
                                    .Replace('/', ' ')))
                                continue;
                            Delete(file.FullName);
                        }

                        _sftp?.DeleteDirectory(path);
                    }
                    else
                        _sftp?.Delete(path);
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
            lock (this)
            {
                if (_sftp?.Exists(path) == false)
                    _sftp?.CreateDirectory(path);
            }
        }

        public void RenameFile(string path, string newPath)
        {
            if (path != newPath)
                lock (this)
                {
                    if (_sftp?.Exists(path) == true)
                        _sftp?.RenameFile(path, newPath);
                }
        }

        public void UploadFile(string localFilePath, string saveToRemotePath, Action<ulong> writeCallBack, CancellationToken cancellationToken)
        {
            var fi = new FileInfo(localFilePath);
            if (!fi.Exists)
                return;

            lock (this)
            {
                try
                {
                    // check parent
                    if (saveToRemotePath.LastIndexOf("/", StringComparison.Ordinal) > 0)
                    {
                        var parent = saveToRemotePath.Substring(0,
                            saveToRemotePath.LastIndexOf("/", StringComparison.Ordinal));
                        if (_sftp?.Exists(parent) == false)
                            _sftp?.CreateDirectory(parent);
                    }

                    using var fileStream = File.OpenRead(fi.FullName);
                    if (!fileStream.CanRead)
                        return;

                    _sftp?.UploadFile(fileStream, saveToRemotePath, obj =>
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            SimpleLogHelper.Debug("SFTP Upload: cancel by CancellationToken");
                            // TODO not a perfect solution
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
            lock (this)
            {
                try
                {
                    var fi = new FileInfo(saveToLocalPath);
                    if (fi.Exists)
                        fi.Delete();
                    if (!fi.Directory.Exists)
                        fi.Directory.Create();
                    using var fileStream = File.OpenWrite(saveToLocalPath);
                    if (!fileStream.CanWrite)
                        return;

                    _sftp?.DownloadFile(remoteFilePath, fileStream, obj =>
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
            _sftp.Disconnect();
            _sftp.Dispose();
        }

        private void InitClient()
        {
            lock (this)
            {
                if (_sftp?.IsConnected != true)
                {
                    _sftp?.Dispose();
                    if (string.IsNullOrEmpty(Password))
                    {
                        var pkf = new PrivateKeyFile(new MemoryStream(Encoding.ASCII.GetBytes(SshKey)), Password);
                        _sftp = new SftpClient(Hostname, Port, Username, pkf);
                    }
                    else
                    {
                        _sftp = new SftpClient(Hostname, Port, Username, Password);
                    }
                    //_sftp.KeepAliveInterval = new TimeSpan(0, 0, 10);
                    _sftp.Connect();
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