using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using PRM.Core;
using PRM.Core.DB;
using PRM.Core.Model;
using PRM.Core.Protocol;
using PRM.Core.Protocol.Putty.SSH;
using PRM.Core.Protocol.Putty.Telnet;
using PRM.Core.Protocol.RDP;
using PRM.Core.Protocol.VNC;
using PRM.View;
using Shawn.Utils;

namespace PRM.ViewModel
{
    public class VmServerEditorPage : NotifyPropertyChangedBase
    {
        public readonly VmServerListPage Host;

        public VmServerEditorPage(ProtocolServerBase server, VmServerListPage host, bool isDuplicate = false)
        {
            Server = (ProtocolServerBase)server.Clone();
            _isDuplicate = isDuplicate;
            if (_isDuplicate)
                Server.Id = 0;
            Host = host;
            IsAddMode = server.GetType() == typeof(ProtocolServerNone) || Server.Id == 0;

            // decrypt pwd
            if (Server.GetType() != typeof(ProtocolServerNone))
                SystemConfig.Instance.DataSecurity.DecryptPwd(Server);

            var assembly = typeof(ProtocolServerBase).Assembly;
            var types = assembly.GetTypes();
            // reflect remote protocols 
            {
                ProtocolList.Clear();
                ProtocolList = types.Where(item => item.IsSubclassOf(typeof(ProtocolServerBase)) && !item.IsAbstract)
                    .Where(x => x.FullName != typeof(ProtocolServerNone).FullName)
                    .Select(type => (ProtocolServerBase)Activator.CreateInstance(type)).OrderBy(x => x.GetListOrder()).ToList();
            }

            // set selected protocol
            try
            {
                ProtocolSelected = ProtocolList.First(x => x.GetType() == Server.GetType());
            }
            catch (Exception)
            {
                ProtocolSelected = ProtocolList.First();
            }

            if (!IsAddMode)
            {
                ProtocolList.Clear();
                ProtocolList.Add(ProtocolSelected);
            }
            else
            {
                if (string.IsNullOrEmpty(Server.GroupName))
                    Server.GroupName = Host.SelectedGroup;
            }

            NameSelections = GlobalData.Instance.VmItemList.Select(x => x.Server.DispName)
                .Distinct()
                .Where(x => !string.IsNullOrEmpty(x)).ToList();

            GroupSelections = GlobalData.Instance.VmItemList.Select(x => x.Server.GroupName)
                .Distinct()
                .Where(x => !string.IsNullOrEmpty(x)).ToList();

            Debug.Assert(Server.GetType() != typeof(ProtocolServerNone));
        }




        private ProtocolServerBase _server = null;
        public ProtocolServerBase Server
        {
            get => _server;
            set => SetAndNotifyIfChanged(nameof(Server), ref _server, value);
        }

        private ProtocolServerBase _protocolSelected = null;
        public ProtocolServerBase ProtocolSelected
        {
            get => _protocolSelected;
            set
            {
                if (value != _protocolSelected)
                {
                    SetAndNotifyIfChanged(nameof(ProtocolSelected), ref _protocolSelected, value);
                    ReflectProtocolEditControl();
                }
            }
        }

        private List<ProtocolServerBase> _protocolList = new List<ProtocolServerBase>();
        public List<ProtocolServerBase> ProtocolList
        {
            get => _protocolList;
            set => SetAndNotifyIfChanged(nameof(ProtocolList), ref _protocolList, value);
        }

        private bool _isDuplicate = false;

        private bool _isAddMode = true;
        public bool IsAddMode
        {
            get => _isAddMode;
            set => SetAndNotifyIfChanged(nameof(IsAddMode), ref _isAddMode, value);
        }


        private ProtocolServerFormBase _protocolEditControl = null;
        public ProtocolServerFormBase ProtocolEditControl
        {
            get => _protocolEditControl;
            set => SetAndNotifyIfChanged(nameof(ProtocolEditControl), ref _protocolEditControl, value);
        }

        
        public List<string> NameSelections { get; set; }
        public List<string> GroupSelections { get; set; }


        private RelayCommand _cmdSave;
        public RelayCommand CmdSave
        {
            get
            {
                if (_cmdSave == null)
                    _cmdSave = new RelayCommand((o) =>
                    {
                        // encrypt pwd
                        SystemConfig.Instance.DataSecurity.EncryptPwd(Server);
                        GlobalData.Instance.ServerListUpdate(Server);
                        Host.Vm.DispPage = null;
                    }, o => (this.Server.DispName.Trim() != "" && (_protocolEditControl?.CanSave() ?? false)));
                return _cmdSave;
            }
        }




        private RelayCommand _cmdCancel;
        public RelayCommand CmdCancel
        {
            get
            {
                if (_cmdCancel == null)
                    _cmdCancel = new RelayCommand((o) =>
                    {
                        Host.Vm.DispPage = null;
                    });
                return _cmdCancel;
            }
        }





        private RelayCommand _cmdImportFromFile;
        public RelayCommand CmdImportFromFile
        {
            get
            {
                if (_cmdImportFromFile == null)
                    _cmdImportFromFile = new RelayCommand((o) =>
                    {
                        var dlg = new OpenFileDialog();
                        dlg.Filter = "PRM json|*.prmj";
                        if (dlg.ShowDialog() == true)
                        {
                            string jsonString = File.ReadAllText(dlg.FileName, Encoding.UTF8);
                            var server = ServerCreateHelper.CreateFromJsonString(jsonString);
                            foreach (var protocolServerBase in ProtocolList)
                            {
                                if (server.GetType() == protocolServerBase.GetType())
                                {
                                    ProtocolSelected = protocolServerBase;
                                    break;
                                }
                            }
                            if (server != null)
                            {
                                server.Id = 0;
                                Server = server;
                                ReflectProtocolEditControl();
                            }
                        }
                    }, o => Server.Id == 0);
                return _cmdImportFromFile;
            }
        }




        private void ReflectProtocolEditControl()
        {
            Debug.Assert(ProtocolSelected != null);
            Debug.Assert(ProtocolSelected.GetType().FullName != null);
            try
            {
                var assembly = typeof(ProtocolServerBase).Assembly;
                var server = Server;
                if (IsAddMode && !_isDuplicate)
                {
                    server = (ProtocolServerBase)assembly.CreateInstance(ProtocolSelected.GetType().FullName);
                    // switch protocol and hold uname pwd.
                    if (server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortUserPwdBase))
                        && Server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortUserPwdBase)))
                        server.Update(Server, typeof(ProtocolServerWithAddrPortUserPwdBase));
                    else if (server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortBase))
                        && Server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortBase)))
                        server.Update(Server, typeof(ProtocolServerWithAddrPortBase));
                    // switch just hold base info
                    else
                        server.Update(Server, typeof(ProtocolServerBase));
                }


                //switch (server)
                //{
                //    case ProtocolServerRDP _:
                //        ProtocolEditControl = new ProtocolServerRDPForm(server);
                //        break;
                //    case ProtocolServerSSH _:
                //        ProtocolEditControl = new ProtocolServerSSHForm(server);
                //        break;
                //    case ProtocolServerTelnet _:
                //        ProtocolEditControl = new ProtocolServerTelnetForm(server);
                //        break;
                //    case ProtocolServerVNC _:
                //        ProtocolEditControl = new ProtocolServerVNCForm(server);
                //        break;
                //    default:
                //        throw new NotImplementedException();
                //}
                //Server = server;


                var types = assembly.GetTypes();
                var formName = ProtocolSelected.GetType().Name + "Form";
                var forms = types.Where(x => x.Name == formName).ToList();
                if (forms.Count == 1)
                {
                    var t = forms[0];
                    var parameters = new object[1];
                    parameters[0] = server;
                    ProtocolEditControl = (ProtocolServerFormBase)assembly.CreateInstance(t.FullName, true, System.Reflection.BindingFlags.Default, null, parameters, null, null);
                    Server = server;
                }
                else
                {
                    if (forms.Count == 0)
                        throw new NotImplementedException($"can not find class '{formName}' in {nameof(VmServerEditorPage)}");
                    else
                        throw new Exception($"error on reflecting class '{formName}' in {nameof(VmServerEditorPage)}");
                }
            }
            catch (Exception e)
            {
                SimpleLogHelper.Fatal(e);
                throw;
            }
        }
    }
}
