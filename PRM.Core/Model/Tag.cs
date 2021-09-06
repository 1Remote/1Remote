using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
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

        private Visibility _objectVisibilityInList = Visibility.Visible;
        [JsonIgnore]
        public Visibility ObjectVisibilityInList
        {
            get => _objectVisibilityInList;
            set => SetAndNotifyIfChanged(nameof(ObjectVisibilityInList), ref _objectVisibilityInList, value);
        }

        private const string PinnedTagsFileName = "PinnedTags.json";
        private static string GetPinnedTagsPath()
        {
            var appDateFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), SystemConfig.AppName);
            var path = Path.Combine(Environment.CurrentDirectory, PinnedTagsFileName);
            if (IOPermissionHelper.IsFileCanWriteNow(path) == false)
            {
                path = Path.Combine(appDateFolder, PinnedTagsFileName);
            }
#if FOR_MICROSOFT_STORE_ONLY
            path = Path.Combine(appDateFolder, PinnedTagsFileName);
#endif
            return path;
        }
        public static Dictionary<string, bool> GetPinnedTags()
        {
            var pinnedTagsPath = GetPinnedTagsPath();
            try
            {
                if (File.Exists(pinnedTagsPath))
                {
                    var json = File.ReadAllText(pinnedTagsPath, Encoding.UTF8);
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
            var pinnedTagsPath = GetPinnedTagsPath();
            Debug.Assert(changedTag != null);
            var tags = GetPinnedTags();
            tags.TryGetValue(changedTag.Name, out var isPinned);
            if (changedTag.IsPinned == true && isPinned == false)
            {
                tags.Remove(changedTag.Name);
                tags.Add(changedTag.Name, true);
                File.WriteAllText(pinnedTagsPath, JsonConvert.SerializeObject(tags), Encoding.UTF8);
            }
            if (changedTag.IsPinned == false && isPinned == true)
            {
                tags.Remove(changedTag.Name);
                tags.Add(changedTag.Name, false);
                File.WriteAllText(pinnedTagsPath, JsonConvert.SerializeObject(tags), Encoding.UTF8);
            }
        }

        public static void UpdateTagsCache(IEnumerable<Tag> tags)
        {
            var pinnedTagsPath = GetPinnedTagsPath();
            var dict = new Dictionary<string, bool>();
            foreach (var tag in tags)
            {
                dict.Add(tag.Name, tag.IsPinned);
            }
            File.WriteAllText(pinnedTagsPath, JsonConvert.SerializeObject(dict), Encoding.UTF8);
        }
    }
}
