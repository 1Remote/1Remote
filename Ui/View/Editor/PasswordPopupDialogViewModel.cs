using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Utils;
using Shawn.Utils;
using Shawn.Utils.Wpf;

namespace _1RM.View.Editor
{
    public class PasswordPopupDialogViewModel : NotifyPropertyChangedBaseScreen
    {
        //public List<ProtocolBaseViewModel> ProtocolList { get; }
        public ProtocolBaseWithAddressPortUserPwd Result { get; } = new FTP();

        public bool DialogResult { get; set; } = false;

        public string Title { get; set; } = "";


        /// <summary>
        /// validate whether all fields are correct to save
        /// </summary>
        /// <returns></returns>
        public virtual bool CanSave()
        {
            if (!string.IsNullOrEmpty(Result.UserName))
                return true;
            return false;
        }


        private RelayCommand? _cmdSave;
        public RelayCommand CmdSave
        {
            get
            {
                return _cmdSave ??= new RelayCommand((o) =>
                {
                    this.DialogResult = true;
                    this.RequestClose(true);
                }, o => CanSave());
            }
        }


        private RelayCommand? _cmdQuit;
        public RelayCommand CmdQuit
        {
            get
            {
                return _cmdQuit ??= new RelayCommand((o) =>
                {
                    this.DialogResult = false;
                    this.RequestClose(false);
                });
            }
        }
    }
}
