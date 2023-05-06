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
using _1RM.Utils;
using _1RM.View.Host;
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
                // delete tmp rdp file, ETA 10s
                Task.Factory.StartNew(() =>
                {
                    try
                    {
                        Thread.Sleep(1000 * 10);
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
                    p.Start();
                    string admin = rdp.IsAdministrativePurposes == true ? " /admin " : "";
                    p.StandardInput.WriteLine($"mstsc {admin} \"" + rdpFile + "\"");
                    p.StandardInput.WriteLine("exit");
                }
                catch (Exception e)
                {
                    MsAppCenterHelper.Error(e);
                }
            }
        }

        private static void ConnectRemoteApp(in RdpApp remoteApp)
        {
            var tmp = Path.GetTempPath();
            var rdpFileName = $"{remoteApp.DisplayName}_{remoteApp.Port}_{remoteApp.UserName}";
            var invalid = new string(Path.GetInvalidFileNameChars()) +
                          new string(Path.GetInvalidPathChars());
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
                p.Start();
                p.StandardInput.WriteLine($"mstsc \"" + rdpFile + "\"");
                p.StandardInput.WriteLine("exit");

                // delete tmp rdp file, ETA 10s
                var t = new Task(() =>
                {
                    try
                    {
                        Thread.Sleep(1000 * 10);
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
            host.OnClosed += OnRequestCloseConnection;
            host.OnFullScreen2Window += this.MoveSessionToTabWindow;
            this.MoveSessionToFullScreen(host.ConnectionId);
            host.Conn();
            SimpleLogHelper.Debug($@"Start Conn: {server.DisplayName}({server.GetHashCode()}) by host({host.GetHashCode()}) with full");
        }

        private void ConnectWithTab(in ProtocolBase protocol, in Runner runner, string assignTabToken)
        {
            var tab = this.GetOrCreateTabWindow(assignTabToken);
            var host = runner.GetHost(protocol, tab);

            string displayName = protocol.DisplayName;
            Execute.OnUIThreadSync(() =>
            {
                tab ??= this.GetOrCreateTabWindow(assignTabToken);
                if (tab == null) return;
                if (tab.IsClosing) return;
                tab.Show();

                // get display area size for host
                Debug.Assert(!_connectionId2Hosts.ContainsKey(host.ConnectionId));
                host.OnClosed += OnRequestCloseConnection;
                host.OnFullScreen2Window += this.MoveSessionToTabWindow;
                tab.GetViewModel().AddItem(new TabItemViewModel(host, displayName));
                _connectionId2Hosts.TryAdd(host.ConnectionId, host);
                host.Conn();
                tab.WindowState = tab.WindowState == WindowState.Minimized ? WindowState.Normal : tab.WindowState;
                tab.Activate();
            });
        }
        #endregion

        private void Connect(in ProtocolBase protocol, in string fromView, in string assignTabToken = "", in string assignRunnerName = "", in string assignCredentialName = "")
        {
            // if is OnlyOneInstance server and it is connected now, activate it and return.
            if (this.ActivateOrReConnIfServerSessionIsOpened(protocol))
                return;

            #region prepare

            // trace source view
            if (string.IsNullOrEmpty(fromView) == false)
                MsAppCenterHelper.TraceSessionOpen(protocol.Protocol, fromView);

            // recode connect count
            _configurationService.Engagement.ConnectCount++;
            _configurationService.Save();


            // update the last conn time
            {
                var vmServer = _appData.GetItemById(protocol.DataSource?.DataSourceName ?? "", protocol.Id);
                vmServer?.UpdateConnectTime();
            }
            #endregion

            // clone and decrypt!
            var protocolClone = protocol.Clone();
            protocolClone.ConnectPreprocess();

            // apply alternate credential
            if (false == ApplyAlternateCredential(ref protocolClone, assignCredentialName))
            {
                return;
            }

            // run script before connected
            {
                int code = protocolClone.RunScriptBeforeConnect();
                if (0 != code)
                {
                    MessageBoxHelper.ErrorAlert($"Script ExitCode = {code}, connection abort!");
                    return;
                }
            }

            // dispatch for specified protocol
            if (protocolClone is RdpApp rdpApp)
            {
                ConnectRemoteApp(rdpApp);
                return;
            }
            else if (protocolClone is RDP rdp)
            {
                if (rdp.IsNeedRunWithMstsc())
                {
                    ConnectRdpByMstsc(rdp);
                    return;
                }
                // rdp full screen
                if (protocolClone.IsThisTimeConnWithFullScreen())
                {
                    this.ConnectWithFullScreen(protocolClone, new InternalDefaultRunner(RDP.ProtocolName));
                    return;
                }
            }
            else if (protocolClone is SSH { OpenSftpOnConnected: true } ssh)
            {
                // open SFTP when SSH is connected.
                var tmpRunner = ProtocolHelper.GetRunner(IoC.Get<ProtocolConfigurationService>(), protocolClone, SFTP.ProtocolName);
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
                this.ConnectWithTab(sftp, tmpRunner, assignTabToken);
            }
            else if (protocolClone is LocalApp { RunWithHosting: false } localApp)
            {
                var tmp = WinCmdRunner.CheckFileExistsAndFullName(localApp.ExePath);
                if (tmp.Item1)
                {
                    Process.Start(tmp.Item2, localApp.Arguments);
                }
                return;
            }


            var runner = ProtocolHelper.GetRunner(IoC.Get<ProtocolConfigurationService>(), protocolClone, protocolClone.Protocol, assignRunnerName)!;
            if (runner.IsRunWithoutHosting())
            {
                runner.RunWithoutHosting(protocol);
            }
            else
            {
                ConnectWithTab(protocolClone, runner, assignTabToken);
            }
        }
    }
}