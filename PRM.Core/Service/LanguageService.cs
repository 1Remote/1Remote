using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using PRM.Core.I;
using PRM.Core.Properties;
using Shawn.Utils;

namespace PRM.Core.Service
{
    public class LanguageService: ILanguageService
    {
        public static LanguageService TmpLanguageService = null;

        private readonly ResourceDictionary _defaultLanguageResourceDictionary;
        private string languageCode = "en-us";
        private readonly ResourceDictionary _applicationResourceDictionary;


        private readonly Dictionary<string, ResourceDictionary> _languageCode2Resources = new Dictionary<string, ResourceDictionary>();

        /// <summary>
        /// code => language file name, all codes leave in small cases, ref https://en.wikipedia.org/wiki/Language_code
        /// </summary>
        public Dictionary<string, string> LanguageCode2Name { get; } = new Dictionary<string, string>();

        public LanguageService(ResourceDictionary applicationResourceDictionary, string languageCode)
        {
            _applicationResourceDictionary = applicationResourceDictionary;

            // add static language resources
            AddStaticLanguageResources("en-us");
            AddStaticLanguageResources("zh-cn");
            AddStaticLanguageResources("de-de");
            AddStaticLanguageResources("fr-fr");
            _defaultLanguageResourceDictionary = _languageCode2Resources["en-us"];

#if DEV
            // check if any field missing in the LanguageResources.
            var en = _languageCode2Resources["en-us"];
            var zh_cn = _languageCode2Resources["zh-cn"];
            var de_de = _languageCode2Resources["de-de"];
            var fr_fr = _languageCode2Resources["fr-fr"];
            Debug.Assert(MultiLangHelper.FindMissingFields(en, zh_cn).Count == 0);
            Debug.Assert(MultiLangHelper.FindMissingFields(en, de_de).Count == 0);
            Debug.Assert(MultiLangHelper.FindMissingFields(en, fr_fr).Count == 0);
#endif

            this.languageCode = _languageCode2Resources.ContainsKey(languageCode) ? languageCode : "en-us";
            SetLanguage(languageCode);
        }

        public void AddXamlLanguageResources(string code, string fullName)
        {
            var resourceDictionary = GetResourceDictionaryByXamlFilePath(fullName);
            if (resourceDictionary?.Contains("language_name") != true) return;
            AddLanguage(code, resourceDictionary["language_name"].ToString(), resourceDictionary);
        }

        private void AddStaticLanguageResources(string code)
        {
            //var path = $"pack://application:,,,/PRM.Core;component/Languages/en_us.xaml";
            var path = $"pack://application:,,,/PRM.Core;component/Languages/{code}.xaml";
            if (LanguageCode2Name.ContainsKey(code)) return;
            var r = GetResourceDictionaryByXamlUri(path);
            Debug.Assert(r != null);
            Debug.Assert(r.Contains("language_name"));
            AddLanguage(code, r["language_name"].ToString(), r);
        }

        [CanBeNull]
        private static ResourceDictionary GetResourceDictionaryByXamlUri(string path)
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
            return null;
        }
        [CanBeNull]
        private static ResourceDictionary GetResourceDictionaryByJsonFilePath(string path)
        {
            Debug.Assert(path.EndsWith(".json", true, CultureInfo.InstalledUICulture));
            try
            {
                var resourceDictionary = MultiLangHelper.LangDictFromJsonFile(path);
                if (resourceDictionary != null)
                {
                    return resourceDictionary;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            return null;
        }

        [CanBeNull]
        private static ResourceDictionary GetResourceDictionaryByXamlFilePath(string path)
        {
            Debug.Assert(path.EndsWith(".xaml", true, CultureInfo.InstalledUICulture));
            try
            {
                var resourceDictionary = MultiLangHelper.LangDictFromXamlFile(path);
                if (resourceDictionary != null)
                {
                    return resourceDictionary;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            return null;
        }


        private void AddLanguage(string code, string name, ResourceDictionary resourceDictionary)
        {
            if (LanguageCode2Name.ContainsKey(code))
            {
                LanguageCode2Name[code] = name;
                _languageCode2Resources[code] = resourceDictionary;
            }
            else
            {
                LanguageCode2Name.Add(code, name);
                _languageCode2Resources.Add(code, resourceDictionary);
            }
        }


        public bool SetLanguage(string code)
        {
            if (!LanguageCode2Name.ContainsKey(code))
                return false;

            if (languageCode == code)
                return true;

            languageCode = code;
            var resource = _languageCode2Resources[code];
            foreach (var key in resource.Keys)
            {
                if (_applicationResourceDictionary.Contains(key))
                {
                    _applicationResourceDictionary[key] = resource[key];
                }
                else
                {
                    _applicationResourceDictionary.Add(key, resource[key]);
                }
            }
            return true;
        }


        public string Translate(Enum e)
        {
            var key = e.GetType().Name + e;
            return Translate(key);
        }

        public string Translate(string key)
        {
            key = key.Trim(new[] { '\'' });
            if (_applicationResourceDictionary.Contains(key))
                return _applicationResourceDictionary[key].ToString();

#if DEBUG
            var tw = new StreamWriter("need translation " + languageCode);
            tw.WriteLine(key);
            tw.Close();
#endif

            return key;
        }

        public string Translate(string key, params object[] parameters)
        {
            var format = Translate(key);
            if (format == null)
                return "!" + key + (parameters.Length > 0 ? ":" + string.Join(",", parameters) : "") + "!";

            return string.Format(format, parameters);
        }
    }
}
