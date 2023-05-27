using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using _1RM.Controls.NoteDisplay;
using _1RM.Model;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.Model;
using _1RM.Service.Locality;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Stylet;

namespace _1RM.View
{
    public class ProtocolBaseViewModel : NotifyPropertyChangedBase
    {
        public DataSourceBase? DataSource => Server.DataSource;
        public string DataSourceName => DataSource?.DataSourceName ?? "";

        public int CustomOrder => LocalityListViewService.ServerCustomOrderGet(Id);

        #region Grouped
        public string GroupedOrder
        {
            get
            {
                var i = LocalityListViewService.GroupedOrderGet(dataSourceName: DataSourceName);
                var mark = IoC.Get<DataSourceService>().LocalDataSource == DataSource ? '!' : '#'; // ! for local, # for remote to make local first when i is same.
                return $"{i}_{mark}_{DataSource}";
            }
        }


        private bool? _groupedIsExpanded = null;
        public bool GroupedIsExpanded
        {
            set
            {
                if (IoC.TryGet<LocalityService>() != null
                    && SetAndNotifyIfChanged(ref _groupedIsExpanded, value))
                {
                    LocalityListViewService.GroupedIsExpandedSet(DataSourceName, value);
                }
            }
            get
            {
                var ret = LocalityListViewService.GroupedIsExpandedGet(DataSourceName);
                _groupedIsExpanded = LocalityListViewService.GroupedIsExpandedGet(DataSourceName);
                return ret;
            }
        }
        #endregion

        public bool IsEditable { get; private set; } = false;
        public bool IsViewable { get; private set; } = false;

        public string Id => Server.Id;

        public string DisplayName => Server.DisplayName;
        public string SubTitle => Server.SubTitle;
        public string ProtocolDisplayNameInShort => Server.ProtocolDisplayName;

        /// <summary>
        /// like: "#work #asd", display in launcher page.
        /// </summary>
        public string TagString { get; private set; }

        public List<Tag> Tags { get; private set; } = new List<Tag>();

        public void ReLoadTags()
        {
            Tags = IoC.TryGet<GlobalData>()?.TagList.Where(x => _server.Tags.Contains(x.Name)).OrderBy(x => x.CustomOrder).ThenBy(x => x.Name).ToList() ?? new List<Tag>();
            RaisePropertyChanged(nameof(Tags));
        }

        private ProtocolBase _server;
        public ProtocolBase Server
        {
            get => _server;
            set
            {
                if (_server != value)
                {
                    _server = value;
                    _server.Tags = _server.Tags.Select(x => x.ToLower()).ToList();

                    if (ConverterNoteToVisibility.IsVisible(Server.Note))
                    {
                        Execute.OnUIThreadSync(() =>
                        {
                            HoverNoteDisplayControl = new NoteIcon(this.Server);
                        });
                    }
                    LastConnectTime = LocalityConnectRecorder.Get(Server);
                    TagString = string.Join(" ", Server.Tags.Select(x => "#" + x));
                    RaisePropertyChanged(nameof(TagString));
                    ReLoadTags();
                    RaisePropertyChanged(nameof(Id));
                    RaisePropertyChanged(nameof(DisplayName));
                    RaisePropertyChanged(nameof(SubTitle));
                    RaisePropertyChanged(nameof(ProtocolDisplayNameInShort));
                    IsViewable = IsEditable = _server.DataSource?.IsWritable == true;
                    RaisePropertyChanged(nameof(DataSource));
                    RaisePropertyChanged(nameof(IsViewable));
                    RaisePropertyChanged(nameof(IsEditable));
                    if (DisplayNameControl != null)
                    {
                        DisplayNameControl = OrgDisplayNameControl;
                        SubTitleControl = OrgSubTitleControl;
                    }
                }
                RaisePropertyChanged();
            }
        }

        public ProtocolBaseViewModel(ProtocolBase psb)
        {
            Server = psb;
            _server = psb;

            if (ConverterNoteToVisibility.IsVisible(Server.Note))
            {
                Execute.OnUIThreadSync(() =>
                {
                    HoverNoteDisplayControl = new NoteIcon(this.Server);
                });
            }
            LastConnectTime = LocalityConnectRecorder.Get(Server);
            TagString = string.Join(" ", Server.Tags.Select(x => "#" + x));
        }


        private object? _orgDisplayNameControl = null;
        public object OrgDisplayNameControl
        {
            get
            {
                Execute.OnUIThreadSync(() =>
                {
                    if (_orgDisplayNameControl is not TextBlock
                        || (_orgDisplayNameControl is TextBlock tb && tb.Text != Server?.DisplayName))
                    {
                        _orgDisplayNameControl = new TextBlock() { Text = Server?.DisplayName, };
                    }
                });
                return _orgDisplayNameControl!;
            }
        }

        private object? _orgSubTitleControl = null;
        public object OrgSubTitleControl
        {
            get
            {
                Execute.OnUIThreadSync(() =>
                {
                    if (_orgSubTitleControl is not TextBlock
                    || (_orgSubTitleControl is TextBlock tb && tb.Text != Server?.SubTitle))
                    {
                        _orgSubTitleControl = new TextBlock() { Text = Server?.SubTitle, };
                    }
                });
                return _orgSubTitleControl!;
            }
        }

        private object? _displayNameControl = null;
        public object? DisplayNameControl
        {
            get => _displayNameControl ??= OrgDisplayNameControl;
            set => SetAndNotifyIfChanged(ref _displayNameControl, value);
        }


        private object? _subTitleControl = null;
        public object SubTitleControl
        {
            get { return _subTitleControl ??= OrgSubTitleControl; }
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
            private set => SetAndNotifyIfChanged(ref _isVisible, value);
        }

        public virtual void SetIsVisible(bool isVisible)
        {
            IsVisible = isVisible;
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
                    GlobalEventHelper.OnRequestServerConnect?.Invoke(Server, fromView: nameof(MainWindowView));
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

        private List<ProtocolAction>? _actions;

        public List<ProtocolAction>? Actions
        {
            get => _actions;
            set => SetAndNotifyIfChanged(ref _actions, value);
        }

        #endregion CMD
    }

    public class ProtocolBaseViewModelDummy : ProtocolBaseViewModel
    {
        public ProtocolBaseViewModelDummy(DataSourceBase source) : base(new Dummy() { DataSource = source })
        {
            base.SetIsVisible(false);
        }

        public override void SetIsVisible(bool isVisible)
        {
            base.SetIsVisible(false);
        }
    }
}