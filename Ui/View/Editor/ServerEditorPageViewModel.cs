using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using _1RM.Controls;
using _1RM.Model;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Model.ProtocolRunner;
using _1RM.Service;
using _1RM.Service.DataSource;
using _1RM.View.Editor.Forms;
using Shawn.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;

namespace _1RM.View.Editor
{
    public class ServerEditorPageViewModel : NotifyPropertyChangedBase
    {
        private readonly GlobalData _globalData;
        private readonly IDataSource? _addToDataSource = null; // target data source in add mode 

        public bool IsAddMode => _serversInBuckEdit == null && string.IsNullOrEmpty(Server.Id);
        public bool IsBuckEdit => IsAddMode == false && _serversInBuckEdit?.Count() > 1;
        private readonly ProtocolBase _orgServer; // to remember original protocol's options, for restore data when switching protocols
        private readonly ProtocolConfigurationService _protocolConfigurationService = IoC.Get<ProtocolConfigurationService>();



        public static ServerEditorPageViewModel Add(GlobalData globalData, IDataSource addToDataSource, List<string>? presetTagNames = null)
        {
            var server = new RDP
            {
                Tags = presetTagNames?.Count == 0 ? new List<string>() : new List<string>(presetTagNames!),
                DataSourceName = addToDataSource.DataSourceName,
            };
            return new ServerEditorPageViewModel(globalData, server, addToDataSource);
        }

        public static ServerEditorPageViewModel Duplicate(GlobalData globalData, IDataSource dataSource, ProtocolBase server)
        {
            return new ServerEditorPageViewModel(globalData, server, dataSource);
        }

        public static ServerEditorPageViewModel Edit(GlobalData globalData, ProtocolBase server)
        {
            return new ServerEditorPageViewModel(globalData, server);
        }

        public static ServerEditorPageViewModel BuckEdit(GlobalData globalData, IEnumerable<ProtocolBase> servers)
        {
            return new ServerEditorPageViewModel(globalData, servers);
        }

        /// <summary>
        /// Add or Edit or Duplicate
        /// </summary>
        private ServerEditorPageViewModel(GlobalData globalData, ProtocolBase server, IDataSource? addToDataSource = null)
        {
            _globalData = globalData;
            _addToDataSource = addToDataSource;

            IoC.Get<DataSourceService>().GetDataSource(server.DataSourceName)?.DecryptToConnectLevel(ref server);

            Server = (ProtocolBase)server.Clone();
            if (addToDataSource != null)
            {
                Server.DataSourceName = addToDataSource.GetName();
                Server.Id = string.Empty; // set id to empty so that we turn into Add / Duplicate mode
            }
            _orgServer = (ProtocolBase)Server.Clone();
            Title = ""; // TODO show data source name.
            Init();
        }


        #region buck edit
        /// <summary>
        /// to remember original protocols' options, for restore use
        /// </summary>
        private readonly IEnumerable<ProtocolBase>? _serversInBuckEdit = null;
        /// <summary>
        /// the common parent class of _serversInBuckEdit
        /// </summary>
        private readonly Type? _sharedTypeInBuckEdit = null;
        private readonly List<string> _sharedTagsInBuckEdit = new List<string>();

        private ServerEditorPageViewModel(GlobalData globalData, IEnumerable<ProtocolBase> servers)
        {
            _globalData = globalData;
            var serverBases = servers as ProtocolBase[] ?? servers.ToArray();
            // decrypt
            for (int i = 0; i < serverBases.Length; i++)
            {
                // decrypt pwd
                var s = Server;
                IoC.Get<DataSourceService>().GetDataSource(s.DataSourceName)?.DecryptToConnectLevel(ref s);
            }

            // must be bulk edit
            Debug.Assert(serverBases.Length > 1);
            // init title
            Title = IoC.Get<ILanguageService>().Translate("server_editor_bulk_editing_title") + " ";
            foreach (var serverBase in serverBases)
            {
                Title += serverBase.DisplayName;
                if (serverBases.Last() != serverBase)
                    Title += ", ";
            }


            Server = (ProtocolBase)serverBases.First().Clone();
            _serversInBuckEdit = serverBases;


            // find the common base class
            {
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
                _sharedTypeInBuckEdit = type;
            }

            // copy the same value properties
            {
                // set the different options to `ServerEditorDifferentOptions` or null
                var properties = _sharedTypeInBuckEdit.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                foreach (var property in properties)
                {
                    if (property.SetMethod?.IsPublic != true || property.SetMethod.IsAbstract != false) continue;
                    var x = serverBases.Select(x => property.GetValue(x)).ToArray();
                    if (x.Distinct().Count() <= 1) continue;
                    if (property.PropertyType == typeof(string))
                        property.SetValue(Server, ProtocolBase.ServerEditorDifferentOptions);
                    else
                        property.SetValue(Server, null);
                }
            }

            // tags
            {
                _sharedTagsInBuckEdit = new List<string>(); // remember the common tags
                bool isAllTagsSameFlag = true;
                for (var i = 0; i < serverBases.Length; i++)
                {
                    foreach (var tagName in serverBases[i].Tags)
                    {
                        if (serverBases.All(x => x.Tags.Contains(tagName)))
                        {
                            _sharedTagsInBuckEdit.Add(tagName);
                        }
                        else
                        {
                            isAllTagsSameFlag = false;
                        }
                    }
                }

                var tags = new List<string>();
                if (isAllTagsSameFlag == false)
                    tags.Add(ProtocolBase.ServerEditorDifferentOptions);
                tags.AddRange(_sharedTagsInBuckEdit);
                Server.Tags = tags;
            }

            _orgServer = Server.Clone();
            // init ui
            {
                if (_serversInBuckEdit.All(x => x.GetType() == _sharedTypeInBuckEdit))
                    UpdateRunners(_serversInBuckEdit.First().Protocol);
                ReflectProtocolEditControl(_sharedTypeInBuckEdit);
            }

            Init();
        }

        #endregion


        private void Init()
        {
            // init protocol list for single add / edit mode
            if (IsAddMode)
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

            NameSelections = _globalData.VmItemList.Select(x => x.Server.DisplayName).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();
            TagSelections = _globalData.TagList.Select(x => x.Name).ToList();
        }

        public string Title { get; set; }


        private ProtocolBase _server = new RDP();
        public ProtocolBase Server
        {
            get => _server;
            set => SetAndNotifyIfChanged(ref _server, value);
        }

        public ObservableCollection<string> Runners { get; } = new ObservableCollection<string>();

        private ProtocolBase _selectedProtocol = new RDP();
        public ProtocolBase SelectedProtocol
        {
            get => _selectedProtocol;
            set
            {
                if (IsBuckEdit == false)
                {
                    // bulk edit do not allow change protocol
                    if (SetAndNotifyIfChanged(ref _selectedProtocol, value))
                    {
                        if (_orgServer.GetType() == Server.GetType())
                            _orgServer.Update(Server);
                        UpdateRunners(_selectedProtocol.Protocol);
                        UpdateServerWhenProtocolChanged(SelectedProtocol.GetType());
                        ReflectProtocolEditControl(SelectedProtocol.GetType());
                    }
                }
            }
        }


        public List<ProtocolBase> ProtocolList { get; set; } = new List<ProtocolBase>();


        private FormBase? _protocolEditControl;
        public FormBase ProtocolEditControl
        {
            get
            {
                if (_protocolEditControl == null) throw new NullReferenceException();
                return _protocolEditControl;
            }
            set => SetAndNotifyIfChanged(ref _protocolEditControl, value);
        }


        /// <summary>
        /// suggested name for name field
        /// </summary>
        public List<string> NameSelections { get; set; } = new List<string>();
        /// <summary>
        /// suggested tag for tag field
        /// </summary>
        public List<string> TagSelections { get; set; } = new List<string>();

        private RelayCommand? _cmdSave;
        public RelayCommand CmdSave
        {
            get
            {
                if (_cmdSave != null) return _cmdSave;
                _cmdSave = new RelayCommand((o) =>
                {
                    // bulk edit
                    if (IsBuckEdit == true && _sharedTypeInBuckEdit != null && _serversInBuckEdit != null)
                    {
                        // copy the same value properties
                        var properties = _sharedTypeInBuckEdit.GetProperties(BindingFlags.Public | BindingFlags.Instance);
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
                                else if (obj.ToString() == ProtocolBase.ServerEditorDifferentOptions)
                                    continue;
                                else
                                    foreach (var server in _serversInBuckEdit)
                                    {
                                        property.SetValue(server, obj);
                                    }
                            }
                        }


                        // merge tags
                        foreach (var server in _serversInBuckEdit)
                        {
                            // process old tags, remove the not existed tags.
                            foreach (var tag in server.Tags.ToArray())
                            {
                                if (_sharedTagsInBuckEdit.Contains(tag) == true)
                                {
                                    // remove tag if it is in common and not in Server.Tags
                                    if (Server.Tags.Contains(tag) == false)
                                        server.Tags.Remove(tag);
                                }
                                else
                                {
                                    // remove tag if it is in not common and ServerEditorDifferentOptions is not existed
                                    if (Server.Tags.Contains(ProtocolBase.ServerEditorDifferentOptions) == false)
                                        server.Tags.Remove(tag);
                                }
                            }

                            // add new tags
                            foreach (var tag in Server.Tags.Where(tag => tag != ProtocolBase.ServerEditorDifferentOptions))
                            {
                                server.Tags.Add(tag);
                            }
                            server.Tags = server.Tags.Distinct().ToList();
                        }

                        // save
                        _globalData.UpdateServer(_serversInBuckEdit);
                    }
                    // edit
                    else if (string.IsNullOrEmpty(Server.Id) == false)
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



        private RelayCommand? _cmdCancel;
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
            var server = (ProtocolBase)protocolServerBaseAssembly.CreateInstance(newProtocolType.FullName)!;
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


            #region change port and username if the old velue is the default port and username
            if (server is ProtocolBaseWithAddressPort newPort && Server is ProtocolBaseWithAddressPort)
            {
                var oldPortDefault = (ProtocolBaseWithAddressPort)protocolServerBaseAssembly.CreateInstance(Server.GetType().FullName!)!;
                if (newPort.Port == oldPortDefault.Port)
                {
                    var newDefault = (ProtocolBaseWithAddressPort)protocolServerBaseAssembly.CreateInstance(newProtocolType.FullName)!;
                    newPort.Port = newDefault.Port;
                }
            }
            if (server is ProtocolBaseWithAddressPortUserPwd newUserName && Server is ProtocolBaseWithAddressPortUserPwd)
            {
                var oldDefault = (ProtocolBaseWithAddressPortUserPwd)protocolServerBaseAssembly.CreateInstance(Server.GetType().FullName!)!;
                if (newUserName.UserName == oldDefault.UserName)
                {
                    var newDefault = (ProtocolBaseWithAddressPortUserPwd)protocolServerBaseAssembly.CreateInstance(newProtocolType.FullName)!;
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

        private void UpdateRunners(string protocolName)
        {
            var selectedRunner = Server.SelectedRunnerName;
            Runners.Clear();
            if (_protocolConfigurationService.ProtocolConfigs.ContainsKey(protocolName))
            {
                var c = _protocolConfigurationService.ProtocolConfigs[protocolName];
                Runners.Add("Follow the global settings");
                foreach (var runner in c.Runners)
                {
                    Runners.Add(runner.Name);
                }
                if (IsBuckEdit && _orgServer.SelectedRunnerName == ProtocolBase.ServerEditorDifferentOptions)
                {
                    Runners.Add(ProtocolBase.ServerEditorDifferentOptions);
                }
                Server.SelectedRunnerName = Runners.Any(x => x == selectedRunner) ? selectedRunner : Runners.First();
            }
            else
            {
                Server.SelectedRunnerName = "";
            }
        }
    }
}