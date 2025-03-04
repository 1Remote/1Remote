using System;
using System.ComponentModel;
using System.Diagnostics;
using _1RM.Model.Protocol.Base;
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
            Content.ProtocolServer.PropertyChanged += ProtocolServerOnPropertyChanged;
        }

        private void ProtocolServerOnPropertyChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(ProtocolBase.DisplayName))
            {
                DisplayName = Content.ProtocolServer.DisplayName;
            }
        }

        ~TabItemViewModel()
        {
            Content.ProtocolServer.PropertyChanged -= ProtocolServerOnPropertyChanged;
        }


        public string DisplayName
        {
            get => _displayName;
            private set
            {
                _displayName = value;
                RaisePropertyChanged();
            }
        }

        public HostBase Content { get; }
        /// <summary>
        /// tab title mark color
        /// </summary>
        public string ColorHex { get; }
        public System.Windows.Media.Imaging.BitmapSource? IconImg { get; }



        private RelayCommand? _cmdReconnect;
        private string _displayName;

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