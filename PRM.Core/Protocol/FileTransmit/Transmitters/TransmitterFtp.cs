using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentFTP;

namespace PRM.Core.Protocol.FileTransmit.Transmitters
{
    public class TransmitterFtp : ITransmitter
    {
        public readonly string Hostname = "";
        public readonly int Port = 21;
        public readonly string Username = "";
        public readonly string Password = "";
        private FtpClient _ftp = null;

        public TransmitterFtp(string host, int port, string username, string password)
        {
            Hostname = host;
            Port = port;
            Username = username;
            Password = password;
            InitClient();
            CheckMeAlive();
        }

        ~TransmitterFtp()
        {
            _ftp?.Dispose();
            _timerKeepAlive.Stop();
        }

        public void Conn()
        {
            InitClient();
        }

        public bool IsConnected()
        {
            lock (this)
            {
                return _ftp?.IsConnected == true;
            }
        }

        public ITransmitter Clone()
        {
            return new TransmitterFtp(Hostname, Port, Username, Password);
        }

        public RemoteItem Get(string path)
        {
            lock (this)
            {
                return Exists(path) ? FtpListItem2RemoteItem(_ftp.GetObjectInfo(path)) : null;
            }
        }

        public List<RemoteItem> ListDirectoryItems(string path)
        {
            lock (this)
            {
                var ret = new List<RemoteItem>();
                IEnumerable<FtpListItem> items = _ftp.GetListing(path);
                if (items == null ||
                    !items.Any())
                    return ret;

                items = items.OrderBy(x => x.Name);
                foreach (var item in items)
                {
                    if (item.Name == "." || item.Name == "..")
                        continue;
                    ret.Add(FtpListItem2RemoteItem(item));
                }
                return ret;
            }
        }

        public bool Exists(string path)
        {
            lock (this)
            {
                if (_ftp.FileExists(path))
                    return true;
                if (_ftp.DirectoryExists(path))
                    return true;
                return false;
            }
        }

        private RemoteItem FtpListItem2RemoteItem(FtpListItem item)
        {
            lock (this)
            {
                var fn = item.FullName;
                var newItem = new RemoteItem()
                {
                    Icon = null,
                    IsDirectory = item.Type == FtpFileSystemObjectType.Directory,
                    Name = item.Name,
                    FullName = fn,
                    LastUpdate = item.RawModified,
                    ByteSize = (ulong)item.Size,
                    IsSymlink = item.Type == FtpFileSystemObjectType.Link,
                };
                if (item.Type == FtpFileSystemObjectType.Directory)
                {
                    if (newItem.IsSymlink)
                    {
                        newItem.Icon = TransmitItemIconCache.GetDictIcon(Environment.GetFolderPath(Environment.SpecialFolder.Favorites));
                    }
                    else
                    {
                        newItem.Icon = TransmitItemIconCache.GetDictIcon();
                    }
                    newItem.ByteSize = 0;
                    newItem.FileType = "folder";
                }
                else
                {
                    if (item.Name.IndexOf(".", StringComparison.Ordinal) > 0)
                    {
                        var ext = item.Name.Substring(item.Name.LastIndexOf(".", StringComparison.Ordinal)).ToLower();
                        newItem.FileType = ext;
                        newItem.Icon = TransmitItemIconCache.GetFileIcon(ext);
                    }
                    else
                    {
                        newItem.Icon = TransmitItemIconCache.GetFileIcon("*");
                    }
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
                        _ftp.DeleteDirectory(path);
                    }
                    else
                        _ftp.DeleteFile(path);
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
                if (_ftp?.DirectoryExists(path) == false)
                    _ftp?.CreateDirectory(path);
            }
        }

        public void RenameFile(string path, string newPath)
        {
            if (path != newPath)
                if (Exists(path))
                    lock (this)
                    {
                        _ftp?.Rename(path, newPath);
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
                        if (_ftp?.DirectoryExists(parent) == false)
                            _ftp?.CreateDirectory(parent);
                    }

                    // Todo ADD resume;

                    //using var fileStream = File.OpenRead(localFilePath);
                    //_ftp?.Upload(fileStream, saveToRemotePath, FtpRemoteExists.Overwrite, true, progress =>
                    //{
                    //    if (cancellationToken.IsCancellationRequested)
                    //    {
                    //        SimpleLogHelper.Debug("FTP Upload: cancel by CancellationToken");
                    //        // not a perfect solution
                    //        fileStream.Close();
                    //        fileStream.Dispose();
                    //    }
                    //    writeCallBack?.Invoke((ulong)progress.TransferredBytes);
                    //});

                    _ftp?.UploadFileAsync(localFilePath, saveToRemotePath, FtpRemoteExists.Overwrite, true, FtpVerify.Delete,
                        new Progress<FtpProgress>(progress =>
                        {
                            writeCallBack?.Invoke((ulong)progress.TransferredBytes);
                        }), cancellationToken).Wait(cancellationToken);
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
            try
            {
                var t = _ftp.DownloadFileAsync(saveToLocalPath, remoteFilePath, FtpLocalExists.Overwrite, FtpVerify.None,
                    new Progress<FtpProgress>(progress =>
                    {
                        readCallBack?.Invoke((ulong)progress.TransferredBytes);
                    }), cancellationToken);
                t.Wait(cancellationToken);
            }
            catch (Exception)
            {
                if (cancellationToken.IsCancellationRequested == false)
                    throw;
            }
        }

        public void Release()
        {
            _ftp?.Disconnect();
            _ftp?.Dispose();
        }

        private void InitClient()
        {
            lock (this)
            {
                if (_ftp?.IsConnected != true)
                {
                    _ftp?.Dispose();
                    _ftp = new FtpClient(Hostname, Port, Username, Password);
                    //_ftp.Credentials = new NetworkCredential(Username, Password);
                    _ftp.Connect();
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
                    _ftp.GetListing("/");
                }

                try
                {
                    _timerKeepAlive.Interval = 10 * 1000;
                    _timerKeepAlive?.Start();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            };
            _timerKeepAlive.Start();
        }
    }
}