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

namespace _1RM.Service.Locality
{
    public class LocalityTagSettings
    {
        public Dictionary<string, Tag> TagDict = new Dictionary<string, Tag>();
    }


    public static class LocalityTagService
    {
        public static string JsonPath => Path.Combine(AppPathHelper.Instance.LocalityDirPath, ".tags.json");
        private static LocalityTagSettings _settings = new LocalityTagSettings();

        private static bool _isLoaded = false;
        private static void Load()
        {
            lock (_settings)
            {
                if (_isLoaded) return;
                _isLoaded = true;
                try
                {
                    var tmp = JsonConvert.DeserializeObject<LocalityTagSettings>(File.ReadAllText(JsonPath));
                    tmp ??= new LocalityTagSettings();
                    _settings = tmp;
                }
                catch
                {
                    _settings = new LocalityTagSettings();
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



        public static void UpdateTags(IEnumerable<Tag> tags)
        {
            Load();
            foreach (var tag in tags)
            {
                var key = tag.Name.ToLower();
                if (_settings.TagDict.ContainsKey(key))
                {
                    _settings.TagDict[key] = tag;
                }
                else
                {
                    _settings.TagDict.Add(key, tag);
                }
            }
            Save();
        }

        public static Dictionary<string, Tag> TagDict
        {
            get
            {
                Load();
                return _settings.TagDict;
            }
        }
    }
}
