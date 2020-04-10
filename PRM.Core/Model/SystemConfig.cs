using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PRM.Core.Model
{
    public partial class SystemConfig : NotifyPropertyChangedBase
    {
        #region singleton
        private static SystemConfig uniqueInstance;
        private static readonly object InstanceLock = new object();
        private SystemConfig()
        {
        }
        public static SystemConfig GetInstance()
        {

            if (uniqueInstance == null)
            {
                throw new NullReferenceException("SystemConfig has not been inited!");
            }
            return uniqueInstance;
        }
        #endregion

        /// <summary>
        /// Must init before app start in app.cs
        /// </summary>
        public static void Init(ResourceDictionary appResourceDictionary)
        {
            if (uniqueInstance == null)
                lock (InstanceLock)
                {
                    if (uniqueInstance == null)
                    {
                        uniqueInstance = new SystemConfig();
                    }
                }
            uniqueInstance.Language = new ConfigLanguage(appResourceDictionary);
        }


        private ConfigLanguage _language = null;
        public ConfigLanguage Language
        {
            get => _language;
            protected set => SetAndNotifyIfChanged(nameof(Language), ref _language, value);
        }
    }
}
