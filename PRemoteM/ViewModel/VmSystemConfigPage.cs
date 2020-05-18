using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using PersonalRemoteManager;
using PRM.Core.DB;
using PRM.Core.Model;
using PRM.Core.Protocol;
using PRM.Core.UI.VM;
using PRM.Core.Ulits;
using PRM.View;
using Shawn.Ulits.PageHost;
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
            SystemConfig = SystemConfig.GetInstance();
            SystemConfig.DataSecurity.OnRsaProgress += OnRsaProgress;
        }

        private void OnRsaProgress(int arg1, int arg2)
        {
            ProgressBarValue = arg1;
            ProgressBarMaximum = arg2;
        }

        public SystemConfig SystemConfig { get; set; }

        private bool _tabIsEnabled = true;
        public bool TabIsEnabled
        {
            get => _tabIsEnabled;
            private set => SetAndNotifyIfChanged(nameof(TabIsEnabled), ref _tabIsEnabled, value);
        }

        private Visibility _progressBarVisibility = Visibility.Hidden;
        public Visibility ProgressBarVisibility
        {
            get => _progressBarVisibility;
            private set => SetAndNotifyIfChanged(nameof(ProgressBarVisibility), ref _progressBarVisibility, value);
        }


        private int _progressBarValue = 0;
        public int ProgressBarValue
        {
            get => _progressBarValue;
            set => SetAndNotifyIfChanged(nameof(ProgressBarValue), ref _progressBarValue, value);
        }

        private int _progressBarMaximum = 0;
        public int ProgressBarMaximum
        {
            get => _progressBarMaximum;
            set
            {
                if (value != _progressBarMaximum)
                {
                    SetAndNotifyIfChanged(nameof(ProgressBarMaximum), ref _progressBarMaximum, value);
                    if (value == 0)
                    {
                        //TabIsEnabled = true;
                        ProgressBarVisibility = Visibility.Collapsed;
                    }
                    else
                    {
                        //TabIsEnabled = false;
                        ProgressBarVisibility = Visibility.Visible;
                    }
                }
            }
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
                        {
                            try
                            {
                                Server.Init();
                            }
                            catch (Exception ee)
                            {
                                MessageBox.Show(
                                    SystemConfig.GetInstance().Language
                                        .GetText("system_options_data_security_error_can_not_open") + ": " +
                                    SystemConfig.DataSecurity.DbPath);
                                return;
                            }
                        }


                        var ret = SystemConfig.GetInstance().DataSecurity.ValidateRsa();
                        switch (ret)
                        {
                            case SystemConfigDataSecurity.ERsaStatues.Ok:
                                break;
                            case SystemConfigDataSecurity.ERsaStatues.CanNotFindPrivateKey:
                                MessageBox.Show(SystemConfig.GetInstance().Language.GetText("system_options_data_security_error_rsa_private_key_not_found"));
                                return;
                            case SystemConfigDataSecurity.ERsaStatues.PrivateKeyContentError:
                                MessageBox.Show(SystemConfig.GetInstance().Language.GetText("system_options_data_security_error_rsa_private_key_not_match"));
                                return;
                            case SystemConfigDataSecurity.ERsaStatues.PrivateKeyIsNotMatch:
                                MessageBox.Show(SystemConfig.GetInstance().Language.GetText("system_options_data_security_error_rsa_private_key_not_match"));
                                return;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }


                        Host.DispPage = null;

                        /***                update config                ***/
                        SystemConfig.GetInstance().Language.Save();
                        SystemConfig.GetInstance().General.Save();
                        SystemConfig.GetInstance().QuickConnect.Save();
                        SystemConfig.GetInstance().DataSecurity.Save();

                        Global.GetInstance().ReloadServers();
                    });
                }
                return _cmdSaveAndGoBack;
            }
        }


        private RelayCommand _cmdGenRsaKey;
        public RelayCommand CmdGenRsaKey
        {
            get
            {
                if (_cmdGenRsaKey == null)
                {
                    _cmdGenRsaKey = new RelayCommand((o) =>
                    {
                        SystemConfig.DataSecurity.GenRsa();
                    });
                }
                return _cmdGenRsaKey;
            }
        }


        private RelayCommand _cmdClearRsaKey;
        public RelayCommand CmdClearRsaKey
        {
            get
            {
                if (_cmdClearRsaKey == null)
                {
                    _cmdClearRsaKey = new RelayCommand((o) =>
                    {
                        SystemConfig.DataSecurity.CleanRsa();
                    });
                }
                return _cmdClearRsaKey;
            }
        }
        #endregion
    }
}
