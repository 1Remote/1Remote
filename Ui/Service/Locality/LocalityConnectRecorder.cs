using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using _1RM.Model;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Utils;
using _1RM.View;
using _1RM.View.Launcher;
using Newtonsoft.Json;
using Shawn.Utils;

namespace _1RM.Service.Locality
{
    public class LocalityConnectRecorderSettings
    {
        /// <summary>
        /// keep the last connect time of each server, in order to sort the server list
        /// </summary>
        public ConcurrentDictionary<string, long> ConnectRecodes = new ConcurrentDictionary<string, long>();
        public List<QuickConnectionItem> QuickConnectionHistory = new List<QuickConnectionItem>();
        public ConcurrentDictionary<string, RdpLocalSetting> RdpCaches = new ConcurrentDictionary<string, RdpLocalSetting>();
    }

    public static class LocalityConnectRecorder
    {
        public static string JsonPath => Path.Combine(AppPathHelper.Instance.LocalityDirPath, ".connection_records.json");
        private static LocalityConnectRecorderSettings _settings = new LocalityConnectRecorderSettings();
        private static bool _isLoaded = false;
        private static void Load()
        {
            lock (_settings)
            {
                if (_isLoaded) return;
                _isLoaded = true;
                try
                {
                    var tmp = JsonConvert.DeserializeObject<LocalityConnectRecorderSettings>(File.ReadAllText(JsonPath));
                    if (tmp != null)
                        _settings = tmp;
                }
                catch
                {
                    _settings = new LocalityConnectRecorderSettings();
                }
            }
        }

        public static bool CanSave { get; private set; } = true;
        private static void Save()
        {
            if (!CanSave) return;
            lock (_settings)
            {
                if (!CanSave) return;
                CanSave = false;
                AppPathHelper.CreateDirIfNotExist(AppPathHelper.Instance.LocalityDirPath, false);
                RetryHelper.Try(() => { File.WriteAllText(JsonPath, JsonConvert.SerializeObject(_settings, Formatting.Indented), Encoding.UTF8); },
                    actionOnError: exception => MsAppCenterHelper.Error(exception));
                CanSave = true;
            }
        }


        #region ConnectTime
        /// <summary>
        /// Update the connect time of id
        /// </summary>
        public static void UpdateConnectTime(this ProtocolBaseViewModel vmServer, long time = 0)
        {
            Load();
            //var serverId = $"{server.Id}_From{server.DataSourceName}";
            if (time == 0)
                time = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            _settings.ConnectRecodes.AddOrUpdate(vmServer.Id, time, (s, l) => time);
            Save();
            vmServer.LastConnectTime = Get(vmServer.Server);
        }

        public static DateTime Get(ProtocolBase server)
        {
            Load();
            if (_settings.ConnectRecodes.TryGetValue(server.Id, out var ut))
            {
                DateTimeOffset dto = DateTimeOffset.FromUnixTimeMilliseconds(ut);
                return dto.LocalDateTime;
            }
            return DateTime.MinValue;
        }


        public static void Cleanup(IEnumerable<string>? existedIds = null)
        {
            Load();
            if (existedIds != null)
            {
                var junks = _settings.ConnectRecodes.Where(x => existedIds.All(y => y != x.Key));
                foreach (var junk in junks)
                {
                    _settings.ConnectRecodes.TryRemove(junk.Key, out _);
                }
            }
            if (_settings.ConnectRecodes.Count > 100)
            {
                var ordered = _settings.ConnectRecodes.OrderByDescending(x => x.Value).ToList();
                var junks = ordered.Where(x => x.Value > ordered[100].Value);
                foreach (var junk in junks)
                {
                    _settings.ConnectRecodes.TryRemove(junk.Key, out _);
                }
            }
        }
        #endregion


        #region QuickConnectionHistory

        public static List<QuickConnectionItem> QuickConnectionHistoryGet() => new List<QuickConnectionItem>(_settings.QuickConnectionHistory);
        public static void QuickConnectionHistoryAdd(QuickConnectionItem item)
        {
            Load();
            var old = _settings.QuickConnectionHistory.FirstOrDefault(x => x.Host == item.Host && x.Protocol == item.Protocol);
            if (old != null)
                _settings.QuickConnectionHistory.Remove(old);
            _settings.QuickConnectionHistory.Insert(0, item);
            if (_settings.QuickConnectionHistory.Count > 50)
            {
                _settings.QuickConnectionHistory.RemoveRange(50, _settings.QuickConnectionHistory.Count - 50);
            }

            Save();
        }
        public static void QuickConnectionHistoryRemove(QuickConnectionItem item)
        {
            Load();
            var old = _settings.QuickConnectionHistory.FirstOrDefault(x => x.Host == item.Host && x.Protocol == item.Protocol);
            if (old != null)
                _settings.QuickConnectionHistory.Remove(old);
            if (_settings.QuickConnectionHistory.Count > 50)
            {
                _settings.QuickConnectionHistory.RemoveRange(50, _settings.QuickConnectionHistory.Count - 50);
            }
            Save();
        }
        #endregion


        #region RDP caches for last session is full screen

        public static RdpLocalSetting? RdpCacheGet(string key)
        {
            Load();
            return _settings.RdpCaches.TryGetValue(key, out var v) ? v : null;
        }


        public static void RdpCacheUpdate(string id, bool isFullScreen, int fullScreenIndex = -1)
        {
            if(ProtocolBase.IsTmpSession(id)) return;
            Load();
            var value = new RdpLocalSetting()
            {
                LastUpdateTime = DateTime.Now,
                FullScreenLastSessionIsFullScreen = isFullScreen,
                FullScreenLastSessionScreenIndex = isFullScreen ? fullScreenIndex : -1,
            };
            _settings.RdpCaches.AddOrUpdate(id, value, (s, setting) => value);
            var obsoletes = _settings.RdpCaches.Where(x => x.Value.LastUpdateTime < DateTime.Now.AddDays(-30)).Select(x => x.Key).ToArray();
            foreach (var obsolete in obsoletes)
            {
                _settings.RdpCaches.TryRemove(obsolete, out _);
            }

            Save();
        }

        #endregion
    }
}
