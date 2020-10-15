using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PRM.Core;
using PRM.Core.Model;
using PRM.Core.Protocol;
using Shawn.Utils;

namespace PRM.ViewModel
{
    public class ActionItem : NotifyPropertyChangedBase
    {
        private string _actionName = "";
        public string ActionName
        {
            get => _actionName;
            set => SetAndNotifyIfChanged(nameof(ActionName), ref _actionName, value);
        }

        public Action Run;
    }




    public class VmSearchBox : NotifyPropertyChangedBase
    {
        public VmSearchBox()
        {
        }


        private VmProtocolServer _selectedItem;
        public VmProtocolServer SelectedItem
        {
            get => _selectedItem;
            private set => SetAndNotifyIfChanged(nameof(SelectedItem), ref _selectedItem, value);
        }

        private int _selectedIndex;
        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                SetAndNotifyIfChanged(nameof(SelectedIndex), ref _selectedIndex, value);
                if (GlobalData.Instance.VmItemList.Count > 0
                    && _selectedIndex >= 0
                    && _selectedIndex < GlobalData.Instance.VmItemList.Count)
                {
                    SelectedItem = GlobalData.Instance.VmItemList[_selectedIndex];
                }
                else
                {
                    SelectedItem = null;
                }
            }
        }



        private ObservableCollection<ActionItem> _actions = new ObservableCollection<ActionItem>();
        public ObservableCollection<ActionItem> Actions
        {
            get => _actions;
            set => SetAndNotifyIfChanged(nameof(Actions), ref _actions, value);
        }

        private int _selectedActionIndex;
        public int SelectedActionIndex
        {
            get => _selectedActionIndex;
            set => SetAndNotifyIfChanged(nameof(SelectedActionIndex), ref _selectedActionIndex, value);
        }


        private string _dispNameFilter;
        public string DispNameFilter
        {
            get => _dispNameFilter;
            set
            {
                SetAndNotifyIfChanged(nameof(DispNameFilter), ref _dispNameFilter, value);
                UpdateDispList(value);
            }
        }



        private Visibility _selectionsVisibility = Visibility.Visible;
        public Visibility SelectionsVisibility
        {
            get => _selectionsVisibility;
            set => SetAndNotifyIfChanged(nameof(SelectionsVisibility), ref _selectionsVisibility, value);
        }


        private bool _popupSelectionsIsOpen = false;
        public bool PopupSelectionsIsOpen
        {
            get => _popupSelectionsIsOpen;
            set => SetAndNotifyIfChanged(nameof(PopupSelectionsIsOpen), ref _popupSelectionsIsOpen, value);
        }




        private bool _popupActionsIsOpen = false;
        public bool PopupActionsIsOpen
        {
            get => _popupActionsIsOpen;
            set => SetAndNotifyIfChanged(nameof(PopupActionsIsOpen), ref _popupActionsIsOpen, value);
        }




        public void ShowActionsList()
        {
            var actions = new ObservableCollection<ActionItem>();
            actions.Add(new ActionItem()
            {
                ActionName = SystemConfig.Instance.Language.GetText("server_card_operate_conn"),
                Run = () =>
                {
                    Debug.Assert(SelectedItem?.Server != null);
                    GlobalEventHelper.OnServerConnect?.Invoke(SelectedItem.Server.Id);
                },
            });
            actions.Add(new ActionItem()
            {
                ActionName = SystemConfig.Instance.Language.GetText("server_card_operate_edit"),
                Run = () =>
                {
                    Debug.Assert(SelectedItem?.Server != null);
                    GlobalEventHelper.OnGoToServerEditPage?.Invoke(SelectedItem.Server.Id, false);
                },
            });
            actions.Add(new ActionItem()
            {
                ActionName = SystemConfig.Instance.Language.GetText("server_card_operate_duplicate"),
                Run = () =>
                {
                    Debug.Assert(SelectedItem?.Server != null);
                    GlobalEventHelper.OnGoToServerEditPage?.Invoke(SelectedItem.Server.Id, true);
                },
            });
            if (SelectedItem.Server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortBase)))
            {
                actions.Add(new ActionItem()
                {
                    ActionName = SystemConfig.Instance.Language.GetText("server_card_operate_copy_address"),
                    Run = () =>
                    {
                        if (SelectedItem.Server is ProtocolServerWithAddrPortBase server)
                            Clipboard.SetText($"{server.Address}:{server.GetPort()}");
                    },
                });
            }
            if (SelectedItem.Server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortUserPwdBase)))
            {
                actions.Add(new ActionItem()
                {
                    ActionName = SystemConfig.Instance.Language.GetText("server_card_operate_copy_username"),
                    Run = () =>
                    {
                        if (SelectedItem.Server is ProtocolServerWithAddrPortUserPwdBase server)
                            Clipboard.SetText(server.UserName);
                    },
                });
            }
            if (SelectedItem.Server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortUserPwdBase)))
            {
                actions.Add(new ActionItem()
                {
                    ActionName = SystemConfig.Instance.Language.GetText("server_card_operate_copy_password"),
                    Run = () =>
                    {
                        if (SelectedItem.Server is ProtocolServerWithAddrPortUserPwdBase server)
                            Clipboard.SetText(server.GetDecryptedPassWord());
                    },
                });
            }
            Actions = actions;
            PopupSelectionsIsOpen = false;
            PopupActionsIsOpen = true;
            SelectedActionIndex = 0;
        }



        private void UpdateDispList(string keyword)
        {
            if (!string.IsNullOrEmpty(keyword))
            {
                // match keyword
                foreach (var item in GlobalData.Instance.VmItemList.Where(x =>
                    x.GetType() != typeof(ProtocolServerNone)))
                {
                    Debug.Assert(item != null);
                    Debug.Assert(!string.IsNullOrEmpty(item.Server.ClassVersion));
                    Debug.Assert(!string.IsNullOrEmpty(item.Server.Protocol));

                    var dispName = item.Server.DispName;
                    var subTitle = item.Server.SubTitle;

                    var f1 = dispName.IsMatchPinyinKeywords(keyword, out var m1);
                    var f2 = subTitle.IsMatchPinyinKeywords(keyword, out var m2);
                    if (f1 || f2)
                    {
                        item.ObjectVisibility = Visibility.Visible;
                        const bool enableHighLine = true;
                        if (enableHighLine)
                        {
                            if (m1 != null && m1.Any(x => x == true))
                            {
                                var sp = new StackPanel()
                                { Orientation = System.Windows.Controls.Orientation.Horizontal };
                                for (int i = 0; i < m1.Count; i++)
                                {
                                    if (m1[i])
                                        sp.Children.Add(new TextBlock()
                                        {
                                            Text = dispName[i].ToString(),
                                            Background = new SolidColorBrush(Color.FromArgb(80, 239, 242, 132)),
                                        });
                                    else
                                        sp.Children.Add(new TextBlock()
                                        {
                                            Text = dispName[i].ToString(),
                                        });
                                }

                                item.DispNameControl = sp;
                            }
                            if (m2 != null && m2.Any(x => x == true))
                            {
                                var sp = new StackPanel()
                                { Orientation = System.Windows.Controls.Orientation.Horizontal };
                                for (int i = 0; i < m2.Count; i++)
                                {
                                    if (m2[i])
                                        sp.Children.Add(new TextBlock()
                                        {
                                            Text = subTitle[i].ToString(),
                                            Background = new SolidColorBrush(Color.FromArgb(80, 239, 242, 132)),
                                        });
                                    else
                                        sp.Children.Add(new TextBlock()
                                        {
                                            Text = subTitle[i].ToString(),
                                        });
                                }

                                item.SubTitleControl = sp;
                            }
                        }
                    }
                    else
                    {
                        item.ObjectVisibility = Visibility.Collapsed;
                    }
                }
            }
            else
            {
                // show all
                foreach (var item in GlobalData.Instance.VmItemList)
                {
                    item.ObjectVisibility = Visibility.Visible;
                    item.DispNameControl = item.OrgDispNameControl;
                    item.SubTitleControl = item.OrgSubTitleControl;
                }
            }

            // index the list to first item
            for (var i = 0; i < GlobalData.Instance.VmItemList.Count; i++)
            {
                var vmClipObject = GlobalData.Instance.VmItemList[i];
                if (vmClipObject.ObjectVisibility == Visibility.Visible)
                {
                    SelectedIndex = i;
                    SelectedItem = vmClipObject;
                    break;
                }
            }

            // hide or show selection grid
            if (GlobalData.Instance.VmItemList.Any(x => x.ObjectVisibility == Visibility.Visible))
            {
                PopupSelectionsIsOpen = true;
                SelectionsVisibility = Visibility.Visible;
            }
            else
            {
                PopupSelectionsIsOpen = false;
                SelectionsVisibility = Visibility.Collapsed;
            }
            PopupActionsIsOpen = false;
        }
    }
}
