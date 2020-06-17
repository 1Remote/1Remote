using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Permissions;
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
using PRM.Core.UI.VM;
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
            SystemConfig.DataSecurity.OnRsaProgress += OnLongTimeProgress;
        }

        private void OnLongTimeProgress(int arg1, int arg2)
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

        private Visibility _progressBarVisibility = Visibility.Collapsed;
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
                        // check if Db is ok
                        if (!AppChecker.CheckDbExisted())
                        {
                            MessageBox.Show(
                                SystemConfig.GetInstance().Language
                                    .GetText("system_options_data_security_error_can_not_open") + ": " +
                                SystemConfig.DataSecurity.DbPath,
                                SystemConfig.GetInstance().Language.GetText("messagebox_title_error"));
                            return;
                        }

                        var dbEncrypted = AppChecker.CheckDbEncrypted();
                        if (!dbEncrypted.Item1)
                        {
                            MessageBox.Show(dbEncrypted.Item2, SystemConfig.GetInstance().Language.GetText("messagebox_title_error"));
                            return;
                        }


                        Host.DispPage = null;

                        /***                update config                ***/
                        SystemConfig.GetInstance().Language.Save();
                        SystemConfig.GetInstance().General.Save();
                        SystemConfig.GetInstance().QuickConnect.Save();
                        SystemConfig.GetInstance().DataSecurity.Save();
                        SystemConfig.GetInstance().Theme.Save();
                        SystemConfig.GetInstance().Theme.ReloadThemes();
                    });
                }
                return _cmdSaveAndGoBack;
            }
        }


        private RelayCommand _cmdSelectDbPath;
        public RelayCommand CmdSelectDbPath
        {
            get
            {
                if (_cmdSelectDbPath == null)
                {
                    _cmdSelectDbPath = new RelayCommand((o) =>
                    {
                        //void func(string path)
                        //{
                        //    try
                        //    {
                        //        using (var db = new SQLiteConnection(path))
                        //        {
                        //            db.CreateTable<Server>();
                        //        }
                        //        SystemConfig.DataSecurity.DbPath = path;
                        //        Global.GetInstance().ReloadServers();
                        //    }
                        //    catch (Exception ee)
                        //    {
                        //        MessageBox.Show(SystemConfig.GetInstance().Language.GetText("system_options_data_security_error_can_not_open"));
                        //    }
                        //}
                        var dlg = new OpenFileDialog();
                        dlg.Filter = "Sqlite Database|*.db";
                        dlg.CheckFileExists = false;
                        //dlg.FileOk += (sender, args) =>
                        //{
                        //    func
                        //};
                        if (dlg.ShowDialog() == true)
                        {
                            var path = dlg.FileName;
                            try
                            {
                                //// TODO 判断权限
                                //if (File.Exists(path))
                                //{
                                //    FileIOPermission f2 = new FileIOPermission(FileIOPermissionAccess.Write,
                                //        dlg.FileName);
                                //    try
                                //    {
                                //        f2.Demand();
                                //    }
                                //    catch (SecurityException s)
                                //    {
                                //        Console.WriteLine(s.Message);
                                //    }
                                //}

                                using (var db = new SQLiteConnection(dlg.FileName))
                                {
                                    db.CreateTable<Server>();
                                }
                                SystemConfig.DataSecurity.DbPath = dlg.FileName;
                                Global.GetInstance().ReloadServers();
                            }
                            catch (Exception ee)
                            {
                                MessageBox.Show(SystemConfig.GetInstance().Language.GetText("system_options_data_security_error_can_not_open"));
                            }
                        }
                    });
                }
                return _cmdSelectDbPath;
            }
        }



        private RelayCommand _cmdSelectRsaPrivateKey;
        public RelayCommand CmdSelectRsaPrivateKey
        {
            get
            {
                if (_cmdSelectRsaPrivateKey == null)
                {
                    _cmdSelectRsaPrivateKey = new RelayCommand((o) =>
                    {
                        var dlg = new OpenFileDialog
                        {
                            Filter = $"private key|*{SystemConfigDataSecurity.PrivateKeyFileExt}",
                            InitialDirectory = SystemConfig.DataSecurity.RsaPrivateKeyPath,
                        };
                        if (dlg.ShowDialog() == true)
                        {
                            var res = SystemConfig.GetInstance().DataSecurity.ValidateRsa(dlg.FileName);
                            switch (res)
                            {
                                case SystemConfigDataSecurity.ERsaStatues.Ok:
                                    SystemConfig.GetInstance().DataSecurity.RsaPrivateKeyPath = dlg.FileName;
                                    break;
                                case SystemConfigDataSecurity.ERsaStatues.CanNotFindPrivateKey:
                                case SystemConfigDataSecurity.ERsaStatues.PrivateKeyContentError:
                                case SystemConfigDataSecurity.ERsaStatues.PrivateKeyIsNotMatch:
                                    MessageBox.Show(SystemConfig.GetInstance().Language.GetText("system_options_data_security_error_rsa_private_key_not_match"));
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException();
                            }
                        }
                    });
                }
                return _cmdSelectRsaPrivateKey;
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
                        var dbEncrypted = AppChecker.CheckDbEncrypted();
                        if (!dbEncrypted.Item1)
                        {
                            MessageBox.Show(dbEncrypted.Item2, SystemConfig.GetInstance().Language.GetText("messagebox_title_error"));

                            if (MessageBox.Show(
                                SystemConfig.GetInstance().Language.GetText("system_options_data_security_info_clear_rebuild_database"),
                                SystemConfig.GetInstance().Language.GetText("messagebox_title_warning"),
                                MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                            {
                                if (File.Exists(SystemConfig.GetInstance().DataSecurity.DbPath))
                                    File.Delete(SystemConfig.GetInstance().DataSecurity.DbPath);
                                AppChecker.CheckDbExisted();
                                Global.GetInstance().ReloadServers();
                                SystemConfig.GetInstance().DataSecurity.Load();
                            }
                            return;
                        }
                        else
                            SystemConfig.DataSecurity.CleanRsa();
                    });
                }
                return _cmdClearRsaKey;
            }
        }



        private RelayCommand _cmdExportToJson;
        public RelayCommand CmdExportToJson
        {
            get
            {
                if (_cmdExportToJson == null)
                {
                    _cmdExportToJson = new RelayCommand((o) =>
                    {
                        var dbEncrypted = AppChecker.CheckDbEncrypted();
                        if (!dbEncrypted.Item1)
                        {
                            MessageBox.Show(dbEncrypted.Item2, SystemConfig.GetInstance().Language.GetText("messagebox_title_error"));
                            return;
                        }
                        var dlg = new SaveFileDialog
                        {
                            Filter = "PRM json array|*.prma",
                            Title = SystemConfig.Language.GetText("system_options_data_security_export_dialog_title"),
                            FileName = DateTime.Now.ToString("yyyyMMddhhmmss") + ".prma"
                        };
                        if (dlg.ShowDialog() == true)
                        {
                            var list = new List<ProtocolServerBase>();
                            foreach (var protocolServerBase in Global.GetInstance().ServerList)
                            {
                                var serverBase = (ProtocolServerBase) protocolServerBase.Clone();
                                if (serverBase is ProtocolServerRDP
                                    || serverBase is ProtocolServerSSH)
                                {
                                    var pwd = (ProtocolServerWithAddrPortUserPwdBase) serverBase;
                                    if (SystemConfig.GetInstance().DataSecurity.Rsa != null)
                                        pwd.Password = SystemConfig.GetInstance().DataSecurity.Rsa.DecodeOrNull(pwd.Password) ?? "";
                                }
                                list.Add(serverBase);
                            }
                            File.WriteAllText(dlg.FileName, JsonConvert.SerializeObject(list, Formatting.Indented), Encoding.UTF8);
                        }
                    });
                }
                return _cmdExportToJson;
            }
        }


        private RelayCommand _cmdImportFromJson;
        public RelayCommand CmdImportFromJson
        {
            get
            {
                if (_cmdImportFromJson == null)
                {
                    _cmdImportFromJson = new RelayCommand((o) =>
                    {
                        var dbEncrypted = AppChecker.CheckDbEncrypted();
                        if (!dbEncrypted.Item1)
                        {
                            MessageBox.Show(dbEncrypted.Item2, SystemConfig.GetInstance().Language.GetText("messagebox_title_error"));
                            return;
                        }
                        var dlg = new OpenFileDialog()
                        {
                            Filter = "PRM json array|*.prma",
                            Title = SystemConfig.GetInstance().Language.GetText("system_options_data_security_import_dialog_title"),
                            FileName = DateTime.Now.ToString("yyyyMMddhhmmss") + ".prma"
                        };
                        if (dlg.ShowDialog() == true)
                        {
                            try
                            {
                                var list = new List<ProtocolServerBase>();
                                var jobj = JsonConvert.DeserializeObject<List<object>>(File.ReadAllText(dlg.FileName, Encoding.UTF8));
                                foreach (var json in jobj)
                                {
                                    var server = ServerFactory.GetInstance().CreateFromJsonString(json.ToString());
                                    if (server != null)
                                    {
                                        server.Id = 0;
                                        list.Add(server);
                                    }
                                }
                                if (list?.Count > 0)
                                {
                                    foreach (var serverBase in list)
                                    {
                                        if (serverBase is ProtocolServerRDP
                                            || serverBase is ProtocolServerSSH)
                                        {
                                            var pwd = (ProtocolServerWithAddrPortUserPwdBase) serverBase;
                                            if (SystemConfig.GetInstance().DataSecurity.Rsa != null)
                                                pwd.Password = SystemConfig.GetInstance().DataSecurity.Rsa.Encode(pwd.Password);
                                        }
                                        Server.AddOrUpdate(serverBase, true);
                                    }
                                }
                                Global.GetInstance().ReloadServers();
                                MessageBox.Show(SystemConfig.GetInstance().Language.GetText("system_options_data_security_import_done"));
                            }
                            catch (Exception e)
                            {
                                MessageBox.Show(SystemConfig.GetInstance().Language.GetText("system_options_data_security_import_error"));
                            }
                        }
                    });
                }
                return _cmdImportFromJson;
            }
        }



        private RelayCommand _cmdImportFromMRemoteNgCsv;
        public RelayCommand CmdImportFromMRemoteNgCsv
        {
            get
            {
                if (_cmdImportFromMRemoteNgCsv == null)
                {
                    _cmdImportFromMRemoteNgCsv = new RelayCommand((o) =>
                    {
                        var dbEncrypted = AppChecker.CheckDbEncrypted();
                        if (!dbEncrypted.Item1)
                        {
                            MessageBox.Show(dbEncrypted.Item2, SystemConfig.GetInstance().Language.GetText("messagebox_title_error"));
                            return;
                        }
                        var dlg = new OpenFileDialog()
                        {
                            Filter = "mRemoteNG csv|*.csv",
                            Title = SystemConfig.GetInstance().Language.GetText("system_options_data_security_import_dialog_title"),
                        };
                        if (dlg.ShowDialog() == true)
                        {
                            try
                            {
                                var list = new List<ProtocolServerBase>();
                                using (var sr = new StreamReader(new FileStream(dlg.FileName, FileMode.Open)))
                                {
                                    // title
                                    var title = sr.ReadLine().ToLower().Split(';').ToList();
                                    int protocolIndex = title.IndexOf("protocol");
                                    int nameIndex = title.IndexOf("name");
                                    int groupIndex = title.IndexOf("panel");
                                    int userIndex = title.IndexOf("username");
                                    int pwdIndex = title.IndexOf("password");
                                    int addressIndex = title.IndexOf("hostname");
                                    int portIndex = title.IndexOf("port");
                                    int portIndex2 = title.IndexOf("por2t");
                                    if (protocolIndex == 0)
                                        throw new ArgumentException("can't find protocol field");

                                    // body
                                    var line = sr.ReadLine();
                                    while (!string.IsNullOrEmpty(line))
                                    {
                                        var arr = line.Split(';');
                                        if (arr.Length >= 15)
                                        {
                                            ProtocolServerBase server = null;
                                            var protocol = arr[protocolIndex].ToLower();
                                            var name = "";
                                            var group ="";
                                            var user = "";
                                            var pwd = "";
                                            var address = "";
                                            int port = 22;
                                            if (nameIndex >= 0)
                                                name = arr[nameIndex];
                                            if (groupIndex >= 0)
                                                group = arr[groupIndex];
                                            if (userIndex >= 0)
                                                user = arr[userIndex];
                                            if (pwdIndex >= 0)
                                                pwd = arr[pwdIndex];
                                            if (addressIndex >= 0)
                                                address = arr[addressIndex];
                                            if (portIndex >= 0)
                                                port = int.Parse(arr[portIndex]);
                                            switch (protocol)
                                            {
                                                case "rdp":
                                                    server = new ProtocolServerRDP()
                                                    {
                                                        DispName = name,
                                                        GroupName = group,
                                                        Address = address,
                                                        UserName = user,
                                                        Password = pwd,
                                                        Port = port.ToString(),
                                                    };
                                                    break;
                                                case "ssh1":
                                                    server = new ProtocolServerSSH()
                                                    {
                                                        DispName = name,
                                                        GroupName = group,
                                                        Address = address,
                                                        UserName = user,
                                                        Password = pwd,
                                                        Port = port.ToString(),
                                                        SshVersion = ProtocolServerSSH.ESshVersion.V1
                                                    };
                                                    break;
                                                case "ssh2":
                                                    server = new ProtocolServerSSH()
                                                    {
                                                        DispName = name,
                                                        GroupName = group,
                                                        Address = address,
                                                        UserName = user,
                                                        Password = pwd,
                                                        Port = port.ToString(),
                                                        SshVersion = ProtocolServerSSH.ESshVersion.V2
                                                    };
                                                    break;
                                                case "vnc":
                                                    // TODO add vnc
                                                    break;
                                                case "telnet":
                                                    // TODO add telnet
                                                    break;
                                            }

                                            if (server != null)
                                                list.Add(server);
                                        }
                                        line = sr.ReadLine();
                                    }
                                }
                                if (list?.Count > 0)
                                {
                                    foreach (var serverBase in list)
                                    {
                                        if (serverBase is ProtocolServerRDP
                                            || serverBase is ProtocolServerSSH)
                                        {
                                            var pwd = (ProtocolServerWithAddrPortUserPwdBase) serverBase;
                                            if (SystemConfig.GetInstance().DataSecurity.Rsa != null)
                                                pwd.Password = SystemConfig.GetInstance().DataSecurity.Rsa.Encode(pwd.Password);
                                        }
                                        Server.AddOrUpdate(serverBase, true);
                                    }
                                }
                                Global.GetInstance().ReloadServers();
                                MessageBox.Show(SystemConfig.GetInstance().Language.GetText("system_options_data_security_import_done"));
                            }
                            catch (Exception e)
                            {
                                MessageBox.Show(SystemConfig.GetInstance().Language.GetText("system_options_data_security_import_error"));
                            }
                        }
                    });
                }
                return _cmdImportFromMRemoteNgCsv;
            }
        }



        private RelayCommand _cmdPuttyThemeCustomize;
        public RelayCommand CmdPuttyThemeCustomize
        {
            get
            {
                if (_cmdPuttyThemeCustomize == null)
                {
                    _cmdPuttyThemeCustomize = new RelayCommand((o) =>
                    {
                        var puttyTheme = SystemConfig.Theme.SelectedPuttyTheme;
                        if (!Directory.Exists(PuttyColorThemes.ThemeRegFileFolder))
                            Directory.CreateDirectory(PuttyColorThemes.ThemeRegFileFolder);
                        var fi = puttyTheme.ToRegFile(Path.Combine(PuttyColorThemes.ThemeRegFileFolder, SystemConfig.Theme.PuttyThemeName + ".reg"));
                        if (fi != null)
                            System.Diagnostics.Process.Start("notepad.exe", fi.FullName);
                    });
                }
                return _cmdPuttyThemeCustomize;
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
