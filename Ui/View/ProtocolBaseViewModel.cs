using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Forms;
using _1RM.Controls;
using _1RM.Controls.NoteDisplay;
using _1RM.Model;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Service;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.Model;
using Newtonsoft.Json;
using NUlid;
using Shawn.Utils;
using Shawn.Utils.Wpf;

namespace _1RM.View
{
    public class ProtocolBaseViewModel : NotifyPropertyChangedBase
    {
        public string DataSourceName { get; }
        public bool IsEditable { get; }
        public bool IsViewable { get; }

        public string Id => Server.Id;

        public string DisplayName => Server.DisplayName;
        public string SubTitle => Server.SubTitle;
        public string ProtocolDisplayNameInShort => Server.ProtocolDisplayNameInShort;

        /// <summary>
        /// like: "#work #asd"
        /// </summary>
        public string TagString { get; }

        public ProtocolBase Server { get; }
        public ProtocolBaseViewModel(ProtocolBase psb, DataSourceBase dataSource)
        {
            Debug.Assert(psb != null);
            Server = psb;
            // TODO how it works with a tmp server?
            DataSourceName = dataSource.DataSourceName;
            psb.DataSourceName = dataSource.DataSourceName;
            IsViewable = IsEditable = dataSource.IsWritable;
            if (ConverterNoteToVisibility.IsVisible(Server.Note))
            {
                HoverNoteDisplayControl = new NoteIcon(this.Server);
            }
            LastConnectTime = ConnectTimeRecorder.Get(Server);
            TagString = string.Join(" ", Server.Tags.Select(x => "#" + x));
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

        private bool _isSelected = false;
        /// <summary>
        /// is selected in list of MainWindow?
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetAndNotifyIfChanged(ref _isSelected, value);
        }


        private bool _isVisible = true;
        public bool IsVisible
        {
            get => _isVisible;
            set => SetAndNotifyIfChanged(ref _isVisible, value);
        }


        private DateTime _lastConnectTime = DateTime.MinValue;
        public DateTime LastConnectTime
        {
            get => _lastConnectTime;
            set => SetAndNotifyIfChanged(ref _lastConnectTime, value);
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
                    GlobalEventHelper.OnRequestGoToServerEditPage?.Invoke(server: Server, showAnimation: true);
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
                    GlobalEventHelper.OnRequestGoToServerDuplicatePage?.Invoke(server: Server, showAnimation: true);
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