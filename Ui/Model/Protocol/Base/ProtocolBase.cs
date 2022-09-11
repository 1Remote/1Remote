using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Media.Imaging;
using Newtonsoft.Json;
using PRM.Service;
using Shawn.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.Image;
using Stylet;

namespace PRM.Model.Protocol.Base
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
        [JsonIgnore] public string ServerEditorDifferentOptions => IoC.Get<ILanguageService>().Translate("server_editor_different_options");

        protected ProtocolBase(string protocol, string classVersion, string protocolDisplayName, string protocolDisplayNameInShort = "")
        {
            Protocol = protocol;
            ClassVersion = classVersion;
            ProtocolDisplayName = protocolDisplayName;
            if (string.IsNullOrWhiteSpace(protocolDisplayNameInShort))
                ProtocolDisplayNameInShort = ProtocolDisplayName;
            else
                ProtocolDisplayNameInShort = protocolDisplayNameInShort;
        }

        public abstract bool IsOnlyOneInstance();

        private int _id = 0;

        [JsonIgnore]
        public int Id
        {
            get => _id;
            set => SetAndNotifyIfChanged(ref _id, value);
        }

        /// <summary>
        /// protocol name
        /// </summary>
        public string Protocol { get; }

        public string ClassVersion { get; }

        [JsonIgnore]
        public string ProtocolDisplayName { get; }

        [JsonIgnore]
        public string ProtocolDisplayNameInShort { get; }

        /// <summary>
        /// this is for old db to new db. do not remove until 2022.05!
        /// </summary>
        [Obsolete]
        public string DispName
        {
            get => DisplayName;
            set => DisplayName = value;
        }

        private string _displayName = "";
        public string DisplayName
        {
            get => _displayName;
            set => SetAndNotifyIfChanged(ref _displayName, value);
        }

        [JsonIgnore]
        public string SubTitle => GetSubTitle();


        private string _groupName = "";
        [Obsolete]
        public string GroupName
        {
            get => _groupName;
            set => SetAndNotifyIfChanged(ref _groupName, value);
        }


        private List<string> _tags = new List<string>();
        public List<string> Tags
        {
            get
            {
                if (string.IsNullOrEmpty(GroupName) == false && _tags.Count == 0)
                {
                    _tags = new List<string>() { GroupName };
                    GroupName = string.Empty;
                }
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
                    SetAndNotifyIfChanged(ref _tags, value?.Distinct()?.OrderBy(x => x)?.ToList());
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

        private DateTime _lastConnTime = DateTime.MinValue;
        public DateTime LastConnTime
        {
            get => _lastConnTime;
            set => SetAndNotifyIfChanged(ref _lastConnTime, value);
        }

        private string _commandBeforeConnected = "";
        public string CommandBeforeConnected
        {
            get => _commandBeforeConnected;
            set
            {
                if (SetAndNotifyIfChanged(ref _commandBeforeConnected, value))
                {
                    if (string.IsNullOrWhiteSpace(value) == false && File.Exists(value) == false)
                    {
                        throw new FileNotFoundException(value);
                    }
                }
            }
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
            set
            {
                if (SetAndNotifyIfChanged(ref _commandAfterDisconnected, value))
                {
                    if (string.IsNullOrWhiteSpace(value) == false && File.Exists(value) == false)
                    {
                        throw new FileNotFoundException(value);
                    }
                }
            }
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
            return JsonConvert.SerializeObject(this, Formatting.Indented);
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
            var clone = this.MemberwiseClone() as ProtocolBase;
            Debug.Assert(clone != null);
            clone.Tags = (this.Tags?.Count > 0) ? new List<string>(this.Tags) : new List<string>();
            return clone;
        }

        public void RunScriptBeforeConnect()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(CommandBeforeConnected) && File.Exists(CommandBeforeConnected))
                {
                    WinCmdRunner.RunScriptFile(CommandBeforeConnected, isAsync: false, isHideWindow: HideCommandBeforeConnectedWindow);
                }
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
            }
        }

        public void RunScriptAfterDisconnected()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(CommandAfterDisconnected) && File.Exists(CommandAfterDisconnected))
                {
                    WinCmdRunner.RunScriptFile(CommandAfterDisconnected, isAsync: true, isHideWindow: true);
                }
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
            }
        }

        /// <summary>
        /// run before connect, decrypt all fields
        /// </summary>
        /// <param name="context"></param>
        public virtual void ConnectPreprocess(PrmContext context)
        {
            if (context?.DataService == null) return;
            var s = this;
            context.DataService.DecryptToRamLevel(ref s);
            context.DataService.DecryptToConnectLevel(ref s);
        }

        public static List<ProtocolBase> GetAllSubInstance()
        {
            var assembly = typeof(ProtocolBase).Assembly;
            var types = assembly.GetTypes();
            // reflect remote protocols
            var protocolList = types.Where(item => item.IsSubclassOf(typeof(ProtocolBase)) && !item.IsAbstract).Select(type => (ProtocolBase)Activator.CreateInstance(type)!).OrderBy(x => x.GetListOrder()).ToList();
            return protocolList;
        }


        public virtual bool ThisTimeConnWithFullScreen()
        {
            return false;
        }
    }
}