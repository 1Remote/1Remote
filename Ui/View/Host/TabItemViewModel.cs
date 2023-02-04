using System;
using System.Diagnostics;
using PRM.Service;
using PRM.Utils;
using PRM.View.Host.ProtocolHosts;
using Shawn.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;

namespace PRM.View.Host
{
    public class TabItemViewModel : NotifyPropertyChangedBase
    {
        public TabItemViewModel(HostBase hostBase, object header)
        {
            Content = hostBase;
            Header = header;
            ColorHex = hostBase.ProtocolServer.ColorHex;
            IconImg = hostBase.ProtocolServer.IconImg;
            CanResizeNow = hostBase.CanResizeNow();
            hostBase.OnCanResizeNowChanged += () => CanResizeNow = hostBase.CanResizeNow();
        }

        public object Header { get; }
        public HostBase Content { get; }
        public HostBase Host => Content;
        private bool _canResizeNow = false;
        public bool CanResizeNow
        {
            get => _canResizeNow;
            set => SetAndNotifyIfChanged(ref _canResizeNow, value);
        }
        /// <summary>
        /// tab title mark color
        /// </summary>
        public string ColorHex { get; }
        public System.Windows.Media.Imaging.BitmapSource? IconImg { get; }



        private RelayCommand? _cmdReconnect;
        public RelayCommand CmdReconnect
        {
            get
            {
                return _cmdReconnect ??= new RelayCommand((o) =>
                {
                    Content.ReConn();
                });
            }
        }
    }
}