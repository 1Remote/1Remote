using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Media.Imaging;
using _1RM.Service;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.Model;
using _1RM.Utils;
using FluentFTP.Helpers;
using Newtonsoft.Json;
using Shawn.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.Image;

namespace _1RM.Model.Protocol.Base
{
    //[JsonConverter(typeof(JsonKnownTypesConverter<ProtocolBase>))] // json serialize/deserialize derived types https://stackoverflow.com/a/60296886/8629624
    //[JsonKnownType(typeof(ProtocolBaseWithAddressPort), nameof(ProtocolBaseWithAddressPort))]
    //[JsonKnownType(typeof(ProtocolBaseWithAddressPortUserPwd), nameof(ProtocolBaseWithAddressPortUserPwd))]
    //[JsonKnownType(typeof(RDP), nameof(RDP))]
    //[JsonKnownType(typeof(SSH), nameof(SSH))]
    //[JsonKnownType(typeof(Telnet), nameof(Telnet))]
    //[JsonKnownType(typeof(VNC), nameof(VNC))]
    //[JsonKnownType(typeof(FTP), nameof(FTP))]
    //[JsonKnownType(typeof(SFTP), nameof(SFTP))]
    public abstract class ProtocolBase : NotifyPropertyChangedBase
    {
        [JsonIgnore] public string ServerEditorDifferentOptions => IoC.Get<ILanguageService>().Translate("server_editor_different_options").Replace(" ", "-");

        protected ProtocolBase(string protocol, string classVersion, string protocolDisplayName)
        {
            Protocol = protocol;
            ClassVersion = classVersion;
            ProtocolDisplayName = protocolDisplayName;
        }

        public abstract bool IsOnlyOneInstance();

        private string _id = string.Empty;

        /// <summary>
        /// ULID since 1Remote
        /// </summary>
        [JsonIgnore]
        public string Id
        {
            get
            {
                if (string.IsNullOrEmpty(_id))
                    GenerateIdForTmpSession();
                return _id;
            }
            set => SetAndNotifyIfChanged(ref _id, value);
        }

        public bool IsTmpSession()
        {
            return _id.StartsWith("TMP_SESSION_") || string.IsNullOrEmpty(_id);
        }

        public void GenerateIdForTmpSession()
        {
            Debug.Assert(IsTmpSession());
            _id = "TMP_SESSION_" + new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds();
        }

        /// <summary>
        /// protocol name
        /// </summary>
        public string Protocol { get; }

        public string ClassVersion { get; }

        [JsonIgnore]
        public string ProtocolDisplayName { get; }

        private string _displayName = "";
        public string DisplayName
        {
            get => _displayName;
            set => SetAndNotifyIfChanged(ref _displayName, value);
        }

        [JsonIgnore]
        public string SubTitle => GetSubTitle();


        // ReSharper disable once ArrangeObjectCreationWhenTypeEvident
        private List<string> _tags = new List<string>();
        public List<string> Tags
        {
            get
            {
                _tags = _tags.Distinct().OrderBy(x => x).ToList();
                return _tags;
            }
            set
            {
                // bulk edit 时可能会传入 null
                // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
                if (value == null)
                    SetAndNotifyIfChanged(ref _tags, new List<string>());
                else
                    SetAndNotifyIfChanged(ref _tags, value.Distinct().Select(x => x.Trim().Replace(" ", "")).OrderBy(x => x).ToList());
            }
        }

        private string _iconBase64 = "";
        public string IconBase64
        {
            get => _iconBase64;
            set
            {
                _iconCache = null;
                SetAndNotifyIfChanged(ref _iconBase64, value);
                RaisePropertyChanged(nameof(IconImg));
            }
        }

        private BitmapSource? _iconCache = null;
        [JsonIgnore]
        public BitmapSource? IconImg
        {
            get
            {
                if (_iconCache != null)
                    return _iconCache;
                try
                {
                    _iconCache = Convert.FromBase64String(_iconBase64).BitmapFromBytes().ToBitmapSource();
                }
                catch (Exception)
                {
                    return null;
                }
                return _iconCache;
            }
        }



        private string _colorHex = "#00000000";
        public string ColorHex
        {
            get => _colorHex;
            set => SetAndNotifyIfChanged(ref _colorHex, value);
        }

        private string _commandBeforeConnected = "";
        public string CommandBeforeConnected
        {
            get => _commandBeforeConnected;
            set => SetAndNotifyIfChanged(ref _commandBeforeConnected, value);
        }

        private bool _hideCommandBeforeConnectedWindow = false;
        public bool HideCommandBeforeConnectedWindow
        {
            get => _hideCommandBeforeConnectedWindow;
            set => SetAndNotifyIfChanged(ref _hideCommandBeforeConnectedWindow, value);
        }

        private string _commandAfterDisconnected = "";
        public string CommandAfterDisconnected
        {
            get => _commandAfterDisconnected;
            set => SetAndNotifyIfChanged(ref _commandAfterDisconnected, value);
        }

        private string _note = "";
        public string Note
        {
            get => _note;
            set => SetAndNotifyIfChanged(ref _note, value);
        }

        private string _selectedRunnerName = "";

        public string SelectedRunnerName
        {
            get => _selectedRunnerName;
            set => SetAndNotifyIfChanged(ref _selectedRunnerName, value);
        }


        /// <summary>
        /// copy all value type fields
        /// </summary>
        public bool Update(ProtocolBase copyFromObj, Type? levelType = null)
        {
            var baseType = levelType ?? this.GetType();
            var myType = this.GetType();
            var yourType = copyFromObj.GetType();
            while (myType != null && myType != baseType)
            {
                myType = myType.BaseType;
            }

            while (yourType != null && yourType != baseType)
            {
                yourType = yourType.BaseType;
            }

            if (myType != null && myType == yourType)
            {
                while (yourType != null)
                {
                    var fields = yourType.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                    foreach (var fi in fields)
                    {
                        if (!fi.IsInitOnly)
                            fi.SetValue(this, fi.GetValue(copyFromObj));
                    }

                    var properties = yourType.GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                    foreach (var property in properties)
                    {
                        if (property.CanWrite && property.SetMethod != null)
                        {
                            // update properties without notify
                            property.SetValue(this, property.GetValue(copyFromObj));
                            // then raise notify
                            base.RaisePropertyChanged(property.Name);
                        }
                    }

                    // update base class
                    yourType = yourType.BaseType;
                }

                return true;
            }

            return false;
        }

        public virtual string ToJsonString()
        {
            return JsonConvert.SerializeObject(this);
            //return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        /// <summary>
        /// json string to instance
        /// </summary>
        /// <param name="jsonString"></param>
        /// <returns></returns>
        public abstract ProtocolBase? CreateFromJsonString(string jsonString);

        /// <summary>
        /// subtitle of every server, different form each protocol
        /// </summary>
        /// <returns></returns>
        protected abstract string GetSubTitle();

        /// <summary>
        /// determine the display order to show items
        /// </summary>
        /// <returns></returns>
        public abstract double GetListOrder();

        /// <summary>
        /// cation: it is a shallow
        /// </summary>
        public ProtocolBase Clone()
        {
            //{
            //    var json = ToJsonString();
            //    var jsonClone = ItemCreateHelper.CreateFromJsonString(json);
            //    if (jsonClone != null)
            //    {
            //        jsonClone.Id = this.Id;
            //        jsonClone.DataSourceName = this.DataSourceName;
            //        return jsonClone;
            //    }
            //}


            var clone = (ProtocolBase)this.MemberwiseClone();
            Debug.Assert(clone != null);
            clone!.Tags = new List<string>(this.Tags);
            if (this is ProtocolBaseWithAddressPortUserPwd p
                && clone is ProtocolBaseWithAddressPortUserPwd c)
            {
                c.AlternateCredentials = new(p.AlternateCredentials.Select(x => x.CloneMe()));
            }
            return clone;
        }

        private Dictionary<string, string> GetEnvironmentVariablesForScript()
        {
            var evs = new Dictionary<string, string>
            {
                { "SESSION_ID", this.GetHashCode().ToString() },
                { "SERVER_ID", this.Id },
                { "SERVER_NAME", this.DisplayName },
                { "SERVER_HOST", "" },
                { "SERVER_TAGS", string.Join(",", this.Tags.ToArray()) }
            };
            if (this is ProtocolBaseWithAddressPort p)
                evs["SERVER_HOST"] = $"{p.Address}:{p.Port}";
            return evs;
        }

        public int RunScriptBeforeConnect(bool isTestRun = false)
        {
            int exitCode = 0;
            try
            {
                if (!string.IsNullOrWhiteSpace(CommandBeforeConnected))
                {
                    var tuple = WinCmdRunner.DisassembleOneLineScriptCmd(CommandBeforeConnected);

                    if (isTestRun)
                    {
                        if (string.IsNullOrEmpty(tuple.Item2) == false)
                            MessageBoxHelper.Info($"We will run: '{tuple.Item1}' with parameters '{tuple.Item2}'");
                        else
                            MessageBoxHelper.Info($"We will run: '{CommandBeforeConnected}'");
                    }

                    exitCode = WinCmdRunner.RunFile(tuple.Item1, arguments: tuple.Item2, isAsync: false,
                        isHideWindow: HideCommandBeforeConnectedWindow && isTestRun != true,
                        workingDirectory: tuple.Item3,
                        envVariables: GetEnvironmentVariablesForScript());

                    if (isTestRun)
                    {
                        MessageBoxHelper.Info($"The exit code of the script = {exitCode}.\r\nOnce the code != 0, we will terminate your connection request.");
                    }
                }
            }
            catch (Exception e)
            {
                exitCode = 1;
                SimpleLogHelper.Error(e);
                MessageBoxHelper.ErrorAlert("We encountered a problem while running the script: " + e.Message, IoC.Get<ILanguageService>().Translate("Script before connect"));
            }
            return exitCode;
        }

        public void RunScriptAfterDisconnected(bool isTestRun = false)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(CommandAfterDisconnected))
                {
                    var tuple = WinCmdRunner.DisassembleOneLineScriptCmd(CommandAfterDisconnected);

                    if (isTestRun)
                    {
                        if (string.IsNullOrEmpty(tuple.Item2) == false)
                            MessageBoxHelper.Info($"We will run: '{tuple.Item1}' with parameters '{tuple.Item2}'");
                        else
                            MessageBoxHelper.Info($"We will run: '{CommandBeforeConnected}'");
                    }

                    var exitCode = WinCmdRunner.RunFile(tuple.Item1, arguments: tuple.Item2, isAsync: true,
                        isHideWindow: isTestRun != true,
                        workingDirectory: tuple.Item3,
                        envVariables: GetEnvironmentVariablesForScript());

                    if (isTestRun)
                    {
                        MessageBoxHelper.Info($"The exit code of the script = {exitCode}.");
                    }
                }
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
                MessageBoxHelper.ErrorAlert("We encountered a problem while running the script: " + e.Message, IoC.Get<ILanguageService>().Translate("Script after disconnected"));
            }
        }

        /// <summary>
        /// run before connect, decrypt all fields
        /// </summary>
        public virtual void ConnectPreprocess()
        {
            var s = this;
            s.DecryptToConnectLevel();
        }

        public static List<ProtocolBase> GetAllSubInstance()
        {
            var assembly = typeof(ProtocolBase).Assembly;
            var types = assembly.GetTypes();
            // reflect remote protocols
            var protocolList = types.Where(item => item.IsSubclassOf(typeof(ProtocolBase)) && !item.IsAbstract).Select(type => (ProtocolBase)Activator.CreateInstance(type)!).OrderBy(x => x.GetListOrder()).ToList();
            return protocolList;
        }


        public virtual bool IsThisTimeConnWithFullScreen()
        {
            return false;
        }

        [JsonIgnore]
        public DataSourceBase? DataSource;
    }
}