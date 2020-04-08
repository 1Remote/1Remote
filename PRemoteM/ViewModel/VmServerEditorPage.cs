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
using PRM.Core.Protocol.RDP;
using PRM.Core.UI.VM;
using PRM.View;
using Shawn.Ulits.PageHost;

namespace PRM.ViewModel
{
    public class VmServerEditorPage : NotifyPropertyChangedBase
    {
        private ProtocolServerBase _server = null;
        public ProtocolServerBase Server
        {
            get => _server;
            set => SetAndNotifyIfChanged(nameof(Server), ref _server, value);
        }

        public readonly VmServerListPage Host;

        public VmServerEditorPage(ProtocolServerBase server, VmServerListPage host)
        {
            Server = server;
            Host = host;

            var assembly = typeof(ProtocolServerBase).Assembly;
            var types = assembly.GetTypes();
            // reflect remote protocols 
            {
                ProtocolList.Clear();
                ProtocolList = types.Where(item => item.IsSubclassOf(typeof(ProtocolServerBase)))
                    .Where(x => x.FullName != typeof(ProtocolServerNone).FullName)
                    .Select(type => (ProtocolServerBase)Activator.CreateInstance(type)).ToList();
            }

            // set selected protocol
            try
            {
                ProtocolSelected = ProtocolList.First(x => x.GetType() == Server.GetType());
            }
            catch (Exception)
            {
                ProtocolSelected = ProtocolList.First();
                Server = (ProtocolServerBase)assembly.CreateInstance(ProtocolSelected.GetType().FullName);
            }

            Debug.Assert(Server.GetType() != typeof(ProtocolServerNone));

            // reflect create remote protocols form
            ReflectProtocolEditControl();
        }




        private ProtocolServerBase _protocolSelected = null;
        public ProtocolServerBase ProtocolSelected
        {
            get => _protocolSelected;
            set => SetAndNotifyIfChanged(nameof(ProtocolSelected), ref _protocolSelected, value);
        }

        private List<ProtocolServerBase> _protocolList = new List<ProtocolServerBase>();
        public List<ProtocolServerBase> ProtocolList
        {
            get => _protocolList;
            set => SetAndNotifyIfChanged(nameof(ProtocolList), ref _protocolList, value);
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
                        Global.GetInstance().ServerListUpdate(Server);
                        Host.Host.DispPage = null;
                    }, o => (this.Server.DispName.Trim() != "" && _protocolEditControl.CanSave()));
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
                        Host.Host.DispPage = null;
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
                            var server = ServerFactory.GetInstance().CreateFromJsonString(jsonString);
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
            var assembly = typeof(ProtocolServerBase).Assembly;
            var types = assembly.GetTypes();
            var formNmae = typeof(ProtocolServerRDP).Name + "Form";
            var forms = types.Where(x => x.Name == formNmae).ToList();
            if (forms.Count == 1)
            {
                var t = forms[0];
                object[] parameters = new object[1];
                parameters[0] = Server;
                ProtocolEditControl = (ProtocolServerFormBase)assembly.CreateInstance(t.FullName, true, System.Reflection.BindingFlags.Default, null, parameters, null, null);
            }
            else
            {
                throw new ArgumentException("反射服务器编辑表单时，表单类不存在或存在重复项目！");
            }

            //if (ProtocolSelected.GetType() == typeof(ProtocolServerRDP))
            //{
            //    ProtocolEditControl = new ProtocolServerRDPForm(Server);
            //}
            //else
            //{
            //}
        }
    }
}
