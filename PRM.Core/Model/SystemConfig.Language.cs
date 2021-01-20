using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Shawn.Utils;

namespace PRM.Core.Model
{
    public sealed class SystemConfigLanguage : SystemConfigBase
    {
        public SystemConfigLanguage(ResourceDictionary appResourceDictionary, Ini ini) : base(ini)
        {
            Debug.Assert(appResourceDictionary != null);
            var appDateFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), SystemConfig.AppName);
            LanguageJsonDir = Path.Combine(appDateFolder, "Languages");
#if DEV
            if (!Directory.Exists(LanguageJsonDir))
                Directory.CreateDirectory(LanguageJsonDir);
            var zh_cn_json = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/PRM.Core;component/Languages/zh-cn.json")).Stream;
            using (var fileStream = File.Create(Path.Combine(LanguageJsonDir, "zh-cn.json")))
            {
                zh_cn_json.Seek(0, SeekOrigin.Begin);
                zh_cn_json.CopyTo(fileStream);
            }
            zh_cn_json.Close();
            var en_us_json = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/PRM.Core;component/Languages/en-us.json")).Stream;
            using (var fileStream = File.Create(Path.Combine(LanguageJsonDir, "en-us.json")))
            {
                en_us_json.Seek(0, SeekOrigin.Begin);
                en_us_json.CopyTo(fileStream);
            }
            en_us_json.Close();
#endif
            AppResourceDictionary = appResourceDictionary;
            InitLanguageCode2Name();
            _defaultLanguageResourceDictionary = GetResourceDictionaryByCode(DefaultLanguageCode);
            Load();
        }
        public const string DefaultLanguageCode = "en-us";
        public readonly string LanguageJsonDir;
        public readonly ResourceDictionary AppResourceDictionary = null;

        private string _currentLanguageCode = "en-us";
        public string CurrentLanguageCode
        {
            get => _currentLanguageCode;
            set
            {
                var newVal = value?.Trim()?.ToLower();
                if (string.IsNullOrEmpty(newVal)
                    || !LanguageCode2Name.ContainsKey(newVal))
                    newVal = DefaultLanguageCode;

                if (_currentLanguageCode != newVal || _currentLanguageResourceDictionary == null)
                {
                    SetAndNotifyIfChanged(nameof(CurrentLanguageCode), ref _currentLanguageCode, newVal);
                    // reload ResourceDictionary
                    _currentLanguageResourceDictionary = GetResourceDictionaryByCode(_currentLanguageCode);
                    if (_currentLanguageResourceDictionary == null)
                    {
                        // use default
                        _currentLanguageCode = DefaultLanguageCode;
                        _currentLanguageResourceDictionary = _defaultLanguageResourceDictionary;
                    }
                    AppResourceDictionary?.ChangeLanguage(_currentLanguageResourceDictionary);
                    GlobalEventHelper.OnLanguageChanged?.Invoke();
                }
            }
        }

        private ResourceDictionary _defaultLanguageResourceDictionary { get; set; } = null;
        private ResourceDictionary _currentLanguageResourceDictionary { get; set; } = null;


        private Dictionary<string, string> _languageCode2Name = new Dictionary<string, string>();
        /// <summary>
        /// code => language file name, all codes leave in small cases, ref https://en.wikipedia.org/wiki/Language_code
        /// </summary>
        public Dictionary<string, string> LanguageCode2Name
        {
            get => _languageCode2Name;
            private set => SetAndNotifyIfChanged(nameof(LanguageCode2Name), ref _languageCode2Name, value);
        }

        private readonly Dictionary<string, string> _languageCode2ResourcePath = new Dictionary<string, string>();


        private void InitLanguageCode2Name()
        {
            LanguageCode2Name.Clear();
            _languageCode2ResourcePath.Clear();
#if DEV
            if (Directory.Exists("Languages"))
            {
                var di = new DirectoryInfo("Languages");
                var fis = di.GetFiles("*.json");

                // add dynamic language resources
                foreach (var fi in fis)
                {
                    try
                    {
                        var resourceDictionary = MultiLangHelper.LangDictFromJsonFile(fi.FullName);
                        if (resourceDictionary != null)
                        {
                            if (resourceDictionary.Contains("language_name"))
                            {
                                var code = fi.Name.ReplaceLast(fi.Extension, "");
                                AddOrUpdateLanguage(code, resourceDictionary["language_name"].ToString(), fi.FullName);
                            }
                        }
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                }
            }
#endif
            if (Directory.Exists(LanguageJsonDir))
            {
                var di = new DirectoryInfo(LanguageJsonDir);
                var fis = di.GetFiles("*.json");

                // add dynamic language resources
                foreach (var fi in fis)
                {
                    try
                    {
                        var resourceDictionary = MultiLangHelper.LangDictFromJsonFile(fi.FullName);
                        if (resourceDictionary != null)
                        {
                            if (resourceDictionary.Contains("language_name"))
                            {
                                var code = fi.Name.ReplaceLast(fi.Extension, "");
                                AddOrUpdateLanguage(code, resourceDictionary["language_name"].ToString(), fi.FullName);
                            }
                        }
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                }
            }
            else
            {
                Directory.CreateDirectory(LanguageJsonDir);
            }

            // add static language resources
            {
                var code = "zh-cn";
                var path = "pack://application:,,,/PRM.Core;component/Languages/zh-cn.xaml";
                if (!LanguageCode2Name.ContainsKey(code))
                {
                    var r = GetResourceDictionaryByPath(path);
                    Debug.Assert(r != null);
                    Debug.Assert(r.Contains("language_name"));
                    AddOrUpdateLanguage(code, r["language_name"].ToString(), path);
                }
            }
            {
                var code = "en-us";
                var path = "pack://application:,,,/PRM.Core;component/Languages/en-us.xaml";
                if (!LanguageCode2Name.ContainsKey(code))
                {
                    var r = GetResourceDictionaryByPath(path);
                    Debug.Assert(r != null);
                    Debug.Assert(r.Contains("language_name"));
                    AddOrUpdateLanguage(code, r["language_name"].ToString(), path);
                }
            }
        }


        /// <summary>
        /// may return null
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        private ResourceDictionary GetResourceDictionaryByPath(string path)
        {
            if (path.ToLower().EndsWith(".xaml"))
            {
                try
                {
                    var resourceDictionary = MultiLangHelper.LangDictFromXamlUri(new Uri(path));
                    if (resourceDictionary != null)
                    {
                        return resourceDictionary;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
            else if (path.ToLower().EndsWith(".json") && File.Exists(path))
            {
                var resourceDictionary = MultiLangHelper.LangDictFromJsonFile(path);
                if (resourceDictionary != null)
                {
                    return resourceDictionary;
                }
            }
            return null;
        }

        /// <summary>
        /// may return null
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        private ResourceDictionary GetResourceDictionaryByCode(string code)
        {
            if (LanguageCode2Name.ContainsKey(code)
                && _languageCode2ResourcePath.ContainsKey(code))
            {
                var path = _languageCode2ResourcePath[code];
                var ret = GetResourceDictionaryByPath(path);
                if (ret != null)
                {
                    return ret;
                }
                LanguageCode2Name.Remove(code);
                _languageCode2ResourcePath.Remove(code);
            }
            return null;
        }

        public void AddOrUpdateLanguage(string code, string name, string path)
        {
            if (LanguageCode2Name.ContainsKey(code))
            {
                LanguageCode2Name[code] = name;
                _languageCode2ResourcePath[code] = path;
            }
            else
            {
                LanguageCode2Name.Add(code, name);
                _languageCode2ResourcePath.Add(code, path);
            }
        }

        public string GetText(string textKey)
        {
            Debug.Assert(_currentLanguageResourceDictionary != null);
            Debug.Assert(_defaultLanguageResourceDictionary != null);
            if (_currentLanguageResourceDictionary.Contains(textKey))
                return _currentLanguageResourceDictionary[textKey].ToString();
            if (_defaultLanguageResourceDictionary.Contains(textKey))
                return _defaultLanguageResourceDictionary[textKey].ToString();

            throw new NotImplementedException("can't find any string by '" + textKey + "'!");
        }





        #region Interface
        private const string _sectionName = "General";
        public override void Save()
        {
            _ini.WriteValue("lang", _sectionName, CurrentLanguageCode);
            _ini.Save();
        }

        public override void Load()
        {
            StopAutoSave = true;
            CurrentLanguageCode = _ini.GetValue("lang", _sectionName, DefaultLanguageCode);
            StopAutoSave = false;
        }

        public override void Update(SystemConfigBase newConfig)
        {
            UpdateBase(this, newConfig, typeof(SystemConfigLanguage));
        }

        #endregion
    }
}
