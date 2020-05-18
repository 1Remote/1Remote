using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ColorPickerWPF.Code;
using Newtonsoft.Json;
using PRM.Core.DB;
using Shawn.Ulits;
using Brush = System.Drawing.Brush;
using Color = System.Windows.Media.Color;

namespace PRM.Core.Protocol
{
    public abstract class ProtocolServerBase : NotifyPropertyChangedBase, ICloneable
    {
        protected ProtocolServerBase(string protocol, string classVersion, string protocolDisplayName, bool onlyOneInstance = true)
        {
            Protocol = protocol;
            ClassVersion = classVersion;
            ProtocolDisplayName = protocolDisplayName;
            OnlyOneInstance = onlyOneInstance;
        }
        
        private bool _OnlyOneInstance = true;
        public bool OnlyOneInstance
        {
            get => _OnlyOneInstance;
            private set => SetAndNotifyIfChanged(nameof(OnlyOneInstance), ref _OnlyOneInstance, value);
        }


        private uint _id = 0;
        [JsonIgnore]
        public uint Id
        {
            get => _id;
            set => SetAndNotifyIfChanged(nameof(Id), ref _id, value);
        }

        public string Protocol { get; }

        public string ClassVersion { get; }

        public string ProtocolDisplayName { get; }


        private string _dispName = "";
        public string DispName
        {
            get => _dispName;
            set
            {
                SetAndNotifyIfChanged(nameof(DispName), ref _dispName, value);
            }
        }

        [JsonIgnore]
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
                SetAndNotifyIfChanged(nameof(IconBase64), ref _iconBase64, value);
                try
                {
                    var bm = NetImageProcessHelper.BitmapFromBytes(Convert.FromBase64String(value));
                    _iconImg = bm.ToBitmapSource();
                    Icon = bm.ToIcon();
                }
                catch (Exception e)
                {
                    _iconImg = null;
                    Icon = null;
                    //Console.WriteLine(e);
                }
            }
        }


        private Icon _icon = null;
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
                SetAndNotifyIfChanged(nameof(IconImg), ref _iconImg, value);
                try
                {
                    _iconBase64 = Convert.ToBase64String(value.ToBytes());
                    Icon = value.ToIcon();
                }
                catch (Exception e)
                {
                    _iconBase64 = null;
                    Icon = null;
                    //Console.WriteLine(e);
                }
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

        private DateTime _lastConnTime = DateTime.MinValue;
        [JsonIgnore]
        public DateTime LastConnTime
        {
            get => _lastConnTime;
            set => SetAndNotifyIfChanged(nameof(LastConnTime), ref _lastConnTime, value);
        }


        [JsonIgnore]
        public Action<uint> OnCmdConn = null;

        public void Conn()
        {
            Debug.Assert(this.Id > 0);
            this.LastConnTime = DateTime.Now;
            Server.AddOrUpdate(this);
            OnCmdConn?.Invoke(this.Id);
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
                    var fields = myType.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                    foreach (var fi in fields)
                    {
                        if (!fi.IsInitOnly)
                            fi.SetValue(this, fi.GetValue(copyFromObj));
                    }
                    var properties = myType.GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
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
            }
            return false;
        }


        ///// <summary>
        ///// copy all value ProtocolServerBase fields
        ///// </summary>
        //public bool Update(ProtocolServerBase copyFromObj)
        //{
        //    var baseType = levelType;
        //    ProtocolServerBase copyObject = this;
        //    var fields = baseType.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        //    foreach (var fi in fields)
        //    {
        //        fi.SetValue(this, fi.GetValue(copyFromObj));
        //    }
        //    var properties = baseType.GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        //    foreach (var property in properties)
        //    {
        //        if (property.SetMethod != null)
        //        {
        //            // update properties without notify
        //            property.SetValue(this, property.GetValue(copyFromObj));
        //            // then raise notify
        //            base.RaisePropertyChanged(property.Name);
        //        }
        //    }
        //    return false;
        //}


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
