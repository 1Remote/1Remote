using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using PRM.Core;

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
                var langRd = MultiLangHelper.LangDictFromJsonFile(@"Languages\" + "zh-cn" + ".json");
                if (langRd != null)
                {
                    var rs = Resources.MergedDictionaries.Where(o => o.Source.AbsolutePath.ToLower().IndexOf("Languages".ToLower()) >= 0).ToArray();
                    foreach (var r in rs)
                    {
                        this.Resources.MergedDictionaries.Remove(r);
                    }
                    this.Resources.MergedDictionaries.Add(langRd);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
    }
}
