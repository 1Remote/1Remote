using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using _1RM.Model.Protocol.Base;
using _1RM.Model.ProtocolRunner;
using _1RM.Model.ProtocolRunner.Default;
using _1RM.Service;
using _1RM.View;
using Shawn.Utils;
using Shawn.Utils.Interface;

namespace _1RM.Model
{
    public class ProtocolAction : NotifyPropertyChangedBase
    {
        public string ActionName { get; }

        private readonly Action _action;

        public void Run()
        {
            _action?.Invoke();
        }

        public ProtocolAction(string actionName, Action action)
        {
            ActionName = actionName;
            _action = action;
        }
    }

    public static class ProtocolActionHelper
    {
        public static List<ProtocolAction> GetActions(this ProtocolBase server)
        {
            #region Build Actions
            var actions = new List<ProtocolAction>();
            {
                if (IoC.Get<SessionControlService>().TabWindowCount > 0)
                    actions.Add(new ProtocolAction(
                        actionName: IoC.Get<ILanguageService>().Translate("Connect (New window)"),
                        action: () => { GlobalEventHelper.OnRequestServerConnect?.Invoke(server.Id, DateTime.Now.Ticks.ToString()); }
                    ));

                // external runners
                var protocolConfigurationService = IoC.Get<ProtocolConfigurationService>();
                if (protocolConfigurationService.ProtocolConfigs.ContainsKey(server.Protocol)
                    && protocolConfigurationService.ProtocolConfigs[server.Protocol].Runners.Count > 1)
                {
                    actions.Add(new ProtocolAction(IoC.Get<ILanguageService>().Translate("Connect") + $" (Internal)", () => { GlobalEventHelper.OnRequestServerConnect?.Invoke(server.Id, assignRunnerName: protocolConfigurationService.ProtocolConfigs[server.Protocol].Runners.First().Name); }));
                    foreach (var runner in protocolConfigurationService.ProtocolConfigs[server.Protocol].Runners)
                    {
                        if (runner is InternalDefaultRunner) continue;
                        if (runner is ExternalRunner er && er.IsExeExisted == false) continue;
                        actions.Add(new ProtocolAction(IoC.Get<ILanguageService>().Translate("Connect") + $" ({runner.Name})", () => { GlobalEventHelper.OnRequestServerConnect?.Invoke(server.Id, assignRunnerName: runner.Name); }));
                    }
                }
                else
                {
                    actions.Add(new ProtocolAction(IoC.Get<ILanguageService>().Translate("Connect"), () => { GlobalEventHelper.OnRequestServerConnect?.Invoke(server.Id); }));
                }

                actions.Add(new ProtocolAction(IoC.Get<ILanguageService>().Translate("Edit"), () =>
                {
                    if(GlobalEventHelper.OnRequestGoToServerEditPage == null)
                        IoC.Get<MainWindowViewModel>()?.ShowMe();
                    GlobalEventHelper.OnRequestGoToServerEditPage?.Invoke(server.Id, false, false);
                }));
                actions.Add(new ProtocolAction(IoC.Get<ILanguageService>().Translate("server_card_operate_duplicate"), () =>
                {
                    if (GlobalEventHelper.OnRequestGoToServerEditPage == null)
                        IoC.Get<MainWindowViewModel>()?.ShowMe();
                    GlobalEventHelper.OnRequestGoToServerEditPage?.Invoke(server.Id, true, false);
                }));
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
                    action: () =>
                    {
                        try
                        {
                            Clipboard.SetDataObject(IoC.Get<DataService>().DecryptOrReturnOriginalString(protocolServerWithAddrPortUserPwdBase.Password));
                        }
                        catch (Exception)
                        {
                            // ignored
                        }
                    }));
            }

            actions.Add(new ProtocolAction(IoC.Get<ILanguageService>().Translate("Delete"), () =>
            {
                GlobalEventHelper.OnRequestDeleteServer?.Invoke(server.Id);
            }));

            #endregion Build Actions

            return actions;
        }
    }
}
