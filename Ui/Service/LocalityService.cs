using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using Newtonsoft.Json;
using _1RM.Model.Protocol;
using _1RM.Service.DataSource;
using _1RM.View.Launcher;
using Shawn.Utils;
using _1RM.View;
using _1RM.Utils;

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
        Custom,
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
        public EnumServerOrderBy ServerOrderBy = EnumServerOrderBy.IdAsc;
        public Dictionary<string, int> ServerCustomOrder = new Dictionary<string, int>();
        public Dictionary<string, int> ServerGroupedOrder = new Dictionary<string, int>();
        public Dictionary<string, bool> ServerGroupedIsExpanded = new Dictionary<string, bool>();
        public ConcurrentDictionary<string, RdpLocalSetting> RdpLocalities = new ConcurrentDictionary<string, RdpLocalSetting>();
        public List<QuickConnectionItem> QuickConnectionHistory = new List<QuickConnectionItem>();
    }

    public sealed class LocalityService
    {
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

        public ReadOnlyCollection<QuickConnectionItem> QuickConnectionHistory => _localitySettings.QuickConnectionHistory.AsReadOnly();

        private readonly LocalitySettings _localitySettings;

        public Dictionary<string, int> ServerCustomOrder => _localitySettings.ServerCustomOrder;
        public Dictionary<string, int> ServerGroupedOrder => _localitySettings.ServerGroupedOrder;
        public Dictionary<string, bool> ServerGroupedIsExpanded => _localitySettings.ServerGroupedIsExpanded;

        #region Interface

        #region Save local settings for every rdp session by session id

        public RdpLocalSetting? RdpLocalityGet(string key)
        {
            if (_localitySettings.RdpLocalities.TryGetValue(key, out var v))
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
            _localitySettings.RdpLocalities.AddOrUpdate(key, value, (s, setting) => value);
            var obsoletes = _localitySettings.RdpLocalities.Where(x => x.Value.LastUpdateTime < DateTime.Now.AddDays(-30)).Select(x => x.Key).ToArray();
            foreach (var obsolete in obsoletes)
            {
                _localitySettings.RdpLocalities.TryRemove(obsolete, out _);
            }

            Save();
        }

        #endregion

        public void QuickConnectionHistoryAdd(QuickConnectionItem item)
        {
            var old = _localitySettings.QuickConnectionHistory.FirstOrDefault(x => x.Host == item.Host && x.Protocol == item.Protocol);
            if (old != null)
                _localitySettings.QuickConnectionHistory.Remove(old);
            _localitySettings.QuickConnectionHistory.Insert(0, item);
            if (_localitySettings.QuickConnectionHistory.Count > 50)
            {
                _localitySettings.QuickConnectionHistory.RemoveRange(50, _localitySettings.QuickConnectionHistory.Count - 50);
            }

            Save();
        }


        public void QuickConnectionHistoryRemove(QuickConnectionItem item)
        {
            var old = _localitySettings.QuickConnectionHistory.FirstOrDefault(x => x.Host == item.Host && x.Protocol == item.Protocol);
            if (old != null)
                _localitySettings.QuickConnectionHistory.Remove(old);
            if (_localitySettings.QuickConnectionHistory.Count > 50)
            {
                _localitySettings.QuickConnectionHistory.RemoveRange(50, _localitySettings.QuickConnectionHistory.Count - 50);
            }

            Save();
        }

        public void ServerCustomOrderRebuild(IEnumerable<ProtocolBaseViewModel> servers)
        {
            int i = 0;
            _localitySettings.ServerCustomOrder.Clear();
            foreach (var server in servers)
            {
                _localitySettings.ServerCustomOrder.Add(server.Id, i);
                ++i;
            }

            Save();
        }

        public void ServerGroupedOrderRebuild(string[] groupNames)
        {
            int i = 0;
            _localitySettings.ServerGroupedOrder.Clear();
            foreach (var str in groupNames.Distinct())
            {
                _localitySettings.ServerGroupedOrder.Add(str, i);
                ++i;
            }
            Save();
        }

        public void ServerGroupedSetIsExpanded(string name, bool isExpanded)
        {
            try
            {
                if (ServerGroupedIsExpanded.ContainsKey(name))
                    ServerGroupedIsExpanded[name] = isExpanded;
                else
                    ServerGroupedIsExpanded.Add(name, isExpanded);
                var ds = IoC.TryGet<DataSourceService>();
                if (ds != null)
                {
                    foreach (var key in ServerGroupedIsExpanded.Keys.ToArray())
                    {
                        if (ds.LocalDataSource?.Name != key && ds.AdditionalSources.All(x => x.Key != key))
                        {
                            ServerGroupedIsExpanded.Remove(key);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MsAppCenterHelper.Error(e);
                _localitySettings.ServerGroupedIsExpanded = new Dictionary<string, bool>();
            }
            Save();
        }

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

                RetryHelper.Try(() => { File.WriteAllText(AppPathHelper.Instance.LocalityJsonPath, JsonConvert.SerializeObject(this._localitySettings, Formatting.Indented), Encoding.UTF8); },
                    actionOnError: exception => MsAppCenterHelper.Error(exception));
                CanSave = true;
            }
        }

        /// <summary>
        /// Load
        /// </summary>
        public LocalityService()
        {
            // Load
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

        #endregion Interface
    }
}