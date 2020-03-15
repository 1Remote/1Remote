using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using PRM.Core.Ulits;
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

        private ResourceDictionary _currentLanguageResourceDictionary = null;

        public ResourceDictionary CurrentLanguageResourceDictionary
        {
            get
            {
                if (_currentLanguageResourceDictionary == null)
                {
                    _currentLanguageResourceDictionary = MultiLangHelper.LangDictFromJsonFile(@"Languages\" + CurrentLanguage + ".json");
                    if (_currentLanguageResourceDictionary == null)
                    {
                        // use default
                        _currentLanguage = "zh-cn";
                        _currentLanguageResourceDictionary =
                            MultiLangHelper.LangDictFromXamlUri(
                                new Uri("pack://application:,,,/PRM.Core;component/Languages/zh-cn.xaml"));
                        Assert(_currentLanguageResourceDictionary != null);
                    }
                }
                return _currentLanguageResourceDictionary;
            }
        }


        #endregion
    }
}
