using System;
using System.IO;

namespace PRM.Core.Protocol.FileTransmit.Transmitters.TransmissionController
{
    public class TransmitItem : NotifyPropertyChangedBase
    {
        public TransmitItem()
        {

        }

        public TransmitItem(RemoteItem remoteItem, string dstPath)
        {
            TransmissionType = ETransmissionType.ServerToHost;
            SrcPath = remoteItem.FullName;
            SrcDirectoryPath = SrcPath.LastIndexOf("/", StringComparison.Ordinal) > 0
                ? SrcPath.Substring(0, SrcPath.LastIndexOf("/", StringComparison.Ordinal))
                : "/";
            ItemName = remoteItem.Name;
            IsDirectory = remoteItem.IsDirectory;
            ByteSize = remoteItem.ByteSize;
            DstPath = dstPath;
            if(IsDirectory)
                DstDirectoryPath = new DirectoryInfo(dstPath)?.Parent?.FullName ?? "";
            else
                DstDirectoryPath = new FileInfo(dstPath)?.Directory?.FullName ?? "";
        }

        public TransmitItem(FileInfo fi, string dstPath)
        {
            TransmissionType = ETransmissionType.HostToServer;
            SrcPath = fi.FullName;
            SrcDirectoryPath = fi.Directory?.FullName ?? "";
            ItemName = fi.Name;
            IsDirectory = false;
            ByteSize = (ulong)fi.Length;
            DstPath = dstPath;
            DstDirectoryPath = dstPath.LastIndexOf("/", StringComparison.Ordinal) > 0
                ? dstPath.Substring(0, dstPath.LastIndexOf("/", StringComparison.Ordinal))
                : "/";
        }

        public TransmitItem(DirectoryInfo di, string dstPath)
        {
            TransmissionType = ETransmissionType.HostToServer;
            SrcPath = di.FullName;
            SrcDirectoryPath = di.Parent?.FullName ?? "";
            ItemName = di.Name;
            IsDirectory = true;
            ByteSize = 0;
            DstPath = dstPath;
            DstDirectoryPath = dstPath.LastIndexOf("/", StringComparison.Ordinal) > 0
                ? dstPath.Substring(0, dstPath.LastIndexOf("/", StringComparison.Ordinal))
                : "/";
        }

        private ETransmissionType _transmissionType;
        public ETransmissionType TransmissionType
        {
            get => _transmissionType;
            protected set => SetAndNotifyIfChanged(nameof(TransmissionType), ref _transmissionType, value);
        }

        public string ItemName { get;private set; }

        private string _srcPath;
        public string SrcPath
        {
            get => _srcPath;
            protected set => SetAndNotifyIfChanged(nameof(SrcPath), ref _srcPath, value);
        }


        private string _srcDirectoryPath;
        public string SrcDirectoryPath
        {
            get => _srcDirectoryPath;
            protected set => SetAndNotifyIfChanged(nameof(SrcDirectoryPath), ref _srcDirectoryPath, value);
        }


        private string _dstPath;
        public string DstPath
        {
            get => _dstPath;
            protected set => SetAndNotifyIfChanged(nameof(DstPath), ref _dstPath, value);
        }


        private string _dstDirectoryPath;
        public string DstDirectoryPath
        {
            get => _dstDirectoryPath;
            protected set => SetAndNotifyIfChanged(nameof(DstPath), ref _dstDirectoryPath, value);
        }



        private bool _isDirectory = false;
        public bool IsDirectory
        {
            get => _isDirectory;
            set => SetAndNotifyIfChanged(nameof(IsDirectory), ref _isDirectory, value);
        }

        private ulong _byteSize = 0;
        public ulong ByteSize
        {
            get => _byteSize;
            set => SetAndNotifyIfChanged(nameof(ByteSize), ref _byteSize, value);
        }

        public ulong TransmittedSize { get; set; } = 0;
    }
}
