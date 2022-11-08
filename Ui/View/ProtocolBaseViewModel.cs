using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Controls;
using PRM.Controls;
using PRM.Controls.NoteDisplay;
using PRM.Model;
using PRM.Model.Protocol;
using PRM.Model.Protocol.Base;
using Shawn.Utils;
using Shawn.Utils.Wpf;

namespace PRM.View
{
    public class ProtocolBaseViewModel : NotifyPropertyChangedBase
    {

        public int Id => Server?.Id ?? 0;

        public ProtocolBase Server { get; }
        public ProtocolBaseViewModel(ProtocolBase psb)
        {
            Debug.Assert(psb != null);
            Server = psb;
            if (ConverterNoteToVisibility.IsVisible(Server.Note))
            {
                HoverNoteDisplayControl = new NoteIcon(this.Server);
            }
        }

        public object OrgDisplayNameControl => new TextBlock() { Text = Server?.DisplayName, };
        public object OrgSubTitleControl => new TextBlock() { Text = Server?.SubTitle, };


        private object? _displayNameControl = null;
        public object? DisplayNameControl
        {
            get => _displayNameControl ??= OrgDisplayNameControl;
            set => SetAndNotifyIfChanged(ref _displayNameControl, value);
        }


        private object? _subTitleControl = null;
        public object? SubTitleControl
        {
            get => _subTitleControl ??= OrgSubTitleControl;
            set => SetAndNotifyIfChanged(ref _subTitleControl, value);
        }

        private NoteIcon? _hoverNoteDisplayControl = null;
        public NoteIcon? HoverNoteDisplayControl
        {
            get => _hoverNoteDisplayControl;
            set => SetAndNotifyIfChanged(ref _hoverNoteDisplayControl, value);
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

        private RelayCommand? _cmdConnServer;
        public RelayCommand? CmdConnServer
        {
            get
            {
                return _cmdConnServer ??= new RelayCommand(o =>
                {
                    GlobalEventHelper.OnRequestServerConnect?.Invoke(Server.Id);
                });
            }
        }

        private RelayCommand? _cmdEditServer;
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

        private RelayCommand? _cmdDuplicateServer;
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

        private List<ProtocolAction>? _actions;
        public List<ProtocolAction>? Actions
        {
            get => _actions;
            set => SetAndNotifyIfChanged(ref _actions, value);
        }

        #endregion CMD
    }
}