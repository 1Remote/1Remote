using System;
using PRM.View.Host.ProtocolHosts;
using Shawn.Utils;

namespace PRM.View.Host
{
    public class TabItemViewModel : NotifyPropertyChangedBase
    {
        public TabItemViewModel()
        {
        }

        private object _header;

        public object Header
        {
            get => _header;
            set => SetAndNotifyIfChanged(ref _header, value);
        }

        private HostBase _content;

        public HostBase Content
        {
            get => _content;
            set
            {
                SetAndNotifyIfChanged(ref _content, value);
                //MarkColorHex = Content.ProtocolServer.MarkColorHex;
                ColorHex = Content.ProtocolServer.ColorHex;
                IconImg = Content.ProtocolServer.IconImg;
                CanResizeNow = _content.CanResizeNow();
                _content.OnCanResizeNowChanged += () => CanResizeNow = _content.CanResizeNow();
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

        private System.Windows.Media.Imaging.BitmapSource _iconImg;

        public System.Windows.Media.Imaging.BitmapSource IconImg
        {
            get => _iconImg;
            private set => SetAndNotifyIfChanged(ref _iconImg, value);
        }
    }
}