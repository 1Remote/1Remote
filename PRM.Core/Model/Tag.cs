using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Shawn.Utils;

namespace PRM.Core.Model
{
    public class Tag : NotifyPropertyChangedBase
    {
        public Tag(string name, bool isPinned)
        {
            _name = name;
            _isPinned = isPinned;
        }

        private string _name = "";
        public string Name
        {
            get => _name;
            set => SetAndNotifyIfChanged(ref _name, value);
        }


        private int _itemsCount = 0;
        public int ItemsCount
        {
            get => _itemsCount;
            set => SetAndNotifyIfChanged(ref _itemsCount, value);
        }


        private bool _isPinned = false;
        public bool IsPinned
        {
            get => _isPinned;
            set
            {
                SetAndNotifyIfChanged(ref _isPinned, value);
                UpdateTagsCache(this);
            }
        }


        private const string PinnedTagsPath = "PinnedTags.json";
        public static Dictionary<string, bool> GetPinnedTags()
        {
            try
            {
                if (File.Exists(PinnedTagsPath))
                {
                    var json = File.ReadAllText(PinnedTagsPath, Encoding.UTF8);
                    return JsonConvert.DeserializeObject<Dictionary<string, bool>>(json);
                }
            }
            catch (Exception e)
            {
                SimpleLogHelper.Warning(e);
            }

            return new Dictionary<string, bool>();
        }

        public static void UpdateTagsCache(Tag changedTag)
        {
            Debug.Assert(changedTag != null);
            var tags = GetPinnedTags();
            tags.TryGetValue(changedTag.Name, out var isPinned);
            if (changedTag.IsPinned == true && isPinned == false)
            {
                tags.Remove(changedTag.Name);
                tags.Add(changedTag.Name, true);
                File.WriteAllText(PinnedTagsPath, JsonConvert.SerializeObject(tags), Encoding.UTF8);
            }
            if (changedTag.IsPinned == false && isPinned == true)
            {
                tags.Remove(changedTag.Name);
                tags.Add(changedTag.Name, false);
                File.WriteAllText(PinnedTagsPath, JsonConvert.SerializeObject(tags), Encoding.UTF8);
            }
        }

        public static void UpdateTagsCache(IEnumerable<Tag> tags)
        {
            var dict = new Dictionary<string, bool>();
            foreach (var tag in tags)
            {
                dict.Add(tag.Name, tag.IsPinned);
            }
            File.WriteAllText(PinnedTagsPath, JsonConvert.SerializeObject(dict), Encoding.UTF8);
        }
    }
}
