using System.Collections.Generic;
using System.Windows.Controls;
using PRM.Core.Model;
using PRM.Core.Protocol;
using PRM.Core.UI.VM;
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
            //LanguageCode2Name = SystemConfig.GetInstance().Language.LanguageCode2Name;
            SelectedLanguageCode = SystemConfig.GetInstance().Language.CurrentLanguageCode;
        }


        private string _selectedLanguageCode = "en-us";
        public string SelectedLanguageCode
        {
            get => _selectedLanguageCode;
            set => SetAndNotifyIfChanged(nameof(SelectedLanguageCode), ref _selectedLanguageCode, value);
        }

        private Dictionary<string, string> _languageCode2Name;
        public Dictionary<string, string> LanguageCode2Name => SystemConfig.GetInstance().Language.LanguageCode2Name;
        //{
        //    get => _languageCode2Name;
        //    set => SetAndNotifyIfChanged(nameof(LanguageCode2Name), ref _languageCode2Name, value);
        //}
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
                        SystemConfig.GetInstance().Language.CurrentLanguageCode = SelectedLanguageCode;
                    });
                }
                return _cmdSaveAndGoBack;
            }
        }
        #endregion
    }
}
