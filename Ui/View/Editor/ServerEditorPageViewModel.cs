using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using _1RM.Model;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Service;
using _1RM.Service.DataSource.DAO;
using _1RM.Service.DataSource.Model;
using _1RM.Utils;
using _1RM.Utils.Tracing;
using _1RM.View.Editor.Forms;
using _1RM.View.Editor.Forms.AlternativeCredential;
using _1RM.View.Utils;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.FileSystem;
using Shawn.Utils.Wpf.Image;
using Stylet;
using Credential = _1RM.Model.Protocol.Base.Credential;

namespace _1RM.View.Editor
{
    public partial class ServerEditorPageViewModel : NotifyPropertyChangedBase
    {
        private readonly GlobalData _globalData;

        public bool IsBuckEdit => _serversInBuckEdit?.Count() > 1;
        private readonly ProtocolBase _orgServer; // to remember original protocol's options, for restore data when switching protocols



        public static ServerEditorPageViewModel Add(GlobalData globalData, DataSourceBase addToDataSource, List<string>? presetTagNames = null)
        {
            var server = new RDP
            {
                Tags = presetTagNames?.Count == 0 ? new List<string>() : new List<string>(presetTagNames!),
            };
            return new ServerEditorPageViewModel(globalData, server, addToDataSource);
        }

        public static ServerEditorPageViewModel Duplicate(GlobalData globalData, DataSourceBase dataSource, ProtocolBase server)
        {
            Debug.Assert(server.IsTmpSession() == false);
            var s = (ProtocolBase)server.Clone();
            s.Id = "";
            return new ServerEditorPageViewModel(globalData, s, dataSource);
        }

        public static ServerEditorPageViewModel Edit(GlobalData globalData, ProtocolBase server)
        {
            Debug.Assert(server.IsTmpSession() == false);
            Debug.Assert(server.DataSource != null);
            return new ServerEditorPageViewModel(globalData, server, server.DataSource);
        }

        public static ServerEditorPageViewModel BuckEdit(GlobalData globalData, IEnumerable<ProtocolBase> servers)
        {
            return new ServerEditorPageViewModel(globalData, servers);
        }

        /// <summary>
        /// Add or Edit or Duplicate
        /// </summary>
        private ServerEditorPageViewModel(GlobalData globalData, ProtocolBase server, DataSourceBase addToDataSource)
        {
            _globalData = globalData;

            server.DecryptToConnectLevel();

            Server = (ProtocolBase)server.Clone();
            if (Server.IsTmpSession()) // Add or Duplicate mode
            {
                Server.DataSource = addToDataSource;
                Server.Id = string.Empty; // set id to empty so that we turn into Add / Duplicate mode
            }
            else // edit mode
            {
            }
            _orgServer = (ProtocolBase)Server.Clone();
            Title = "";


            // reflect remote protocols
            ProtocolList = ProtocolBase.GetAllSubInstance();
            // set selected protocol
            try
            {
                SelectedProtocol = ProtocolList.First(x => x.GetType() == Server.GetType());
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
                UnifyTracing.Error(e);
                SelectedProtocol = ProtocolList.First();
            }

            Debug.Assert(IsBuckEdit == false);
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
        private readonly List<Credential> _sharedCredentialsInBuckEdit = new List<Credential>();


        /// <summary>
        /// BuckEdit
        /// </summary>
        private ServerEditorPageViewModel(GlobalData globalData, IEnumerable<ProtocolBase> servers)
        {
            _globalData = globalData;
            var serverBases = servers.Select(x => x.Clone()).ToArray();
            Debug.Assert(serverBases.Count() > 1);
            // decrypt
            for (int i = 0; i < serverBases.Length; i++)
            {
                // decrypt pwd
                serverBases[i].DecryptToConnectLevel();
            }

            // must be bulk edit
            Debug.Assert(serverBases.Length > 1);
            // init title
            Title = IoC.Translate("server_editor_bulk_editing_title") + " ";
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
                    var type_new = AssemblyHelper.FindCommonBaseClass(type, types[i]);
                    if (type != type_new && type_new != null)
                    {
                        type = type_new;
                        // 将 Server 转换成这个公共的类型，以便批量编辑时 AlternativeCredentialEditViewModel 能够正确用户名、密码等字段的显示
                        Server = (ProtocolBase)serverBases[i].Clone();
                    }
                }

                Debug.Assert(type == typeof(ProtocolBase) || type.IsSubclassOf(typeof(ProtocolBase)));
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
                        property.SetValue(Server, Server.ServerEditorDifferentOptions);
                    else
                        property.SetValue(Server, null);
                }
            }

            // tags
            {
                _sharedTagsInBuckEdit = new List<string>(); // remember the common tags
                bool isAllTheSameFlag = true;
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
                            isAllTheSameFlag = false;
                        }
                    }
                }

                var list = new List<string>();
                if (isAllTheSameFlag == false)
                    list.Add(Server.ServerEditorDifferentOptions);
                list.AddRange(_sharedTagsInBuckEdit);
                Server.Tags = list;
            }


            // AlternateCredentials
            if (Server is ProtocolBaseWithAddressPort protocol
                && (_sharedTypeInBuckEdit.IsSubclassOf(typeof(ProtocolBaseWithAddressPort)) || _sharedTypeInBuckEdit == typeof(ProtocolBaseWithAddressPort)))
            {
                var ss = serverBases.Select(x => (ProtocolBaseWithAddressPort)x).ToArray();
                bool isAllTheSameFlag = true;
                foreach (var s in ss)
                {
                    foreach (var c in s.AlternateCredentials)
                    {
                        if (ss.All(x => x.AlternateCredentials.Any(y => y.IsValueEqualTo(c) == true)))
                        {
                            if (_sharedCredentialsInBuckEdit.All(x => x.IsValueEqualTo(c) == false))
                            {
                                _sharedCredentialsInBuckEdit.Add(c);
                            }
                        }
                        else
                        {
                            isAllTheSameFlag = false;
                        }
                    }
                }

                var list = new List<Credential>();
                if (isAllTheSameFlag == false)
                    list.Add(new Credential(isEditable: false) { Name = Server.ServerEditorDifferentOptions });
                list.AddRange(_sharedCredentialsInBuckEdit);
                protocol.AlternateCredentials = new ObservableCollection<Credential>(list);
            }

            AppArgumentsBulkInit(_serversInBuckEdit);

            Server.DataSource = serverBases.All(x => x.DataSource == serverBases.First().DataSource) ? serverBases.First().DataSource : null;
            _orgServer = Server.Clone();

            // init ui
            if (_serversInBuckEdit.All(x => x.GetType() == _sharedTypeInBuckEdit))
                UpdateRunners(_serversInBuckEdit.First().Protocol);
            ReflectProtocolEditControl(_sharedTypeInBuckEdit);
            Debug.Assert(IsBuckEdit == true);
        }

        #endregion

        public string Title { get; }


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


        private ProtocolBaseFormViewModel? _editorViewModel;
        public ProtocolBaseFormViewModel? EditorViewModel
        {
            get => _editorViewModel;
            set => SetAndNotifyIfChanged(ref _editorViewModel, value);
        }


        /// <summary>
        /// suggested name for name field
        /// </summary>
        public List<string> NameSelections => _globalData.VmItemList.Select(x => x.Server.DisplayName).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();
        /// <summary>
        /// suggested tag for tag field
        /// </summary>
        public List<string> TagSelections => _globalData.TagList.Select(x => x.Name).ToList();

        private RelayCommand? _cmdSave;
        public RelayCommand CmdSave
        {
            get
            {
                if (_cmdSave != null) return _cmdSave;
                _cmdSave = new((o) =>
                {
                    if (string.IsNullOrEmpty(Server.DisplayName) || EditorViewModel?.CanSave() != true)
                        return;

                    MaskLayerController.ShowMask(IoC.Get<ProcessingRingViewModel>(), IoC.Get<MainWindowViewModel>());

                    Task.Factory.StartNew(() =>
                    {
                        try
                        {
                            var ret = Result.Success();
                            // bulk edit
                            if (IsBuckEdit)
                            {
                                if (_sharedTypeInBuckEdit == null) throw new NullReferenceException($"{nameof(_sharedTypeInBuckEdit)} should not be null!");
                                if (_serversInBuckEdit == null) throw new NullReferenceException($"{nameof(_serversInBuckEdit)} should not be null!");
                                // copy the same value properties
                                var properties = _sharedTypeInBuckEdit.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                                foreach (var property in properties)
                                {
                                    if (property.SetMethod?.IsPublic != true
                                        || property.SetMethod.IsAbstract == true
                                        || property.Name == nameof(ProtocolBase.Id)
                                        || property.Name == nameof(ProtocolBase.Tags)
                                        || property.Name == nameof(ProtocolBaseWithAddressPortUserPwd.InheritedCredentialName)
                                        || property.Name == nameof(ProtocolBaseWithAddressPortUserPwd.UsePrivateKeyForConnect)
                                        || property.Name == nameof(ProtocolBaseWithAddressPortUserPwd.UserName)
                                        || property.Name == nameof(ProtocolBaseWithAddressPortUserPwd.Password)
                                        || property.Name == nameof(ProtocolBaseWithAddressPortUserPwd.PrivateKey)
                                        || property.Name == nameof(ProtocolBaseWithAddressPortUserPwd.AlternateCredentials)
                                        || property.Name == nameof(LocalApp.ArgumentList))
                                    {
                                        continue;
                                    }


                                    if (property.PropertyType.IsGenericType
                                        && (property.PropertyType.GetGenericTypeDefinition() == typeof(List<>)
                                            || property.PropertyType.GetGenericTypeDefinition() == typeof(ObservableCollection<>)))
                                    {
                                        SimpleLogHelper.Warning(property.Name + " IsGenericType!");
                                    }
                                    var obj = property.GetValue(Server);
                                    if (obj == null)
                                        continue;
                                    else if (obj.ToString() == Server.ServerEditorDifferentOptions)
                                        continue;
                                    else
                                        foreach (var server in _serversInBuckEdit)
                                        {
                                            property.SetValue(server, obj);
                                        }
                                }

                                // handle credentials
                                if (Server is ProtocolBaseWithAddressPortUserPwd sp && sp.InheritedCredentialName != sp.ServerEditorDifferentOptions)
                                {
                                    foreach (var server in _serversInBuckEdit)
                                    {
                                        if (server is not ProtocolBaseWithAddressPortUserPwd protocol) continue;
                                        // do not overwrite the user & pass when the server is inherited from credential
                                        if (protocol.InheritedCredentialName == sp.ServerEditorDifferentOptions && !string.IsNullOrEmpty(protocol.InheritedCredentialName)) continue;
                                        if (sp.InheritedCredentialName != sp.ServerEditorDifferentOptions)
                                            protocol.InheritedCredentialName = sp.InheritedCredentialName;
                                        if (sp.UsePrivateKeyForConnect != null)
                                            protocol.UsePrivateKeyForConnect = sp.UsePrivateKeyForConnect;
                                        if (sp.UserName != sp.ServerEditorDifferentOptions)
                                            protocol.UserName = sp.UserName;
                                        if (sp.Password != sp.ServerEditorDifferentOptions)
                                            protocol.Password = sp.Password;
                                        if (sp.PrivateKey != sp.ServerEditorDifferentOptions)
                                            protocol.PrivateKey = sp.PrivateKey;
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
                                            if (Server.Tags.Contains(tag) == false)
                                                server.Tags.Remove(tag);
                                        }
                                        else
                                        {
                                            if (Server.Tags.Contains(Server.ServerEditorDifferentOptions) == false)
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



                                // merge AlternateCredentials
                                if (Server is ProtocolBaseWithAddressPort newServer)
                                    foreach (var server in _serversInBuckEdit)
                                    {
                                        if (server is not ProtocolBaseWithAddressPort protocol) continue;

                                        foreach (var credential in protocol.AlternateCredentials.ToArray())
                                        {
                                            if (_sharedCredentialsInBuckEdit.Any(x => x.IsValueEqualTo(credential))) // 编辑之前共有，编辑后不再共有，删除
                                            {
                                                if (newServer.AlternateCredentials.All(x => x.IsValueEqualTo(credential) == false))
                                                {
                                                    protocol.AlternateCredentials.Remove(credential);
                                                }
                                            }
                                            else
                                            {
                                                if (newServer.AlternateCredentials.All(x => x.Name != Server.ServerEditorDifferentOptions && x.IsEditable != false))
                                                {
                                                    protocol.AlternateCredentials.Remove(credential);
                                                }
                                            }
                                        }

                                        foreach (var credential in newServer.AlternateCredentials.Where(x => x.Name != Server.ServerEditorDifferentOptions))
                                        {
                                            if (protocol.AlternateCredentials.All(x => x.IsValueEqualTo(credential) == false))
                                            {
                                                protocol.AlternateCredentials.Add(credential);
                                            }
                                        }
                                    }

                                AppArgumentsBulkMerge(_serversInBuckEdit);

                                // save
                                ret = _globalData.UpdateServer(_serversInBuckEdit);
                            }
                            else
                            {
                                // edit
                                if (Server.IsTmpSession() == false)
                                {
                                    ret = _globalData.UpdateServer(Server);
                                }
                                // add
                                else if (Server.DataSource != null)
                                {
                                    ret = _globalData.AddServer(Server, Server.DataSource);
                                }
                            }

                            if (ret.IsSuccess)
                            {
                                IoC.Get<MainWindowViewModel>().ShowList(true);
                            }
                            else
                            {
                                MessageBoxHelper.ErrorAlert(ret.ErrorInfo);
                            }
                        }
                        catch (Exception e)
                        {
                            UnifyTracing.Error(e);
                            MessageBoxHelper.ErrorAlert(e.Message);
                        }
                        finally
                        {
                            MaskLayerController.HideMask();
                        }
                    });

                }, o => (this.Server.DisplayName?.Trim() != "" && (EditorViewModel?.CanSave() ?? true)));
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
                    IoC.Get<MainWindowViewModel>().ShowList(false);
                });
                return _cmdCancel;
            }
        }




        private void UpdateServerWhenProtocolChanged(Type newProtocolType)
        {
            if (string.IsNullOrEmpty(newProtocolType?.FullName)) return;
            // change protocol
            var protocolServerBaseAssembly = typeof(ProtocolBase).Assembly;
            var server = (ProtocolBase)protocolServerBaseAssembly.CreateInstance(newProtocolType!.FullName)!;
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
            if (server is ProtocolBaseWithAddressPort newPort)
            {
                bool isDefaultPort = false;
                if (Server is ProtocolBaseWithAddressPort)
                {
                    var oldPortDefault = (ProtocolBaseWithAddressPort)protocolServerBaseAssembly.CreateInstance(Server.GetType().FullName!)!;
                    isDefaultPort = newPort.Port == oldPortDefault.Port;
                }
                else if (string.IsNullOrEmpty(newPort.Port) || newPort.Port == "3389")
                {
                    isDefaultPort = true;
                }
                if (isDefaultPort)
                {
                    var newDefault = (ProtocolBaseWithAddressPort)protocolServerBaseAssembly.CreateInstance(newProtocolType.FullName)!;
                    newPort.Port = newDefault.Port;
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
            Execute.OnUIThreadSync(() =>
            {
                try
                {
                    if (protocolType == typeof(RDP))
                    {
                        EditorViewModel = new RdpFormViewModel((RDP)Server);
                    }
                    else if (protocolType == typeof(RdpApp))
                    {
                        EditorViewModel = new RdpAppFormViewModel((RdpApp)Server);
                    }
                    else if (protocolType == typeof(SSH))
                    {
                        EditorViewModel = new SshFormViewModel((SSH)Server);
                    }
                    else if (protocolType == typeof(Telnet))
                    {
                        EditorViewModel = new TelnetFormViewModel((Telnet)Server);
                    }
                    else if (protocolType == typeof(Serial))
                    {
                        EditorViewModel = new SerialFormViewModel((Serial)Server);
                    }
                    else if (protocolType == typeof(FTP))
                    {
                        EditorViewModel = new FtpFormViewModel((FTP)Server);
                    }
                    else if (protocolType == typeof(SFTP))
                    {
                        EditorViewModel = new SftpFormViewModel((SFTP)Server);
                    }
                    else if (protocolType == typeof(VNC))
                    {
                        EditorViewModel = new VncFormViewModel((VNC)Server);
                    }
                    else if (protocolType == typeof(LocalApp))
                    {
                        EditorViewModel = new LocalAppFormViewModel((LocalApp)Server);
                    }
                    else if (protocolType == typeof(ProtocolBaseWithAddressPortUserPwd))
                    {
                        EditorViewModel = new ProtocolBaseWithAddressPortUserPwdFormViewModel((ProtocolBaseWithAddressPortUserPwd)Server);
                    }
                    else if (protocolType == typeof(ProtocolBaseWithAddressPort))
                    {
                        EditorViewModel = new ProtocolBaseWithAddressPortFormViewModel((ProtocolBaseWithAddressPort)Server);
                    }
                    else if (protocolType == typeof(ProtocolBase))
                    {
                        EditorViewModel = null;
                    }
                    else
                        throw new NotImplementedException($"can not find from for '{protocolType.Name}' in {nameof(ServerEditorPageViewModel)}");
                }
                catch (Exception e)
                {
                    SimpleLogHelper.Error(e);
                    throw;
                }
            });
        }

        private void UpdateRunners(string protocolName)
        {
            var selectedRunner = Server.SelectedRunnerName;
            Runners.Clear();
            if (IoC.Get<ProtocolConfigurationService>().ProtocolConfigs.ContainsKey(protocolName))
            {
                var c = IoC.Get<ProtocolConfigurationService>().ProtocolConfigs[protocolName];
                Runners.Add("Follow the global settings");
                foreach (var runner in c.Runners)
                {
                    Runners.Add(runner.Name);
                }
                if (IsBuckEdit && _orgServer.SelectedRunnerName == Server.ServerEditorDifferentOptions)
                {
                    Runners.Add(Server.ServerEditorDifferentOptions);
                }
                Server.SelectedRunnerName = Runners.Any(x => x == selectedRunner) ? selectedRunner : Runners.First();
            }
            else
            {
                Server.SelectedRunnerName = "";
            }
        }




        private RelayCommand? _cmdSelectScript;
        public RelayCommand CmdSelectScript
        {
            get
            {
                return _cmdSelectScript ??= new RelayCommand((o) =>
                {
                    lock (this)
                    {
                        var path = SelectFileHelper.OpenFile(title: "Select a script", filter: $"script|*.bat;*.cmd;*.ps1;*.py|*|*.*");
                        if (path == null || File.Exists(path) == false) return;
                        if (o?.ToString()?.ToLower() == "before")
                        {
                            Server.CommandBeforeConnected = path;
                        }
                        else
                        {
                            Server.CommandAfterDisconnected = path;
                        }
                    }
                });
            }
        }


        private RelayCommand? _cmdSelectImage;
        public RelayCommand CmdSelectImage
        {
            get
            {
                return _cmdSelectImage ??= new RelayCommand((o) =>
                {
                    var dlg = new IconPopupDialogViewModel(Server.IconImg);
                    MaskLayerController.ShowWindowWithMask(dlg, onCloseWithTrue: () =>
                    {
                        Server.IconBase64 = dlg.Icon.ToBase64();
                    });
                });
            }
        }




        private RelayCommand? _cmdTestScript;
        public RelayCommand CmdTestScript
        {
            get
            {
                return _cmdTestScript ??= new RelayCommand((o) =>
                {
                    MaskLayerController.ShowProcessingRing(assignLayerContainer: IoC.Get<MainWindowViewModel>());
                    Task.Factory.StartNew(() =>
                    {
                        if (o?.ToString()?.ToLower() == "before")
                        {
                            Server.RunScriptBeforeConnect(true);
                        }
                        else
                        {
                            Server.RunScriptAfterDisconnected(true);
                        }
                        MaskLayerController.HideMask(IoC.Get<MainWindowViewModel>());
                    });
                });
            }
        }







        private RelayCommand? _cmdEditCredential;
        public RelayCommand CmdEditCredential
        {
            get
            {
                return _cmdEditCredential ??= new RelayCommand((o) =>
                {
                    if (Server is ProtocolBaseWithAddressPort protocol)
                    {
                        var credential = o as Credential;
                        var existedNames = protocol.AlternateCredentials.Select(x => x.Name).ToList();
                        if (IsBuckEdit && _serversInBuckEdit?.Count() > 0)
                        {
                            foreach (var s in _serversInBuckEdit)
                            {
                                if (s is ProtocolBaseWithAddressPort p)
                                    existedNames.AddRange(p.AlternateCredentials.Select(x => x.Name));
                            }
                        }
                        existedNames = existedNames.Distinct().ToList();
                        var vm = AlternativeCredentialEditViewModel.NewFormProtocol(protocol, existedNames, credential);
                        vm.OnSave += () =>
                        {
                            if (credential != null && protocol.AlternateCredentials.Any(x => x.Equals(credential)))
                            {
                                // edit
                                var i = protocol.AlternateCredentials.IndexOf(credential);
                                protocol.AlternateCredentials.Remove(credential);
                                protocol.AlternateCredentials.Insert(i, vm.New);
                            }
                            else
                            {
                                // add
                                protocol.AlternateCredentials.Add(vm.New);
                            }
                        };
                        MaskLayerController.ShowWindowWithMask(vm);
                    }
                }, o => Server is ProtocolBaseWithAddressPort);
            }
        }




        private RelayCommand? _cmdDeleteCredential;
        public RelayCommand CmdDeleteCredential
        {
            get
            {
                return _cmdDeleteCredential ??= new RelayCommand((o) =>
                {
                    if (o is Credential credential
                        && Server is ProtocolBaseWithAddressPort protocol)
                    {
                        if (MessageBoxHelper.Confirm(IoC.Translate("confirm_to_delete_selected"), ownerViewModel: IoC.Get<MainWindowViewModel>()))
                        {
                            if (protocol.AlternateCredentials.Contains(credential) == true)
                            {
                                protocol.AlternateCredentials.Remove(credential);
                            }
                        }
                    }
                }, o => Server is ProtocolBaseWithAddressPort);
            }
        }
    }
}