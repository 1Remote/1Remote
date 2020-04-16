using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Shawn.Ulits;

namespace PRM.Core.Model
{
    public sealed class SystemConfigLanguage : SystemConfigBase
    {
        public SystemConfigLanguage(ResourceDictionary appResourceDictionary, Ini ini) : base(ini)
        {
            Debug.Assert(appResourceDictionary != null);
            AppResourceDictionary = appResourceDictionary;
            InitLanguageCode2Name();
            Load();
        }


        public const string DefaultLanguage = "en-us";
        public const string LanguageJsonDir = "Languages";
        public readonly ResourceDictionary AppResourceDictionary;

        private ResourceDictionary _lastMainResourceDictionary = null;

        private string _currentLanguageCode = "en-us";
        public string CurrentLanguageCode
        {
            get => _currentLanguageCode;
            set
            {
                var newVal = value?.Trim()?.ToLower();
                if (!string.IsNullOrEmpty(newVal)
                    && LanguageCode2Name.ContainsKey(newVal))
                {
                    SetAndNotifyIfChanged(nameof(CurrentLanguageCode), ref _currentLanguageCode, newVal);
                    // reload ResourceDictionary
                    _currentLanguageResourceDictionary = null;
                }
                var tmp = CurrentLanguageResourceDictionary;
                Debug.Assert(tmp != null);
                RaisePropertyChanged(nameof(CurrentLanguageResourceDictionary));
                AppResourceDictionary?.ChangeLanguage(CurrentLanguageResourceDictionary);// 修改语言配置
            }
        }


        private ResourceDictionary _defaultLanguageResourceDictionary = null;
        private ResourceDictionary DefaultLanguageResourceDictionary
        {
            get
            {
                if (_defaultLanguageResourceDictionary == null)
                {
                    _defaultLanguageResourceDictionary = GetResourceDictionaryByCode(DefaultLanguage);
                    Debug.Assert(_defaultLanguageResourceDictionary != null);
                }
                return _defaultLanguageResourceDictionary;
            }
        }


        private ResourceDictionary _currentLanguageResourceDictionary = null;
        private ResourceDictionary CurrentLanguageResourceDictionary
        {
            get
            {
                if (_currentLanguageResourceDictionary == null)
                {
                    _currentLanguageResourceDictionary = GetResourceDictionaryByCode(_currentLanguageCode);
                    if (_currentLanguageResourceDictionary != null)
                    {
                        // add lost key from default language
                        foreach (var key in DefaultLanguageResourceDictionary.Keys)
                        {
                            if (!_currentLanguageResourceDictionary.Contains(key))
                                _currentLanguageResourceDictionary.Add(key, DefaultLanguageResourceDictionary[key]);
                        }
                    }
                    else
                    {
                        // use default
                        _currentLanguageCode = DefaultLanguage;
                        _currentLanguageResourceDictionary = _defaultLanguageResourceDictionary;
                    }

                }
                return _currentLanguageResourceDictionary;
            }
        }


        private Dictionary<string,string> _languageCode2Name = new Dictionary<string,string>();
        /// <summary>
        /// code = language file name, all in small cases, ref https://en.wikipedia.org/wiki/Language_code
        /// </summary>
        public Dictionary<string, string> LanguageCode2Name
        {
            get => _languageCode2Name;
            private set => SetAndNotifyIfChanged(nameof(LanguageCode2Name), ref _languageCode2Name, value);
        }

        private readonly Dictionary<string, string> _languageCode2ResourcePath = new Dictionary<string, string>();


        public void InitLanguageCode2Name()
        {
            LanguageCode2Name.Clear();
            _languageCode2ResourcePath.Clear();

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
                                var code = fi.Name.Replace(fi.Extension, "");
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
                var path = "pack://application:,,,/PRM.Core;component/Languages/zh-cn.xaml";
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

        private void AddOrUpdateLanguage(string code, string name, string path)
        {
            LanguageCode2Name.Add(code, name);
            _languageCode2ResourcePath.Add(code, path);
        }

        public string GetText(string textKey)
        {
            if (CurrentLanguageResourceDictionary.Contains(textKey))
                return CurrentLanguageResourceDictionary[textKey].ToString();
            if (DefaultLanguageResourceDictionary.Contains(textKey))
                return DefaultLanguageResourceDictionary[textKey].ToString();

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
            CurrentLanguageCode = _ini.GetValue("lang", _sectionName, CurrentLanguageCode);
        }

        public override void Update(SystemConfigBase newConfig)
        {
            UpdateBase(this, newConfig, typeof(SystemConfigLanguage));
        }

        #endregion
    }
}
