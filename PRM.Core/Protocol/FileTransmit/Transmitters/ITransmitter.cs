using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media.Imaging;
using PRM.Core.Protocol.FileTransmitter;
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
        List<RemoteItem> ListDirectoryItems(string path);
        bool Exists(string path);
        void ChangeDirectory(string path);
        void Delete(string path);
        void Delete(RemoteItem item);
        void CreateDirectory(string path);
        void RenameFile(string path, string newPath);
        /// <summary>
        /// write fileStream to server path
        /// </summary>
        /// <param name="fileStream"></param>
        /// <param name="path"></param>
        /// <param name="writeCallBack">callback will offer data length has been wirte</param>
        void UploadFile(Stream fileStream, string path, Action<ulong> writeCallBack = null);
        /// <summary>
        /// read server path and wirte to local fileStream\
        /// </summary>
        /// <param name="fileStream"></param>
        /// <param name="path"></param>
        /// <param name="readCallBack">callback will offer data length has been wirte</param>
        void DownloadFile(string path, Stream fileStream, Action<ulong> readCallBack = null);
    }
}
