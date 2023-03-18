﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using _1RM.Model;
using _1RM.Utils;
using Shawn.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;

namespace _1RM.Service
{
    public class MockLanguageService : ILanguageService
    {
        public void AddXamlLanguageResources(string code, string fullName)
        {
            return;
        }

        public string Translate(Enum e)
        {
            return e.ToString();
        }

        public string Translate(string key)
        {
            return key;
        }

        public string Translate(string key, params object[] parameters)
        {
            return key;
        }
    }

    public class LanguageService : ILanguageService
    {
        private string _languageCode = "en-us";
        private readonly ResourceDictionary _applicationResourceDictionary;


        public readonly Dictionary<string, ResourceDictionary> Resources = new Dictionary<string, ResourceDictionary>();

        /// <summary>
        /// code => language file name, all codes leave in small cases, ref https://en.wikipedia.org/wiki/Language_code
        /// </summary>
        public Dictionary<string, string> LanguageCode2Name { get; } = new Dictionary<string, string>();


        public LanguageService(ResourceDictionary applicationResourceDictionary)
        {
            _applicationResourceDictionary = applicationResourceDictionary;
            // add static language resources
            foreach (var file in LanguagesResources.Files)
            {
                AddStaticLanguageResources(file);
            }
        }

        public void AddXamlLanguageResources(string code, string fullName)
        {
            var resourceDictionary = GetResourceDictionaryByXamlFilePath(fullName);
            if (resourceDictionary?.Contains("language_name") != true) return;
            AddLanguage(code, resourceDictionary["language_name"].ToString()!, resourceDictionary);
        }

        private void AddStaticLanguageResources(string code)
        {
            code = code.ToLower();
            if (code.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                code = code.Replace(".xaml", "");
            var path = ResourceUriHelper.GetUriPathFromCurrentAssembly($"Resources/Languages/{code}.xaml");
            if (LanguageCode2Name.ContainsKey(code)) return;
            var r = GetResourceDictionaryByXamlUri(path);
            Debug.Assert(r != null);
            Debug.Assert(r?.Contains("language_name") == true);
            if (r != null)
                AddLanguage(code, r["language_name"].ToString()!, r);
        }

        private static ResourceDictionary? GetResourceDictionaryByXamlUri(string path)
        {
            try
            {
                var resourceDictionary = MultiLanguageHelper.LangDictFromXamlUri(new Uri(path));
                if (resourceDictionary != null)
                {
                    return resourceDictionary;
                }
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
            }
            return null;
        }

        private static ResourceDictionary? GetResourceDictionaryByXamlFilePath(string path)
        {
            Debug.Assert(path.EndsWith(".xaml", true, CultureInfo.InstalledUICulture));
            try
            {
                var resourceDictionary = MultiLanguageHelper.LangDictFromXamlFile(path);
                if (resourceDictionary != null)
                {
                    return resourceDictionary;
                }
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
            }
            return null;
        }


        private void AddLanguage(string code, string name, ResourceDictionary resourceDictionary)
        {
            if (LanguageCode2Name.ContainsKey(code))
            {
                LanguageCode2Name[code] = name;
                Resources[code] = resourceDictionary;
            }
            else
            {
                LanguageCode2Name.Add(code, name);
                Resources.Add(code, resourceDictionary);
            }
        }

        public bool SetLanguage(string code)
        {
            if (!LanguageCode2Name.ContainsKey(code))
                return false;

            _languageCode = code;
            var resource = Resources[code];

            var en = Resources["en-us"];
            var missingFields = MultiLanguageHelper.FindMissingFields(en, resource);
            if (missingFields.Count > 0)
            {
                foreach (var field in missingFields)
                {
                    resource.Add(field, en[field]);
                }
#if DEBUG
                var mf = string.Join(", ", missingFields);
                MessageBox.Show($"language resource missing:\r\n {mf}", Translate("Error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
                File.WriteAllText("LANGUAGE_ERROR.txt", mf);
#endif
            }

            _applicationResourceDictionary?.ChangeLanguage(resource);
            GlobalEventHelper.OnLanguageChanged?.Invoke();
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
                return _applicationResourceDictionary[key].ToString() ?? key;

            MsAppCenterHelper.Error(new DirectoryNotFoundException($"int {_languageCode}, key not found: {key}"));
#if DEBUG
            var tw = new StreamWriter("need translation " + _languageCode + ".txt", true);
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
