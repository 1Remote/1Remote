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
            SelectedLanguageCode = SystemConfig.GetInstance().Language.CurrentLanguageCode;
            // create new SystemConfigGeneral object
            General = new SystemConfigGeneral(SystemConfig.GetInstance().Ini);
            QuickConnect = new SystemConfigQuickConnect(SystemConfig.GetInstance().Ini);
        }


        private string _selectedLanguageCode = "en-us";
        public string SelectedLanguageCode
        {
            get => _selectedLanguageCode;
            set => SetAndNotifyIfChanged(nameof(SelectedLanguageCode), ref _selectedLanguageCode, value);
        }


        public Dictionary<string, string> LanguageCode2Name => SystemConfig.GetInstance().Language.LanguageCode2Name;



        private SystemConfigGeneral _general;
        public SystemConfigGeneral General
        {
            get => _general;
            set => SetAndNotifyIfChanged(nameof(General), ref _general, value);
        }




        private SystemConfigQuickConnect _quickConnect;
        public SystemConfigQuickConnect QuickConnect
        {
            get => _quickConnect;
            set => SetAndNotifyIfChanged(nameof(QuickConnect), ref _quickConnect, value);
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
                        // General 
                        if (SystemConfig.GetInstance().Language.CurrentLanguageCode != SelectedLanguageCode)
                        {
                            SystemConfig.GetInstance().Language.CurrentLanguageCode = SelectedLanguageCode;
                            SystemConfig.GetInstance().Language.Save();
                        }
                        SystemConfig.GetInstance().General.Update(General);
                        SystemConfig.GetInstance().General.Save();

                        SystemConfig.GetInstance().QuickConnect.Update(QuickConnect);
                        SystemConfig.GetInstance().QuickConnect.Save();


                        if (SystemConfig.GetInstance().General.AppStartAutomatically == true)
                        {
                            SetSelfStartingHelper.SetSelfStart();
                        }
                        else
                        {
                            SetSelfStartingHelper.UnsetSelfStart();
                        }
                    });
                }
                return _cmdSaveAndGoBack;
            }
        }
        #endregion
    }
}
