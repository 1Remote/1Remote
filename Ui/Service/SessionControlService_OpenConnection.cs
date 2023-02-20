using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using _1RM.Model;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Model.ProtocolRunner;
using _1RM.Model.ProtocolRunner.Default;
using _1RM.Utils;
using _1RM.View;
using _1RM.View.Host;
using _1RM.View.Host.ProtocolHosts;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Stylet;
using ProtocolHostStatus = _1RM.View.Host.ProtocolHosts.ProtocolHostStatus;
using _1RM.Service.DataSource;
using System.Collections.Generic;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using System.Text;
using System.Runtime.Intrinsics.X86;
using Google.Protobuf.WellKnownTypes;

namespace _1RM.Service
{
    public partial class SessionControlService
    {
        #region Open Via Different
        private void ConnectRdpByMstsc(in RDP rdp)
        {
            var tmp = Path.GetTempPath();
            var rdpFileName = $"{rdp.DisplayName}_{rdp.Port}_{MD5Helper.GetMd5Hash16BitString(rdp.UserName)}";
            var invalid = new string(Path.GetInvalidFileNameChars()) +
                          new string(Path.GetInvalidPathChars());
            rdpFileName = invalid.Aggregate(rdpFileName, (current, c) => current.Replace(c.ToString(), ""));
            var rdpFile = Path.Combine(tmp, rdpFileName + ".rdp");

            // write a .rdp file for mstsc.exe
            {
                File.WriteAllText(rdpFile, rdp.ToRdpConfig().ToString());
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
            }
        }

        private void ConnectRemoteApp(in RdpApp remoteApp)
        {
            var tmp = Path.GetTempPath();
            var rdpFileName = $"{remoteApp.DisplayName}_{remoteApp.Port}_{remoteApp.UserName}";
            var invalid = new string(Path.GetInvalidFileNameChars()) +
                          new string(Path.GetInvalidPathChars());
            rdpFileName = invalid.Aggregate(rdpFileName, (current, c) => current.Replace(c.ToString(), ""));
            var rdpFile = Path.Combine(tmp, rdpFileName + ".rdp");

            // write a .rdp file for mstsc.exe
            {
                File.WriteAllText(rdpFile, remoteApp.ToRdpConfig().ToString());
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
                if (tab == null)
                    return;

                Execute.OnUIThreadSync(() =>
                {
                    tab.Show();
                });

                // get display area size for host
                Debug.Assert(!_connectionId2Hosts.ContainsKey(host.ConnectionId));
                host.OnClosed += OnRequestCloseConnection;
                host.OnFullScreen2Window += this.MoveSessionToTabWindow;
                tab.GetViewModel().AddItem(new TabItemViewModel(host, displayName));
                _connectionId2Hosts.TryAdd(host.ConnectionId, host);
                host.Conn();
                if (tab.WindowState == WindowState.Minimized)
                {
                    tab.WindowState = WindowState.Normal;
                }
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
                var vmServer = _appData.GetItemById(protocol.DataSourceName, protocol.Id);
                vmServer?.UpdateConnectTime();
            }
            #endregion

            // clone and decrypt!
            var protocolClone = protocol.Clone();
            protocolClone.ConnectPreprocess();

            // apply alternate credential
            ApplyAlternateCredential(ref protocolClone, assignCredentialName);

            // run script before connected
            protocolClone.RunScriptBeforeConnect();

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
                    this.ConnectRdpByMstsc(rdp);
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
                Debug.Assert(tmpRunner != null);
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