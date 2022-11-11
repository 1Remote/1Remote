using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using _1RM.Model;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Service;
using _1RM.Utils;
using Shawn.Utils;
using Shawn.Utils.Wpf;

namespace _1RM.View.Editor
{
    public class PasswordPopupDialogViewModel : NotifyPropertyChangedBaseScreen
    {
        public static string LastUsername = "";
        public static string LastPassword = "";

        //public List<ProtocolBaseViewModel> ProtocolList { get; }

        public bool DialogResult { get; set; } = false;

        public string Title { get; set; } = "";

        private string _userName = "Administrator";
        public string UserName
        {
            get => _userName;
            set => SetAndNotifyIfChanged(ref _userName, value);
        }

        private string _password = "";
        public string Password
        {
            get => _password;
            set => SetAndNotifyIfChanged(ref _password, value);
        }

        public PasswordPopupDialogViewModel()
        {
            if (string.IsNullOrEmpty(UnSafeStringEncipher.SimpleDecrypt(LastUsername)) == false)
                UserName = UnSafeStringEncipher.SimpleDecrypt(LastUsername);

            if (string.IsNullOrEmpty(UnSafeStringEncipher.SimpleDecrypt(LastPassword)) == false)
                Password = UnSafeStringEncipher.SimpleDecrypt(LastPassword);
        }


        /// <summary>
        /// validate whether all fields are correct to save
        /// </summary>
        /// <returns></returns>
        public virtual bool CanSave()
        {
            if (!string.IsNullOrEmpty(UserName))
            {
                LastUsername = UnSafeStringEncipher.SimpleEncrypt(UserName);
                LastPassword = UnSafeStringEncipher.SimpleEncrypt(Password);
                return true;
            }
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
