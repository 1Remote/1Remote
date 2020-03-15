using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using PRM.Core;
using PRM.Core.Model;
using PRM.Core.Ulits;

namespace PersonalRemoteManager
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        private void App_OnStartup(object sender, StartupEventArgs e)
        {
            try
            {
                Global.GetInstance().CurrentLanguage = "xxxx";
                MultiLangHelper.ChangeLanguage(this.Resources, Global.GetInstance().CurrentLanguageResourceDictionary);

                Global.GetInstance().CurrentLanguage = "zh-cn";
                MultiLangHelper.ChangeLanguage(this.Resources, Global.GetInstance().CurrentLanguageResourceDictionary);

                Global.GetInstance().CurrentLanguage = "en-us";
                MultiLangHelper.ChangeLanguage(this.Resources, Global.GetInstance().CurrentLanguageResourceDictionary);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
    }
}
