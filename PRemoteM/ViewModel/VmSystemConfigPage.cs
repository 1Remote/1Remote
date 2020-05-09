using System.Collections.Generic;
using System.Windows.Controls;
using PersonalRemoteManager;
using PRM.Core.Model;
using PRM.Core.Protocol;
using PRM.Core.UI.VM;
using PRM.Core.Ulits;
using PRM.View;
using Shawn.Ulits.PageHost;
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
            SystemConfig = SystemConfig.GetInstance();
        }

        public SystemConfig SystemConfig { get; set; }

        private SystemConfigGeneral _general;
        public SystemConfigGeneral General
        {
            get => _general;
            set => SetAndNotifyIfChanged(nameof(General), ref _general, value);
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
                        Host.DispPage = null;

                        /***                update config                ***/
                        SystemConfig.GetInstance().Language.Save();
                        SystemConfig.GetInstance().General.Save();
                        SystemConfig.GetInstance().QuickConnect.Save();
                        SystemConfig.GetInstance().DataSecurity.Save();

                    });
                }
                return _cmdSaveAndGoBack;
            }
        }
        #endregion
    }
}
