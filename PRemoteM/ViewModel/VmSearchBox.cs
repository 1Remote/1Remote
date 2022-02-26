using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using PRM.Core;
using PRM.Core.Model;
using PRM.Core.Protocol;
using PRM.Core.Protocol.Runner.Default;
using PRM.Model;
using PRM.Utils.Filters;
using Shawn.Utils.PageHost;
using Shawn.Utils;

namespace PRM.ViewModel
{

    public class VmSearchBox : NotifyPropertyChangedBase
    {
        private readonly double _gridMainWidth;
        private readonly double _oneItemHeight;
        private readonly double _oneActionHeight;
        private readonly double _cornerRadius;
        private readonly FrameworkElement _gridMenuActions;

        public PrmContext Context { get; }

        public VmSearchBox(PrmContext context, double gridMainWidth, double oneItemHeight, double oneActionHeight, double cornerRadius, FrameworkElement gridMenuActions)
        {
            Context = context;
            _gridMainWidth = gridMainWidth;
            _oneItemHeight = oneItemHeight;
            _oneActionHeight = oneActionHeight;
            this._gridMenuActions = gridMenuActions;
            _cornerRadius = cornerRadius;
            ReCalcWindowHeight(false);
        }

        private VmProtocolServer _selectedItem;

        public VmProtocolServer SelectedItem
        {
            get
            {
                if (_selectedItem == null)
                    if (Context.AppData.VmItemList.Count > 0
                        && _selectedIndex >= 0
                        && _selectedIndex < Context.AppData.VmItemList.Count)
                    {
                        _selectedItem = Context.AppData.VmItemList[_selectedIndex];
                    }
                return _selectedItem;
            }
            private set => SetAndNotifyIfChanged(ref _selectedItem, value);
        }

        private int _selectedIndex;

        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                SetAndNotifyIfChanged(ref _selectedIndex, value);
                if (Context.AppData.VmItemList.Count > 0
                    && _selectedIndex >= 0
                    && _selectedIndex < Context.AppData.VmItemList.Count)
                {
                    SelectedItem = Context.AppData.VmItemList[_selectedIndex];
                }
                else
                {
                    SelectedItem = null;
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
                if (_filter != value)
                {
                    SetAndNotifyIfChanged(ref _filter, value);
                }


                Task.Factory.StartNew(() =>
                {
                    var filter = _filter;
                    Thread.Sleep(70);
                    if (filter == _filter)
                    {
                        UpdateItemsList(value);
                    }
                });
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

        public void ReCalcWindowHeight(bool showGridAction)
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
                int visibleCount = Context.AppData.VmItemList.Count(vm => vm.ObjectVisibilityInLauncher == Visibility.Visible);
                if (visibleCount >= nMaxCount)
                    GridSelectionsHeight = _oneItemHeight * nMaxCount;
                else
                    GridSelectionsHeight = _oneItemHeight * visibleCount;
                GridMainHeight = GridKeywordHeight + GridSelectionsHeight;
            }
        }

        public void ShowActionsList()
        {
            if (Context.AppData.VmItemList.All(x => x.ObjectVisibilityInLauncher != Visibility.Visible)
                || SelectedIndex < 0
                || SelectedIndex >= Context.AppData.VmItemList.Count)
            {
                return;
            }

            var protocolServer = Context.AppData.VmItemList[SelectedIndex].Server;
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

        private void ShowAllItems()
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                // show all
                foreach (var vm in Context.AppData.VmItemList)
                {
                    vm.ObjectVisibilityInLauncher = Visibility.Visible;
                    vm.DispNameControl = vm.OrgDispNameControl;
                    vm.SubTitleControl = vm.OrgSubTitleControl;
                }
            });
        }

        private readonly SolidColorBrush _highLightBrush = new SolidColorBrush(Color.FromArgb(80, 239, 242, 132));

        public void UpdateItemsList(string keyword)
        {
            var tmp = TagAndKeywordFilter.DecodeKeyword(keyword);
            var tagFilters = tmp.Item1;
            var keyWords = tmp.Item2;
            TagFilters = tagFilters;


            foreach (var vm in Context.AppData.VmItemList)
            {
                Debug.Assert(vm != null);
                Debug.Assert(!string.IsNullOrEmpty(vm.Server.ClassVersion));
                Debug.Assert(!string.IsNullOrEmpty(vm.Server.Protocol));
                var server = vm.Server;
                var s = TagAndKeywordFilter.MatchKeywords(server, tagFilters, keyWords);

                App.Current.Dispatcher.Invoke(() =>
                {
                    if (s.Item1 == false)
                    {
                        vm.ObjectVisibilityInLauncher = Visibility.Collapsed;
                    }
                    else
                    {
                        if (s.Item2 == null)
                        {
                            vm.ObjectVisibilityInLauncher = Visibility.Visible;
                            vm.DispNameControl = vm.OrgDispNameControl;
                            vm.SubTitleControl = vm.OrgSubTitleControl;
                        }
                        else
                        {
                            var mrs = s.Item2;
                            if (mrs.IsMatchAllKeywords)
                            {
                                vm.ObjectVisibilityInLauncher = Visibility.Visible;
                                var dispName = server.DisplayName;
                                var subTitle = server.SubTitle;
                                var m1 = mrs.HitFlags[0];
                                if (m1.Any(x => x == true))
                                {
                                    var sp = new StackPanel() {Orientation = System.Windows.Controls.Orientation.Horizontal};
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
                                    var sp = new StackPanel() {Orientation = System.Windows.Controls.Orientation.Horizontal};
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
                    }
                });
            }
            return;




            if (keyWords.Count == 0 && tagFilters.Count == 0)
            {
                ShowAllItems();
            }
            else
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var vm in Context.AppData.VmItemList)
                    {
                        Debug.Assert(vm != null);
                        Debug.Assert(!string.IsNullOrEmpty(vm.Server.ClassVersion));
                        Debug.Assert(!string.IsNullOrEmpty(vm.Server.Protocol));
                        var server = vm.Server; 

                        bool bTagMatched = true;
                        foreach (var tagFilter in tagFilters)
                        {
                            if (tagFilter.IsExcluded == false && server.Tags.Any(x => String.Equals(x, tagFilter.TagName, StringComparison.CurrentCultureIgnoreCase)) == false)
                            {
                                bTagMatched = false;
                                break;
                            }
                            if (tagFilter.IsExcluded == true && server.Tags.Any(x => String.Equals(x, tagFilter.TagName, StringComparison.CurrentCultureIgnoreCase)) == true)
                            {
                                bTagMatched = false;
                                break;
                            }
                        }

                        if (!bTagMatched)
                        {
                            vm.ObjectVisibilityInLauncher = Visibility.Collapsed;
                        }
                        else if (keyWords.Count == 0)
                        {
                            vm.ObjectVisibilityInLauncher = Visibility.Visible;
                            vm.DispNameControl = vm.OrgDispNameControl;
                            vm.SubTitleControl = vm.OrgSubTitleControl;
                        }
                        else
                        {
                            var dispName = server.DisplayName;
                            var subTitle = server.SubTitle;

                            var mrs = Context.KeywordMatchService.Match(new List<string>() { dispName, subTitle }, keyWords);
                            if (mrs.IsMatchAllKeywords)
                            {
                                vm.ObjectVisibilityInLauncher = Visibility.Visible;

                                var m1 = mrs.HitFlags[0];
                                if (m1.Any(x => x == true))
                                {
                                    var sp = new StackPanel() { Orientation = System.Windows.Controls.Orientation.Horizontal };
                                    //// if tag name search enabled, and keyword match the tag name, show tag name as prefix
                                    //if (string.IsNullOrEmpty(prefix) == false)
                                    //    foreach (var c in $"{prefix} - ")
                                    //    {
                                    //        sp.Children.Add(new TextBlock()
                                    //        {
                                    //            Text = c.ToString(),
                                    //        });
                                    //    }
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
                            else
                            {
                                vm.ObjectVisibilityInLauncher = Visibility.Collapsed;
                            }
                        }
                    }
                });
            }


            App.Current.Dispatcher.Invoke(() =>
            {
                // reorder
                OrderItemByLastConnTime();


                // index the list to first item
                for (var i = 0; i < Context.AppData.VmItemList.Count; i++)
                {
                    var vm = Context.AppData.VmItemList[i];
                    if (vm.ObjectVisibilityInLauncher == Visibility.Visible)
                    {
                        SelectedIndex = i;
                        SelectedItem = vm;
                        break;
                    }
                }

                ReCalcWindowHeight(false);
            });
        }

        private void OrderItemByLastConnTime()
        {
            for (var i = 1; i < Context.AppData.VmItemList.Count; i++)
            {
                var s0 = Context.AppData.VmItemList[i - 1];
                var s1 = Context.AppData.VmItemList[i];
                if (s0.Server.LastConnTime < s1.Server.LastConnTime)
                {
                    Context.AppData.VmItemList = new ObservableCollection<VmProtocolServer>(Context.AppData.VmItemList.OrderByDescending(x => x.Server.LastConnTime));
                    break;
                }
            }
        }


        public void AddSelectedIndexOnVisibilityItems(int step)
        {
            var index = SelectedIndex;
            int count = 0;
            if (step > 0)
            {
                for (int i = SelectedIndex + 1; i < Context.AppData.VmItemList.Count; i++)
                {
                    if (Context.AppData.VmItemList[i].ObjectVisibilityInLauncher == Visibility.Visible)
                    {
                        ++count;
                        index = i;
                        if (count == step)
                            break;
                    }
                }
            }
            else if (step < 0)
            {
                step = Math.Abs(step);
                for (int i = SelectedIndex - 1; i >= 0; i--)
                {
                    if (Context.AppData.VmItemList[i].ObjectVisibilityInLauncher == Visibility.Visible)
                    {
                        ++count;
                        index = i;
                        if (count == step)
                            break;
                    }
                }
            }
            SelectedIndex = index;
        }
    }
}