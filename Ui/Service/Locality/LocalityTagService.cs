﻿using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
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
                    actionOnError: exception => SentryIoHelper.Error(exception));
                CanSave = true;
            }
        }

        public static Tag? GetTag(string tagName)
        {
            Load();
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

        public static bool IsPinned(string key)
        {
            Load();
            return _settings.TagDict.TryGetValue(key.ToLower(), out var tag) && tag.IsPinned;
        }
    }
}
