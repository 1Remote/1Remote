using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Markup;
using Newtonsoft.Json;

namespace Shawn.Utils
{
    public static class MultiLangHelper
    {
        /// <summary>
        /// get lang file path by this key: VarResourceDictionary[MultiLangHelper.LangFilePathKey]
        /// to determine which lang it is
        /// </summary>
        public const string LangFilePathKey = "__Lang_File_Path_Key";

        /// <summary>
        /// to determine which lang it is
        /// </summary>
        public const string ResourceTypeKey = "__Resource_Type_Key";

        public const string ResourceTypeValue = "__Resource_Type_Value=languages";

        private static void SetKey(IDictionary rd, string key, string value)
        {
            if (!rd.Contains(key))
                rd.Add(key, value);
            else
                rd[key] = value;
        }

        public static ResourceDictionary LangDictFromJsonFile(string jsonPath)
        {
            var fi = new FileInfo(jsonPath);
            if (!fi.Exists) return null;
            var rd = LangDictFromJsonString(File.ReadAllText(fi.FullName));
            SetKey(rd, LangFilePathKey, fi.FullName);
            SetKey(rd, ResourceTypeKey, ResourceTypeValue);
            return rd;
        }

        public static ResourceDictionary LangDictFromJsonString(string jsonString)
        {
            try
            {
                var rd = new ResourceDictionary();
                var kvs = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonString);
                foreach (var kv in kvs)
                {
                    SetKey(rd, kv.Key, kv.Value);
                }
                SetKey(rd, LangFilePathKey, "from_memory");
                SetKey(rd, ResourceTypeKey, ResourceTypeValue);
                return rd;
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
                return new ResourceDictionary();
            }
        }

        public static ResourceDictionary LangDictFromXamlUri(Uri uri)
        {
            try
            {
                var rd = new ResourceDictionary()
                {
                    Source = uri
                };
                SetKey(rd, LangFilePathKey, uri.AbsolutePath);
                SetKey(rd, ResourceTypeKey, ResourceTypeValue);
                return rd;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static string ResourceDictionaryToJson(ResourceDictionary lang)
        {
            var kvs = new Dictionary<string, string>();
            foreach (var key in lang.Keys)
            {
                kvs.Add(key.ToString(), lang[key].ToString());
            }
            return JsonConvert.SerializeObject(kvs);
        }

        public static void SaveToLangResourceDictionary(ResourceDictionary lang, string path)
        {
            try
            {
                StreamWriter writer = new StreamWriter(path);
                XamlWriter.Save(lang, writer);
                writer.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public static void ChangeLanguage(this ResourceDictionary resources, ResourceDictionary lang)
        {
            Debug.Assert(resources != null);
            Debug.Assert(lang != null);

            var rs1 = resources.MergedDictionaries.Where(o => o.Source != null && o.Source.IsAbsoluteUri && o.Source.AbsolutePath.ToLower().IndexOf("Languages/".ToLower(), StringComparison.Ordinal) >= 0).ToArray();
            foreach (var r in rs1)
            {
                resources.MergedDictionaries.Remove(r);
            }
            var rs2 = resources.MergedDictionaries.Where(o => o.Contains(ResourceTypeKey) && o[ResourceTypeKey].ToString() == ResourceTypeValue).ToArray();
            foreach (var r in rs2)
            {
                resources.MergedDictionaries.Remove(r);
            }
            resources.MergedDictionaries.Add(lang);
        }
    }
}