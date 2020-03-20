using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using PRM.Core;
using PRM.Core.Model;
using PRM.Core.ViewModel;
using PRM.View;
using Shawn.Ulits;

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


                //var vm = new VmMain();
                //var sb = new SearchBoxWindow(vm);
                //sb.ShowDialog();
                //Environment.Exit(0);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
    }
}
