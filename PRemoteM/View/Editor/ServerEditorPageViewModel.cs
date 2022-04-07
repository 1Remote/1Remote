using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using PRM.Controls;
using PRM.Model;
using PRM.Model.Protocol;
using PRM.Model.Protocol.Base;
using PRM.Service;
using PRM.View.Editor.Forms;
using Shawn.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;

namespace PRM.View.Editor
{
    public class ServerEditorPageViewModel : NotifyPropertyChangedBase
    {
        //private readonly PrmContext _context;
        private readonly GlobalData _globalData;
        private readonly IDataService _dataService;

        public bool IsAddMode => _orgServers == null && Server.Id == 0;
        public bool IsBuckEdit => IsAddMode == false && _orgServers?.Count() > 1;

        #region single edit
        /// <summary>
        /// to remember original protocol's options, for restore use
        /// </summary>
        private readonly ProtocolBase _orgServer = null;
        public ServerEditorPageViewModel(GlobalData globalData, IDataService dataService, ProtocolBase server, bool isDuplicate = false)
        {
            _globalData = globalData;
            _dataService = dataService;
            Server = (ProtocolBase)server.Clone();
            if (isDuplicate)
            {
                Server.Id = 0; // set id = 0 and turn into edit mode
            }
            _orgServer = (ProtocolBase)Server.Clone();
            Title = "";
            Init();
        }
        #endregion

        #region Buck edit
        /// <summary>
        /// to remember original protocols' options, for restore use
        /// </summary>
        private readonly IEnumerable<ProtocolBase> _orgServers = null;
        /// <summary>
        /// the common parent class of _orgServers
        /// </summary>
        private readonly Type _orgServersCommonType = null;
        private readonly List<string> _commonTags;

        public ServerEditorPageViewModel(GlobalData globalData, IDataService dataService, IEnumerable<ProtocolBase> servers)
        {
            _globalData = globalData;
            _dataService = dataService;
            var serverBases = servers as ProtocolBase[] ?? servers.ToArray();
            // must be bulk edit
            Debug.Assert(serverBases.Count() > 1);
            // init title
            Title = IoC.Get<ILanguageService>().Translate("server_editor_bulk_editing_title") + " ";
            foreach (var serverBase in serverBases)
            {
                Title += serverBase.DisplayName;
                if (serverBases.Last() != serverBase)
                    Title += ", ";
            }


            Server = (ProtocolBase)serverBases.First().Clone();
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
            Debug.Assert(type.IsSubclassOf(typeof(ProtocolBase)));
            _orgServersCommonType = type;

            // copy the same value properties
            // set the different options to `ServerEditorDifferentOptions` or null
            var properties = _orgServersCommonType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var property in properties)
            {
                if (property.SetMethod?.IsPublic != true || property.SetMethod.IsAbstract != false) continue;
                var x = serverBases.Select(x => property.GetValue(x)).ToArray();
                if (x.Distinct().Count() <= 1) continue;
                if (property.PropertyType == typeof(string))
                    property.SetValue(Server, Server.ServerEditorDifferentOptions);
                else
                    property.SetValue(Server, null);
            }

            // tags
            _commonTags = new List<string>(); // remember the common tags
            bool isAllTagsSameFlag = true;
            for (var i = 0; i < serverBases.Length; i++)
            {
                foreach (var tagName in serverBases[i].Tags)
                {
                    if (serverBases.All(x => x.Tags.Contains(tagName)))
                    {
                        _commonTags.Add(tagName);
                    }
                    else
                    {
                        isAllTagsSameFlag = false;  
                    }
                }
            }
            Server.Tags = new List<string>(_commonTags.Count + 1);
            if (isAllTagsSameFlag == false)
                Server.Tags.Add(Server.ServerEditorDifferentOptions);
            Server.Tags.AddRange(_commonTags);

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
            if (IsBuckEdit == false)
            {
                // reflect remote protocols
                ProtocolList = ProtocolBase.GetAllSubInstance();
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
            var s = Server;
            _dataService.DecryptToConnectLevel(ref s);
            NameSelections = _globalData.VmItemList.Select(x => x.Server.DisplayName).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();
            TagSelections = _globalData.TagList.Select(x => x.Name).ToList();
        }

        public string Title { get; set; }


        private ProtocolBase _server = null;
        public ProtocolBase Server
        {
            get => _server;
            set => SetAndNotifyIfChanged(ref _server, value);
        }


        private ProtocolBase _selectedProtocol = null;
        public ProtocolBase SelectedProtocol
        {
            get => _selectedProtocol;
            set
            {
                if (IsBuckEdit)
                {
                    // bulk edit can not change protocol
                }
                if (value == _selectedProtocol) return;

                SetAndNotifyIfChanged(ref _selectedProtocol, value);
                if (_orgServer.GetType() == Server.GetType())
                    _orgServer.Update(Server);
                UpdateServerWhenProtocolChanged(SelectedProtocol.GetType());
                ReflectProtocolEditControl(SelectedProtocol.GetType());
            }
        }


        public List<ProtocolBase> ProtocolList { get; set; } = new List<ProtocolBase>();


        private FormBase _protocolEditControl = null;
        public FormBase ProtocolEditControl
        {
            get => _protocolEditControl;
            set => SetAndNotifyIfChanged(ref _protocolEditControl, value);
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
                    if (IsBuckEdit == true)
                    {
                        // copy the same value properties
                        var properties = _orgServersCommonType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                        foreach (var property in properties)
                        {
                            if (property.SetMethod?.IsPublic == true
                                && property.SetMethod.IsAbstract == false
                                && property.Name != nameof(ProtocolBase.Id)
                                && property.Name != nameof(ProtocolBase.Tags))
                            {
                                var obj = property.GetValue(Server);
                                if (obj == null)
                                    continue;
                                else if (obj.ToString() == Server.ServerEditorDifferentOptions)
                                    continue;
                                else
                                    foreach (var server in _orgServers)
                                    {
                                        property.SetValue(server, obj);
                                    }
                            }
                        }


                        // merge tags
                        foreach (var server in _orgServers)
                        {
                            // process old tags, remove the not existed tags.
                            foreach (var tag in server.Tags.ToArray())
                            {
                                if (_commonTags.Contains(tag) == true)
                                {
                                    // remove tag if it is in common and not in Server.Tags
                                    if(Server.Tags.Contains(tag) == false)
                                        server.Tags.Remove(tag);
                                }
                                else
                                {
                                    // remove tag if it is in not common and ServerEditorDifferentOptions is not existed
                                    if(Server.Tags.Contains(Server.ServerEditorDifferentOptions) == false)
                                        server.Tags.Remove(tag);
                                }
                            }

                            // add new tags
                            foreach (var tag in Server.Tags.Where(tag => tag != Server.ServerEditorDifferentOptions))
                            {
                                server.Tags.Add(tag);
                            }

                            server.Tags = server.Tags.Distinct().ToList();
                        }

                        // save
                        _globalData.UpdateServer(_orgServers);
                    }
                    // edit
                    else if (Server.Id > 0)
                    {
                        _globalData.UpdateServer(Server);
                    }
                    // add
                    else
                    {
                        _globalData.AddServer(Server);
                    }
                    IoC.Get<MainWindowViewModel>().ShowList();
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
                    IoC.Get<MainWindowViewModel>().ShowList();
                });
                return _cmdCancel;
            }
        }




        private void UpdateServerWhenProtocolChanged(Type newProtocolType)
        {
            Debug.Assert(newProtocolType?.FullName != null);
            // change protocol
            var protocolServerBaseAssembly = typeof(ProtocolBase).Assembly;
            var server = (ProtocolBase)protocolServerBaseAssembly.CreateInstance(newProtocolType.FullName);
            // restore original server base info
            if (_orgServer.GetType() == server.GetType())
            {
                server.Update(_orgServer);
            }
            else if (_orgServer.GetType().IsSubclassOf(typeof(ProtocolBaseWithAddressPortUserPwd)) && server.GetType().IsSubclassOf(typeof(ProtocolBaseWithAddressPortUserPwd)))
            {
                server.Update(_orgServer, typeof(ProtocolBaseWithAddressPortUserPwd));
            }
            else if (_orgServer.GetType().IsSubclassOf(typeof(ProtocolBaseWithAddressPort)) && server.GetType().IsSubclassOf(typeof(ProtocolBaseWithAddressPort)))
            {
                server.Update(_orgServer, typeof(ProtocolBaseWithAddressPort));
            }
            else
            {
                server.Update(_orgServer, typeof(ProtocolBase));
            }


            // switch protocol and hold user name & pwd.
            if (server.GetType().IsSubclassOf(typeof(ProtocolBaseWithAddressPortUserPwd)) && Server.GetType().IsSubclassOf(typeof(ProtocolBaseWithAddressPortUserPwd)))
            {
                server.Update(Server, typeof(ProtocolBaseWithAddressPortUserPwd));
            }
            else if (server.GetType().IsSubclassOf(typeof(ProtocolBaseWithAddressPort)) && Server.GetType().IsSubclassOf(typeof(ProtocolBaseWithAddressPort)))
            {
                server.Update(Server, typeof(ProtocolBaseWithAddressPort));
            }
            // switch just hold base info
            else
            {
                server.Update(Server, typeof(ProtocolBase));
            }


            #region change default port and username
            if (server is ProtocolBaseWithAddressPort newPort && Server is ProtocolBaseWithAddressPort)
            {
                var oldPortDefault = (ProtocolBaseWithAddressPort)protocolServerBaseAssembly.CreateInstance(Server.GetType().FullName);
                if (newPort.Port == oldPortDefault.Port)
                {
                    var newDefault = (ProtocolBaseWithAddressPort)protocolServerBaseAssembly.CreateInstance(newProtocolType.FullName);
                    newPort.Port = newDefault.Port;
                }
            }
            if (server is ProtocolBaseWithAddressPortUserPwd newUserName && Server is ProtocolBaseWithAddressPortUserPwd)
            {
                var oldDefault = (ProtocolBaseWithAddressPortUserPwd)protocolServerBaseAssembly.CreateInstance(Server.GetType().FullName);
                if (newUserName.UserName == oldDefault.UserName)
                {
                    var newDefault = (ProtocolBaseWithAddressPortUserPwd)protocolServerBaseAssembly.CreateInstance(newProtocolType.FullName);
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
                if (protocolType == typeof(RDP))
                {
                    ProtocolEditControl = new RdpForm(Server);
                }
                else if (protocolType == typeof(RdpApp))
                {
                    ProtocolEditControl = new RdpAppForm(Server);
                }
                else if (protocolType == typeof(SSH))
                {
                    ProtocolEditControl = new SshForm(Server);
                }
                else if (protocolType == typeof(Telnet))
                {
                    ProtocolEditControl = new TelnetForm(Server);
                }
                else if (protocolType == typeof(FTP))
                {
                    ProtocolEditControl = new FTPForm(Server);
                }
                else if (protocolType == typeof(SFTP))
                {
                    ProtocolEditControl = new SftpForm(Server);
                }
                else if (protocolType == typeof(VNC))
                {
                    ProtocolEditControl = new VncForm(Server);
                }
                else if (protocolType == typeof(ProtocolBaseWithAddressPortUserPwd))
                {
                    ProtocolEditControl = new BaseFormWithAddressPortUserPwd(Server);
                }
                else if (protocolType == typeof(ProtocolBaseWithAddressPort))
                {
                    ProtocolEditControl = new BaseFormWithAddressPort(Server);
                }
                else if (protocolType == typeof(LocalApp))
                {
                    ProtocolEditControl = new AppForm(Server);
                }
                else
                    throw new NotImplementedException($"can not find from for '{protocolType.Name}' in {nameof(ServerEditorPageViewModel)}");
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
                throw;
            }
        }
    }
}