using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ColorPickerWPF.Code;
using Newtonsoft.Json;
using PRM.Core.Model;
using Shawn.Utils;
using Color = System.Windows.Media.Color;

namespace PRM.Core.Protocol
{
    public abstract class ProtocolServerBase : NotifyPropertyChangedBase, ICloneable
    {
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
            Server_editor_different_options = SystemConfig.Instance.Language.GetText("server_editor_different_options");
        }

        public abstract bool IsOnlyOneInstance();

        private int _id = 0;

        [JsonIgnore]
        public int Id
        {
            get => _id;
            set => SetAndNotifyIfChanged(nameof(Id), ref _id, value);
        }

        public string Protocol { get; }

        public string ClassVersion { get; }

        [JsonIgnore]
        public string ProtocolDisplayName { get; }

        [JsonIgnore]
        public string ProtocolDisplayNameInShort { get; }

        private string _dispName = "";

        public string DispName
        {
            get => _dispName;
            set => SetAndNotifyIfChanged(nameof(DispName), ref _dispName, value);
        }

        [JsonIgnore]
        public string SubTitle => GetSubTitle();


        private string _groupName = "";

        public string GroupName
        {
            get => _groupName;
            set => SetAndNotifyIfChanged(nameof(GroupName), ref _groupName, value);
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
                _tags = _tags?.Distinct()?.ToList();
                return _tags;
            }
            set => SetAndNotifyIfChanged(nameof(Tags), ref _tags, value);
        }

        private string _iconBase64 = "";

        public string IconBase64
        {
            get => _iconBase64;
            set
            {
                iconCache = null;
                SetAndNotifyIfChanged(nameof(IconBase64), ref _iconBase64, value);
                RaisePropertyChanged(nameof(IconImg));
            }
        }

        private BitmapSource iconCache = null;

        [JsonIgnore]
        public BitmapSource IconImg
        {
            get
            {
                if (iconCache != null)
                    return iconCache;
                try
                {
                    iconCache = Convert.FromBase64String(_iconBase64).BitmapFromBytes().ToBitmapSource();
                }
                catch (Exception)
                {
                    return null;
                }
                return iconCache;
            }
        }


        private string _markColorHex = "#FFFFFF";

        public string MarkColorHex
        {
            get => _markColorHex;
            set => SetAndNotifyIfChanged(nameof(MarkColorHex), ref _markColorHex, value);
        }

        private DateTime _lastConnTime = DateTime.MinValue;

        public DateTime LastConnTime
        {
            get => _lastConnTime;
            set => SetAndNotifyIfChanged(nameof(LastConnTime), ref _lastConnTime, value);
        }

        private string _commandBeforeConnected = "";

        public string CommandBeforeConnected
        {
            get => _commandBeforeConnected;
            set => SetAndNotifyIfChanged(nameof(CommandBeforeConnected), ref _commandBeforeConnected, value);
        }

        private string _commandAfterDisconnected = "";

        public string CommandAfterDisconnected
        {
            get => _commandAfterDisconnected;
            set => SetAndNotifyIfChanged(nameof(CommandAfterDisconnected), ref _commandAfterDisconnected, value);
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
        public object Clone()
        {
            Server_editor_different_options = SystemConfig.Instance.Language.GetText("server_editor_different_options");
            return MemberwiseClone();
        }

        public virtual bool EqualTo(ProtocolServerBase compare)
        {
            var t1 = this.GetType();
            var t2 = compare.GetType();
            if (t1 != t2) return false;
            var properties = t1.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var property in properties)
            {
                if (!property.CanWrite || property.SetMethod == null) continue;
                var v1 = property.GetValue(this)?.ToString();
                var v2 = property.GetValue(compare)?.ToString();
                if (v1 != v2)
                    return false;
            }
            return true;
        }

        public void RunScriptBeforConnect()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(CommandBeforeConnected))
                {
                    // TODO add some params
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
                    // TODO add some params
                    CmdRunner.RunCmdAsync(CommandAfterDisconnected);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}