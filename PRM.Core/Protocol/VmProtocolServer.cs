using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PRM.Core.Protocol
{
    public class VmProtocolServer : NotifyPropertyChangedBase
    {
        private ProtocolServerBase _server = null;

        public ProtocolServerBase Server
        {
            get => _server;
            set => SetAndNotifyIfChanged(nameof(Server), ref _server, value);
        }

        public VmProtocolServer(ProtocolServerBase psb)
        {
            Server = psb;
            if (Server.GetType() == typeof(ProtocolServerNone))
            {
                ObjectVisibility = Visibility.Collapsed;
                return;
            }
            OrgDispNameControl = new TextBlock()
            {
                Text = psb.DispName,
            };
            DispNameControl = OrgDispNameControl;

            OrgSubTitleControl = new TextBlock()
            {
                Text = psb.SubTitle,
            };
            SubTitleControl = OrgSubTitleControl;
        }

        public readonly object OrgDispNameControl = null;
        public readonly object OrgSubTitleControl = null;


        private object _dispNameControl = null;
        public object DispNameControl
        {
            get => _dispNameControl;
            set => SetAndNotifyIfChanged(nameof(DispNameControl), ref _dispNameControl, value);
        }



        private object _subTitleControl = null;
        public object SubTitleControl
        {
            get => _subTitleControl;
            set => SetAndNotifyIfChanged(nameof(SubTitleControl), ref _subTitleControl, value);
        }



        private Visibility _objectVisibility = Visibility.Visible;

        public Visibility ObjectVisibility
        {
            get
            {
                if (Server.GetType() == typeof(ProtocolServerNone))
                {
                    return Visibility.Collapsed;
                }
                return _objectVisibility;
            }
            set
            {
                if (Server.GetType() == typeof(ProtocolServerNone))
                {
                    return;
                }
                SetAndNotifyIfChanged(nameof(ObjectVisibility), ref _objectVisibility, value);
            }
        }
    }
}
