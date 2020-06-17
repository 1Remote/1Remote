using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using PRM.Core.DB;
using PRM.Core.Model;
using PRM.Model;
using Shawn.Ulits;

namespace PRM
{
    public static class AppInit
    {
        public static void Init(ResourceDictionary appResourceDictionary)
        {
            var appDateFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), SystemConfig.AppName);
            if (!Directory.Exists(appDateFolder))
                Directory.CreateDirectory(appDateFolder);
            SimpleLogHelper.LogFileName = Path.Combine(appDateFolder, "PRemoteM.log.md");
            var iniPath = Path.Combine(appDateFolder, SystemConfig.AppName + ".ini");
            var ini = new Ini(iniPath);
            
            var language = new SystemConfigLanguage(appResourceDictionary, ini);
            var general = new SystemConfigGeneral(ini);
            var quickConnect = new SystemConfigQuickConnect(ini);
            var theme = new SystemConfigTheme(ini);
            var dataSecurity = new SystemConfigDataSecurity(ini);
            
            // config create instance (settings & langs)
            SystemConfig.Init();
            SystemConfig.Instance.General = general;
            SystemConfig.Instance.Language = language;
            SystemConfig.Instance.QuickConnect = quickConnect;
            SystemConfig.Instance.DataSecurity = dataSecurity;
            SystemConfig.Instance.Theme = theme;

            // server data holder init
            Global.GetInstance().OnServerConn += WindowPool.ShowRemoteHost;
        }
    }
}
