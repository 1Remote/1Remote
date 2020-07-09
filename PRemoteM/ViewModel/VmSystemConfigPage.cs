using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.AccessControl;
using System.Security.Permissions;
using System.Security.Principal;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using Newtonsoft.Json;
using PRM.Core.DB;
using PRM.Core.Model;
using PRM.Core.Protocol;
using PRM.Core.Protocol.Putty;
using PRM.Core.Protocol.Putty.SSH;
using PRM.Core.Protocol.RDP;
using Shawn.Ulits;
using SQLite;
using NotifyPropertyChangedBase = PRM.Core.NotifyPropertyChangedBase;

namespace PRM.ViewModel
{
    public class VmSystemConfigPage : NotifyPropertyChangedBase
    {
        public readonly VmMain Host;
        public VmSystemConfigPage(VmMain vmMain)
        {
            Host = vmMain;
            // create new SystemConfigGeneral object
            SystemConfig = SystemConfig.Instance;
        }

        public SystemConfig SystemConfig { get; set; }

        private Visibility _progressBarVisibility = Visibility.Collapsed;
        public Visibility ProgressBarVisibility
        {
            get => _progressBarVisibility;
            private set => SetAndNotifyIfChanged(nameof(ProgressBarVisibility), ref _progressBarVisibility, value);
        }


        #region CMD

        private RelayCommand _cmdSaveAndGoBack;
        public RelayCommand CmdSaveAndGoBack
        {
            get
            {
                if (_cmdSaveAndGoBack == null)
                {
                    _cmdSaveAndGoBack = new RelayCommand((o) =>
                    {
                        // check if Db is ok
                        var c1 = SystemConfig.DataSecurity.CheckIfDbIsOk();
                        if (!c1.Item1)
                        {
                            MessageBox.Show(c1.Item2, SystemConfig.Language.GetText("messagebox_title_error"));
                            //MessageBox.Show(
                            //    SystemConfig.Language
                            //        .GetText("system_options_data_security_error_can_not_open") + ": " +
                            //    SystemConfig.DataSecurity.DbPath,
                            //    SystemConfig.Language.GetText("messagebox_title_error"));
                            return;
                        }


                        Host.DispPage = null;

                        /***                update config                ***/
                        SystemConfig.Language.Save();
                        SystemConfig.General.Save();
                        SystemConfig.QuickConnect.Save();
                        SystemConfig.DataSecurity.Save();
                        SystemConfig.Theme.Save();
                        SystemConfig.Theme.ReloadPuttyThemes();
                    });
                }
                return _cmdSaveAndGoBack;
            }
        }




        private RelayCommand _cmdOpenPath;
        public RelayCommand CmdOpenPath
        {
            get
            {
                if (_cmdOpenPath == null)
                {
                    _cmdOpenPath = new RelayCommand((o) =>
                    {
                        var path = o.ToString();
                        if (File.Exists(path))
                        {
                            System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo("Explorer.exe");
                            psi.Arguments = "/e,/select," + path;
                            System.Diagnostics.Process.Start(psi);
                        }

                        if (Directory.Exists(path))
                        {
                            System.Diagnostics.Process.Start("explorer.exe", path);
                        }
                    });
                }
                return _cmdOpenPath;
            }
        }
        #endregion
    }
}
