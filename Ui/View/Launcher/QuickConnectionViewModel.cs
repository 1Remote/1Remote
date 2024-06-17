using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using _1RM.Model;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Service;
using _1RM.Service.Locality;
using _1RM.Utils;
using _1RM.View.Editor;
using Shawn.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;
using Stylet;

namespace _1RM.View.Launcher
{
    public class QuickConnectionViewModel : NotifyPropertyChangedBaseScreen
    {
        public readonly QuickConnectionItem OpenConnectActionItem;
        //private LauncherWindowViewModel _launcherWindowViewModel;
        public List<ProtocolBaseWithAddressPort> Protocols { get; }
        public QuickConnectionViewModel()
        {
            Protocols = new List<ProtocolBaseWithAddressPort>();
            var protocols = ProtocolBase.GetAllSubInstance().Select(x => x as ProtocolBaseWithAddressPort).Where(x => x != null).ToList();
            foreach (var protocol in protocols)
            {
                if (protocol != null
                    && protocol.Protocol != RdpApp.ProtocolName)
                {
                    Protocols.Add(protocol);
                }
            }
            _selectedProtocol = Protocols.First(x => x.Protocol == RDP.ProtocolName);


            OpenConnectActionItem = new QuickConnectionItem()
            {
                Host = IoC.Translate("Connect"),
            };
        }

        protected override void OnViewLoaded()
        {
            if (this.View is QuickConnectionView view)
            {
                Execute.OnUIThreadSync(() => { view.TbKeyWord.Focus(); });
                RebuildConnectionHistory();
            }
        }

        public void Show()
        {
            if (this.View is not QuickConnectionView view) return;
            if (IoC.TryGet<LauncherWindowView>()?.IsClosing != false) return;

            IoC.Get<LauncherWindowViewModel>().ServerSelectionsViewVisibility = Visibility.Collapsed;
            RebuildConnectionHistory();


            if (IoC.Get<LauncherWindowViewModel>().ServerSelectionsViewModel is { AnyKeyExceptTabPressAfterShow: true, SelectedItem: { DataSource: {IsWritable: true}, Server: {} p}})
            {
                SelectedProtocol = Protocols.FirstOrDefault(x => x.Protocol == p.Protocol) ?? Protocols.First();
                if (p is ProtocolBaseWithAddressPort pbap)
                {
                    Filter = pbap.Address + ":" + pbap.Port;
                }
                if (p is ProtocolBaseWithAddressPortUserPwd pup && SelectedProtocol is ProtocolBaseWithAddressPortUserPwd pup2)
                {
                    pup2.UserName = pup.UserName;
                    pup2.Password = pup.Password;
                }
            }


            Execute.OnUIThread(() =>
            {
                view.TbKeyWord.Focus();
                view.TbKeyWord.CaretIndex  = view.TbKeyWord.Text.Length;
            });
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
                    _debounceDispatcher.Debounce(100, (obj) =>
                    {
                        if (value == _filter)
                        {
                            CalcVisibleByFilter();
                        }
                    });
                }
            }
        }

        private ProtocolBaseWithAddressPort _selectedProtocol;
        public ProtocolBaseWithAddressPort SelectedProtocol
        {
            get => _selectedProtocol;
            set => SetAndNotifyIfChanged(ref _selectedProtocol, value);
        }


        private int _selectedIndex = 0;
        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (SetAndNotifyIfChanged(ref _selectedIndex, value))
                {
                    if (this.View is not QuickConnectionView view) return;
                    if (IoC.TryGet<LauncherWindowView>()?.IsClosing != false) return;
                    Execute.OnUIThread(() => { view.ListBoxHistory.ScrollIntoView(view.ListBoxHistory.SelectedItem); });
                }
            }
        }

        private ObservableCollection<QuickConnectionItem> _connectHistory = new ObservableCollection<QuickConnectionItem>();
        public ObservableCollection<QuickConnectionItem> ConnectHistory
        {
            get => _connectHistory;
            set
            {
                if (SetAndNotifyIfChanged(ref _connectHistory, value))
                {
                    SelectedIndex = 0;
                }
            }
        }


        public void RebuildConnectionHistory()
        {
            var list = LocalityConnectRecorder.QuickConnectionHistoryGet();
            list.Insert(0, OpenConnectActionItem);
            ConnectHistory = new ObservableCollection<QuickConnectionItem>(list);
            IoC.Get<LauncherWindowViewModel>().ReSetWindowHeight();
        }


        public double ReCalcGridMainHeight()
        {
            var tmp = LauncherWindowViewModel.LAUNCHER_SERVER_LIST_ITEM_HEIGHT * ConnectHistory.Count;
            var ret = LauncherWindowViewModel.LAUNCHER_GRID_KEYWORD_HEIGHT + tmp;
            return ret;
        }


        private string _lastKeyword = string.Empty;
        public void CalcVisibleByFilter()
        {
            if (this.View is not QuickConnectionView view) return;
            if (IoC.TryGet<LauncherWindowView>()?.IsClosing != false) return;
            if (string.IsNullOrEmpty(_filter) == false && _lastKeyword == _filter) return;

            _lastKeyword = _filter;

            var keyword = _filter.Trim();
            if (string.IsNullOrEmpty(keyword))
            {
                RebuildConnectionHistory();
                return;
            }

            var newList = LocalityConnectRecorder.QuickConnectionHistoryGet().Where(x => x.Host.StartsWith(keyword, StringComparison.OrdinalIgnoreCase));
            var list = newList?.ToList() ?? new List<QuickConnectionItem>();
            list.Insert(0, OpenConnectActionItem);
            ConnectHistory = new ObservableCollection<QuickConnectionItem>(list);
            IoC.Get<LauncherWindowViewModel>().ReSetWindowHeight();
        }


        public void OpenConnection()
        {
            if (this.View is not QuickConnectionView view) return;
            if (IoC.TryGet<LauncherWindowView>()?.IsClosing != false) return;

            if (ConnectHistory.Count > 0
                && SelectedIndex >= 0
                && SelectedIndex < ConnectHistory.Count)
            {
                var host = Filter;
                var protocol = SelectedProtocol.Protocol;
                var item = ConnectHistory[SelectedIndex];
                if (item == OpenConnectActionItem
                    && string.IsNullOrWhiteSpace(host))
                    return;


                // if open current input
                if (item == OpenConnectActionItem)
                {
                    //host = Filter;
                    //protocol = SelectedProtocol.Protocol;
                }
                // if open from history
                else
                {
                    host = item.Host;
                    protocol = item.Protocol;
                }

                // Hide Ui
                Filter = "";
                IoC.Get<LauncherWindowViewModel>().HideMe();

                // create protocol
                var server = (Protocols.FirstOrDefault(x => x.Protocol == protocol) ?? SelectedProtocol).Clone();
                server.DisplayName = host;
                if (server is ProtocolBaseWithAddressPort protocolBaseWithAddressPort)
                {
                    protocolBaseWithAddressPort.Address = host;
                    var i = host.LastIndexOf(":", StringComparison.Ordinal);
                    if (i > 0)
                    {
                        var portStr = host.Substring(i + 1);
                        if (int.TryParse(portStr, out var port))
                        {
                            protocolBaseWithAddressPort.Port = port.ToString();
                            protocolBaseWithAddressPort.Address = host.Substring(0, i);
                        }
                    }
                }

                // pop password window if needed
                if (server is ProtocolBaseWithAddressPortUserPwd protocolBaseWithAddressPortUserPwd)
                {
                    var pwdDlg = new PasswordPopupDialogViewModel(protocolBaseWithAddressPortUserPwd is SSH or SFTP);
                    pwdDlg.Title = $"[{server.ProtocolDisplayName}]{host}";
                    if (string.IsNullOrEmpty(pwdDlg.UserName))
                    {
                        pwdDlg.UserName = protocolBaseWithAddressPortUserPwd.UserName;
                        pwdDlg.Password = UnSafeStringEncipher.DecryptOrReturnOriginalString(protocolBaseWithAddressPortUserPwd.Password) ?? protocolBaseWithAddressPortUserPwd.Password;
                    }
                    if (IoC.Get<IWindowManager>().ShowDialog(pwdDlg) == true)
                    {
                        protocolBaseWithAddressPortUserPwd.UserName = pwdDlg.UserName;
                        if (pwdDlg.UsePrivateKeyForConnect)
                        {
                            protocolBaseWithAddressPortUserPwd.UsePrivateKeyForConnect = true;
                            protocolBaseWithAddressPortUserPwd.Password = "";
                            protocolBaseWithAddressPortUserPwd.PrivateKey = pwdDlg.PrivateKey;
                        }
                        else
                        {
                            protocolBaseWithAddressPortUserPwd.UsePrivateKeyForConnect = false;
                            protocolBaseWithAddressPortUserPwd.PrivateKey = "";
                            protocolBaseWithAddressPortUserPwd.Password = pwdDlg.Password;
                        }
                        pwdDlg.PrivateKey = "";
                        pwdDlg.Password = "";
                    }
                    else
                    {
                        return;
                    }
                }

                MsAppCenterHelper.TraceSpecial("Quick connect", server.Protocol);

                // save history
                LocalityConnectRecorder.QuickConnectionHistoryAdd(new QuickConnectionItem() { Host = host, Protocol = protocol });
                GlobalEventHelper.OnRequestQuickConnect?.Invoke(server, fromView: $"{nameof(LauncherWindowView)} - {nameof(QuickConnectionView)}");
            }
        }
    }
}
