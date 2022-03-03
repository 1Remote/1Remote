using System.Collections.Generic;
using System.Windows.Controls;
using PRM.Model;
using PRM.Model.Protocol.Base;
using Shawn.Utils;
using Shawn.Utils.Wpf;

namespace PRM.View
{
    public class VmProtocolServer : NotifyPropertyChangedBase
    {
        private ProtocolServerBase _server = null;

        public int Id => Server?.Id ?? 0;

        public ProtocolServerBase Server
        {
            get => _server;
            set => SetAndNotifyIfChanged(ref _server, value);
        }

        public VmProtocolServer(ProtocolServerBase psb)
        {
            Server = psb;
            //_dispNameControl = OrgDispNameControl;
            //_subTitleControl = OrgSubTitleControl;
        }

        public object OrgDispNameControl => new TextBlock() { Text = Server.DisplayName, };
        public object OrgSubTitleControl => new TextBlock() { Text = Server?.SubTitle, };


        private object _dispNameControl = null;
        public object DispNameControl
        {
            get
            {
                if (_dispNameControl == null)
                    _dispNameControl = OrgDispNameControl;
                return _dispNameControl;
            }
            set => SetAndNotifyIfChanged(ref _dispNameControl, value);
        }


        private object _subTitleControl = null;
        public object SubTitleControl
        {
            get
            {
                if (_subTitleControl == null)
                    _subTitleControl = OrgSubTitleControl;
                return _subTitleControl;
            }
            set => SetAndNotifyIfChanged(ref _subTitleControl, value);
        }

        private bool _isSelected;
        /// <summary>
        /// is selected in list of MainWindow?
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetAndNotifyIfChanged(ref _isSelected, value);
        }

        #region CMD

        private RelayCommand _cmdConnServer;

        public RelayCommand CmdConnServer
        {
            get
            {
                return _cmdConnServer ??= new RelayCommand(o =>
                {
                    GlobalEventHelper.OnRequestServerConnect?.Invoke(Server.Id);
                });
            }
        }

        private RelayCommand _cmdEditServer;

        public RelayCommand CmdEditServer
        {
            get
            {
                return _cmdEditServer ??= new RelayCommand((o) =>
                {
                    GlobalEventHelper.OnRequestGoToServerEditPage?.Invoke(Server.Id, false, true);
                });
            }
        }

        private RelayCommand _cmdDuplicateServer;
        public RelayCommand CmdDuplicateServer
        {
            get
            {
                return _cmdDuplicateServer ??= new RelayCommand((o) =>
                {
                    GlobalEventHelper.OnRequestGoToServerEditPage?.Invoke(Server.Id, true, true);
                });
            }
        }

        private List<ActionForServer> _actions;
        public List<ActionForServer> Actions
        {
            get => _actions;
            set => SetAndNotifyIfChanged(ref _actions, value);
        }

        #endregion CMD
    }
}