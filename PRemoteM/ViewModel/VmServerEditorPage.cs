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
using PRM.Core.Protocol.BaseClassForm;
using Shawn.Utils;

using Shawn.Utils;

namespace PRM.ViewModel
{
    public class VmServerEditorPage : NotifyPropertyChangedBase
    {
        private readonly PrmContext _context;

        /// <summary>
        /// to remember original protocol options
        /// </summary>
        private readonly ProtocolServerBase _orgServer = null;
        private readonly IEnumerable<ProtocolServerBase> _orgServers = null;
        private readonly Type _orgServersCommonType = null;

        public VmServerEditorPage(PrmContext context, ProtocolServerBase server, bool isDuplicate = false)
        {
            _context = context;
            _orgServer = (ProtocolServerBase)server.Clone();
            Server = (ProtocolServerBase)server.Clone();
            _isDuplicate = isDuplicate;
            if (_isDuplicate)
                Server.Id = 0;
            IsAddMode = Server.Id <= 0;
            Title = "";
            Init();
        }


        public VmServerEditorPage(PrmContext context, IEnumerable<ProtocolServerBase> servers)
        {
            IsAddMode = false;

            Title = SystemConfig.Instance.Language.GetText("server_editor_bulk_editing_title");
            foreach (var serverBase in servers)
            {
                Title += serverBase.DispName;
                if (servers.Last() != serverBase)
                    Title += ", ";
            }

            var protocolServerBases = servers as ProtocolServerBase[] ?? servers.ToArray();
            if (protocolServerBases?.Any() == false)
            {
                App.Window.Vm.DispPage = null;
                return;
            }

            _context = context;

            var server = protocolServerBases.First();
            Server = (ProtocolServerBase)server.Clone();

            if (protocolServerBases.Count() == 1)
            {
                _orgServer = server;
                _orgServers = null;
                IsAddMode = Server.Id <= 0;
            }
            else
            {
                _orgServer = null;
                _orgServers = protocolServerBases;
                // finding the common base class
                _orgServersCommonType = null;
                if (protocolServerBases.All(x => x.GetType() == server.GetType()))
                {
                    _orgServersCommonType = server.GetType();
                }
                else if (protocolServerBases.All(x => x.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortUserPwdBase))))
                {
                    _orgServersCommonType = typeof(ProtocolServerWithAddrPortUserPwdBase);
                }
                else if (protocolServerBases.All(x => x.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortBase))))
                {
                    _orgServersCommonType = typeof(ProtocolServerWithAddrPortBase);
                }
                else
                {
                    string msg = "error when finding the common base class with:\r\n";
                    foreach (var serverBase in servers)
                    {
                        msg += $"{serverBase.Id} {serverBase.GetType()}\r\n";
                    }
                    throw new Exception(msg);
                }

                Server.Update(server, _orgServersCommonType);
                // copy the same value properties
                var properties = _orgServersCommonType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                foreach (var property in properties)
                {
                    if (property.SetMethod?.IsPublic == true && property.SetMethod.IsAbstract == false)
                    {
                        var x = protocolServerBases.Select(x => property.GetValue(x)).ToArray();
                        if (x.Distinct().Count() > 1)
                        {
                            if(property.PropertyType == typeof(string))
                                property.SetValue(Server, Server.Server_editor_different_options);
                            else
                                property.SetValue(Server, null);
                        }
                    }
                }

                // tags
                var tags = new List<string>();
                if (servers.All(x => x.Tags.Count == servers.First().Tags.Count))
                {
                    bool flag = true;
                    foreach (var tag in servers.First().Tags)
                    {
                        if (servers.All(x => x.Tags.Contains(tag)) == false)
                        {
                            flag = false;
                            break;
                        }
                    }

                    if (servers.First().Tags.Count == 0 || flag)
                        tags = new List<string>(servers.First().Tags);
                    else
                        tags.Add(Server.Server_editor_different_options);
                }
                else
                    tags.Add(Server.Server_editor_different_options);

                Server.Tags = tags;

                // init ui
                ReflectProtocolEditControl(_orgServersCommonType);
            }

            Init();
        }



        private void Init()
        {
            ProtocolList.Clear();
            // init protocol list for single add / edit mode
            if (_orgServers?.Any() != true)
            {
                var assembly = typeof(ProtocolServerBase).Assembly;
                var types = assembly.GetTypes();
                // reflect remote protocols
                {
                    ProtocolList = types.Where(item => item.IsSubclassOf(typeof(ProtocolServerBase)) && !item.IsAbstract).Select(type => (ProtocolServerBase)Activator.CreateInstance(type)).OrderBy(x => x.GetListOrder()).ToList();
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
            }

            // decrypt pwd
            _context.DbOperator.DecryptPwdIfItIsEncrypted(Server);

            NameSelections = _context.AppData.VmItemList.Select(x => x.Server.DispName).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();
            TagSelections = _context.AppData.Tags.Select(x=>x.Name).Distinct().OrderBy(x => x).ToList();
        }

        public string Title { get; set; }

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
                    if (_orgServer.GetType() == Server.GetType())
                        _orgServer.Update(Server);
                    ReflectProtocolEditControl(ProtocolSelected.GetType());
                }
            }
        }
        public List<ProtocolServerBase> ProtocolList { get; set; } = new List<ProtocolServerBase>();

        private readonly bool _isDuplicate = false;

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
                    TagsEditor?.Parse();

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
                                if(obj == null)
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
                            _context.AppData.ServerListUpdate(server, false);
                        }

                        _context.AppData.VmItemListDataChanged?.Invoke();
                        App.Window.Vm.DispPage = null;
                    }
                    else
                    {
                        _context.AppData.ServerListUpdate(Server);
                        App.Window.Vm.DispPage = null;
                    }
                }, o => (this.Server.DispName?.Trim() != "" && (_protocolEditControl?.CanSave() ?? false)));
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
                    App.Window.Vm.DispPage = null;
                });
                return _cmdCancel;
            }
        }

        private void ReflectProtocolEditControl(Type protocolType)
        {
            Debug.Assert(protocolType?.FullName != null);

            var formName = protocolType.Name + "Form";
            var protocolServerBaseAssembly = typeof(ProtocolServerBase).Assembly;

            ProtocolServerBase server = (ProtocolServerBase)Server.Clone();

            if (_orgServers != null)
            {
                if (protocolType == typeof(ProtocolServerBase))
                {
                    formName = string.Empty;
                }
            }
            else
            {
                try
                {
                    // change protocol
                    server = (ProtocolServerBase) protocolServerBaseAssembly.CreateInstance(protocolType.FullName);

                    // restore original server base info
                    if (_orgServer.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortUserPwdBase))
                        && server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortUserPwdBase)))
                        server.Update(_orgServer, typeof(ProtocolServerWithAddrPortUserPwdBase));
                    else if (_orgServer.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortBase))
                             && server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortBase)))
                        server.Update(_orgServer, typeof(ProtocolServerWithAddrPortBase));
                    else
                        server.Update(_orgServer, typeof(ProtocolServerBase));
                    if (_orgServer.GetType() == server.GetType())
                        server.Update(_orgServer);

                    // switch protocol and hold user name & pwd.
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
                catch (Exception e)
                {
                    SimpleLogHelper.Fatal(e);
                    throw;
                }
            }

            if (!string.IsNullOrEmpty(formName))
                try
                {
                    var types = protocolServerBaseAssembly.GetTypes();
                    var forms = types.Where(x => x.Name == formName).ToList();
                    if (forms.Count == 1)
                    {
                        var t = forms[0];
                        var parameters = new object[1];
                        parameters[0] = server;
                        ProtocolEditControl = (ProtocolServerFormBase)protocolServerBaseAssembly.CreateInstance(t.FullName, true, System.Reflection.BindingFlags.Default, null, parameters, null, null);
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
                    Console.WriteLine(e);
                    throw;
                }
        }
    }
}