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
            AppPathHelper.CreateDirIfNotExist(AppPathHelper.Instance.LocalityDirPath, false);
            RetryHelper.Try(() => { File.WriteAllText(JsonPath, JsonConvert.SerializeObject(Settings, Formatting.Indented), Encoding.UTF8); }, actionOnError: exception => UnifyTracing.Error(exception));
        }

        public static void ServerOrderBySet(EnumServerOrderBy value)
        {
            if (Settings.ServerOrderBy == value) return;
            Settings.ServerOrderBy = value;
            Save();
        }




        public static void ServerCustomOrderSave(IEnumerable<ProtocolBaseViewModel> servers)
        {
            int i = 0;
            Settings.ServerCustomOrder.Clear();
            foreach (var server in servers)
            {
                Settings.ServerCustomOrder.Add(server.Id, i);
                ++i;
            }
            Save();
        }

        public static void ServerCustomOrderSave(List<ProtocolBaseViewModel> servers, List<int> orders)
        {
            if (servers.Count <= 1) return;
            if (servers.Count != orders.Count) return;
            Settings.ServerCustomOrder.Clear();
            for (var i = 0; i < servers.Count; i++)
            {
                var server = servers[i];
                Settings.ServerCustomOrder.Add(server.Id, orders[i]);
            }
            Save();
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
