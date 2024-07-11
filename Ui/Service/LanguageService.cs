using System;
using System.Collections;
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
        public const string NAME = "Name";
        public const string XXX_IS_ALREADY_EXISTED = "XXX is already existed!";
        public const string CAN_NOT_BE_EMPTY = "Can not be empty!";

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

        private static readonly string[] Special_Marks_in_XAML_Content = { "&", "<", ">", "\r", "\n" };
        private static readonly string[] Special_Characters_in_XAML_Content = { "&amp;", "&lt;", "&gt;", "\\r", "\\n" };
        private static ResourceDictionary? GetResourceDictionaryByXamlUri(string path)
        {
            try
            {
                var resourceDictionary = MultiLanguageHelper.LangDictFromXamlUri(new Uri(path));
                if (resourceDictionary != null)
                {
                    foreach (var key in resourceDictionary.Keys)
                    {
                        if (resourceDictionary[key] is string val)
                        {
                            for (int j = 0; j < Special_Characters_in_XAML_Content.Length; j++)
                            {
                                val = val.Replace(Special_Characters_in_XAML_Content[j], Special_Marks_in_XAML_Content[j]);
                            }
                            resourceDictionary[key] = val;
                        }
                    }
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
                    foreach (var key in resourceDictionary.Keys)
                    {
                        if (resourceDictionary[key] is string val)
                        {
                            for (int j = 0; j < Special_Characters_in_XAML_Content.Length; j++)
                            {
                                val = val.Replace(Special_Characters_in_XAML_Content[j], Special_Marks_in_XAML_Content[j]);
                            }
                            resourceDictionary[key] = val;
                        }
                    }
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

            _applicationResourceDictionary.ChangeLanguage(resource);
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
            if (string.IsNullOrEmpty(key) || _applicationResourceDictionary == null)
                return "";
            else
            {
                string val = key;
                key = key.Trim(new[] { '\'' });
                if (_applicationResourceDictionary.Contains(key))
                {
                    val = _applicationResourceDictionary[key].ToString() ?? key;
                }
                else
                {
                    string message = "";
                    var stacktrace = new StackTrace();
                    for (var i = 0; i < stacktrace.FrameCount; i++)
                    {
                        var frame = stacktrace.GetFrame(i);
                        if (frame == null) continue;
                        message += frame.GetMethod() + " -> " + frame.GetFileName() + ": " + frame.GetFileLineNumber() + "\r\n";
                    }

                    MsAppCenterHelper.Error(new Exception($"[Warning] In {_languageCode}, key not found: `{key}`"), new Dictionary<string, string>()
                    {
                        {"StackTrace", message}
                    });
#if DEBUG
                    var tw = new StreamWriter("need translation " + _languageCode + ".txt", true);
                    tw.WriteLine(key);
                    tw.Close();
#endif
                }
                return val;
            }
        }

        public string Translate(string key, params object[] parameters)
        {
            var format = Translate(key);
            if (string.IsNullOrEmpty(format))
                return "!" + key + (parameters.Length > 0 ? ":" + string.Join(",", parameters) : "") + "!";
            return string.Format(format, parameters);
        }
    }
}
