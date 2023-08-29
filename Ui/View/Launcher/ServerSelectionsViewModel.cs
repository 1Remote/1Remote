using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using _1RM.Controls.NoteDisplay;
using _1RM.Model;
using _1RM.Service;
using _1RM.Utils;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.PageHost;
using Stylet;
#if DEBUG
using System.Diagnostics;
#endif

namespace _1RM.View.Launcher
{
    public class ServerSelectionsViewModel : NotifyPropertyChangedBaseScreen
    {
        private readonly SolidColorBrush _highLightBrush = new SolidColorBrush(Color.FromArgb(80, 239, 242, 132));


        protected override void OnViewLoaded()
        {
            if (IoC.Get<LauncherWindowViewModel>().View is LauncherWindowView { IsClosing: false } window
                && this.View is ServerSelectionsView view)
            {
                view.TbKeyWord.Focus();
                CalcNoteFieldVisibility();

                IoC.Get<GlobalData>().OnDataReloaded += RebuildVmServerList;
                RebuildVmServerList();
            }
        }

        public bool AnyKeyExceptTabPressAfterShow = false;
        public void Show()
        {
            if (this.View is not ServerSelectionsView view) return;
            if (IoC.TryGet<LauncherWindowView>()?.IsClosing != false) return;

            Filter = "";
            IoC.Get<LauncherWindowViewModel>().ServerSelectionsViewVisibility = Visibility.Visible;
            ShowCredentials = IoC.Get<ConfigurationService>().Launcher.ShowCredentials;
            Execute.OnUIThread(() =>
            {
                view.GridActionsList.Visibility = Visibility.Collapsed;
                view.TbKeyWord.Focus();
            });
            CalcNoteFieldVisibility();
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
//#if DEBUG
//                    CalcVisibleByFilter();
//#else
                    _debounceDispatcher.Debounce(200, (obj) =>
                    {
                        if (value == _filter)
                        {
                            SimpleLogHelper.DebugWarning("CalcVisibleByFilter");
                            CalcVisibleByFilter();
                        }
                    });
//#endif
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

        private int _selectedIndex = -1;
        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (SetAndNotifyIfChanged(ref _selectedIndex, value))
                {
                    RaisePropertyChanged(nameof(SelectedItem));
                    CalcNoteFieldVisibility();
                    if (SelectedItem != null && this.View is ServerSelectionsView view)
                    {
                        Execute.OnUIThreadSync(() =>
                        {
                            try
                            {
                                view.ListBoxSelections.ScrollIntoView(view.ListBoxSelections.SelectedItem);
                            }
                            catch (Exception)
                            {
                                // ignored
                            }
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
                SetAndNotifyIfChanged(ref _vmServerList, value);
                if (_vmServerList.Count > 0)
                {
                    SelectedIndex = 0;
                }
                else
                {
                    SelectedIndex = -1;
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

        private List<TagFilter> _tagFilters = new List<TagFilter>();
        public List<TagFilter> TagFilters
        {
            get => _tagFilters;
            set => SetAndNotifyIfChanged(ref _tagFilters, value);
        }


        public void AppendServer(ProtocolBaseViewModel viewModel)
        {
            Execute.OnUIThread(() =>
            {
                viewModel.PropertyChanged -= OnLastConnectTimeChanged;
                viewModel.PropertyChanged += OnLastConnectTimeChanged;
                viewModel.LauncherMainTitleViewModel.UnHighLightAll();
                viewModel.LauncherSubTitleViewModel.UnHighLightAll();
                VmServerList.Add(viewModel);
            });
        }

        public void RebuildVmServerList()
        {
            if (this.View is not ServerSelectionsView view) return;
            if (IoC.TryGet<LauncherWindowView>()?.IsClosing != false) return;

            var selectedId = SelectedItem?.Id ?? "";
            VmServerList = new ObservableCollection<ProtocolBaseViewModel>(IoC.Get<GlobalData>().VmItemList.OrderByDescending(x => x.LastConnectTime));
            foreach (var viewModel in VmServerList)
            {
                viewModel.KeywordMark = double.MinValue;
                viewModel.PropertyChanged -= OnLastConnectTimeChanged;
                viewModel.PropertyChanged += OnLastConnectTimeChanged;
            }

            if (string.IsNullOrEmpty(selectedId) == false)
            {
                var s = VmServerList.FirstOrDefault(x => x.Id == selectedId);
                if (s != null)
                    SelectedIndex = VmServerList.IndexOf(s);
            }
            else
            {
                SelectedIndex = 0;
            }

            foreach (var viewModel in VmServerList)
            {
                viewModel.LauncherMainTitleViewModel.UnHighLightAll();
                viewModel.LauncherSubTitleViewModel.UnHighLightAll();
            }
            IoC.Get<LauncherWindowViewModel>().ReSetWindowHeight();
        }

        private void OnLastConnectTimeChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProtocolBaseViewModel.LastConnectTime))
            {
                VmServerList = new ObservableCollection<ProtocolBaseViewModel>(VmServerList.OrderByDescending(x => x.LastConnectTime));
            }
        }


        public double ReCalcGridMainHeight()
        {
            if (this.View is not ServerSelectionsView view) return LauncherWindowViewModel.MAX_WINDOW_HEIGHT;
            if (IoC.TryGet<LauncherWindowView>()?.IsClosing != false) return LauncherWindowViewModel.MAX_WINDOW_HEIGHT;
            double ret = LauncherWindowViewModel.MAX_WINDOW_HEIGHT;
            // show server list
            if (view.GridActionsList.Visibility != Visibility.Visible)
            {
                var tmp = LauncherWindowViewModel.LAUNCHER_SERVER_LIST_ITEM_HEIGHT * Math.Min(VmServerList.Count, LauncherWindowViewModel.LAUNCHER_OUTLINE_CORNER_RADIUS);
                ret = LauncherWindowViewModel.LAUNCHER_GRID_KEYWORD_HEIGHT + tmp;
            }
            return ret;
        }


        private string _lastKeyword = string.Empty;
        public void CalcVisibleByFilter()
        {
            if (this.View is not ServerSelectionsView view) return;
            if (IoC.TryGet<LauncherWindowView>()?.IsClosing != false) return;
            if (string.IsNullOrEmpty(Filter) == false && _lastKeyword == Filter) return;

            _lastKeyword = Filter;

            var keyword = Filter.Trim();
            if (string.IsNullOrEmpty(keyword))
            {
                SelectedIndex = -1;
                RebuildVmServerList();
                TagFilters = new List<TagFilter>();
                return;
            }

            var tmp = TagAndKeywordEncodeHelper.DecodeKeyword(keyword);
            TagFilters = tmp.TagFilterList;

            var newList = new List<ProtocolBaseViewModel>();
#if DEBUG
            var sw = new Stopwatch();
#endif
            foreach (var vm in IoC.Get<GlobalData>().VmItemList)
            {
                vm.KeywordMark = double.MinValue;
                var server = vm.Server;
                var s = TagAndKeywordEncodeHelper.MatchKeywords(server, tmp, ShowCredentials);
                if (s.Item1 == true)
                {
                    newList.Add(vm);
                    //continue;
                    if (s.Item2 is { } results)
                    {
                        foreach (var list in results.HitFlags)
                        {
                            if (list is { } hitFlags && hitFlags.Count > vm.KeywordMark)
                            {
                                // 计算 resultsHitFlag 中最长有多少个连续的 true
                                var l = 0;
                                foreach (var f in hitFlags)
                                {
                                    if (f == true)
                                    {
                                        l++;
                                    }
                                    else
                                    {
                                        vm.KeywordMark = Math.Max(vm.KeywordMark, l);
                                        l = 0;
                                    }
                                }
                            }
                        }
                    }
                    if (s.Item2 == null)
                    {
                        vm.LauncherMainTitleViewModel.UnHighLightAll();
                        if (ShowCredentials)
                            vm.LauncherSubTitleViewModel.UnHighLightAll();
                    }
                    else
                    {
                        var mrs = s.Item2;
                        if (mrs.IsMatchAllKeywords)
                        {
                            var m1 = mrs.HitFlags[0];
                            if (m1.Any(x => x == true))
                            {
                                vm.LauncherMainTitleViewModel.HighLight(m1);
                            }
                            else
                            {
                                vm.LauncherMainTitleViewModel.UnHighLightAll();
                            }

                            if (ShowCredentials)
                            {
                                var m2 = mrs.HitFlags[1];
                                if (m2.Any(x => x == true))
                                {
                                    vm.LauncherSubTitleViewModel.HighLight(m2);
                                }
                                else
                                {
                                    vm.LauncherSubTitleViewModel.UnHighLightAll();
                                }
                            }
                        }
                    }
                }
            }
#if DEBUG
            sw.Stop();
            SimpleLogHelper.DebugInfo($"CalcVisibleByFilter: {sw.ElapsedMilliseconds}ms");
#endif

#if DEBUG
            var sw2 = new Stopwatch();
#endif
            VmServerList = new ObservableCollection<ProtocolBaseViewModel>(newList.OrderByDescending(x => x.KeywordMark)
                .ThenByDescending(x => x.LastConnectTime));
            IoC.Get<LauncherWindowViewModel>().ReSetWindowHeight();
#if DEBUG
            sw2.Stop();
            SimpleLogHelper.DebugInfo($"CalcVisibleByFilter-step2: {sw2.ElapsedMilliseconds}ms");
#endif
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


        private bool _isShowNoteFieldEnabled;
        public bool IsShowNoteFieldEnabled
        {
            get => this._isShowNoteFieldEnabled;
            set => this.SetAndNotifyIfChanged(ref _isShowNoteFieldEnabled, value);
        }


        private bool _showCredentials;
        public bool ShowCredentials
        {
            get => _showCredentials;
            set => SetAndNotifyIfChanged(ref _showCredentials, value);
        }

        private Visibility _gridNoteVisibility = Visibility.Visible;
        public Visibility GridNoteVisibility
        {
            get => _gridNoteVisibility;
            set => this.SetAndNotifyIfChanged(ref _gridNoteVisibility, value);
        }

        public void CalcNoteFieldVisibility()
        {
            if (this.View is not ServerSelectionsView view) return;
            if (IoC.TryGet<LauncherWindowView>()?.IsClosing != false) return;

            if (IoC.Get<LauncherWindowViewModel>().ServerSelectionsViewVisibility != Visibility.Visible)
            {
                GridNoteVisibility = Visibility.Collapsed;
            }
            else
            {
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
            }

            Execute.OnUIThreadSync(() =>
            {
                if (GridNoteVisibility == Visibility.Visible)
                {
                    RaisePropertyChanged(nameof(GridNoteVisibility));
                    var sb = new Storyboard();
                    sb.AddFadeIn(0.3);
                    sb.Begin(IoC.Get<LauncherWindowView>().NoteField);
                }
                else
                {
                    var sb = new Storyboard();
                    sb.AddFadeOut(0.3);
                    sb.Completed += (_, _) =>
                    {
                        RaisePropertyChanged(nameof(GridNoteVisibility));
                    };
                    sb.Begin(IoC.Get<LauncherWindowView>().NoteField);
                }
            });

            IoC.Get<LauncherWindowViewModel>().ReSetWindowHeight();
        }

        #endregion




        private RelayCommand? _cmdShowActionsList;
        public RelayCommand CmdShowActionsList
        {
            get
            {
                return _cmdShowActionsList ??= new RelayCommand((o) =>
                {
                    if (this.View is ServerSelectionsView view && o is ProtocolBaseViewModel p)
                    {
                        view.ShowActionsList(p);
                    }
                });
            }
        }
    }
}
