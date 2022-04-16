using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using PRM.Model;
using PRM.Service;
using PRM.Utils;
using Shawn.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.PageHost;
using Stylet;
using Ui;

namespace PRM.View
{
    public class LauncherWindowViewModel : NotifyPropertyChangedBaseScreen
    {
        private double _gridMainWidth;
        private double _oneItemHeight;
        private double _oneActionHeight;
        private double _cornerRadius;
        private FrameworkElement _gridMenuActions = null!;


        #region properties

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

        private int _selectedIndex;
        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (SetAndNotifyIfChanged(ref _selectedIndex, value))
                {
                    RaisePropertyChanged(nameof(SelectedItem));
                    if (this.View is LauncherWindowView view)
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

        private string _filter = "";
        public string Filter
        {
            get => _filter;
            set
            {
                if (SetAndNotifyIfChanged(ref _filter, value))
                {
                    Task.Factory.StartNew(() =>
                    {
                        var filter = _filter;
                        Thread.Sleep(100);
                        if (filter == _filter)
                        {
                            CalcVisibleByFilter();
                        }
                    });
                }
            }
        }

        private double _gridMainHeight;
        public double GridMainHeight
        {
            get => _gridMainHeight;
            set
            {
                SetAndNotifyIfChanged(ref _gridMainHeight, value);
                GridMainClip = new RectangleGeometry(new Rect(new Size(_gridMainWidth, GridMainHeight)), _cornerRadius, _cornerRadius);
            }
        }

        private RectangleGeometry? _gridMainClip = null;
        public RectangleGeometry? GridMainClip
        {
            get => _gridMainClip;
            set => SetAndNotifyIfChanged(ref _gridMainClip, value);
        }

        public double GridKeywordHeight { get; } = 46;

        private double _gridSelectionsHeight;
        public double GridSelectionsHeight
        {
            get => _gridSelectionsHeight;
            set => SetAndNotifyIfChanged(ref _gridSelectionsHeight, value);
        }

        private double _gridActionsHeight;
        public double GridActionsHeight
        {
            get => _gridActionsHeight;
            set => SetAndNotifyIfChanged(ref _gridActionsHeight, value);
        }

        private List<TagFilter>? _tagFilters;
        public List<TagFilter>? TagFilters
        {
            get => _tagFilters;
            set => SetAndNotifyIfChanged(ref _tagFilters, value);
        }

        #endregion

        public LauncherWindowViewModel()
        {
            ReCalcWindowHeight(false);
            RebuildVmServerList();
            IoC.Get<GlobalData>().VmItemListDataChanged += RebuildVmServerList;
        }

        protected override void OnViewLoaded()
        {
            HideMe();
            SetHotKey();
            GlobalEventHelper.OnLauncherHotKeyChanged += SetHotKey;
            var view = (LauncherWindowView)this.View;
            _gridMenuActions = view.GridMenuActions;
            _gridMainWidth = (double)view.FindResource("GridMainWidth");
            _oneItemHeight = (double)view.FindResource("OneItemHeight");
            _oneActionHeight = (double)view.FindResource("OneActionItemHeight");
            _cornerRadius = (double)view.FindResource("CornerRadius");
            if (this.View is LauncherWindowView window)
            {
                window.ShowActivated = true;
                window.ShowInTaskbar = false;
                window.Deactivated += (s, a) => { HideMe(); };
                window.KeyDown += (s, a) => { if (a.Key == Key.Escape) HideMe(); };
            }
        }


        private void RebuildVmServerList()
        {
            VmServerList = new ObservableCollection<ProtocolBaseViewModel>(IoC.Get<GlobalData>().VmItemList.OrderByDescending(x => x.Server.LastConnTime));

            Execute.OnUIThread(() =>
            {
                foreach (var vm in VmServerList)
                {
                    vm.DisplayNameControl = vm.OrgDisplayNameControl;
                    vm.SubTitleControl = vm.OrgSubTitleControl;
                }
            });
        }

        public void ReCalcWindowHeight(bool showGridAction)
        {
            Execute.OnUIThread(() =>
            {
                // show action list
                if (showGridAction)
                {
                    GridSelectionsHeight = (Actions?.Count ?? 0) * _oneActionHeight;
                    GridActionsHeight = GridKeywordHeight + GridSelectionsHeight;
                    GridMainHeight = GridActionsHeight;
                }
                // show server list
                else
                {
                    const int nMaxCount = 8;
                    int visibleCount = VmServerList.Count();
                    if (visibleCount >= nMaxCount)
                        GridSelectionsHeight = _oneItemHeight * nMaxCount;
                    else
                        GridSelectionsHeight = _oneItemHeight * visibleCount;
                    GridMainHeight = GridKeywordHeight + GridSelectionsHeight;
                    SimpleLogHelper.Debug($"Launcher resize:  w = {_gridMainWidth}, h = {GridMainHeight}");
                }
            });
        }

        public void ShowActionsList()
        {
            if (SelectedIndex < 0
                || SelectedIndex >= VmServerList.Count)
            {
                return;
            }

            RaisePropertyChanged(nameof(SelectedItem));

            var protocolServer = VmServerList[SelectedIndex].Server;
            Actions = new ObservableCollection<ProtocolAction>(protocolServer.GetActions());
            SelectedActionIndex = 0;

            ReCalcWindowHeight(true);

            _gridMenuActions.Visibility = Visibility.Visible;

            var sb = new Storyboard();
            sb.AddSlideFromLeft(0.3, _gridMainWidth);
            sb.Begin(_gridMenuActions);
        }

        public void HideActionsList()
        {
            var sb = new Storyboard();
            sb.AddSlideToLeft(0.3, _gridMainWidth);
            sb.Completed += (o, args) =>
            {
                _gridMenuActions.Visibility = Visibility.Hidden;
                ReCalcWindowHeight(false);
            };
            sb.Begin(_gridMenuActions);
        }

        private readonly SolidColorBrush _highLightBrush = new SolidColorBrush(Color.FromArgb(80, 239, 242, 132));

        public void CalcVisibleByFilter()
        {
            var keyword = _filter.Trim();
            var tmp = TagAndKeywordEncodeHelper.DecodeKeyword(keyword);
            var tagFilters = tmp.Item1;
            var keyWords = tmp.Item2;
            TagFilters = tagFilters;

            var newList = new List<ProtocolBaseViewModel>();
            foreach (var vm in IoC.Get<GlobalData>().VmItemList)
            {
                var server = vm.Server;
                var s = TagAndKeywordEncodeHelper.MatchKeywords(server, tagFilters, keyWords);
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
                    VmServerList = new ObservableCollection<ProtocolBaseViewModel>(newList.OrderByDescending(x => x.Server.LastConnTime));
                });
            }
            ReCalcWindowHeight(false);
        }

        public void AddSelectedIndexOnVisibilityItems(int step)
        {
            var index = SelectedIndex + step;
            if (index < 0)
                index = 0;
            if (index >= VmServerList.Count)
                index = VmServerList.Count - 1;
            SelectedIndex = index;
        }


        public void ShowMe()
        {
            if (this.View is LauncherWindowView window)
            {
                SimpleLogHelper.Debug($"Call shortcut to invoke launcher Visibility = {window.Visibility}");
                window.GridMenuActions.Visibility = Visibility.Hidden;

                if (IoC.Get<MainWindowViewModel>().TopLevelViewModel != null) return;
                if (IoC.Get<ConfigurationService>().Launcher.LauncherEnabled == false) return;
                if (window.Visibility == Visibility.Visible) return;

                lock (this)
                {
                    window.WindowState = WindowState.Normal;
                    if (window.Visibility == Visibility.Visible) return;

                    Filter = "";
                    CalcVisibleByFilter();
                    // show position
                    var p = ScreenInfoEx.GetMouseSystemPosition();
                    var screenEx = ScreenInfoEx.GetCurrentScreenBySystemPosition(p);
                    window.Top = screenEx.VirtualWorkingAreaCenter.Y - GridMainHeight / 2;
                    window.Left = screenEx.VirtualWorkingAreaCenter.X - window.Width / 2;

                    window.Show();
                    window.Visibility = Visibility.Visible;
                    window.Activate();
                    window.Topmost = true; // important
                    window.Topmost = false; // important
                    window.Topmost = true; // important
                    window.Focus(); // important
                    window.TbKeyWord.Focus();
                }
            }
        }


        public void HideMe()
        {
            if (this.View is LauncherWindowView window)
            {
                if (window.Visibility != Visibility.Visible) return;
                lock (this)
                {
                    if (window.Visibility != Visibility.Visible) return;
                    window.Visibility = Visibility.Hidden;
                    window.Hide();
                    window.GridMenuActions.Visibility = Visibility.Hidden;
                    this.Filter = "";
                }
                SimpleLogHelper.Debug("Call HideMe()");
            }
        }




        /// <summary>
        /// use it after Show() has been called
        /// </summary>
        public void SetHotKey()
        {
            if (this.View is LauncherWindowView window)
            {
                GlobalHotkeyHooker.Instance.Unregist(window);
                if (IoC.Get<ConfigurationService>().Launcher.LauncherEnabled == false)
                    return;
                var r = GlobalHotkeyHooker.Instance.Register(window, (uint)IoC.Get<ConfigurationService>().Launcher.HotKeyModifiers, IoC.Get<ConfigurationService>().Launcher.HotKeyKey, this.ShowMe);
                var title = IoC.Get<ILanguageService>().Translate("messagebox_title_warning");
                switch (r.Item1)
                {
                    case GlobalHotkeyHooker.RetCode.Success:
                        break;

                    case GlobalHotkeyHooker.RetCode.ERROR_HOTKEY_NOT_REGISTERED:
                        {
                            var msg = $"{IoC.Get<ILanguageService>().Translate("hotkey_registered_fail")}: {r.Item2}";
                            SimpleLogHelper.Warning(msg);
                            MessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.None);
                            break;
                        }
                    case GlobalHotkeyHooker.RetCode.ERROR_HOTKEY_ALREADY_REGISTERED:
                        {
                            var msg = $"{IoC.Get<ILanguageService>().Translate("hotkey_already_registered")}: {r.Item2}";
                            SimpleLogHelper.Warning(msg);
                            MessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.None);
                            break;
                        }
                    default:
                        throw new ArgumentOutOfRangeException(r.Item1.ToString());
                }
            }
        }


        public void OpenSessionAndHide()
        {
            HideMe();
            var item = SelectedItem;
            if (item?.Id != null)
            {
                GlobalEventHelper.OnRequestServerConnect?.Invoke(item.Id);
            }
        }
    }
}