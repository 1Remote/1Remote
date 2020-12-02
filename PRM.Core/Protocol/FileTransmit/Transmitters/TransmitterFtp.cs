using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentFTP;
using PRM.Core.Protocol.FileTransmit.Transmitters;
using Renci.SshNet;
using Renci.SshNet.Sftp;

namespace PRM.Core.Protocol.FileTransmitter
{
    public class TransmitterFtp : ITransmitter
    {
        public readonly string Hostname = "";
        public readonly int Port = 21;
        public readonly string Username = "";
        public readonly string Password = "";
        private FtpClient _ftp = null;

        private readonly object _locker = new object();

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
            lock (_locker)
            {
                _ftp?.Dispose();
            }
            _timerKeepAlive.Stop();
        }

        public void Conn()
        {
            InitClient();
        }

        public bool IsConnected()
        {
            lock (_locker)
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
            lock (_locker)
            {
                return Exists(path) ? FtpListItem2RemoteItem(_ftp.GetObjectInfo(path)) : null;
            }
        }

        public List<RemoteItem> ListDirectoryItems(string path)
        {
            var ret = new List<RemoteItem>();
            IEnumerable<FtpListItem> items = new List<FtpListItem>();
            lock (_locker)
            {
                items = _ftp.GetListing(path);
            }
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

        public bool Exists(string path)
        {
            lock (_locker)
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

        public void Delete(string path)
        {
            lock (_locker)
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
            Delete(item.FullName);
        }

        public void CreateDirectory(string path)
        {
            lock (_locker)
            {
                _ftp.CreateDirectory(path);
            }
        }

        public void RenameFile(string path, string newPath)
        {
            lock (_locker)
            {
                if (Exists(path))
                    _ftp.Rename(path, newPath);
            }
        }

        public void UploadFile(string localFilePath, string saveToRemotePath, Action<ulong> writeCallBack = null)
        {
            lock (_locker)
            {
                var fi = new FileInfo(localFilePath);
                if (!fi.Exists)
                    return;

                // Todo ADD resume;
                //client.UploadFile(fileStream, path, writeCallBack);
                _ftp.UploadFile(
                    localFilePath, 
                    saveToRemotePath, 
                    FtpRemoteExists.Overwrite,
                    true,
                    FtpVerify.Delete, progress =>
                {
                    writeCallBack?.Invoke((ulong)progress.TransferredBytes);
                });
            }
        }

        public void DownloadFile(string remoteFilePath, string saveToLocalPath, Action<ulong> readCallBack = null)
        {
            lock (_locker)
            {
                _ftp.DownloadFile(saveToLocalPath, remoteFilePath, FtpLocalExists.Overwrite, FtpVerify.None,
                    progress =>
                    {
                        readCallBack?.Invoke((ulong)progress.TransferredBytes);
                    });
            }
        }

        public void Release()
        {
            // TODO stop UploadFile DownloadFile
            _ftp?.Disconnect();
            _ftp?.Dispose();
        }


        //public async Task ReadItemAsync(string path, Stream fileStream, Action<ulong> readCallBack = null)
        //{
        //    if(!fileStream.CanWrite)
        //        return;
        //    _sftp.DownloadFile(path, fileStream, readCallBack);
        //}

        private void InitClient()
        {
            lock (_locker)
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
                lock (_locker)
                {
                    _ftp.GetListing("/");
                }
                _timerKeepAlive.Interval = 10 * 1000;
                _timerKeepAlive.Start();
            };
            _timerKeepAlive.Start();
        }
    }
}
