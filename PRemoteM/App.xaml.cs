using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using PRM.Core;
using PRM.Core.DB;
using PRM.Core.Model;
using PRM.RDP;
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




#if DEBUG
                // TODO 测试用
                if (File.Exists(PRM_DAO.DbPath))
                    File.Delete(PRM_DAO.DbPath);
                if (PRM_DAO.GetInstance().ListAllServer().Count == 0)
                {
                    var di = new DirectoryInfo(@"D:\rdpjson");
                    if (di.Exists)
                    {
                        // read from jsonfile 
                        var fis = di.GetFiles("*.rdpjson", SearchOption.AllDirectories);
                        var rdp = new ServerRDP();
                        foreach (var fi in fis)
                        {
                            var newRdp = rdp.CreateFromJsonString(File.ReadAllText(fi.FullName));
                            if (newRdp != null)
                            {
                                PRM_DAO.GetInstance().Insert(ServerOrm.ConvertFrom(newRdp));
                            }
                        }
                    }
                    else
                    {
                        di.Create();
                    }
                }
#endif




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
