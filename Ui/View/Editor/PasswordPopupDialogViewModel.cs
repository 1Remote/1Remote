using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using _1RM.Model;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Service;
using _1RM.Service.Locality;
using _1RM.Utils;
using Shawn.Utils;
using Shawn.Utils.Wpf;

namespace _1RM.View.Editor
{
    public class PasswordPopupDialogViewModel : NotifyPropertyChangedBaseScreen
    {
        public bool DialogResult { get; set; } = false;

        private string _title = "";
        public string Title
        {
            get => _title;
            set
            {
                if (SetAndNotify(ref _title, value))
                {
                    if (View is PasswordPopupDialogView v)
                    {
                        v.Title = value;
                    }
                }
            }
        }

        private string _userName = "";
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

        private bool _canUsePrivateKeyForConnect = true;
        public bool CanUsePrivateKeyForConnect
        {
            get => _canUsePrivateKeyForConnect;
            set => SetAndNotifyIfChanged(ref _canUsePrivateKeyForConnect, value);
        }

        private bool _usePrivateKeyForConnect = false;
        public bool UsePrivateKeyForConnect
        {
            get => _canUsePrivateKeyForConnect && _usePrivateKeyForConnect;
            set => SetAndNotifyIfChanged(ref _usePrivateKeyForConnect, CanUsePrivateKeyForConnect && value);
        }


        private bool _canRememberInfo = true;
        public bool CanRememberInfo
        {
            get => _canRememberInfo;
            set
            {
                if (SetAndNotifyIfChanged(ref _canRememberInfo, value))
                {
                    RaisePropertyChanged(nameof(IsRememberInfo));
                }
            }
        }

        public bool IsRememberInfo
        {
            get => _canRememberInfo && IoC.Get<LocalityService>().GetMisc<bool>($"{nameof(PasswordPopupDialogViewModel)}.{nameof(IsRememberInfo)}");
            set
            {
                IoC.Get<LocalityService>().SetMisc($"{nameof(PasswordPopupDialogViewModel)}.{nameof(IsRememberInfo)}", value.ToString());
                RaisePropertyChanged();
            }
        }

        private string _privateKey = "";
        public string PrivateKey
        {
            get => _privateKey;
            set => SetAndNotifyIfChanged(ref _privateKey, value);
        }

        public PasswordPopupDialogViewModel(bool canUsePrivateKeyForConnect = false, bool canRememberInfo = false)
        {
            CanUsePrivateKeyForConnect = canUsePrivateKeyForConnect;
            CanRememberInfo = canRememberInfo;
        }

        protected override void OnViewLoaded()
        {
            base.OnViewLoaded();
            if (View is PasswordPopupDialogView v)
            {
                v.Title = Title;

                v.TbUserName.Text = UserName;
                v.TbPwd.Password = Password;

                if (!string.IsNullOrEmpty(v.TbUserName.Text))
                {
                    v.TbPwd.Focus();
                    v.TbPwd.CaretIndex = v.TbPwd.Password.Length;
                }
                else
                {
                    v.TbUserName.Focus();
                    v.TbUserName.CaretIndex = v.TbUserName.Text.Length;
                }
            }
        }


        /// <summary>
        /// validate whether all fields are correct to save
        /// </summary>
        /// <returns></returns>
        public virtual bool CanSave()
        {
            if (!string.IsNullOrEmpty(UserName))
            {
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
