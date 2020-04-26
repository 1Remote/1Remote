using System;
using System.Drawing;
using System.Windows;
using System.Windows.Media;
using PRM.Core.Protocol;
using Color = System.Drawing.Color;

namespace PRM.Core.Ulits.DragablzTab
{
    public class TabItemViewModel : NotifyPropertyChangedBase
    {
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
            set => SetAndNotifyIfChanged(nameof(Content), ref _content, value);
        }

        

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetAndNotifyIfChanged(nameof(IsSelected), ref _isSelected, value);
        }


        
        private string _markColorHex = "#FFFFFF";
        public string MarkColorHex
        {
            get => _markColorHex;
            set
            {
                try
                {
                    SetAndNotifyIfChanged(nameof(MarkColorHex), ref _markColorHex, value);
                }
                catch (Exception)
                {
                }
            }
        }
    }
}
