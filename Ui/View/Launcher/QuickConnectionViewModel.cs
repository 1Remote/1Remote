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
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Service;
using _1RM.Utils;
using _1RM.View.Editor;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.PageHost;
using Stylet;

namespace _1RM.View.Launcher
{
    public class QuickConnectionViewModel : NotifyPropertyChangedBaseScreen
    {
        public TextBox TbKeyWord { get; private set; } = new TextBox();
        //private LauncherWindowViewModel _launcherWindowViewModel;
        public List<ProtocolBaseWithAddressPort> Protocols { get; }
        public QuickConnectionViewModel()
        {
            Protocols = new List<ProtocolBaseWithAddressPort>();
            var protocols = ProtocolBase.GetAllSubInstance().Select(x => x as ProtocolBaseWithAddressPort).Where(x=>x != null).ToList();
            foreach (var protocol in protocols)
            {
                if (protocol != null 
                    && protocol.Protocol != RdpApp.ProtocolName)
                {
                    Protocols.Add(protocol);
                }
            }
            _selectedProtocol = Protocols.First(x => x.Protocol == RDP.ProtocolName);
        }

        public void Init(LauncherWindowViewModel launcherWindowViewModel)
        {

        }

        protected override void OnViewLoaded()
        {
            if (this.View is QuickConnectionView window)
            {
                TbKeyWord = window.TbKeyWord;
                TbKeyWord.Focus();
            }
        }

        private ProtocolBaseWithAddressPort _selectedProtocol;
        public ProtocolBaseWithAddressPort SelectedProtocol
        {
            get => _selectedProtocol;
            set => SetAndNotifyIfChanged(ref _selectedProtocol, value);
        }

        private string _filter = "";
        public string Filter
        {
            get => _filter;
            set => SetAndNotifyIfChanged(ref _filter, value);
        }


        private ObservableCollection<ProtocolAction> _actions = new ObservableCollection<ProtocolAction>();
        public ObservableCollection<ProtocolAction> Actions
        {
            get => _actions;
            set => SetAndNotifyIfChanged(ref _actions, value);
        }

        public void OpenConnection(string address)
        {
            var server = SelectedProtocol.Clone();
            server.DisplayName = address;

            if (server is ProtocolBaseWithAddressPort protocolBaseWithAddressPort)
            {
                protocolBaseWithAddressPort.Address = address;
                var i = address.LastIndexOf(":", StringComparison.Ordinal);
                if (i > 0)
                {
                    var portStr = address.Substring(i + 1);
                    if (int.TryParse(portStr, out var port))
                    {
                        protocolBaseWithAddressPort.Port = port.ToString();
                        protocolBaseWithAddressPort.Address = address.Substring(0, i);
                    }
                }
            }


            if (server is ProtocolBaseWithAddressPortUserPwd protocolBaseWithAddressPortUserPwd)
            {
                var pwdDlg = IoC.Get<PasswordPopupDialogViewModel>();
                pwdDlg.Title = "TXT: connect to " + Filter;
                pwdDlg.Result.UserName = protocolBaseWithAddressPortUserPwd.UserName;
                if (IoC.Get<IWindowManager>().ShowDialog(pwdDlg) == true)
                {
                    protocolBaseWithAddressPortUserPwd.UserName = pwdDlg.Result.UserName;
                    protocolBaseWithAddressPortUserPwd.Password = pwdDlg.Result.Password;
                }
                else
                {
                    return;
                }
            }
            else
            {
                return;
            }

            GlobalEventHelper.OnRequestQuickConnect?.Invoke(server);
        }
    }
}
