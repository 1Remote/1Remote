using System;
using PRM.View.Host.ProtocolHosts;
using Shawn.Utils;

namespace PRM.View.Host
{
    public class TabItemViewModel : NotifyPropertyChangedBase
    {
        private object? _header;
        public object? Header
        {
            get => _header;
            set => SetAndNotifyIfChanged(ref _header, value);
        }

        private HostBase? _content;
        public HostBase? Content
        {
            get => _content;
            set
            {
                if (SetAndNotifyIfChanged(ref _content, value) && value != null)
                {
                    ColorHex = value.ProtocolServer.ColorHex;
                    IconImg = value.ProtocolServer.IconImg;
                    CanResizeNow = value.CanResizeNow();
                    value.OnCanResizeNowChanged += () => CanResizeNow = value.CanResizeNow();
                }
            }
        }

        private bool _canResizeNow = false;
        public bool CanResizeNow
        {
            get => _canResizeNow;
            set => SetAndNotifyIfChanged(ref _canResizeNow, value);
        }

        private string _colorHex = "#00000000";
        /// <summary>
        /// tab title mark color
        /// </summary>
        public string ColorHex
        {
            get => _colorHex;
            private set
            {
                try
                {
                    SetAndNotifyIfChanged(ref _colorHex, value);
                }
                catch (Exception)
                {
                    ColorHex = "#00000000";
                }
            }
        }

        private System.Windows.Media.Imaging.BitmapSource? _iconImg;
        public System.Windows.Media.Imaging.BitmapSource? IconImg
        {
            get => _iconImg;
            private set => SetAndNotifyIfChanged(ref _iconImg, value);
        }
    }
}