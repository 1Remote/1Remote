using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using Newtonsoft.Json;

namespace PRM.Core
{
    public static class MultiLangHelper
    {
        public static ResourceDictionary LangDictFromJsonFile(string jsonPath)
        {
            if (File.Exists(jsonPath))
            {
                return LangDictFromJsonString(File.ReadAllText(jsonPath));
            }
            return null;
        }

        public static ResourceDictionary LangDictFromJsonString(string jsonString)
        {
            try
            {
                var rd = new ResourceDictionary();
                var kvs = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonString);
                foreach (var kv in kvs)
                {
                    rd.Add(kv.Key, kv.Value);
                }
#if DEBUG
                // TODO 调试用
                SaveLangResourceDictionary(rd, "zh-ch.xaml");
#endif
                return rd;
            }
            catch (Exception e)
            {
                SimpleLogHelper.Warning(e);
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

        public static void SaveLangResourceDictionary(ResourceDictionary lang, string path)
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
    }
}
