using System;
using System.Collections.Generic;
using System.Windows;
using _1RM.Model.Protocol.Base;
using _1RM.Model.ProtocolRunner;
using _1RM.Model.ProtocolRunner.Default;
using _1RM.Service;
using _1RM.Utils;
using _1RM.Utils.Windows;
using _1RM.View;
using Shawn.Utils.Interface;

namespace _1RM.Model;

public static class ProtocolActionHelper
{
    public static List<ProtocolAction> GetActions(this ProtocolBaseViewModel vm)
    {
        var server = vm.Server;
        bool writable = server.DataSource?.IsWritable != false;
        #region Build Actions
        var actions = new List<ProtocolAction>();
        {
            if (IoC.Get<SessionControlService>().TabWindowCount > 0)
            {
                actions.Add(new ProtocolAction(
                    actionName: IoC.Get<ILanguageService>().Translate("Connect (New window)"),
                    action: () => { GlobalEventHelper.OnRequestServerConnect?.Invoke(server, fromView: $"{nameof(LauncherWindowView)} - Action - New window", assignTabToken: DateTime.Now.Ticks.ToString()); }
                ));
            }

            if (server is ProtocolBaseWithAddressPortUserPwd { AlternateCredentials.Count: > 0 } protocol)
            {
                foreach (var credential in protocol.AlternateCredentials)
                {
                    actions.Add(new ProtocolAction(
                        actionName: IoC.Get<ILanguageService>().Translate("Connect") + $" ({IoC.Get<ILanguageService>().Translate("with alternative")} `{credential.Name}`)",
                        action: () => { GlobalEventHelper.OnRequestServerConnect?.Invoke(server, fromView: $"{nameof(LauncherWindowView)} - Action - AlternateCredentials", assignCredentialName: credential.Name); }
                    ));
                }
            }

            // external runners
            var protocolConfigurationService = IoC.Get<ProtocolConfigurationService>();
            if (protocolConfigurationService.ProtocolConfigs.ContainsKey(server.Protocol)
                && protocolConfigurationService.ProtocolConfigs[server.Protocol].Runners.Count > 1)
            {
                //actions.Add(new ProtocolAction(IoC.Get<ILanguageService>().Translate("Connect") + $" (Internal)", () => { GlobalEventHelper.OnRequestServerConnect?.Invoke(server.Id, assignRunnerName: protocolConfigurationService.ProtocolConfigs[server.Protocol].Runners.First().Name, fromView: nameof(LauncherWindowView)); }));
                foreach (var runner in protocolConfigurationService.ProtocolConfigs[server.Protocol].Runners)
                {
                    if (runner is InternalDefaultRunner) continue;
                    if (runner is ExternalRunner { IsExeExisted: false }) continue;
                    actions.Add(new ProtocolAction(IoC.Get<ILanguageService>().Translate("Connect") + $" (via {runner.Name})", () =>
                    {
                        GlobalEventHelper.OnRequestServerConnect?.Invoke(server, fromView: $"{nameof(LauncherWindowView)} - Action - {runner.Name}", assignRunnerName: runner.Name);
                    }));
                }
            }

            if (writable)
            {
                actions.Add(new ProtocolAction(IoC.Get<ILanguageService>().Translate("Edit"), () =>
                {
                    if (GlobalEventHelper.OnRequestGoToServerEditPage == null)
                        IoC.Get<MainWindowViewModel>()?.ShowMe();
                    GlobalEventHelper.OnRequestGoToServerEditPage?.Invoke(server: server, showAnimation: false);
                }));
                actions.Add(new ProtocolAction(IoC.Get<ILanguageService>().Translate("server_card_operate_duplicate"), () =>
                {
                    if (GlobalEventHelper.OnRequestGoToServerEditPage == null)
                        IoC.Get<MainWindowViewModel>()?.ShowMe();
                    GlobalEventHelper.OnRequestGoToServerDuplicatePage?.Invoke(server: server, showAnimation: false);
                }));
            }
        };


        if (server is ProtocolBaseWithAddressPort protocolServerWithAddrPortBase)
        {
            actions.Add(new ProtocolAction(IoC.Get<ILanguageService>().Translate("server_card_operate_copy_address"),
                () =>
                {
                    try
                    {
                        Clipboard.SetDataObject($"{protocolServerWithAddrPortBase.Address}:{protocolServerWithAddrPortBase.GetPort()}");
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                }));
        }


        if (writable)
        {
            if (server is ProtocolBaseWithAddressPortUserPwd tmp)
            {
                actions.Add(new ProtocolAction(IoC.Get<ILanguageService>().Translate("server_card_operate_copy_username"),
                    () =>
                    {
                        try
                        {
                            Clipboard.SetDataObject(tmp.UserName);
                        }
                        catch (Exception)
                        {
                            // ignored
                        }
                    }));
            }

            if (server is ProtocolBaseWithAddressPortUserPwd protocolServerWithAddrPortUserPwdBase)
            {
                actions.Add(new ProtocolAction(IoC.Get<ILanguageService>().Translate("server_card_operate_copy_password"),
                    action: async () =>
                    {
                        if (await WindowsHelloHelper.VerifyAsyncUi() == true)
                        {
                            try
                            {
                                Clipboard.SetDataObject(UnSafeStringEncipher.DecryptOrReturnOriginalString(protocolServerWithAddrPortUserPwdBase.Password));
                            }
                            catch (Exception)
                            {
                                // ignored
                            }
                        }
                    }));
            }
        }


        actions.Add(new ProtocolAction(IoC.Get<LanguageService>().Translate("Create desktop shortcut"), () =>
            {
                var iconPath= AppStartupHelper.MakeIcon(server.Id, server.IconImg);
                AppStartupHelper.InstallDesktopShortcutByUlid(server.DisplayName, new[] { server.Id }, iconPath);
            }));

        #endregion Build Actions

        return actions;
    }
}