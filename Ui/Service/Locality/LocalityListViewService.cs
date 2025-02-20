using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using _1RM.Utils;
using Shawn.Utils;
using _1RM.Model;
using System.Diagnostics;
using _1RM.Service.DataSource;
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
        private static LocalityListViewSettings _settings = new LocalityListViewSettings();

        private static bool _isLoaded = false;
        private static void Load()
        {
            lock (_settings)
            {
                if (_isLoaded) return;
                _isLoaded = true;
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
        }

        public static bool CanSave { get; private set; }= true;
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

        public static EnumServerOrderBy ServerOrderByGet()
        {
            Load();
            return _settings.ServerOrderBy;
        }

        public static void ServerOrderBySet(EnumServerOrderBy value)
        {
            Load();
            if (_settings.ServerOrderBy == value) return;
            _settings.ServerOrderBy = value;
            Save();
        }




        public static int ServerCustomOrderGet(string id)
        {
            Load();
            return _settings.ServerCustomOrder.ContainsKey(id) ? _settings.ServerCustomOrder[id] : int.MaxValue;
        }

        public static void ServerCustomOrderSave(IEnumerable<ProtocolBaseViewModel> servers)
        {
            Load();
            int i = 0;
            _settings.ServerCustomOrder.Clear();
            foreach (var server in servers)
            {
                _settings.ServerCustomOrder.Add(server.Id, i);
                ++i;
            }
            Save();
        }




        public static int GroupedOrderGet(string dataSourceName)
        {
            Load();
            return _settings.GroupedOrder.ContainsKey(dataSourceName) ? _settings.GroupedOrder[dataSourceName] : int.MaxValue;
        }

        public static void GroupedOrderSave(IEnumerable<string> dataSourceNames)
        {
            Load();
            int i = 0;
            _settings.GroupedOrder.Clear();
            foreach (var str in dataSourceNames.Distinct())
            {
                _settings.GroupedOrder.Add(str, i);
                ++i;
            }
            Save();
        }




        public static bool GroupedIsExpandedGet(string dataSourceName)
        {
            Load();
            if (_settings.GroupedIsExpanded.ContainsKey(dataSourceName))
                return _settings.GroupedIsExpanded[dataSourceName];
            return true;
        }
        public static void GroupedIsExpandedSet(string dataSourceName, bool isExpanded)
        {
            Load();
            try
            {
                if (_settings.GroupedIsExpanded.ContainsKey(dataSourceName))
                    _settings.GroupedIsExpanded[dataSourceName] = isExpanded;
                else
                    _settings.GroupedIsExpanded.Add(dataSourceName, isExpanded);
                var ds = IoC.TryGet<DataSourceService>();
                if (ds != null)
                {
                    foreach (var key in _settings.GroupedIsExpanded.Keys.ToArray())
                    {
                        if (ds.LocalDataSource?.Name != key && ds.AdditionalSources.All(x => x.Key != key))
                        {
                            _settings.GroupedIsExpanded.Remove(key);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MsAppCenterHelper.Error(e);
                _settings.GroupedIsExpanded = new Dictionary<string, bool>();
            }
            Save();
        }

        public static double ServerListNameWidthGet()
        {
            Load();
            return _settings.ServerListNameWidth;
        }

        public static void ServerListNameWidthSet(double value)
        {
            Load();
            if (_settings.ServerListNameWidth == value) return;
            _settings.ServerListNameWidth = value;
            Save();
        }
        public static double ServerListNoteWidthGet()
        {
            Load();
            return _settings.ServerListNoteWidth;
        }

        public static void ServerListNoteWidthSet(double value)
        {
            Load();
            if (_settings.ServerListNoteWidth == value) return;
            _settings.ServerListNoteWidth = value;
            Save();
        }
    }
}
