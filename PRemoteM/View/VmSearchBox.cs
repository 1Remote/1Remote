using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using PRM.Model;
using PRM.Utils;
using PRM.Utils.Filters;
using Shawn.Utils;
using Shawn.Utils.Wpf.PageHost;

namespace PRM.View
{

    public class VmSearchBox : NotifyPropertyChangedBase
    {
        private readonly double _gridMainWidth;
        private readonly double _oneItemHeight;
        private readonly double _oneActionHeight;
        private readonly double _cornerRadius;
        private readonly FrameworkElement _gridMenuActions;

        public PrmContext Context { get; }

        #region properties

        public VmProtocolServer SelectedItem
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
                }
            }
        }


        private ObservableCollection<VmProtocolServer> _vmServerList = new ObservableCollection<VmProtocolServer>();
        public ObservableCollection<VmProtocolServer> VmServerList
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


        private ObservableCollection<ActionForServer> _actions = new ObservableCollection<ActionForServer>();
        public ObservableCollection<ActionForServer> Actions
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

        private RectangleGeometry _gridMainClip = null;
        public RectangleGeometry GridMainClip
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

        private List<TagFilter> _tagFilters;
        public List<TagFilter> TagFilters
        {
            get => _tagFilters;
            set => SetAndNotifyIfChanged(ref _tagFilters, value);
        }

        #endregion

        public VmSearchBox(PrmContext context, double gridMainWidth, double oneItemHeight, double oneActionHeight, double cornerRadius, FrameworkElement gridMenuActions)
        {
            Context = context;
            _gridMainWidth = gridMainWidth;
            _oneItemHeight = oneItemHeight;
            _oneActionHeight = oneActionHeight;
            this._gridMenuActions = gridMenuActions;
            _cornerRadius = cornerRadius;
            ReCalcWindowHeight(false);
            RebuildVmServerList();
            Context.AppData.VmItemListDataChanged += RebuildVmServerList;
        }


        private void RebuildVmServerList()
        {
            VmServerList = new ObservableCollection<VmProtocolServer>(Context.AppData.VmItemList.OrderByDescending(x => x.Server.LastConnTime));
            App.UiDispatcher.Invoke(() =>
            {
                foreach (var vm in VmServerList)
                {
                    vm.DispNameControl = vm.OrgDispNameControl;
                    vm.SubTitleControl = vm.OrgSubTitleControl;
                }
            });
        }

        public void ReCalcWindowHeight(bool showGridAction)
        {
            App.UiDispatcher.Invoke(() =>
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
            Actions = new ObservableCollection<ActionForServer>(protocolServer.GetActions(Context, RemoteWindowPool.Instance.TabWindowCount));
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

            var newList = new List<VmProtocolServer>();
            foreach (var vm in Context.AppData.VmItemList)
            {
                var server = vm.Server;
                var s = TagAndKeywordEncodeHelper.MatchKeywords(server, tagFilters, keyWords);
                if (s.Item1 == true)
                {
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        if (s.Item2 == null)
                        {
                            vm.DispNameControl = vm.OrgDispNameControl;
                            vm.SubTitleControl = vm.OrgSubTitleControl;
                        }
                        else
                        {
                            var mrs = s.Item2;
                            if (mrs.IsMatchAllKeywords)
                            {
                                var dispName = server.DisplayName;
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
                                                Text = dispName[i].ToString(),
                                                Background = _highLightBrush,
                                            });
                                        else
                                            sp.Children.Add(new TextBlock()
                                            {
                                                Text = dispName[i].ToString(),
                                            });
                                    }

                                    vm.DispNameControl = sp;
                                }
                                else
                                {
                                    vm.DispNameControl = vm.OrgDispNameControl;
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
                App.Current.Dispatcher.Invoke(() =>
                {
                    VmServerList = new ObservableCollection<VmProtocolServer>(newList.OrderByDescending(x => x.Server.LastConnTime));
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
    }
}