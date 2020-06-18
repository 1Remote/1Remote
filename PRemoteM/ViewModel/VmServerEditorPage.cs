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
using PRM.Core.Protocol.RDP;
using PRM.View;
using Shawn.Ulits;

namespace PRM.ViewModel
{
    public class VmServerEditorPage : NotifyPropertyChangedBase
    {
        public readonly VmServerListPage Host;

        public VmServerEditorPage(ProtocolServerBase server, VmServerListPage host)
        {
            Server = server;
            Host = host;
            IsAddMode = server.GetType() == typeof(ProtocolServerNone) || server.Id == 0;

            // decrypt pwd
            if (server.GetType() != typeof(ProtocolServerNone))
                SystemConfig.Instance.DataSecurity.DecryptPwd(Server);

            var assembly = typeof(ProtocolServerBase).Assembly;
            var types = assembly.GetTypes();
            // reflect remote protocols 
            {
                ProtocolList.Clear();
                ProtocolList = types.Where(item => item.IsSubclassOf(typeof(ProtocolServerBase)) && !item.IsAbstract)
                    .Where(x => x.FullName != typeof(ProtocolServerNone).FullName)
                    .Select(type => (ProtocolServerBase)Activator.CreateInstance(type)).ToList();
            }

            // set selected protocol
            try
            {
                ProtocolSelected = ProtocolList.First(x => x.GetType() == Server.GetType());
            }
            catch (Exception exception)
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
            var assembly = typeof(ProtocolServerBase).Assembly;
            var server = Server;
            if (IsAddMode)
            {
                server = (ProtocolServerBase)assembly.CreateInstance(ProtocolSelected.GetType().FullName);
                // switch protocol and hold uname pwd.
                if (server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortUserPwdBase))
                    && Server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortUserPwdBase)))
                    server.Update(Server, typeof(ProtocolServerWithAddrPortUserPwdBase));
                // switch just hold base info
                else
                    server.Update(Server, typeof(ProtocolServerBase));
            }


            switch (server)
            {
                case ProtocolServerRDP _:
                    ProtocolEditControl = new ProtocolServerRDPForm(server);
                    break;
                case ProtocolServerSSH _:
                    ProtocolEditControl = new ProtocolServerSSHForm(server);
                    break;
                default:
                    throw new NotImplementedException();
            }
            Server = server;


            //var types = assembly.GetTypes();
            //var formName = ProtocolSelected.GetType().Name + "Form";
            //var forms = types.Where(x => x.Name == formName).ToList();
            //if (forms.Count == 1)
            //{
            //    var t = forms[0];
            //    object[] parameters = new object[1];
            //    parameters[0] = server;
            //    ProtocolEditControl = (ProtocolServerFormBase)assembly.CreateInstance(t.FullName, true, System.Reflection.BindingFlags.Default, null, parameters, null, null);
            //    Server = server;
            //}
            //else
            //{
            //    throw new ArgumentException("反射服务器编辑表单时，表单类不存在或存在重复项目！");
            //}
        }
    }
}
