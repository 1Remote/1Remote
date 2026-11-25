using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Shawn.Utils;
using _1RM.Service.DataSource;
using _1RM.Utils.Tracing;
using _1RM.View;

namespace _1RM.Service.Locality
{
    public class LocalityListViewSettings
    {
        public EnumServerOrderBy ServerOrderBy = EnumServerOrderBy.IdAsc;
        public Dictionary<string, int> ServerCustomOrder = new Dictionary<string, int>();
        public Dictionary<string, int> ConnectionOrder = new Dictionary<string, int>();
        public static string ConnectionKey(string source, string id) => $"{source.Length}:{source}{id}";

        public int GetConnectionOrder(string source, string id)
        {
            if (ConnectionOrder.TryGetValue(ConnectionKey(source, id), out int order)) return order;
            return ServerCustomOrder.GetValueOrDefault(id, int.MaxValue);
        }

        public Dictionary<string, int> MergeVisibleOrder(IEnumerable<string> loadedKeys, IReadOnlyList<string> visibleKeys)
        {
            // Preserve offline and filtered entries. Only replace the slots occupied by visible rows.
            var full = ConnectionOrder.OrderBy(pair => pair.Value).Select(pair => pair.Key)
                .Concat(loadedKeys).Distinct(StringComparer.Ordinal);
            var visible = new HashSet<string>(visibleKeys, StringComparer.Ordinal);
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            int next = 0;
            foreach (var key in full)
                result.Add(visible.Contains(key) ? visibleKeys[next++] : key, result.Count);
            return result;
        }

        public Dictionary<string, int> GroupedOrder = new Dictionary<string, int>();
        public Dictionary<string, bool> GroupedIsExpanded = new Dictionary<string, bool>();
        public double ServerListNameWidth = 300;
        public double ServerListNoteWidth = 100;
    }

    public static class LocalityListViewService
    {
        public static string JsonPath => Path.Combine(AppPathHelper.Instance.LocalityDirPath, ".list_view.json");
        private static LocalityListViewSettings? _settings;
        public static LocalityListViewSettings Settings
        {
            get
            {
                if (_settings == null)
                {
                    Load();
                }
                return _settings!;
            }
            private set => _settings = value;
        }


        public static void Load()
        {
            if (!File.Exists(JsonPath))
                _settings = new LocalityListViewSettings();
            try
            {
                var tmp = JsonConvert.DeserializeObject<LocalityListViewSettings>(File.ReadAllText(JsonPath));
                tmp ??= new LocalityListViewSettings();
                _settings = tmp;
            }
            catch
            {
                _settings = new LocalityListViewSettings();
            }
        }

        public static void Save()
        {
            RetryHelper.Try(WriteSettings, actionOnError: exception => UnifyTracing.Error(exception));
        }

        private static void WriteSettings()
        {
            AppPathHelper.CreateDirIfNotExist(AppPathHelper.Instance.LocalityDirPath, false);
            var temp = JsonPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temp, JsonConvert.SerializeObject(Settings, Formatting.Indented), Encoding.UTF8);
                if (File.Exists(JsonPath)) File.Replace(temp, JsonPath, null);
                else File.Move(temp, JsonPath);
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }

        public static void ServerOrderBySet(EnumServerOrderBy value)
        {
            if (Settings.ServerOrderBy == value) return;
            Settings.ServerOrderBy = value;
            Save();
        }




        public static void ServerCustomOrderSave(IEnumerable<ProtocolBaseViewModel> rows, IReadOnlyList<string> visible)
        {
            var all = rows.Where(row => row is not ProtocolBaseViewModelDummy)
                .OrderBy(row => row.CustomOrder).ThenBy(row => row.OrderKey, StringComparer.Ordinal).ToList();
            var before = Settings.ConnectionOrder;
            var previousMode = Settings.ServerOrderBy;
            Settings.ConnectionOrder = Settings.MergeVisibleOrder(all.Select(row => row.OrderKey), visible);
            Settings.ServerOrderBy = EnumServerOrderBy.Custom;
            try { WriteSettings(); }
            catch
            {
                Settings.ConnectionOrder = before;
                Settings.ServerOrderBy = previousMode;
                throw;
            }
            foreach (var row in all) row.CustomOrder = Settings.ConnectionOrder[row.OrderKey];
        }

        public static int GroupedOrderGet(string dataSourceName)
        {
            return Settings.GroupedOrder.GetValueOrDefault(dataSourceName, int.MaxValue);
        }

        public static void GroupedOrderSave(IEnumerable<string> dataSourceNames)
        {
            int i = 0;
            Settings.GroupedOrder.Clear();
            foreach (var str in dataSourceNames.Distinct())
            {
                Settings.GroupedOrder.Add(str, i);
                ++i;
            }
            Save();
        }


        public static bool GroupedIsExpandedGet(string dataSourceName)
        {
            return Settings.GroupedIsExpanded.GetValueOrDefault(dataSourceName, true);
        }
        public static void GroupedIsExpandedSet(string dataSourceName, bool isExpanded)
        {
            Load();
            try
            {
                Settings.GroupedIsExpanded[dataSourceName] = isExpanded;
                var ds = IoC.TryGet<DataSourceService>();
                if (ds != null)
                {
                    foreach (var key in Settings.GroupedIsExpanded.Keys.ToArray())
                    {
                        if (ds.LocalDataSource?.Name != key && ds.AdditionalSources.All(x => x.Key != key))
                        {
                            Settings.GroupedIsExpanded.Remove(key);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                UnifyTracing.Error(e);
                Settings.GroupedIsExpanded = new Dictionary<string, bool>();
            }
            Save();
        }

        public static void ServerListNameWidthSet(double value)
        {
            if (Math.Abs(Settings.ServerListNameWidth - value) < 0.1) return;
            Settings.ServerListNameWidth = value;
            Save();
        }

        public static void ServerListNoteWidthSet(double value)
        {
            if (Math.Abs(Settings.ServerListNoteWidth - value) < 0.1) return;
            Settings.ServerListNoteWidth = value;
            Save();
        }
    }
}
