using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Shawn.Utils;
using _1RM.Model;
using _1RM.Utils.Tracing;

namespace _1RM.Service.Locality
{
    public class LocalityTagSettings
    {
        public ConcurrentDictionary<string, Tag> TagDict = new ConcurrentDictionary<string, Tag>();
    }


    public static class LocalityTagService
    {
        public static string JsonPath => Path.Combine(AppPathHelper.Instance.LocalityDirPath, ".tags.json");
        private static LocalityTagSettings _settings = new LocalityTagSettings();

        private static bool _isLoaded = false;
        public static void Load()
        {
            lock (_settings)
            {
                if (_isLoaded) return;
                _isLoaded = true;
                try
                {
                    var tmp = JsonConvert.DeserializeObject<LocalityTagSettings>(File.ReadAllText(JsonPath, Encoding.UTF8));
                    tmp ??= new LocalityTagSettings();
                    _settings = tmp;
                }
                catch
                {
                    _settings = new LocalityTagSettings();
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
                    actionOnError: exception => UnifyTracing.Error(exception));
                CanSave = true;
            }
        }

        public static Tag? GetTag(string tagName, bool load = true)
        {
            if (load)
            {
                Load();
            }
            _settings.TagDict.TryGetValue(tagName.ToLower(), out var ret);
            return ret;
        }

        public static Tag? GetAndRemoveTag(string tagName)
        {
            Load();
            _settings.TagDict.TryRemove(tagName.ToLower(), out var ret);
            Save();
            return ret;
        }

        public static bool IsFirstTimeUse()
        {
            return !File.Exists(JsonPath);
        }

        public static void UpdateTag(Tag tag)
        {
            Load();
            _settings.TagDict.AddOrUpdate(tag.Name.ToLower(), tag, (_, _) => tag);
            Save();
        }

        public static void UpdateTags(IEnumerable<Tag> tags)
        {
            Load();
            int i = 0;
            foreach (var tag in tags)
            {
                tag.CustomOrder = i++;
                _settings.TagDict.AddOrUpdate(tag.Name.ToLower(), tag, (_, _) => tag);
            }
            Save();
        }

        public static ConcurrentDictionary<string, Tag> TagDict
        {
            get
            {
                Load();
                return _settings.TagDict;
            }
        }

        public static bool GetIsPinned(string key, bool load = false)
        {
            if (load)
                Load();
            return _settings.TagDict.TryGetValue(key.ToLower(), out var tag) && tag.IsPinned;
        }

        public static int GetCustomOrder(string key, bool load = false)
        {
            if (load)
                Load();
            return _settings.TagDict.TryGetValue(key.ToLower(), out var tag) ? tag.CustomOrder : 0;
        }
    }
}
