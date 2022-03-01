using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Contexts;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using JsonKnownTypes;
using Newtonsoft.Json;
using PRM.Core.Model;
using PRM.Core.Protocol.Base;
using PRM.Core.Protocol.FileTransmit.FTP;
using PRM.Core.Protocol.FileTransmit.SFTP;
using PRM.Core.Protocol.Putty.SSH;
using PRM.Core.Protocol.Putty.Telnet;
using PRM.Core.Protocol.RDP;
using PRM.Core.Protocol.VNC;
using PRM.Core.Service;
using Shawn.Utils;

namespace PRM.Core.Protocol
{
    //[JsonConverter(typeof(JsonKnownTypesConverter<ProtocolServerBase>))] // json serialize/deserialize derived types https://stackoverflow.com/a/60296886/8629624
    //[JsonKnownType(typeof(ProtocolServerWithAddrPortBase), nameof(ProtocolServerWithAddrPortBase))]
    //[JsonKnownType(typeof(ProtocolServerWithAddrPortUserPwdBase), nameof(ProtocolServerWithAddrPortUserPwdBase))]
    //[JsonKnownType(typeof(ProtocolServerRDP), nameof(ProtocolServerRDP))]
    //[JsonKnownType(typeof(ProtocolServerSSH), nameof(ProtocolServerSSH))]
    //[JsonKnownType(typeof(ProtocolServerTelnet), nameof(ProtocolServerTelnet))]
    //[JsonKnownType(typeof(ProtocolServerVNC), nameof(ProtocolServerVNC))]
    //[JsonKnownType(typeof(ProtocolServerFTP), nameof(ProtocolServerFTP))]
    //[JsonKnownType(typeof(ProtocolServerSFTP), nameof(ProtocolServerSFTP))]
    public abstract class ProtocolServerBase : NotifyPropertyChangedBase
    {
        [JsonIgnore]
        public string Server_editor_different_options { get; private set; }

        protected ProtocolServerBase(string protocol, string classVersion, string protocolDisplayName, string protocolDisplayNameInShort = "")
        {
            Protocol = protocol;
            ClassVersion = classVersion;
            ProtocolDisplayName = protocolDisplayName;
            if (string.IsNullOrWhiteSpace(protocolDisplayNameInShort))
                ProtocolDisplayNameInShort = ProtocolDisplayName;
            else
                ProtocolDisplayNameInShort = protocolDisplayNameInShort;
            Server_editor_different_options = LanguageService.TmpLanguageService?.Translate("server_editor_different_options") ?? "";
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
                if (string.IsNullOrEmpty(GroupName) == false && (_tags == null || _tags.Count == 0))
                {
                    _tags = new List<string>() { GroupName };
                    GroupName = string.Empty;
                }
                _tags = _tags?.Distinct()?.OrderBy(x => x).ToList();
                return _tags;
            }
            set => SetAndNotifyIfChanged(ref _tags, value?.Distinct()?.OrderBy(x => x)?.ToList());
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

        private BitmapSource _iconCache = null;

        [JsonIgnore]
        public BitmapSource IconImg
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
            set => SetAndNotifyIfChanged(ref _commandBeforeConnected, value);
        }

        private string _commandAfterDisconnected = "";

        public string CommandAfterDisconnected
        {
            get => _commandAfterDisconnected;
            set => SetAndNotifyIfChanged(ref _commandAfterDisconnected, value);
        }


        /// <summary>
        /// copy all value type fields
        /// </summary>
        public bool Update(ProtocolServerBase copyFromObj, Type levelType = null)
        {
            var baseType = levelType;
            if (baseType == null)
                baseType = this.GetType();
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
                ProtocolServerBase copyObject = this;
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
        public abstract ProtocolServerBase CreateFromJsonString(string jsonString);

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
        public ProtocolServerBase Clone()
        {
            var clone = this.MemberwiseClone() as ProtocolServerBase;
            clone.Server_editor_different_options = LanguageService.TmpLanguageService?.Translate("server_editor_different_options") ?? "<different options>";
            clone.Tags = new List<string>(this.Tags);
            return clone;
        }

        public void RunScriptBeforeConnect()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(CommandBeforeConnected))
                {
                    CmdRunner.RunCmdAsync(CommandBeforeConnected);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public void RunScriptAfterDisconnected()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(CommandAfterDisconnected))
                {
                    CmdRunner.RunCmdAsync(CommandAfterDisconnected);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        /// <summary>
        /// run before connect, decrypt all fields
        /// </summary>
        /// <param name="context"></param>
        public virtual void ConnectPreprocess(PrmContext context)
        {
            var s = this;
            context.DataService.DecryptToRamLevel(ref s);
            context.DataService.DecryptToConnectLevel(ref s);
        }

        public static List<ProtocolServerBase> GetAllSubInstance()
        {
            var assembly = typeof(ProtocolServerBase).Assembly;
            var types = assembly.GetTypes();
            // reflect remote protocols
            var protocolList = types.Where(item => item.IsSubclassOf(typeof(ProtocolServerBase)) && !item.IsAbstract).Select(type => (ProtocolServerBase)Activator.CreateInstance(type)).OrderBy(x => x.GetListOrder()).ToList();
            return protocolList;
        }
    }
}