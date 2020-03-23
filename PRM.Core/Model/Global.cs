using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using PRM.Core.Base;
using Shawn.Ulits;
using static System.Diagnostics.Debug;

namespace PRM.Core.Model
{
    public class Global
    {
        #region singleton
        private static Global uniqueInstance;
        private static readonly object InstanceLock = new object();
        private Global()
        {
        }
        public static Global GetInstance()
        {
            lock (InstanceLock)
            {
                if (uniqueInstance == null)
                {
                    uniqueInstance = new Global();
                }
            }
            return uniqueInstance;
        }
        #endregion


        #region language

        private string _currentLanguage = "zh-cn";
        public string CurrentLanguage
        {
            get => _currentLanguage;
            set
            {
                _currentLanguage = value;
                // reload ResourceDictionary
                _currentLanguageResourceDictionary = null;
            }
        }

        public const string DefaultLanguage = "zh-cn";
        private ResourceDictionary _defaultLanguageResourceDictionary = null;
        public ResourceDictionary DefaultLanguageResourceDictionary
        {
            get
            {
                if (_defaultLanguageResourceDictionary == null)
                {
                    _defaultLanguageResourceDictionary = MultiLangHelper.LangDictFromJsonFile(@"Languages\" + DefaultLanguage + ".json") ??
                                                         MultiLangHelper.LangDictFromXamlUri(new Uri("pack://application:,,,/PRM.Core;component/Languages/zh-cn.xaml"));
                    Assert(_defaultLanguageResourceDictionary != null);
                }
                return _defaultLanguageResourceDictionary;
            }
        }
        private ResourceDictionary _currentLanguageResourceDictionary = null;
        public ResourceDictionary CurrentLanguageResourceDictionary
        {
            get
            {
                if (_currentLanguageResourceDictionary == null)
                {
                    _currentLanguageResourceDictionary = MultiLangHelper.LangDictFromJsonFile(@"Languages\" + CurrentLanguage + ".json");
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
                        _currentLanguage = DefaultLanguage;
                        _currentLanguageResourceDictionary = _defaultLanguageResourceDictionary;
                    }

                }
                return _currentLanguageResourceDictionary;
            }
        }

        public string GetText(string textKey)
        {
            if (CurrentLanguageResourceDictionary.Contains(textKey))
                return CurrentLanguageResourceDictionary[textKey].ToString();
            if (DefaultLanguageResourceDictionary.Contains(textKey))
                return DefaultLanguageResourceDictionary[textKey].ToString();

            throw new NotImplementedException("can't find any string by '" + textKey + "'!");
        }

        #endregion


        #region MyRegion

        public readonly List<ServerAbstract> ServerList = new List<ServerAbstract>();

        #endregion
    }
}
