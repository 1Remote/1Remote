using System;
using System.Windows.Media.Imaging;

namespace PRM.Core.Protocol.FileTransmit.Transmitters
{
    public class RemoteItem : NotifyPropertyChangedBase
    {
        private bool _isSelected = false;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetAndNotifyIfChanged(nameof(IsSelected), ref _isSelected, value);
        }

        private bool _isRenaming = false;
        public bool IsRenaming
        {
            get => _isRenaming;
            set => SetAndNotifyIfChanged(nameof(IsRenaming), ref _isRenaming, value);
        }

        private BitmapSource _icon;
        public BitmapSource Icon
        {
            get => _icon;
            set => SetAndNotifyIfChanged(nameof(Icon), ref _icon, value);
        }

        private string _name;
        public string Name
        {
            get => _name;
            set => SetAndNotifyIfChanged(nameof(Name), ref _name, value);
        }

        private string _fullName;
        public string FullName
        {
            get => _fullName;
            set => SetAndNotifyIfChanged(nameof(FullName), ref _fullName, value);
        }

        private string _fileType;
        public string FileType
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_fileType)) return _fileType;
                if (IsDirectory)
                {
                    FileType = "folder";
                }
                else if (_fullName.IndexOf(".", StringComparison.Ordinal) > 0)
                {
                    var ext = _fullName.Substring(_fullName.LastIndexOf(".", StringComparison.Ordinal)).ToLower();
                    FileType = ext;
                }
                return _fileType;
            }
            set => SetAndNotifyIfChanged(nameof(FileType), ref _fileType, value);
        }

        private ulong _byteSize = 0;
        public ulong ByteSize
        {
            get => _byteSize;
            set => SetAndNotifyIfChanged(nameof(ByteSize), ref _byteSize, value);
        }


        private bool _isDirectory = false;
        public bool IsDirectory
        {
            get => _isDirectory;
            set => SetAndNotifyIfChanged(nameof(IsDirectory), ref _isDirectory, value);
        }

        private bool _isSymlink = false;
        public bool IsSymlink
        {
            get => _isSymlink;
            set => SetAndNotifyIfChanged(nameof(IsSymlink), ref _isSymlink, value);
        }

        private DateTime _lastUpdate;
        public DateTime LastUpdate
        {
            get => _lastUpdate;
            set => SetAndNotifyIfChanged(nameof(LastUpdate), ref _lastUpdate, value);
        }
    }
}
