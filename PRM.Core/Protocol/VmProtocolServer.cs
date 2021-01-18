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
            SubTitleControl = OrgSubTitleControl;
        }

        public object OrgDispNameControl =>
            new TextBlock()
            {
                Text = Server.DispName,
            };

        public object OrgSubTitleControl =>
            new TextBlock()
            {
                Text = Server.SubTitle,
            };


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
            get => _objectVisibility;
            set => SetAndNotifyIfChanged(nameof(ObjectVisibility), ref _objectVisibility, value);
        }
    }
}
