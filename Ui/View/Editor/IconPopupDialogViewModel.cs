using System;
using System.Windows;
using System.Windows.Interop;
using _1RM.Utils;
using Shawn.Utils.Wpf;
using System.Windows.Media.Imaging;
using _1RM.Resources.Icons;
using Shawn.Utils.Wpf.FileSystem;
using Shawn.Utils.Wpf.Image;

namespace _1RM.View.Editor
{
    public class IconPopupDialogViewModel : NotifyPropertyChangedBaseScreen
    {
        public IconPopupDialogViewModel(BitmapSource? icon = null)
        {
            var r = new Random();
            _icon = icon ?? ServerIcons.Instance.Icons[r.Next(0, ServerIcons.Instance.Icons.Count)];
        }

        private BitmapSource _icon;
        public BitmapSource Icon
        {
            get => _icon;
            set
            {
                _icon = value;
                RaisePropertyChanged();
            }
        }

        private BitmapSource? _selectedIcon;
        public BitmapSource? SelectedIcon
        {
            get => _selectedIcon;
            set
            {
                if (value != null
                    && SetAndNotify(ref _selectedIcon, value))
                {
                    Icon = value;
                }
            }
        }

        public void ServerIconsOnDoubleClick()
        {
            this.RequestClose(true);
        }


        private RelayCommand? _cmdSelectImage;
        public RelayCommand CmdSelectImage
        {
            get
            {
                return _cmdSelectImage ??= new RelayCommand((o) =>
                {
                    var path = SelectFileHelper.OpenFile(title: IoC.Translate("logo_selector_open_file_dialog_title"), filter: "image|*.jpg;*.png;*.bmp;*.ico;*.exe|all files|*.*");
                    if (path != null)
                    {
                        BitmapSource? img = null;
                        MsAppCenterHelper.TraceSpecial("SessionLogo", "");
                        if (path.EndsWith(".exe", true, null))
                        {
                            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
                            if (icon != null)
                            {
                                img = Imaging.CreateBitmapSourceFromHIcon(
                                    icon.Handle,
                                    new Int32Rect(0, 0, icon.Width, icon.Height),
                                    BitmapSizeOptions.FromEmptyOptions());
                            }
                        }
                        else if (path.EndsWith(".ico", true, null))
                        {
                            using var icon = new System.Drawing.Icon(path, -1, -1);
                            img = Imaging.CreateBitmapSourceFromHIcon(
                                icon.Handle,
                                new Int32Rect(0, 0, icon.Width, icon.Height),
                                BitmapSizeOptions.FromEmptyOptions());
                        }
                        else
                        {
                            img = NetImageProcessHelper.ReadImgFile(path)?.ToBitmapSource();
                        }
                        if (img != null)
                        {
                            SelectedIcon = null;
                            Icon = img;
                        }
                    }
                });
            }
        }


        private RelayCommand? _cmdSave;
        public RelayCommand CmdSave
        {
            get
            {
                return _cmdSave ??= new RelayCommand((o) =>
                {
                    this.RequestClose(true);
                });
            }
        }


        private RelayCommand? _cmdCancel;
        public RelayCommand CmdCancel
        {
            get
            {
                return _cmdCancel ??= new RelayCommand((o) =>
                {
                    this.RequestClose(false);
                });
            }
        }
    }
}
