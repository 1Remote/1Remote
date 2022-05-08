using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
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
using Markdig.Wpf;
using PRM.Model.Protocol.Base;
using XamlReader = System.Windows.Markup.XamlReader;

namespace PRM.View
{
    public class LauncherWindowViewModel : NotifyPropertyChangedBaseScreen
    {
        private double _keywordHeight;
        private double _listAreaWidth;
        private double _serverListItemHeight;
        private double _actionListItemHeight;
        private double _outlineCornerRadius;
        private FrameworkElement _gridMenuActions = new Grid();
        private Border? _noteField = null;


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
                    CalcNoteFieldVisibility();
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
                if (SetAndNotifyIfChanged(ref _gridMainHeight, value))
                {
                    GridMainClip = new RectangleGeometry(new Rect(new Size(_listAreaWidth, GridMainHeight)), _outlineCornerRadius, _outlineCornerRadius);
                }
            }
        }


        private RectangleGeometry? _gridMainClip = null;
        public RectangleGeometry? GridMainClip
        {
            get => _gridMainClip;
            set => SetAndNotifyIfChanged(ref _gridMainClip, value);
        }


        private double _gridSelectionsHeight;
        public double GridSelectionsHeight
        {
            get => _gridSelectionsHeight;
            set => SetAndNotifyIfChanged(ref _gridSelectionsHeight, value);
        }


        public Visibility GridNoteVisibility { get; set; } = Visibility.Visible;

        private double _gridNoteHeight;
        public double GridNoteHeight
        {
            get => _gridNoteHeight;
            set => SetAndNotifyIfChanged(ref _gridNoteHeight, value);
        }

        private double _noteWidth = 500;
        public double NoteWidth 
        {
            get => _noteWidth;
            set => SetAndNotifyIfChanged(ref _noteWidth, value);
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
            RebuildVmServerList();
        }

        protected override void OnViewLoaded()
        {
            HideMe();
            SetHotKey();
            CalcNoteFieldVisibility();
            GlobalEventHelper.OnLauncherHotKeyChanged += SetHotKey;
            if (this.View is LauncherWindowView window)
            {
                _gridMenuActions = window.GridMenuActions;
                _keywordHeight = (double)window.FindResource("LauncherGridKeywordHeight");
                _listAreaWidth = (double)window.FindResource("LauncherListAreaWidth");
                _serverListItemHeight = (double)window.FindResource("LauncherServerListItemHeight");
                _actionListItemHeight = (double)window.FindResource("LauncherActionListItemHeight");
                _outlineCornerRadius = (double)window.FindResource("LauncherOutlineCornerRadius");
                _noteField = window.NoteField;
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
            const int nMaxCount = 8;
            double maxSelectionHeight = _serverListItemHeight * nMaxCount;
            double maxHeight = _keywordHeight + maxSelectionHeight;
            GridNoteHeight = maxHeight;
            Execute.OnUIThread(() =>
            {
                // show action list
                if (showGridAction)
                {
                    GridSelectionsHeight = (Actions?.Count ?? 0) * _actionListItemHeight;
                    GridMainHeight = maxHeight;
                }
                // show server list
                else
                {
                    var tmp = _serverListItemHeight * VmServerList.Count();
                    GridSelectionsHeight = Math.Min(tmp, maxSelectionHeight);
                    GridMainHeight = _keywordHeight + GridSelectionsHeight;
                }
            });
        }

        public void ShowActionsList(ProtocolBase? protocolBase = null)
        {
            if (protocolBase == null)
            {
                if (SelectedIndex < 0
                    || SelectedIndex >= VmServerList.Count)
                {
                    return;
                }
                protocolBase = VmServerList[SelectedIndex].Server;
            }

            Actions = new ObservableCollection<ProtocolAction>(protocolBase.GetActions());
            SelectedActionIndex = 0;

            ReCalcWindowHeight(true);

            _gridMenuActions.Visibility = Visibility.Visible;

            var sb = new Storyboard();
            sb.AddSlideFromLeft(0.3, _listAreaWidth);
            sb.Begin(_gridMenuActions);
        }

        public void HideActionsList()
        {
            var sb = new Storyboard();
            sb.AddSlideToLeft(0.3, _listAreaWidth);
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
                    window.Left = screenEx.VirtualWorkingAreaCenter.X - window.BorderMainContent.ActualWidth / 2;

                    var noteWidth = (screenEx.VirtualWorkingArea.Width - window.BorderMainContent.ActualWidth - 100) / 2;
                    if (noteWidth < 100)
                        noteWidth = 100;
                    NoteWidth = Math.Min(noteWidth, NoteWidth);

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



        
        public void SetHotKey()
        {
            if (this.View is LauncherWindowView window)
            {
                GlobalHotkeyHooker.Instance.Unregist(window);
                if (IoC.Get<ConfigurationService>().Launcher.LauncherEnabled == false)
                    return;
                var r = GlobalHotkeyHooker.Instance.Register(window, (uint)IoC.Get<ConfigurationService>().Launcher.HotKeyModifiers, IoC.Get<ConfigurationService>().Launcher.HotKeyKey, this.ShowMe);
                switch (r.Item1)
                {
                    case GlobalHotkeyHooker.RetCode.Success:
                        break;

                    case GlobalHotkeyHooker.RetCode.ERROR_HOTKEY_NOT_REGISTERED:
                        {
                            var msg = $"{IoC.Get<ILanguageService>().Translate("hotkey_registered_fail")}: {r.Item2}";
                            SimpleLogHelper.Warning(msg);
                            MessageBoxHelper.Warning(msg, useNativeBox: true);
                            break;
                        }
                    case GlobalHotkeyHooker.RetCode.ERROR_HOTKEY_ALREADY_REGISTERED:
                        {
                            var msg = $"{IoC.Get<ILanguageService>().Translate("hotkey_already_registered")}: {r.Item2}";
                            SimpleLogHelper.Warning(msg);
                            MessageBoxHelper.Warning(msg, useNativeBox: true);
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


        private RelayCommand? _cmdHideNoteField;
        public RelayCommand CmdHideNoteField
        {
            get
            {
                return _cmdHideNoteField ??= new RelayCommand((o) =>
                {
                    IoC.Get<ConfigurationService>().Launcher.ShowNoteField = false;
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
                    IoC.Get<ConfigurationService>().Launcher.ShowNoteField = true;
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
            set => this.SetAndNotifyIfChanged(ref this._isShowNoteFieldEnabled, value);
        }

        private void CalcNoteFieldVisibility()
        {
            Visibility newVisibility;
            if (IoC.Get<ConfigurationService>().Launcher.ShowNoteField == false)
                newVisibility = Visibility.Collapsed;
            else if (string.IsNullOrEmpty(SelectedItem?.Server?.Note?.Trim()) == false)
                newVisibility = Visibility.Visible;
            else
                newVisibility = Visibility.Collapsed;
            if (GridNoteVisibility == newVisibility) return;

            IsShowNoteFieldEnabled = IoC.Get<ConfigurationService>().Launcher.ShowNoteField == false;
            GridNoteVisibility = newVisibility;
            if (_noteField != null)
            {
                if (GridNoteVisibility == Visibility.Visible)
                {
                    RaisePropertyChanged(nameof(GridNoteVisibility));
                    var sb = new Storyboard();
                    sb.AddFadeIn(0.3);
                    sb.Begin(_noteField);
                }
                else
                {
                    var sb = new Storyboard();
                    sb.AddFadeOut(0.3);
                    sb.Completed += (sender, args) => { RaisePropertyChanged(nameof(GridNoteVisibility)); };
                    sb.Begin(_noteField);
                }
            }
            else
            {
                RaisePropertyChanged(nameof(GridNoteVisibility));
            }
        }
    }
}