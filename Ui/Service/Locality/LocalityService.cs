using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using _1RM.Model;
using _1RM.Model.Protocol;
using _1RM.Service.DataSource;
using _1RM.Utils;
using _1RM.View;
using _1RM.View.Launcher;
using Newtonsoft.Json;
using Shawn.Utils;

namespace _1RM.Service.Locality
{
    public enum EnumServerOrderBy
    {
        IdAsc = -1,
        ProtocolAsc = 0,
        ProtocolDesc = 1,
        NameAsc = 2,
        NameDesc = 3,

        //TagAsc = 4,
        //TagDesc = 5,
        AddressAsc = 6,
        AddressDesc = 7,
        Custom = 999,
    }

    internal class LocalitySettings
    {
        public double MainWindowWidth = 800;
        public double MainWindowHeight = 530;
        public double TabWindowTop = -1;
        public double TabWindowLeft = -1;
        public double TabWindowWidth = 800;
        public double TabWindowHeight = 600;
        public WindowState TabWindowState = WindowState.Normal;
        public WindowStyle TabWindowStyle = WindowStyle.SingleBorderWindow;
        public int FtpColumnFileNameLength = -1;
        public int FtpColumnFileTimeLength = -1;
        public int FtpColumnFileTypeLength = -1;
        public int FtpColumnFileSizeLength = -1;
        public Dictionary<string, string> Misc = new Dictionary<string, string>();
    }

    public sealed class LocalityService
    {
        private readonly LocalitySettings _localitySettings;
        public static string JsonPath => Path.Combine(AppPathHelper.Instance.LocalityDirPath, ".locality.json");
        public bool CanSave = true;
        private void Save()
        {
            if (!CanSave) return;
            lock (this)
            {
                if (!CanSave) return;
                CanSave = false;
                AppPathHelper.CreateDirIfNotExist(AppPathHelper.Instance.LocalityDirPath, false);
                RetryHelper.Try(() => { File.WriteAllText(JsonPath, JsonConvert.SerializeObject(this._localitySettings, Formatting.Indented), Encoding.UTF8); },
                    actionOnError: exception => MsAppCenterHelper.Error(exception));
                CanSave = true;
            }
        }

        public LocalityService()
        {
            // Load
            _localitySettings = new LocalitySettings();
            try
            {
                var tmp = JsonConvert.DeserializeObject<LocalitySettings>(File.ReadAllText(JsonPath));
                if (tmp != null)
                    _localitySettings = tmp;
            }
            catch
            {
                // ignored
            }
        }


        public double MainWindowWidth
        {
            get => _localitySettings.MainWindowWidth;
            set
            {
                if (Math.Abs(_localitySettings.MainWindowWidth - value) > 0.001)
                {
                    _localitySettings.MainWindowWidth = value;
                    Save();
                }
            }
        }

        public double MainWindowHeight
        {
            get => _localitySettings.MainWindowHeight;
            set
            {
                if (Math.Abs(_localitySettings.MainWindowHeight - value) > 0.001)
                {
                    _localitySettings.MainWindowHeight = value;
                    Save();
                }
            }
        }

        public double TabWindowTop
        {
            get => _localitySettings.TabWindowTop;
            set
            {
                if (Math.Abs(_localitySettings.TabWindowTop - value) > 0.001)
                {
                    _localitySettings.TabWindowTop = value;
                    Save();
                }
            }
        }

        public double TabWindowLeft
        {
            get => _localitySettings.TabWindowLeft;
            set
            {
                if (Math.Abs(_localitySettings.TabWindowLeft - value) > 0.001)
                {
                    _localitySettings.TabWindowLeft = value;
                    Save();
                }
            }
        }

        public double TabWindowWidth
        {
            get => _localitySettings.TabWindowWidth;
            set
            {
                if (Math.Abs(_localitySettings.TabWindowWidth - value) > 0.001)
                {
                    _localitySettings.TabWindowWidth = value;
                    Save();
                }
            }
        }

        public double TabWindowHeight
        {
            get => _localitySettings.TabWindowHeight;
            set
            {
                if (Math.Abs(_localitySettings.TabWindowHeight - value) > 0.001)
                {
                    _localitySettings.TabWindowHeight = value;
                    Save();
                }
            }
        }

        public WindowState TabWindowState
        {
            get => _localitySettings.TabWindowState;
            set
            {
                if (_localitySettings.TabWindowState != value)
                {
                    _localitySettings.TabWindowState = value;
                    Save();
                }
            }
        }

        public WindowStyle TabWindowStyle
        {
            get => _localitySettings.TabWindowStyle;
            set
            {
                if (_localitySettings.TabWindowStyle != value)
                {
                    _localitySettings.TabWindowStyle = value;
                    Save();
                }
            }
        }

        public int FtpColumnFileNameLength
        {
            get => _localitySettings.FtpColumnFileNameLength;
            set
            {
                if (_localitySettings.FtpColumnFileNameLength != value)
                {
                    _localitySettings.FtpColumnFileNameLength = value;
                    Save();
                }
            }
        }

        public int FtpColumnFileTimeLength
        {
            get => _localitySettings.FtpColumnFileTimeLength;
            set
            {
                if (_localitySettings.FtpColumnFileTimeLength != value)
                {
                    _localitySettings.FtpColumnFileTimeLength = value;
                    Save();
                }
            }
        }

        public int FtpColumnFileTypeLength
        {
            get => _localitySettings.FtpColumnFileTypeLength;
            set
            {
                if (_localitySettings.FtpColumnFileTypeLength != value)
                {
                    _localitySettings.FtpColumnFileTypeLength = value;
                    Save();
                }
            }
        }
        public int FtpColumnFileSizeLength
        {
            get => _localitySettings.FtpColumnFileSizeLength;
            set
            {
                if (_localitySettings.FtpColumnFileSizeLength != value)
                {
                    _localitySettings.FtpColumnFileSizeLength = value;
                    Save();
                }
            }
        }

        public void SetMisc(string key, string value)
        {
            if (_localitySettings.Misc.ContainsKey(key))
            {
                if (_localitySettings.Misc[key] == value) return;
                _localitySettings.Misc[key] = value;
            }
            else
            {
                _localitySettings.Misc.Add(key, value);
            }
            Save();
        }

        
        public T GetMisc<T>(string key, T defaultValue = default!)
        {
            var value = _localitySettings.Misc.ContainsKey(key) ? _localitySettings.Misc[key] : "";
            // 如果 T 是 int
            if (typeof(T) == typeof(int))
            {
                return int.TryParse(value, out var result) ? (T)(object)result : defaultValue;
            }
            // 如果 T 是 bool
            if (typeof(T) == typeof(bool))
            {
                return bool.TryParse(value, out var result) ? (T)(object)result : defaultValue;
            }
            // 如果 T 是 float
            if (typeof(T) == typeof(float))
            {
                return float.TryParse(value, out var result) ? (T)(object)result : defaultValue;
            }
            // 如果 T 是 float
            if (typeof(T) == typeof(double))
            {
                return double.TryParse(value, out var result) ? (T)(object)result : defaultValue;
            }
            // 如果 T 是 string
            if (typeof(T) == typeof(string))
            {
                return (T)(object)value;
            }
            throw new NotSupportedException($"Not support type {typeof(T)}");
        }
    }
}