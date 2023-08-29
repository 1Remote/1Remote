using System;
using System.Diagnostics;
using _1RM.View.Host.ProtocolHosts;
using Shawn.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;

namespace _1RM.View.Host
{
    public class TabItemViewModel : NotifyPropertyChangedBase
    {
        public TabItemViewModel(HostBase hostBase, string displayName)
        {
            Content = hostBase;
            DisplayName = displayName;
            ColorHex = hostBase.ProtocolServer.ColorHex;
            IconImg = hostBase.ProtocolServer.IconImg;
        }

        public string DisplayName { get; }
        public HostBase Content { get; }
        public HostBase Host => Content;
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