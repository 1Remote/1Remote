using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using Newtonsoft.Json;
using PRM.Core.Service;
using Shawn.Utils;

namespace PRM.Core.Model
{
    public class Tag : NotifyPropertyChangedBase
    {
        private readonly Action _saveOnPinnedChanged;
        public Tag(string name, bool isPinned, Action saveOnPinnedChanged)
        {
            _name = name;
            _isPinned = isPinned;
            this._saveOnPinnedChanged = saveOnPinnedChanged;
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
                _saveOnPinnedChanged.Invoke();
            }
        }

        private Visibility _objectVisibility = Visibility.Visible;
        [JsonIgnore]
        public Visibility ObjectVisibility
        {
            get => _objectVisibility;
            set => SetAndNotifyIfChanged(ref _objectVisibility, value);
        }

        #region The old code // TODO del after 2022.05.31

        private static string GetJsonPath()
        {
            const string pinnedTagsPath = "PinnedTags.json";
            var appDateFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ConfigurationService.AppName);
            var path = Path.Combine(Environment.CurrentDirectory, pinnedTagsPath);
            if (IOPermissionHelper.IsFileCanWriteNow(path) == false)
            {
                path = Path.Combine(appDateFolder, pinnedTagsPath);
            }
#if FOR_MICROSOFT_STORE_ONLY
            path = Path.Combine(appDateFolder, pinnedTagsPath);
#endif
            return path;
        }

        public static Dictionary<string, bool> GetPinnedTags()
        {
            try
            {
                var path = GetJsonPath();
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path, Encoding.UTF8);
                    File.Delete(path);
                    return JsonConvert.DeserializeObject<Dictionary<string, bool>>(json);
                }
            }
            catch (Exception e)
            {
                SimpleLogHelper.Warning(e);
            }

            return new Dictionary<string, bool>();
        }
        #endregion
    }
}
