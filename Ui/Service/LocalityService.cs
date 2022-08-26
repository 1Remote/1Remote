using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using Newtonsoft.Json;
using _1RM.Model.Protocol;
using Shawn.Utils;

namespace _1RM.Service
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
        public EnumServerOrderBy ServerOrderBy = EnumServerOrderBy.IdAsc;
        public ConcurrentDictionary<string, RdpLocalSetting> RdpLocalitys = new();
    }

    public sealed class LocalityService
    {
        public LocalityService()
        {
            _localitySettings = new LocalitySettings();
            try
            {
                var tmp = JsonConvert.DeserializeObject<LocalitySettings>(File.ReadAllText(AppPathHelper.Instance.LocalityJsonPath));
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
        public EnumServerOrderBy ServerOrderBy
        {
            get => _localitySettings.ServerOrderBy;
            set
            {
                if (_localitySettings.ServerOrderBy != value)
                {
                    _localitySettings.ServerOrderBy = value;
                    Save();
                }
            }
        }


        private readonly LocalitySettings _localitySettings;

        public RdpLocalSetting? RdpLocalityGet(string key)
        {
            if (_localitySettings.RdpLocalitys.TryGetValue(key, out var v))
            {
                return v;
            }
            return null;
        }

        public void RdpLocalityUpdate(string key, bool isFullScreen, int fullScreenIndex = -1)
        {
            var value = new RdpLocalSetting()
            {
                LastUpdateTime = DateTime.Now,
                FullScreenLastSessionIsFullScreen = isFullScreen,
                FullScreenLastSessionScreenIndex = isFullScreen ? fullScreenIndex : -1,
            };
            _localitySettings.RdpLocalitys.AddOrUpdate(key, value, (s, setting) => value);
            var obsoletes = _localitySettings.RdpLocalitys.Where(x => x.Value.LastUpdateTime < DateTime.Now.AddDays(-30)).Select(x => x.Key).ToArray();
            foreach (var obsolete in obsoletes)
            {
                _localitySettings.RdpLocalitys.TryRemove(obsolete, out _);
            }
            Save();
        }

        #region Interface

        public bool CanSave = true;
        private void Save()
        {
            if (!CanSave) return;
            lock (this)
            {
                if (!CanSave) return;
                CanSave = false;
                var fi = new FileInfo(AppPathHelper.Instance.LocalityJsonPath);
                if (fi?.Directory?.Exists == false)
                    fi.Directory.Create();
                File.WriteAllText(AppPathHelper.Instance.LocalityJsonPath, JsonConvert.SerializeObject(this._localitySettings, Formatting.Indented), Encoding.UTF8);
                CanSave = true;
            }
        }

        #endregion Interface
    }
}