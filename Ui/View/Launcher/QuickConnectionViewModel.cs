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
using _1RM.Utils.Tracing;
using _1RM.View.Editor;
using _1RM.View.Utils;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Stylet;
using File = System.IO.File;

namespace _1RM.View.Launcher
{
    public class QuickConnectionViewModel : NotifyPropertyChangedBaseScreen
    {
        public readonly QuickConnectionItem OpenConnectActionItem; // A placeholder for the "Connect" button
        public List<ProtocolBaseWithAddressPort> Protocols { get; } // A list of protocols that can be used for quick connection
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

            // Placeholder for the "Connect" button
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

        private ProtocolBase? _serverSelectionsViewSelected = null;
        public void Show()
        {
            if (this.View is not QuickConnectionView view) return;
            if (IoC.TryGet<LauncherWindowView>()?.IsClosing != false) return;

            IoC.Get<LauncherWindowViewModel>().ServerSelectionsViewVisibility = Visibility.Collapsed;
            RebuildConnectionHistory();

            // clear info
            Filter = "";
            if (SelectedProtocol is ProtocolBaseWithAddressPortUserPwd sp)
            {
                sp.Address = "";
                sp.UserName = "";
                sp.Password = "";
            }
            _serverSelectionsViewSelected = null;


            // if selected server in launcher list, and it is writable, then auto fill the server info into quick connect view
            if (IoC.Get<LauncherWindowViewModel>().ServerSelectionsViewModel is { AnyKeyExceptTabPressAfterShow: true, SelectedItem: { DataSource: { IsWritable: true }, Server: { } serverSelectionsViewSelected } })
            {
                _serverSelectionsViewSelected = serverSelectionsViewSelected;
                SelectedProtocol = Protocols.FirstOrDefault(x => x.Protocol == _serverSelectionsViewSelected.Protocol) ?? Protocols.First();
                // fill address and port
                if (_serverSelectionsViewSelected is ProtocolBaseWithAddressPort pap)
                {
                    Filter = pap.Address + ":" + pap.Port;
                }
            }


            Execute.OnUIThread(() =>
            {
                view.TbKeyWord.Focus();
                view.TbKeyWord.CaretIndex = view.TbKeyWord.Text.Length;
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
                    if (SelectedIndex >= 0 && SelectedIndex < ConnectHistory.Count && ConnectHistory.Count > 0 && ConnectHistory[SelectedIndex] != OpenConnectActionItem)
                    {
                        var p = ConnectHistory[SelectedIndex].Protocol;
                        SelectedProtocol = Protocols.FirstOrDefault(x => x.Protocol == p) ?? SelectedProtocol;
                    }
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
            if (!IoC.Get<ConfigurationService>().Launcher.AllowSaveInfoInQuickConnect)
            {
                LocalityConnectRecorder.QuickConnectionHistoryRemoveAll();
            }
            var list = LocalityConnectRecorder.QuickConnectionHistoryGetAll();
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

            var newList = LocalityConnectRecorder.QuickConnectionHistoryGetAll().Where(x => x.Host.StartsWith(keyword, StringComparison.OrdinalIgnoreCase));
            var list = newList?.ToList() ?? new List<QuickConnectionItem>();
            list.Insert(0, OpenConnectActionItem);
            ConnectHistory = new ObservableCollection<QuickConnectionItem>(list);
            IoC.Get<LauncherWindowViewModel>().ReSetWindowHeight();
        }


        public async void OpenConnection()
        {
            try
            {
                var serverSelectionsViewSelected = _serverSelectionsViewSelected;
                _serverSelectionsViewSelected = null; // release the reference to avoid memory leak

                if (this.View is not QuickConnectionView) return;
                if (IoC.TryGet<LauncherWindowView>()?.IsClosing != false) return;

                if (ConnectHistory.Count > 0
                    && SelectedIndex >= 0
                    && SelectedIndex < ConnectHistory.Count)
                {
                    var host = ConnectHistory[SelectedIndex] != OpenConnectActionItem ? ConnectHistory[SelectedIndex].Host.Trim() : Filter.Trim();
                    if (string.IsNullOrWhiteSpace(host))
                        return;

                    string protocol = SelectedProtocol.Protocol;
                    string address = host;
                    string port = "";
                    var i = host.LastIndexOf(":", StringComparison.Ordinal);
                    if (i >= 0)
                    {
                        if (int.TryParse(host.Substring(i + 1), out var intPort))
                        {
                            address = host.Substring(0, i).Trim();
                            port = intPort.ToString();
                        }
                        else
                        {
                            // invalid port, reset address to let it be invalid
                            address = "";
                        }
                    }

                    // stop if address is empty
                    if (string.IsNullOrWhiteSpace(address))
                        return;

                    // Hide Ui
                    Filter = "";
                    IoC.Get<LauncherWindowViewModel>().HideMe();

                    // create protocol
                    var server = (Protocols.FirstOrDefault(x => x.Protocol == protocol) ?? SelectedProtocol).Clone();
                    server.DisplayName = host;
                    if (server is ProtocolBaseWithAddressPort protocolBaseWithAddressPort)
                    {
                        protocolBaseWithAddressPort.Address = address;
                        if (string.IsNullOrWhiteSpace(port) == false)
                        {
                            protocolBaseWithAddressPort.Port = port;
                        }
                    }

                    if (serverSelectionsViewSelected != null)
                    {
                        server.IconBase64 = serverSelectionsViewSelected.IconBase64;
                    }

                    // pop password window if needed
                    var pwdDlg = new PasswordPopupDialogViewModel();
                    if (server is ProtocolBaseWithAddressPortUserPwd protocolBaseWithAddressPortUserPwd)
                    {
                        pwdDlg = new PasswordPopupDialogViewModel(protocolBaseWithAddressPortUserPwd is SSH or SFTP, IoC.Get<ConfigurationService>().Launcher.AllowSaveInfoInQuickConnect)
                        {
                            Title = $"[{server.ProtocolDisplayName}]{host}"
                        };

                        // if selected server in launcher list, and it is writable, then auto fill the server password into quick connect view
                        // fill user and password
                        if (serverSelectionsViewSelected is ProtocolBaseWithAddressPortUserPwd pup)
                        {
                            pwdDlg.UserName = pup.UserName;
                            pwdDlg.Password = UnSafeStringEncipher.DecryptOrReturnOriginalString(pup.Password);
                            pwdDlg.PrivateKey = UnSafeStringEncipher.DecryptOrReturnOriginalString(pup.PrivateKey);
                        }
                        // otherwise, fill in the last used username and password
                        else
                        {
                            if (IoC.Get<ConfigurationService>().Launcher.AllowSaveInfoInQuickConnect)
                            {
                                // find saved username and password then fill in
                                var history = LocalityConnectRecorder.QuickConnectionHistoryGetAll().FirstOrDefault(x => x.Host == host && x.Protocol == protocol);
                                if (history != null)
                                {
                                    (pwdDlg.UserName, pwdDlg.Password, pwdDlg.PrivateKey) = history.GetUserPassword();
                                }
                            }
                        }
                        if (!string.IsNullOrEmpty(pwdDlg.PrivateKey) && File.Exists(pwdDlg.PrivateKey))
                        {
                            pwdDlg.UsePrivateKeyForConnect = true;
                        }

                        MaskLayerController.ShowWindowWithMask(pwdDlg);

                        if (await pwdDlg.WaitDialogResult() == true)
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
                        }
                        else
                        {
                            return;
                        }
                    }


                    // save history
                    if (IoC.Get<ConfigurationService>().Launcher.AllowSaveInfoInQuickConnect)
                    {
                        if (pwdDlg.CanRememberInfo)
                        {
                            LocalityConnectRecorder.QuickConnectionHistoryAddOrUpdate(host, protocol, pwdDlg.UserName, pwdDlg.Password, pwdDlg.PrivateKey);
                        }
                        else
                        {
                            LocalityConnectRecorder.QuickConnectionHistoryAddOrUpdate(host, protocol, "", "", "");
                        }
                    }

                    // connect
                    GlobalEventHelper.OnRequestQuickConnect?.Invoke(server, fromView: $"{nameof(LauncherWindowView)} - {nameof(QuickConnectionView)}");
                }
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
                UnifyTracing.Error(e, new Dictionary<string, string>()
                {
                    {"Action", "QuickConnectionViewModel.OpenConnection"}
                });
            }
        }
    }
}
