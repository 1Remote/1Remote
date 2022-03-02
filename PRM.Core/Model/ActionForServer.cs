using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using PRM.Core.Protocol;
using PRM.Core.Protocol.Runner;
using PRM.Core.Protocol.Runner.Default;

namespace PRM.Core.Model
{
    public class ActionForServer : NotifyPropertyChangedBase
    {
        public string ActionName { get; }


        private readonly Action _action;

        public void Run()
        {
            _action?.Invoke();
        }

        public ActionForServer(string actionName, Action action)
        {
            ActionName = actionName;
            _action = action;
        }
    }

    public static class ActionForServerEx
    {
        public static List<ActionForServer> GetActions(this ProtocolServerBase server, PrmContext context, int tabWindowCount)
        {
            #region Build Actions
            var actions = new List<ActionForServer>();
            {
                if (tabWindowCount > 0)
                    actions.Add(new ActionForServer(
                        actionName: context.LanguageService.Translate("Connect (New window)"),
                        action: () => { GlobalEventHelper.OnRequestServerConnect?.Invoke(server.Id, DateTime.Now.Ticks.ToString()); }
                    ));

                // external runners
                if (context.ProtocolConfigurationService.ProtocolConfigs.ContainsKey(server.Protocol)
                && context.ProtocolConfigurationService.ProtocolConfigs[server.Protocol].Runners.Count > 1)
                {
                    actions.Add(new ActionForServer(context.LanguageService.Translate("Connect") + $" (Internal)", () => { GlobalEventHelper.OnRequestServerConnect?.Invoke(server.Id, assignRunnerName: context.ProtocolConfigurationService.ProtocolConfigs[server.Protocol].Runners.First().Name); }));
                    foreach (var runner in context.ProtocolConfigurationService.ProtocolConfigs[server.Protocol].Runners)
                    {
                        if (runner is InternalDefaultRunner) continue;
                        if (runner is ExternalRunner er && er.IsExeExisted == false) continue;
                        actions.Add(new ActionForServer(context.LanguageService.Translate("Connect") + $" ({runner.Name})", () => { GlobalEventHelper.OnRequestServerConnect?.Invoke(server.Id, assignRunnerName: runner.Name); }));
                    }
                }
                else
                {
                    actions.Add(new ActionForServer(context.LanguageService.Translate("Connect"), () => { GlobalEventHelper.OnRequestServerConnect?.Invoke(server.Id); }));
                }

                actions.Add(new ActionForServer(context.LanguageService.Translate("Edit"), () => { GlobalEventHelper.OnRequestGoToServerEditPage?.Invoke(server.Id, false, false); }));
                actions.Add(new ActionForServer(context.LanguageService.Translate("server_card_operate_duplicate"), () => { GlobalEventHelper.OnRequestGoToServerEditPage?.Invoke(server.Id, true, false); }));
            };
            if (server is ProtocolServerWithAddrPortBase protocolServerWithAddrPortBase)
            {
                actions.Add(new ActionForServer(context.LanguageService.Translate("server_card_operate_copy_address"),
                    () =>
                    {
                        try
                        {
                            Clipboard.SetText($"{protocolServerWithAddrPortBase.Address}:{protocolServerWithAddrPortBase.GetPort()}");
                        }
                        catch (Exception)
                        {
                            // ignored
                        }
                    }));
            }
            if (server is ProtocolServerWithAddrPortUserPwdBase tmp)
            {
                actions.Add(new ActionForServer(context.LanguageService.Translate("server_card_operate_copy_username"),
                    () =>
                   {
                       try
                       {
                           Clipboard.SetText(tmp.UserName);
                       }
                       catch (Exception)
                       {
                           // ignored
                       }
                   }));
            }
            if (server is ProtocolServerWithAddrPortUserPwdBase protocolServerWithAddrPortUserPwdBase)
            {
                actions.Add(new ActionForServer(context.LanguageService.Translate("server_card_operate_copy_password"),
                    action: () =>
                    {
                        try
                        {
                            Clipboard.SetText(context.DataService.DecryptOrReturnOriginalString(protocolServerWithAddrPortUserPwdBase.Password));
                        }
                        catch (Exception)
                        {
                            // ignored
                        }
                    }));
            }

            actions.Add(new ActionForServer(context.LanguageService.Translate("Delete"), () =>
            {
                GlobalEventHelper.OnRequestDeleteServer?.Invoke(server.Id);
            }));

            #endregion Build Actions

            return actions;
        }
    }
}
