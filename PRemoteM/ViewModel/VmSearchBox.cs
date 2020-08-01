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
using Shawn.Ulits;

namespace PRM.ViewModel
{
    public class ProtocolServerBaseInSearchBox : NotifyPropertyChangedBase
    {
        private ProtocolServerBase _server = null;
        public ProtocolServerBase Server
        {
            get => _server;
            set => SetAndNotifyIfChanged(nameof(Server), ref _server, value);
        }

        public ProtocolServerBaseInSearchBox(ProtocolServerBase psb)
        {
            Server = psb;
            DispNameControl = (new TextBlock()
            {
                Text = psb.DispName,
            });
            SubTitleControl = (new TextBlock()
            {
                Text = psb.SubTitle,
            });
        }


        private object _dispNameControl = null;
        public object DispNameControl
        {
            get => _dispNameControl;
            set => SetAndNotifyIfChanged(nameof(DispNameControl), ref _dispNameControl, value);
        }



        private object _subTitleControl = null;
        public object SubTitleControl
        {
            get => _subTitleControl;
            set => SetAndNotifyIfChanged(nameof(SubTitleControl), ref _subTitleControl, value);
        }
    }

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
            UpdateDispList("");
        }



        private ObservableCollection<ProtocolServerBaseInSearchBox> _servers = new ObservableCollection<ProtocolServerBaseInSearchBox>();
        /// <summary>
        /// ServerList data source for listbox
        /// </summary>
        public ObservableCollection<ProtocolServerBaseInSearchBox> Servers
        {
            get => _servers;
            set
            {
                SetAndNotifyIfChanged(nameof(Servers), ref _servers, value);
                if (Servers.Count > 0 && _selectedServerIndex >= 0 && _selectedServerIndex < Servers.Count)
                {
                    SelectedServer = Servers[_selectedServerIndex];
                }
                else
                {
                    SelectedServer = null;
                }
            }
        }


        private ProtocolServerBaseInSearchBox _selectedServer;
        public ProtocolServerBaseInSearchBox SelectedServer
        {
            get => _selectedServer;
            private set => SetAndNotifyIfChanged(nameof(SelectedServer), ref _selectedServer, value);
        }

        private int _selectedServerIndex;
        public int SelectedServerIndex
        {
            get => _selectedServerIndex;
            set
            {
                SetAndNotifyIfChanged(nameof(SelectedServerIndex), ref _selectedServerIndex, value);
                if (Servers.Count > 0 && _selectedServerIndex >= 0 && _selectedServerIndex < Servers.Count)
                {
                    SelectedServer = Servers[_selectedServerIndex];
                }
                else
                {
                    SelectedServer = null;
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



        // TODO OnLanguageChanged


        public void ShowActionsList()
        {
            var actions = new ObservableCollection<ActionItem>();
            actions.Add(new ActionItem()
            {
                ActionName = SystemConfig.Instance.Language.GetText("server_card_operate_conn"),
                Run = () =>
                {
                    Debug.Assert(SelectedServer?.Server != null);
                    GlobalEventHelper.OnServerConnect?.Invoke(SelectedServer.Server.Id);
                },
            });
            actions.Add(new ActionItem()
            {
                ActionName = SystemConfig.Instance.Language.GetText("server_card_operate_edit"),
                Run = () =>
                {
                    Debug.Assert(SelectedServer?.Server != null);
                    GlobalEventHelper.OnGoToServerEditPage?.Invoke(SelectedServer.Server.Id, false);
                },
            });
            actions.Add(new ActionItem()
            {
                ActionName = SystemConfig.Instance.Language.GetText("server_card_operate_duplicate"),
                Run = () =>
                {
                    Debug.Assert(SelectedServer?.Server != null);
                    GlobalEventHelper.OnGoToServerEditPage?.Invoke(SelectedServer.Server.Id, true);
                },
            });
            if (SelectedServer.Server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortBase)))
            {
                actions.Add(new ActionItem()
                {
                    ActionName = SystemConfig.Instance.Language.GetText("server_card_operate_copy_address"),
                    Run = () =>
                    {
                        if (SelectedServer.Server is ProtocolServerWithAddrPortBase server)
                            Clipboard.SetText($"{server.Address}:{server.GetPort()}");
                    },
                });
            }
            if (SelectedServer.Server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortUserPwdBase)))
            {
                actions.Add(new ActionItem()
                {
                    ActionName = SystemConfig.Instance.Language.GetText("server_card_operate_copy_username"),
                    Run = () =>
                    {
                        if (SelectedServer.Server is ProtocolServerWithAddrPortUserPwdBase server)
                            Clipboard.SetText(server.UserName);
                    },
                });
            }
            if (SelectedServer.Server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortUserPwdBase)))
            {
                actions.Add(new ActionItem()
                {
                    ActionName = SystemConfig.Instance.Language.GetText("server_card_operate_copy_password"),
                    Run = () =>
                    {
                        if (SelectedServer.Server is ProtocolServerWithAddrPortUserPwdBase server)
                            Clipboard.SetText(server.GetDecryptedPassWord());
                    },
                });
            }
            Actions = actions;
            PopupSelectionsIsOpen = false;
            PopupActionsIsOpen = true;
            SelectedActionIndex = 0;
        }



        private void UpdateDispList(string keyWord)
        {
            Servers.Clear();

            var tmp = new List<ProtocolServerBaseInSearchBox>();
            if (!string.IsNullOrEmpty(keyWord))
            {
                var keyWords = keyWord.Split(new string[]{" "}, StringSplitOptions.RemoveEmptyEntries);
                var keyWordIsMatch = new List<bool>(keyWords.Length);
                for (var i = 0; i < keyWords.Length; i++)
                    keyWordIsMatch.Add(false);

                // match keyword
                foreach (var item in GlobalData.Instance.ServerList.Where(x => x.GetType() != typeof(ProtocolServerNone)))
                {
                    Debug.Assert(!string.IsNullOrEmpty(item.ClassVersion));
                    Debug.Assert(!string.IsNullOrEmpty(item.Protocol));

                    var mDispName = new List<List<bool>>();
                    var mSubTitle = new List<List<bool>>();
                    var dispName = item.DispName;
                    var subTitle = item.SubTitle;
                    for (var i = 0; i < keyWordIsMatch.Count; i++)
                    {
                        var f1 = dispName.IsMatchPinyinKeyWords(keyWords[i], out var m1);
                        var f2 = subTitle.IsMatchPinyinKeyWords(keyWords[i], out var m2);
                        mDispName.Add(m1);
                        mSubTitle.Add(m2);
                        keyWordIsMatch[i] = f1 || f2;
                    }

                    if (keyWordIsMatch.All(x => x == true))
                    {
                        var m1 = new List<bool>();
                        var m2 = new List<bool>();
                        for (var i = 0; i < dispName.Length; i++)
                            m1.Add(false);
                        for (var i = 0; i < subTitle.Length; i++)
                            m2.Add(false);
                        for (var i = 0; i < keyWordIsMatch.Count; i++)
                        {
                            if (mDispName[i] != null)
                                for (int j = 0; j < mDispName[i].Count; j++)
                                    m1[j] |= mDispName[i][j];
                            if (mSubTitle[i] != null)
                                for (int j = 0; j < mSubTitle[i].Count; j++)
                                    m2[j] |= mSubTitle[i][j];
                        }


                        var semite = new ProtocolServerBaseInSearchBox(item);
                        const bool enableHighLine = true;
                        // highline matched chars.
                        if (enableHighLine)
                        {
                            if (m1.Any(x => x == true))
                            {
                                var sp = new StackPanel() { Orientation = System.Windows.Controls.Orientation.Horizontal };
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

                                semite.DispNameControl = sp;
                            }

                            if (m2.Any(x => x == true))
                            {
                                var sp = new StackPanel() { Orientation = System.Windows.Controls.Orientation.Horizontal };
                                var subtitle = item.SubTitle;
                                for (int i = 0; i < m2.Count; i++)
                                {
                                    if (m2[i])
                                        sp.Children.Add(new TextBlock()
                                        {
                                            Text = subtitle[i].ToString(),
                                            Background = new SolidColorBrush(Color.FromArgb(120, 239, 242, 132)),
                                        });
                                    else
                                        sp.Children.Add(new TextBlock()
                                        {
                                            Text = subtitle[i].ToString(),
                                        });
                                }

                                semite.SubTitleControl = sp;
                            }
                        }

                        tmp.Add(semite);
                    }
                }
            }

            var odometer = tmp.OrderByDescending(x => x.Server.LastConnTime);

            if (!odometer.Any())
                PopupSelectionsIsOpen = false;
            else
            {
                foreach (var searchBox in odometer)
                {
                    Servers.Add(searchBox);
                }
                SelectedServerIndex = 0;
                PopupSelectionsIsOpen = true;
            }
        }
    }
}
