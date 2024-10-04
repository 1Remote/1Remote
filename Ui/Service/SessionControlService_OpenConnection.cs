using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Model.ProtocolRunner;
using _1RM.Model.ProtocolRunner.Default;
using _1RM.Service.Locality;
using _1RM.Utils;
using _1RM.View.Editor;
using _1RM.View.Host;
using _1RM.View.Host.ProtocolHosts;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Stylet;

namespace _1RM.Service
{
    public partial class SessionControlService
    {
        #region Open Via Different
        private static void ConnectRdpByMstsc(in RDP rdp)
        {
            var tmp = Path.GetTempPath();
            var rdpFileName = $"{rdp.DisplayName}_{rdp.Port}_{MD5Helper.GetMd5Hash16BitString(rdp.UserName)}";
            var invalid = new string(Path.GetInvalidFileNameChars()) +
                          new string(Path.GetInvalidPathChars());
            rdpFileName = invalid.Aggregate(rdpFileName, (current, c) => current.Replace(c.ToString(), ""));
            var rdpFile = Path.Combine(tmp, rdpFileName + ".rdp");
            var text = rdp.ToRdpConfig().ToString();

            // write a .rdp file for mstsc.exe
            if (RetryHelper.Try(() =>
                {
                    File.WriteAllText(rdpFile, text);
                }, actionOnError: exception => MsAppCenterHelper.Error(exception)))
            {
                // delete tmp rdp file, ETA 30s
                Task.Factory.StartNew(() =>
                {
                    try
                    {
                        Thread.Sleep(1000 * 30);
                        if (File.Exists(rdpFile))
                            File.Delete(rdpFile);
                    }
                    catch (Exception e)
                    {
                        SimpleLogHelper.Error(e);
                    }
                });

                try
                {
                    string admin = rdp.IsAdministrativePurposes == true ? " /admin " : "";
                    var p = new Process
                    {
                        StartInfo =
                        {
                            FileName = "mstsc.exe",
                            Arguments = $"{admin} \"" + rdpFile + "\""
                        },
                    };
                    var protocol = rdp;
                    AddUnHostingWatch(p, protocol);
                    p.EnableRaisingEvents = true;
                    p.Start();
                }
                catch (Exception e)
                {
                    MsAppCenterHelper.Error(e);
                    MessageBoxHelper.ErrorAlert(e.Message + "\r\n while Run mstsc.exe");
                }
            }
        }

        private static void ConnectRemoteApp(in RdpApp remoteApp)
        {
            var tmp = Path.GetTempPath();
            var rdpFileName = $"{remoteApp.DisplayName}_{remoteApp.Port}_{remoteApp.UserName}";
            var invalid = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            rdpFileName = invalid.Aggregate(rdpFileName, (current, c) => current.Replace(c.ToString(), ""));
            var rdpFile = Path.Combine(tmp, rdpFileName + ".rdp");

            // write a .rdp file for mstsc.exet.Replace(c.ToString(), ""));
            var text = remoteApp.ToRdpConfig().ToString();
            // write a .rdp file for mstsc.exe
            if (RetryHelper.Try(() =>
            {
                File.WriteAllText(rdpFile, text);
            }, actionOnError: exception => MsAppCenterHelper.Error(exception)))
            {
                var p = new Process
                {
                    StartInfo =
                    {
                        FileName = "cmd.exe",
                        UseShellExecute = false,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };
                var protocol = remoteApp;
                AddUnHostingWatch(p, protocol);
                p.Start();
                p.StandardInput.WriteLine($"mstsc \"" + rdpFile + "\"");
                p.StandardInput.WriteLine("exit");

                // delete tmp rdp file, ETA 10s
                var t = new Task(async () =>
                {
                    try
                    {
                        await Task.Delay(1000 * 10);
                        if (File.Exists(rdpFile))
                            File.Delete(rdpFile);
                    }
                    catch (Exception e)
                    {
                        SimpleLogHelper.Error(e);
                    }
                });
                t.Start();
            }
        }

        private void ConnectWithFullScreen(in ProtocolBase server, in Runner runner)
        {
            // fullscreen normally
            var host = runner.GetHost(server);
            if (host == null)
                return;

            Debug.Assert(!_connectionId2Hosts.ContainsKey(host.ConnectionId));
            _connectionId2Hosts.TryAdd(host.ConnectionId, host);
            host.OnProtocolClosed += OnRequestCloseConnection;
            host.OnFullScreen2Window += this.MoveSessionToTabWindow;
            this.MoveSessionToFullScreen(host.ConnectionId);
            host.Conn();
            SimpleLogHelper.Debug($@"Start Conn: {server.DisplayName}({server.GetHashCode()}) by host({host.GetHashCode()}) with full");
        }

        public string ConnectWithTab(in ProtocolBase protocol, in Runner runner, string assignTabToken)
        {
            TabWindowView? tab = null;
            ProtocolBase p = protocol;
            Runner r = runner;
            Execute.OnUIThreadSync(() =>
            {
                lock (_dictLock)
                {
                    tab = this.GetOrCreateTabWindow(assignTabToken);
                    if (tab == null) return;
                    if (tab.IsClosing) return;
                    tab.Show();

                    var host = r.GetHost(p, tab) as HostBase;
                    if (host == null) return;
                    // get display area size for host
                    Debug.Assert(!_connectionId2Hosts.ContainsKey(host.ConnectionId));
                    host.OnProtocolClosed += OnRequestCloseConnection;
                    host.OnFullScreen2Window += this.MoveSessionToTabWindow;
                    tab.GetViewModel().AddItem(new TabItemViewModel(host, p.DisplayName));
                    _connectionId2Hosts.TryAdd(host.ConnectionId, host);
                    host.Conn();
                    tab.WindowState = tab.WindowState == WindowState.Minimized ? WindowState.Normal : tab.WindowState;
                    tab.Activate();
                }
            });
            return tab?.Token ?? "";
        }
        #endregion

        private async Task<string> Connect(ProtocolBase protocol, string fromView, string assignTabToken = "", string assignRunnerName = "", string assignCredentialName = "")
        {

            #region prepare

            // trace source view
            if (string.IsNullOrEmpty(fromView) == false)
                MsAppCenterHelper.TraceSessionOpen(protocol.Protocol, fromView);

            // connect count save to config
            _configurationService.Engagement.ConnectCount++;
            _configurationService.Save();


            // update the last conn time
            {
                var vmServer = _appData.GetItemById(protocol.DataSource?.DataSourceName ?? "", protocol.Id);
                vmServer?.ConnectTimeAddOrUpdate();
                if (IoC.Get<ConfigurationService>().General.ShowRecentlySessionInTray)
                    IoC.Get<TaskTrayService>().ReloadTaskTrayContextMenu();
            }

            // clone and decrypt!
            var protocolClone = protocol.Clone();
            protocolClone.ConnectPreprocess();

            // apply alternate credential
            {
                if (protocolClone is ProtocolBaseWithAddressPort p)
                {
                    var c = await GetCredential(p, assignCredentialName);
                    if (c == null)
                    {
                        return "";
                    }

                    p.SetCredential(c);
                    if (string.IsNullOrEmpty(assignCredentialName) == false)
                        p.DisplayName += $" ({c.Name})";
                }
            }



            // check if need to input password
            if (protocolClone is ProtocolBaseWithAddressPortUserPwd { AskPasswordWhenConnect: true } pb)
            {
                bool flag = false;
                Execute.OnUIThreadSync(() =>
                {
                    var pwdDlg = new PasswordPopupDialogViewModel(protocolClone is SSH or SFTP);
                    pwdDlg.Title = $"[{pb.ProtocolDisplayName}]({pb.DisplayName}) -> {pb.Address}:{pb.Port}";
                    pwdDlg.UserName = pb.UserName;
                    if (pb.UsePrivateKeyForConnect == true)
                    {
                        pwdDlg.CanUsePrivateKeyForConnect = true;
                        pwdDlg.UsePrivateKeyForConnect = true;
                        pwdDlg.PrivateKey = pb.PrivateKey;
                    }
                    else
                    {
                        pwdDlg.UsePrivateKeyForConnect = false;
                        pwdDlg.Password = pb.Password;
                    }

                    if (IoC.Get<IWindowManager>().ShowDialog(pwdDlg) == true)
                    {
                        flag = true;
                        pb.UserName = pwdDlg.UserName;
                        if (pwdDlg.UsePrivateKeyForConnect)
                        {
                            pb.UsePrivateKeyForConnect = true;
                            pb.Password = "";
                            pb.PrivateKey = pwdDlg.PrivateKey;
                        }
                        else
                        {
                            pb.UsePrivateKeyForConnect = false;
                            pb.PrivateKey = "";
                            pb.Password = pwdDlg.Password;
                        }
                        pwdDlg.PrivateKey = "";
                        pwdDlg.Password = "";
                    }
                    else
                    {
                        pwdDlg.Password = "";
                    }
                });

                if (flag == false)
                {
                    return "";
                }
            }

            #endregion


            // if is OnlyOneInstance server and it is connected now, activate it and return.
            if (this.ActivateOrReConnIfServerSessionIsOpened(protocolClone))
                return "";


            // run script before connected
            {
                int code = protocolClone.RunScriptBeforeConnect();
                if (0 != code)
                {
                    MessageBoxHelper.ErrorAlert($"Script ExitCode = {code}, connection abort!");
                    return "";
                }
            }

            // dispatch for specified protocol
            if (protocolClone is RdpApp rdpApp)
            {
                ConnectRemoteApp(rdpApp);
                return "";
            }
            else if (protocolClone is RDP rdp)
            {
                if (rdp.IsNeedRunWithMstsc())
                {
                    ConnectRdpByMstsc(rdp);
                    return "";
                }
                // rdp full screen
                if (protocolClone.IsThisTimeConnWithFullScreen())
                {
                    this.ConnectWithFullScreen(protocolClone, new InternalDefaultRunner(RDP.ProtocolName));
                    return "";
                }
            }
            else if (protocolClone is SSH { OpenSftpOnConnected: true } ssh)
            {
                // open SFTP when SSH is connected.
                var tmpRunner = RunnerHelper.GetRunner(IoC.Get<ProtocolConfigurationService>(), protocolClone, SFTP.ProtocolName);
                var sftp = new SFTP
                {
                    ColorHex = ssh.ColorHex,
                    IconBase64 = ssh.IconBase64,
                    DisplayName = ssh.DisplayName + " (SFTP)",
                    Address = ssh.Address,
                    Port = ssh.Port,
                    UserName = ssh.UserName,
                    Password = ssh.Password,
                    PrivateKey = ssh.PrivateKey
                };
                assignTabToken = await Connect(sftp, fromView, assignTabToken, tmpRunner.Name, assignCredentialName);
            }
            else if (protocolClone is LocalApp { RunWithHosting: false } localApp)
            {
                var tmp = WinCmdRunner.CheckFileExistsAndFullName(localApp.GetExePath());
                if (tmp.Item1)
                {
                    var process = Process.Start(tmp.Item2, localApp.GetArguments(false));
                    AddUnHostingWatch(process, localApp);
                }
                return "";
            }


            string tabToken = "";
            var runner = RunnerHelper.GetRunner(IoC.Get<ProtocolConfigurationService>(), protocolClone, protocolClone.Protocol, assignRunnerName)!;
            if (runner.IsRunWithoutHosting())
            {
                runner.RunWithoutHosting(protocolClone);
            }
            else
            {
                tabToken = ConnectWithTab(protocolClone, runner, assignTabToken);
            }
            return tabToken;
        }
    }
}