using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using PRM.Core.Model;
using PRM.Core.Protocol.FileTransmit.FTP;
using PRM.Core.Protocol.FileTransmit.SFTP;

namespace PRM.Core.Protocol.FileTransmit.Host
{
    public partial class FileTransmitHost : ProtocolHostBase, IDisposable
    {
        private readonly VmFileTransmitHost _vmRemote;
        public FileTransmitHost(PrmContext context, ProtocolServerBase protocolServer) : base(context, protocolServer, false)
        {
            InitializeComponent();
            Focusable = true;
            Loaded += (s, e) => Keyboard.Focus(this);

            if (protocolServer is ProtocolServerSFTP protocolServerSftp)
            {
                _vmRemote = new VmFileTransmitHost(context, protocolServerSftp);
            }
            else if (protocolServer is ProtocolServerFTP protocolServerFtp)
            {
                _vmRemote = new VmFileTransmitHost(context, protocolServerFtp);
            }
            else
                throw new ArgumentException($"Send {protocolServer.GetType()} to {nameof(FileTransmitHost)}!");

            DataContext = _vmRemote;

            _vmRemote.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(VmFileTransmitHost.SelectedRemoteItem))
                {
                    TvFileList.ScrollIntoView(TvFileList.SelectedItem);
                }
            };
        }

        #region Base Interface
        public override void Conn()
        {
            _vmRemote?.Conn();
        }

        public override void Close()
        {
            _vmRemote?.Release();
            base.Close();
        }

        public override void GoFullScreen()
        {
            throw new NotImplementedException();
        }

        public override void MakeItFocus()
        {
            Dispatcher.Invoke(() =>
            {
                TvFileList.Focus();
            });
        }

        public override ProtocolHostType GetProtocolHostType()
        {
            return ProtocolHostType.Native;
        }

        public override IntPtr GetHostHwnd()
        {
            return IntPtr.Zero;
        }

        #endregion

        private void TvFileList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            _vmRemote.CmdListViewDoubleClick.Execute();
        }

        private void TextBox_OnGotFocus(object sender, RoutedEventArgs e)
        {
            var textBox = (TextBox)sender;
            textBox.Dispatcher.BeginInvoke(new Action(() => textBox.SelectAll()));
        }


        /// <summary>
        /// right key to show up menu
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FileList_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            _vmRemote.FileList_OnPreviewMouseRightButtonDown(sender, e);
        }

        private void TvFileList_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _vmRemote.TvFileList_OnPreviewMouseDown(sender, e);
        }

        private void TvFileList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _vmRemote.CmdEndRenaming.Execute();
        }


        private void ListViewColumnHeaderClick(object sender, RoutedEventArgs e)
        {
            var headerClicked = e.OriginalSource as GridViewColumnHeader;
            if (headerClicked == null)
                return;
            var p = headerClicked.Parent as GridViewHeaderRowPresenter;
            foreach (var t in p.Columns)
            {
                t.HeaderTemplate = TvFileList.Resources["HeaderTemplateDefault"] as DataTemplate;
            }

            if (headerClicked.Role == GridViewColumnHeaderRole.Padding)
                return;


            if (int.TryParse(headerClicked.CommandParameter.ToString(), out var ot))
            {
                // cancel order
                if (_vmRemote.RemoteItemsOrderBy == (ot + 1))
                {
                    ot = -1;
                    headerClicked.Column.HeaderTemplate = TvFileList.Resources["HeaderTemplateDefault"] as DataTemplate;
                }
                else
                if (_vmRemote.RemoteItemsOrderBy == ot)
                {
                    ++ot;
                    headerClicked.Column.HeaderTemplate = TvFileList.Resources["HeaderTemplateArrowUp"] as DataTemplate;
                }
                else
                {
                    //ot = ot;
                    headerClicked.Column.HeaderTemplate = TvFileList.Resources["HeaderTemplateArrowDown"] as DataTemplate;
                }
                _vmRemote.RemoteItemsOrderBy = ot;
            }
        }

        private void TvFileList_OnKeyDown(object sender, KeyEventArgs e)
        {
            lock (this)
            {
                if (_vmRemote.RemoteItems.Count == 0)
                    return;

                switch (e.Key)
                {
                    case Key.Up:
                        {
                            e.Handled = true;
                            var i = _vmRemote.RemoteItems.IndexOf(_vmRemote.SelectedRemoteItem);
                            if (i > 0)
                                _vmRemote.SelectedRemoteItem = _vmRemote.RemoteItems[i - 1];
                            return;
                        }
                    case Key.Down:
                        {
                            e.Handled = true;
                            var i = _vmRemote.RemoteItems.IndexOf(_vmRemote.SelectedRemoteItem);
                            if (i + 1 < _vmRemote.RemoteItems.Count)
                                _vmRemote.SelectedRemoteItem = _vmRemote.RemoteItems[i + 1];
                            return;
                        }
                }

                if (_vmRemote.RemoteItems.Any(x => x.IsRenaming))
                    return;

                // prevent shortcut like 'ctrl + s' trigger selection change
                if (Keyboard.IsKeyDown(Key.LeftCtrl)
                    || Keyboard.IsKeyDown(Key.RightCtrl)
                    || Keyboard.IsKeyDown(Key.LeftAlt)
                    || Keyboard.IsKeyDown(Key.RightAlt))
                    return;


                // get keydown value and select item name start with this value
                var key = e.Key.ToString();
                if (key.Length == 1)
                {
                    if (_vmRemote?.SelectedRemoteItem != null
                        && _vmRemote.SelectedRemoteItem.Name.StartsWith(key, true, CultureInfo.CurrentCulture))
                    {
                        var i = _vmRemote.RemoteItems.IndexOf(_vmRemote.SelectedRemoteItem);
                        for (int j = i + 1; j < _vmRemote.RemoteItems.Count; j++)
                        {
                            if (_vmRemote.RemoteItems[j].Name.StartsWith(key, true, CultureInfo.CurrentCulture))
                            {
                                _vmRemote.SelectedRemoteItem = _vmRemote.RemoteItems[j];
                                return;
                            }
                        }
                    }

                    if (_vmRemote.RemoteItems.Any(x => x.Name.StartsWith(key, true, CultureInfo.CurrentCulture)))
                    {
                        var item = _vmRemote.RemoteItems.First(x => x.Name.StartsWith(key, true, CultureInfo.CurrentCulture));
                        _vmRemote.SelectedRemoteItem = item;
                    }
                }
            }
        }

        public override void ReConn()
        {
            _vmRemote?.Conn();
        }

        public void Dispose()
        {
            _vmRemote?.Release();
        }
    }


    [ValueConversion(typeof(long), typeof(string))]
    public class ByteLength2ReadableStringConverter : IValueConverter
    {
        #region IValueConverter
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null)
                return "";
            ulong ss = (ulong)value;
            if (ss < 1024)
                return ss + " Bytes";
            else if (ss < 1024 * 1024)
                return (ss / 1024.0).ToString("F2") + " KB";
            else if (ss < (ulong)1024 * 1024 * 1024)
                return (ss / 1024.0 / 1024).ToString("F2") + " MB";
            else // if (ss < (long)1024 * 1024 * 1024 * 1024)
                return (ss / 1024.0 / 1024 / 1024).ToString("F2") + " GB";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return 0;
        }
        #endregion
    }




    [ValueConversion(typeof(long), typeof(string))]
    public class WidthSub5 : IValueConverter
    {
        #region IValueConverter
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null)
                return "";
            double w = (double)value;
            if (w > 100)
            {
                return w - 16;
            }
            return w;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return 0;
        }
        #endregion
    }
}
