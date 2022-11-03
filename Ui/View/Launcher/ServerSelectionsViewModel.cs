using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using _1RM.Controls.NoteDisplay;
using _1RM.Model;
using _1RM.Model.Protocol.Base;
using _1RM.Service;
using _1RM.Utils;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.PageHost;
using Stylet;

namespace _1RM.View.Launcher
{
    public class ServerSelectionsViewModel : NotifyPropertyChangedBaseScreen
    {
        public FrameworkElement GridMenuActions { get; private set; } = new Grid();
        public TextBox TbKeyWord { get; private set; } = new TextBox();
        private LauncherWindowViewModel? _launcherWindowViewModel;
        private readonly SolidColorBrush _highLightBrush = new SolidColorBrush(Color.FromArgb(80, 239, 242, 132));

        public ServerSelectionsViewModel()
        {
        }

        public void Init(LauncherWindowViewModel launcherWindowViewModel)
        {
            _launcherWindowViewModel = launcherWindowViewModel;
        }


        protected override void OnViewLoaded()
        {
            if (this.View is ServerSelectionsView window)
            {
                GridMenuActions = window.GridMenuActions;
                GridMenuActions.Focus();
                TbKeyWord = window.TbKeyWord;
                CalcNoteFieldVisibility();
            }
        }

        private readonly DebounceDispatcher _debounceDispatcher = new();
        private string _filter = "";
        public string Filter
        {
            get => _filter;
            set
            {
                if (SetAndNotifyIfChanged(ref _filter, value))
                {
                    _debounceDispatcher.Debounce(150, (obj) =>
                    {
                        if (value == _filter)
                        {
                            SimpleLogHelper.Warning("CalcVisibleByFilter");
                            CalcVisibleByFilter();
                        }
                    });
                }
            }
        }

        public ProtocolBaseViewModel? SelectedItem
        {
            get
            {
                if (VmServerList.Count > 0
                    && _selectedIndex >= 0
                    && _selectedIndex < VmServerList.Count)
                {
                    return VmServerList[_selectedIndex];
                }
                return null;
            }
        }

        private int _selectedIndex = 0;
        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (SetAndNotifyIfChanged(ref _selectedIndex, value))
                {
                    RaisePropertyChanged(nameof(SelectedItem));
                    CalcNoteFieldVisibility();
                    if (this.View is ServerSelectionsView view)
                    {
                        Execute.OnUIThread(() =>
                        {
                            view.ListBoxSelections.ScrollIntoView(view.ListBoxSelections.SelectedItem);
                        });
                    }
                }
            }
        }


        private ObservableCollection<ProtocolBaseViewModel> _vmServerList = new ObservableCollection<ProtocolBaseViewModel>();
        public ObservableCollection<ProtocolBaseViewModel> VmServerList
        {
            get => _vmServerList;
            set
            {
                if (SetAndNotifyIfChanged(ref _vmServerList, value))
                {
                    SelectedIndex = 0;
                }
            }
        }


        private ObservableCollection<ProtocolAction> _actions = new ObservableCollection<ProtocolAction>();
        public ObservableCollection<ProtocolAction> Actions
        {
            get => _actions;
            set => SetAndNotifyIfChanged(ref _actions, value);
        }

        private int _selectedActionIndex;
        public int SelectedActionIndex
        {
            get => _selectedActionIndex;
            set => SetAndNotifyIfChanged(ref _selectedActionIndex, value);
        }

        private double _gridSelectionsHeight;
        public double GridSelectionsHeight
        {
            get => _gridSelectionsHeight;
            set => SetAndNotifyIfChanged(ref _gridSelectionsHeight, value);
        }

        private List<TagFilter> _tagFilters = new List<TagFilter>();
        public List<TagFilter> TagFilters
        {
            get => _tagFilters;
            set => SetAndNotifyIfChanged(ref _tagFilters, value);
        }




        public void RebuildVmServerList()
        {
            VmServerList = new ObservableCollection<ProtocolBaseViewModel>(IoC.Get<GlobalData>().VmItemList.OrderByDescending(x => x.LastConnectTime));

            Execute.OnUIThread(() =>
            {
                foreach (var vm in VmServerList)
                {
                    vm.DisplayNameControl = vm.OrgDisplayNameControl;
                    vm.SubTitleControl = vm.OrgSubTitleControl;
                }
            });
        }


        public double ReCalcGridMainHeight(bool showGridAction)
        {
            double ret = LauncherWindowViewModel.MAX_WINDOW_HEIGHT;
            Execute.OnUIThread(() =>
            {
                // show server list
                if (showGridAction == false)
                {
                    var tmp = LauncherWindowViewModel.LAUNCHER_SERVER_LIST_ITEM_HEIGHT * VmServerList.Count;
                    GridSelectionsHeight = Math.Min(tmp, LauncherWindowViewModel.MAX_SELECTION_HEIGHT);
                    ret = LauncherWindowViewModel.LAUNCHER_GRID_KEYWORD_HEIGHT + GridSelectionsHeight;
                }
                //// show action list
                //else
                //{
                //    LauncherWindowViewModel.GridMainHeight = LauncherWindowViewModel.MaxWindowHeight;
                //}
            });
            return ret;
        }

        private string _lastKeyword = string.Empty;
        public void CalcVisibleByFilter()
        {
            if (string.IsNullOrEmpty(_filter) == false && _lastKeyword == _filter) return;
            _lastKeyword = _filter;

            var keyword = _filter.Trim();
            var tmp = TagAndKeywordEncodeHelper.DecodeKeyword(keyword);
            TagFilters = tmp.TagFilterList;

            var newList = new List<ProtocolBaseViewModel>();
            foreach (var vm in IoC.Get<GlobalData>().VmItemList)
            {
                var server = vm.Server;
                var s = TagAndKeywordEncodeHelper.MatchKeywords(server, tmp);
                if (s.Item1 == true)
                {
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        if (s.Item2 == null)
                        {
                            vm.DisplayNameControl = vm.OrgDisplayNameControl;
                            vm.SubTitleControl = vm.OrgSubTitleControl;
                        }
                        else
                        {
                            var mrs = s.Item2;
                            if (mrs.IsMatchAllKeywords)
                            {
                                var displayName = server.DisplayName;
                                var subTitle = server.SubTitle;
                                var m1 = mrs.HitFlags[0];
                                if (m1.Any(x => x == true))
                                {
                                    var sp = new StackPanel() { Orientation = System.Windows.Controls.Orientation.Horizontal };
                                    for (int i = 0; i < m1.Count; i++)
                                    {
                                        if (m1[i])
                                            sp.Children.Add(new TextBlock()
                                            {
                                                Text = displayName[i].ToString(),
                                                Background = _highLightBrush,
                                            });
                                        else
                                            sp.Children.Add(new TextBlock()
                                            {
                                                Text = displayName[i].ToString(),
                                            });
                                    }

                                    vm.DisplayNameControl = sp;
                                }
                                else
                                {
                                    vm.DisplayNameControl = vm.OrgDisplayNameControl;
                                }

                                var m2 = mrs.HitFlags[1];
                                if (m2.Any(x => x == true))
                                {
                                    var sp = new StackPanel() { Orientation = System.Windows.Controls.Orientation.Horizontal };
                                    for (int i = 0; i < m2.Count; i++)
                                    {
                                        if (m2[i])
                                            sp.Children.Add(new TextBlock()
                                            {
                                                Text = subTitle[i].ToString(),
                                                Background = _highLightBrush,
                                            });
                                        else
                                            sp.Children.Add(new TextBlock()
                                            {
                                                Text = subTitle[i].ToString(),
                                            });
                                    }

                                    vm.SubTitleControl = sp;
                                }
                                else
                                {
                                    vm.SubTitleControl = vm.OrgSubTitleControl;
                                }
                            }
                        }
                    });
                    newList.Add(vm);
                }
            }

            if (string.IsNullOrEmpty(keyword) && newList.Count == 0)
            {
                RebuildVmServerList();
            }
            else
            {
                Execute.OnUIThread(() =>
                {
                    VmServerList = new ObservableCollection<ProtocolBaseViewModel>(newList.OrderByDescending(x => x.LastConnectTime));
                });
            }
            _launcherWindowViewModel?.ReSetWindowHeight(false);
        }


        #region NoteField

        private RelayCommand? _cmdHideNoteField;
        public RelayCommand CmdHideNoteField
        {
            get
            {
                return _cmdHideNoteField ??= new RelayCommand((o) =>
                {
                    IoC.Get<ConfigurationService>().Launcher.ShowNoteFieldInLauncher = false;
                    IoC.Get<ConfigurationService>().Save();
                    CalcNoteFieldVisibility();
                    IsShowNoteFieldEnabled = true;
                });
            }
        }

        private RelayCommand? _cmdShowNoteField;
        public RelayCommand CmdShowNoteField
        {
            get
            {
                return _cmdShowNoteField ??= new RelayCommand((o) =>
                {
                    IoC.Get<ConfigurationService>().Launcher.ShowNoteFieldInLauncher = true;
                    IoC.Get<ConfigurationService>().Save();
                    CalcNoteFieldVisibility();
                    IsShowNoteFieldEnabled = false;
                });
            }
        }


        public Border? NoteField = null;

        private bool _isShowNoteFieldEnabled;
        public bool IsShowNoteFieldEnabled
        {
            get => this._isShowNoteFieldEnabled;
            set => this.SetAndNotifyIfChanged(ref _isShowNoteFieldEnabled, value);
        }

        private Visibility _gridNoteVisibility = Visibility.Visible;
        public Visibility GridNoteVisibility
        {
            get => _gridNoteVisibility;
            set => this.SetAndNotifyIfChanged(ref _gridNoteVisibility, value);
        }

        public void CalcNoteFieldVisibility()
        {
            if (_launcherWindowViewModel?.ServerSelectionsViewVisibility != Visibility.Visible)
            {
                GridNoteVisibility = Visibility.Collapsed;
                return;
            }

            Visibility newVisibility;
            if (IoC.Get<ConfigurationService>().Launcher.ShowNoteFieldInLauncher == false)
                newVisibility = Visibility.Collapsed;
            else if (ConverterNoteToVisibility.IsVisible(SelectedItem?.Server?.Note))
                newVisibility = Visibility.Visible;
            else
                newVisibility = Visibility.Collapsed;
            if (GridNoteVisibility == newVisibility) return;

            IsShowNoteFieldEnabled = IoC.Get<ConfigurationService>().Launcher.ShowNoteFieldInLauncher == false;
            GridNoteVisibility = newVisibility;
            if (NoteField != null)
            {
                if (GridNoteVisibility == Visibility.Visible)
                {
                    RaisePropertyChanged(nameof(GridNoteVisibility));
                    var sb = new Storyboard();
                    sb.AddFadeIn(0.3);
                    sb.Begin(NoteField);
                }
                else
                {
                    var sb = new Storyboard();
                    sb.AddFadeOut(0.3);
                    sb.Completed += (sender, args) => { RaisePropertyChanged(nameof(GridNoteVisibility)); };
                    sb.Begin(NoteField);
                }
            }
            else
            {
                RaisePropertyChanged(nameof(GridNoteVisibility));
            }

            _launcherWindowViewModel?.ReSetWindowHeight(false);
        }
        #endregion
    }
}
