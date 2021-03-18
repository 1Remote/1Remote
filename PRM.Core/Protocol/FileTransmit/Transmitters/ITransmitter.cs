using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Shawn.Utils;

namespace PRM.Core.Protocol.FileTransmit.Transmitters
{
    public static class TransmitItemIconCache
    {
        private static readonly Dictionary<string, BitmapImage> _fileIcons = new Dictionary<string, BitmapImage>();
        private static readonly Dictionary<string, BitmapImage> _dictIcons = new Dictionary<string, BitmapImage>();
        private static readonly object _locker = new object();

        public static BitmapImage GetFileIcon(string key = "*")
        {
            lock (_locker)
            {
                if (!_fileIcons.ContainsKey("*"))
                    _fileIcons.Add("*", SystemIconHelper.GetFileIconByExt("*").ToBitmapImage());
                if (_fileIcons.ContainsKey(key))
                    return _fileIcons[key];
                var icon = SystemIconHelper.GetFileIconByExt(key).ToBitmapImage();
                _fileIcons.Add(key, icon);
                return icon;
            }
        }

        public static BitmapImage GetDictIcon(string key = "")
        {
            lock (_locker)
            {
                if (!_dictIcons.ContainsKey(""))
                    _dictIcons.Add("", SystemIconHelper.GetFolderIcon().ToBitmapImage());
                if (_dictIcons.ContainsKey(key))
                    return _dictIcons[key];
                var icon = SystemIconHelper.GetFolderIcon(key).ToBitmapImage();
                _dictIcons.Add(key, icon);
                return icon;
            }
        }
    }

    public interface ITransmitter
    {
        void Conn();

        bool IsConnected();

        ITransmitter Clone();

        RemoteItem Get(string path);

        /// <summary>
        /// get items of a directory, sub-directory treat as a item too
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        List<RemoteItem> ListDirectoryItems(string path);

        bool Exists(string path);

        void Delete(string path);

        void Delete(RemoteItem item);

        void CreateDirectory(string path);

        void RenameFile(string path, string newPath);

        /// <summary>
        /// write local file to server path
        /// </summary>
        /// <param name="localFilePath"></param>
        /// <param name="saveToRemotePath"></param>
        /// <param name="writeCallBack">callback will offer data length has been written</param>
        /// <param name="cancellationToken"></param>
        void UploadFile(string localFilePath, string saveToRemotePath, Action<ulong> writeCallBack, CancellationToken cancellationToken);

        /// <summary>
        /// read server path and written to local fileStream\
        /// </summary>
        /// <param name="remoteFilePath"></param>
        /// <param name="saveToLocalPath"></param>
        /// <param name="readCallBack">callback will offer data length has been written</param>
        /// <param name="cancellationToken"></param>
        void DownloadFile(string remoteFilePath, string saveToLocalPath, Action<ulong> readCallBack, CancellationToken cancellationToken);

        void Release();
    }
}