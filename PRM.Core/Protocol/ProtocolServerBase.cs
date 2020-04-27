using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ColorPickerWPF.Code;
using Newtonsoft.Json;
using Shawn.Ulits;
using Brush = System.Drawing.Brush;
using Color = System.Windows.Media.Color;

namespace PRM.Core.Protocol
{
    public abstract class ProtocolServerBase : NotifyPropertyChangedBase
    {
        protected ProtocolServerBase(string serverType, string classVersion, string protocolFullName)
        {
            ServerType = serverType;
            ClassVersion = classVersion;
            ProtocolFullName = protocolFullName;
        }


        private uint _id = 0;
        [JsonIgnore]
        public uint Id
        {
            get => _id;
            set => SetAndNotifyIfChanged(nameof(Id), ref _id, value);
        }



        private string _serverType = "";
        public string ServerType
        {
            get => _serverType;
            protected set => SetAndNotifyIfChanged(nameof(ServerType), ref _serverType, value);
        }

        private string _classVersion = "";
        public string ClassVersion
        {
            get => _classVersion;
            protected set => SetAndNotifyIfChanged(nameof(ClassVersion), ref _classVersion, value);
        }

        private string _protocolFullName = "";
        public string ProtocolFullName
        {
            get => _protocolFullName;
            protected set => SetAndNotifyIfChanged(nameof(ProtocolFullName), ref _protocolFullName, value);
        }

        private string _dispName = "";
        public string DispName
        {
            get => _dispName;
            set
            {
                SetAndNotifyIfChanged(nameof(DispName), ref _dispName, value);
            }
        }

        public string SubTitle => GetSubTitle();

        private string _groupName = "";
        public string GroupName
        {
            get => _groupName;
            set => SetAndNotifyIfChanged(nameof(GroupName), ref _groupName, value);
        }

        private string _iconBase64 = "";
        public string IconBase64
        {
            get => _iconBase64;
            set
            {
                try
                {
                    var bm = NetImageProcessHelper.BitmapFromBytes(Convert.FromBase64String(value));
                    var icon = bm.ToIcon();
                    IconImg = bm.ToBitmapSource();
                    Icon = icon;
                    SetAndNotifyIfChanged(nameof(IconBase64), ref _iconBase64, value);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }


        private Icon _icon;
        [JsonIgnore]
        public Icon Icon
        {
            get => _icon;
            private set => SetAndNotifyIfChanged(nameof(Icon), ref _icon, value);
        }


        private BitmapSource _iconImg;
        [JsonIgnore]
        public BitmapSource IconImg
        {
            get => _iconImg;
            set
            {
                _iconBase64 = Convert.ToBase64String(value.ToBytes());
                Icon = value.ToIcon();
                SetAndNotifyIfChanged(nameof(IconImg), ref _iconImg, value);
            }
        }


        private string _markColorHex = "#FFFFFF";
        public string MarkColorHex
        {
            get => _markColorHex;
            set
            {
                try
                {
                    var color = (Color)System.Windows.Media.ColorConverter.ConvertFromString(value);
                    SetAndNotifyIfChanged(nameof(MarkColorHex), ref _markColorHex, value);
                    SetAndNotifyIfChanged(nameof(MarkColor), ref _markColor, color);
                }
                catch (Exception)
                {
                }
            }
        }

        private Color _markColor = Colors.White;
        [JsonIgnore]
        public Color MarkColor
        {
            get => _markColor;
            set
            {
                SetAndNotifyIfChanged(nameof(MarkColorHex), ref _markColorHex, value.ToHexString());
                SetAndNotifyIfChanged(nameof(MarkColor), ref _markColor, value);
            }
        }

        [JsonIgnore]
        public Action<uint> OnCmdConn = null;

        public void Conn()
        {
            Debug.Assert(this.Id > 0);
            OnCmdConn?.Invoke(this.Id);
        }


        /// <summary>
        /// copy all value type fields
        /// </summary>
        public bool Update(ProtocolServerBase org)
        {
            var myType = this.GetType();
            var yourType = org.GetType();
            if (myType == yourType)
            {
                ProtocolServerBase copyObject = this;
                while (yourType != null)
                {
                    var fields = myType.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                    foreach (var fi in fields)
                    {
                        fi.SetValue(this, fi.GetValue(org));
                    }
                    var properties = myType.GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                    foreach (var property in properties)
                    {
                        if (property.SetMethod != null)
                        {
                            // update properties without notify
                            property.SetValue(this, property.GetValue(org));
                            // then raise notify
                            base.RaisePropertyChanged(property.Name);
                        }
                    }
                    // update base class
                    yourType = yourType.BaseType;
                }
            }
            return false;
        }




        public abstract string ToJsonString();
        public abstract ProtocolServerBase CreateFromJsonString(string jsonString);




        protected abstract string GetSubTitle();




        /// <summary>
        /// cation: it is a shallow
        /// </summary>
        public object Clone()
        {
            return MemberwiseClone();
        }
    }
}
