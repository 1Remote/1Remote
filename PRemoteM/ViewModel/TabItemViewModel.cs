using System;
using PRM.Core;
using PRM.Core.Protocol;

namespace PRM.ViewModel
{
    public class TabItemViewModel : NotifyPropertyChangedBase
    {
        public TabItemViewModel()
        {
            _markColorHex = "#102b3e";
        }

        private object _header;

        public object Header
        {
            get => _header;
            set => SetAndNotifyIfChanged(nameof(Header), ref _header, value);
        }

        private ProtocolHostBase _content;

        public ProtocolHostBase Content
        {
            get => _content;
            set
            {
                SetAndNotifyIfChanged(nameof(Content), ref _content, value);
                MarkColorHex = Content.ProtocolServer.MarkColorHex;
                IconImg = Content.ProtocolServer.IconImg;
                CanResizeNow = _content.CanResizeNow();
                _content.OnCanResizeNowChanged += () => CanResizeNow = _content.CanResizeNow();
            }
        }

        private bool _canResizeNow = false;

        public bool CanResizeNow
        {
            get => _canResizeNow;
            set => SetAndNotifyIfChanged(nameof(CanResizeNow), ref _canResizeNow, value);
        }

        private string _markColorHex;

        /// <summary>
        /// tab title mark color
        /// </summary>
        public string MarkColorHex
        {
            get => _markColorHex;
            private set
            {
                try
                {
                    SetAndNotifyIfChanged(nameof(MarkColorHex), ref _markColorHex, value);
                }
                catch (Exception)
                {
                    MarkColorHex = "#FFFFFF";
                }
            }
        }

        private System.Windows.Media.Imaging.BitmapSource _iconImg;

        public System.Windows.Media.Imaging.BitmapSource IconImg
        {
            get => _iconImg;
            private set => SetAndNotifyIfChanged(nameof(IconImg), ref _iconImg, value);
        }
    }
}