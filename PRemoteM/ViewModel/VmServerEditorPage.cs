using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using PRM.Controls;
using PRM.Core;
using PRM.Core.Model;
using PRM.Core.Protocol;
using PRM.Core.Protocol.Extend;
using PRM.Core.Protocol.FileTransmit.FTP;
using PRM.Core.Protocol.FileTransmit.SFTP;
using PRM.Core.Protocol.Putty.SSH;
using PRM.Core.Protocol.Putty.Telnet;
using PRM.Core.Protocol.RDP;
using PRM.Core.Protocol.VNC;
using PRM.View.ProtocolEditors;
using Shawn.Utils;

namespace PRM.ViewModel
{
    public class VmServerEditorPage : NotifyPropertyChangedBase
    {
        private readonly PrmContext _context;
        public bool IsAddMode => _orgServer?.Id == 0;


        #region single edit
        /// <summary>
        /// to remember original protocol's options, for restore use
        /// </summary>
        private readonly ProtocolServerBase _orgServer = null;
        public VmServerEditorPage(PrmContext context, ProtocolServerBase server, bool isDuplicate = false)
        {
            _context = context;
            Server = (ProtocolServerBase)server.Clone();
            if (isDuplicate)
                Server.Id = 0; // set id = 0 and turn into edit mode
            _orgServer = (ProtocolServerBase)Server.Clone();
            Title = "";
            Init();
        }
        #endregion

        #region Buck edit
        /// <summary>
        /// to remember original protocols' options, for restore use
        /// </summary>
        private readonly IEnumerable<ProtocolServerBase> _orgServers = null;
        private readonly Type _orgServersCommonType = null;

        public VmServerEditorPage(PrmContext context, IEnumerable<ProtocolServerBase> servers)
        {
            var serverBases = servers as ProtocolServerBase[] ?? servers.ToArray();
            // must be bulk edit
            Debug.Assert(serverBases.Count() > 1);
            // init title
            Title = context.LanguageService.Translate("server_editor_bulk_editing_title");
            foreach (var serverBase in serverBases)
            {
                Title += serverBase.DisplayName;
                if (serverBases.Last() != serverBase)
                    Title += ", ";
            }

            _context = context;

            Server = (ProtocolServerBase)serverBases.First().Clone();

            _orgServers = serverBases;


            // find the common base class
            var types = new List<Type>();
            foreach (var server in serverBases)
            {
                if (types.All(x => x != server.GetType()))
                    types.Add(server.GetType());
            }

            var type = types.First();
            for (int i = 1; i < types.Count; i++)
            {
                type = AssemblyHelper.FindCommonBaseClass(type, types[i]);
            }
            Debug.Assert(type.IsSubclassOf(typeof(ProtocolServerBase)));
            _orgServersCommonType = type;

            // copy the same value properties
            // set the different options to `Server_editor_different_options` or null
            var properties = _orgServersCommonType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var property in properties)
            {
                if (property.SetMethod?.IsPublic != true || property.SetMethod.IsAbstract != false) continue;
                var x = serverBases.Select(x => property.GetValue(x)).ToArray();
                if (x.Distinct().Count() <= 1) continue;
                if (property.PropertyType == typeof(string))
                    property.SetValue(Server, Server.Server_editor_different_options);
                else
                    property.SetValue(Server, null);
            }

            // tags
            var tags = new List<string>();
            if (serverBases.All(x => x.Tags.Count == serverBases.First().Tags.Count))
            {
                bool flag = true;
                foreach (var tag in serverBases.First().Tags)
                {
                    if (serverBases.All(x => x.Tags.Contains(tag)) == false)
                    {
                        flag = false;
                        break;
                    }
                }

                if (serverBases.First().Tags.Count == 0 || flag)
                    tags = new List<string>(serverBases.First().Tags);
                else
                    tags.Add(Server.Server_editor_different_options);
            }
            else
                tags.Add(Server.Server_editor_different_options);
            Server.Tags = tags;

            _orgServer = Server.Clone();
            // init ui
            ReflectProtocolEditControl(_orgServersCommonType);

            Init();
        }

        #endregion


        private void Init()
        {
            ProtocolList.Clear();
            // init protocol list for single add / edit mode
            if (_orgServers?.Any() != true)
            {
                // reflect remote protocols
                ProtocolList = ProtocolServerBase.GetAllSubInstance();
                // set selected protocol
                try
                {
                    SelectedProtocol = ProtocolList.First(x => x.GetType() == Server.GetType());
                }
                catch (Exception)
                {
                    SelectedProtocol = ProtocolList.First();
                }
            }

            // decrypt pwd
            _context.DataService.DecryptToConnectLevel(Server);
            NameSelections = _context.AppData.VmItemList.Select(x => x.Server.DisplayName).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();
            TagSelections = _context.AppData.Tags.Select(x => x.Name).Distinct().OrderBy(x => x).ToList();
        }

        public string Title { get; set; }

        private ProtocolServerBase _server = null;

        public ProtocolServerBase Server
        {
            get => _server;
            set => SetAndNotifyIfChanged(nameof(Server), ref _server, value);
        }

        private ProtocolServerBase _selectedProtocol = null;

        public ProtocolServerBase SelectedProtocol
        {
            get => _selectedProtocol;
            set
            {
                if (_orgServers != null)
                {
                    // bulk edit can not change protocol
                }
                if (value == _selectedProtocol) return;

                SetAndNotifyIfChanged(nameof(SelectedProtocol), ref _selectedProtocol, value);
                if (_orgServer.GetType() == Server.GetType())
                    _orgServer.Update(Server);
                UpdateServerWhenProtocolChanged(SelectedProtocol.GetType());
                ReflectProtocolEditControl(SelectedProtocol.GetType());
            }
        }
        public List<ProtocolServerBase> ProtocolList { get; set; } = new List<ProtocolServerBase>();


        private ProtocolServerFormBase _protocolEditControl = null;

        public ProtocolServerFormBase ProtocolEditControl
        {
            get => _protocolEditControl;
            set => SetAndNotifyIfChanged(nameof(ProtocolEditControl), ref _protocolEditControl, value);
        }

        public List<string> NameSelections { get; set; }
        public List<string> TagSelections { get; set; }

        public TagsEditor TagsEditor { get; set; }
        private RelayCommand _cmdSave;
        public RelayCommand CmdSave
        {
            get
            {
                if (_cmdSave != null) return _cmdSave;
                _cmdSave = new RelayCommand((o) =>
                {
                    // bulk edit
                    if (_orgServers != null)
                    {
                        // copy the same value properties
                        var properties = _orgServersCommonType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                        foreach (var property in properties)
                        {
                            if (property.SetMethod?.IsPublic == true
                                && property.SetMethod.IsAbstract == false
                                && property.Name != nameof(ProtocolServerBase.Id)
                                && property.Name != nameof(ProtocolServerBase.Tags))
                            {
                                var obj = property.GetValue(Server);
                                if (obj == null)
                                    continue;
                                else if (obj.ToString() == Server.Server_editor_different_options)
                                    continue;
                                else
                                    foreach (var server in _orgServers)
                                    {
                                        property.SetValue(server, obj);
                                    }
                            }
                        }


                        // merge tags
                        if (Server.Tags.Contains(Server.Server_editor_different_options))
                        {
                            foreach (var server in _orgServers)
                            {
                                foreach (var tag in Server.Tags)
                                {
                                    if (tag != Server.Server_editor_different_options)
                                        server.Tags.Add(tag);
                                }

                                server.Tags = server.Tags.Distinct().ToList();
                            }
                        }
                        else
                        {
                            foreach (var server in _orgServers)
                            {
                                server.Tags = Server.Tags;
                            }
                        }

                        foreach (var server in _orgServers.ToList())
                        {
                            _context.AppData.UpdateServer(server, false);
                        }

                        _context.AppData.VmItemListDataChanged?.Invoke();
                        App.MainUi.Vm.DispPage = null;
                    }
                    // edit
                    else if (Server.Id > 0)
                    {
                        _context.AppData.UpdateServer(Server);
                    }
                    // add
                    else
                    {
                        _context.AppData.AddServer(Server);
                    }
                    App.MainUi.Vm.DispPage = null;
                }, o => (this.Server.DisplayName?.Trim() != "" && (_protocolEditControl?.CanSave() ?? false)));
                return _cmdSave;
            }
        }

        private RelayCommand _cmdCancel;

        public RelayCommand CmdCancel
        {
            get
            {
                if (_cmdCancel != null) return _cmdCancel;
                _cmdCancel = new RelayCommand((o) =>
                {
                    App.MainUi.Vm.DispPage = null;
                });
                return _cmdCancel;
            }
        }

        private void UpdateServerWhenProtocolChanged(Type newProtocolType)
        {
            Debug.Assert(newProtocolType?.FullName != null);
            // change protocol
            var protocolServerBaseAssembly = typeof(ProtocolServerBase).Assembly;
            var server = (ProtocolServerBase)protocolServerBaseAssembly.CreateInstance(newProtocolType.FullName);
            // restore original server base info
            if (_orgServer.GetType() == server.GetType())
            {
                server.Update(_orgServer);
            }
            else if (_orgServer.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortUserPwdBase)) && server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortUserPwdBase)))
            {
                server.Update(_orgServer, typeof(ProtocolServerWithAddrPortUserPwdBase));
            }
            else if (_orgServer.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortBase)) && server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortBase)))
            {
                server.Update(_orgServer, typeof(ProtocolServerWithAddrPortBase));
            }
            else
            {
                server.Update(_orgServer, typeof(ProtocolServerBase));
            }


            // switch protocol and hold user name & pwd.
            if (server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortUserPwdBase)) && Server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortUserPwdBase)))
            {
                server.Update(Server, typeof(ProtocolServerWithAddrPortUserPwdBase));
            }
            else if (server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortBase)) && Server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortBase)))
            {
                server.Update(Server, typeof(ProtocolServerWithAddrPortBase));
            }
            // switch just hold base info
            else
            {
                server.Update(Server, typeof(ProtocolServerBase));
            }


            #region change default port and username
            if (server is ProtocolServerWithAddrPortBase newPort && Server is ProtocolServerWithAddrPortBase)
            {
                var oldPortDefault = (ProtocolServerWithAddrPortBase)protocolServerBaseAssembly.CreateInstance(Server.GetType().FullName);
                if (newPort.Port == oldPortDefault.Port)
                {
                    var newDefault = (ProtocolServerWithAddrPortBase)protocolServerBaseAssembly.CreateInstance(newProtocolType.FullName);
                    newPort.Port = newDefault.Port;
                }
            }
            if (server is ProtocolServerWithAddrPortUserPwdBase newUserName && Server is ProtocolServerWithAddrPortUserPwdBase)
            {
                var oldDefault = (ProtocolServerWithAddrPortUserPwdBase)protocolServerBaseAssembly.CreateInstance(Server.GetType().FullName);
                if (newUserName.UserName == oldDefault.UserName)
                {
                    var newDefault = (ProtocolServerWithAddrPortUserPwdBase)protocolServerBaseAssembly.CreateInstance(newProtocolType.FullName);
                    newUserName.UserName = newDefault.UserName;
                }
            } 
            #endregion

            Server = server;
        }

        /// <summary>
        /// switch UI when Selected protocol changed
        /// keep the common field value between 2 protocols
        /// </summary>
        /// <param name="protocolType"></param>
        private void ReflectProtocolEditControl(Type protocolType)
        {
            Debug.Assert(protocolType?.FullName != null);

            try
            {
                ProtocolEditControl = null;
                if (protocolType == typeof(ProtocolServerRDP))
                {
                    ProtocolEditControl = new RdpForm(Server);
                }
                else if (protocolType == typeof(ProtocolServerRemoteApp))
                {
                    ProtocolEditControl = new RdpAppForm(Server);
                }
                else if (protocolType == typeof(ProtocolServerSSH))
                {
                    ProtocolEditControl = new SshForm(Server);
                }
                else if (protocolType == typeof(ProtocolServerTelnet))
                {
                    ProtocolEditControl = new TelnetForm(Server);
                }
                else if (protocolType == typeof(ProtocolServerFTP))
                {
                    ProtocolEditControl = new FTPForm(Server);
                }
                else if (protocolType == typeof(ProtocolServerSFTP))
                {
                    ProtocolEditControl = new SftpForm(Server);
                }
                else if (protocolType == typeof(ProtocolServerVNC))
                {
                    ProtocolEditControl = new VncForm(Server);
                }
                else if (protocolType == typeof(ProtocolServerWithAddrPortUserPwdBase))
                {
                    ProtocolEditControl = new BaseFormWithAddressPortUserPwd(Server);
                }
                else if (protocolType == typeof(ProtocolServerWithAddrPortBase))
                {
                    ProtocolEditControl = new BaseFormWithAddressPort(Server);
                }
                else if (protocolType == typeof(ProtocolServerApp))
                {
                    ProtocolEditControl = new AppForm(Server);
                }
                else
                    throw new NotImplementedException($"can not find from for '{protocolType.Name}' in {nameof(VmServerEditorPage)}");
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
                throw;
            }
        }
    }
}