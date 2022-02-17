using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using PRM.Core.Protocol;
using PRM.Core.Protocol.Runner.Default;

namespace PRM.Core.Model
{
    public class ActionForServer : NotifyPropertyChangedBase
    {
        public string ActionName { get; }

        public int Id { get; }

        private readonly Action<int> _action;

        public void Run()
        {
            _action?.Invoke(Id);
        }

        public ActionForServer(int id, string actionName, Action<int> action)
        {
            Id = id;
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
                        id: server.Id,
                        actionName: context.LanguageService.Translate("Connect (New window)"),
                        action: (id) => { GlobalEventHelper.OnRequestServerConnect?.Invoke(id, DateTime.Now.Ticks.ToString()); }
                    ));

                // external runners
                if (context.ProtocolConfigurationService.ProtocolConfigs.ContainsKey(server.Protocol)
                && context.ProtocolConfigurationService.ProtocolConfigs[server.Protocol].Runners.Count > 1)
                {
                    actions.Add(new ActionForServer(server.Id, context.LanguageService.Translate("Connect") + $" (Internal)", (id) => { GlobalEventHelper.OnRequestServerConnect?.Invoke(id, assignRunnerName: context.ProtocolConfigurationService.ProtocolConfigs[server.Protocol].Runners.First().Name); }));
                    foreach (var runner in context.ProtocolConfigurationService.ProtocolConfigs[server.Protocol].Runners)
                    {
                        if (runner is InternalDefaultRunner) continue;
                        actions.Add(new ActionForServer(server.Id, context.LanguageService.Translate("Connect") + $" ({runner.Name})", (id) => { GlobalEventHelper.OnRequestServerConnect?.Invoke(id, assignRunnerName: runner.Name); }));
                    }
                }
                else
                {
                    actions.Add(new ActionForServer(server.Id, context.LanguageService.Translate("Connect"), (id) => { GlobalEventHelper.OnRequestServerConnect?.Invoke(id); }));
                }

                actions.Add(new ActionForServer(server.Id, context.LanguageService.Translate("Edit"), (id) => { GlobalEventHelper.OnRequestGoToServerEditPage?.Invoke(id, false, false); }));
                actions.Add(new ActionForServer(server.Id, context.LanguageService.Translate("server_card_operate_duplicate"), (id) => { GlobalEventHelper.OnRequestGoToServerEditPage?.Invoke(id, true, false); }));
            };
            if (server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortBase)))
            {
                actions.Add(new ActionForServer(
                    server.Id,
                    context.LanguageService.Translate("server_card_operate_copy_address"),
                    (id) =>
                    {
                        var pb = context.AppData.VmItemList.First(x => x.Server.Id == id);
                        if (pb.Server is ProtocolServerWithAddrPortBase tmp)
                            try
                            {
                                Clipboard.SetText($"{tmp.Address}:{tmp.GetPort()}");
                            }
                            catch (Exception)
                            {
                                // ignored
                            }
                    }));
            }
            if (server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortUserPwdBase)))
            {
                actions.Add(new ActionForServer(server.Id,
                    actionName: context.LanguageService.Translate("server_card_operate_copy_username"),
                    action: (id) =>
                   {
                       var pb = context.AppData.VmItemList.First(x => x.Server.Id == id);
                       if (pb.Server is ProtocolServerWithAddrPortUserPwdBase tmp)
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
            if (server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortUserPwdBase)))
            {
                actions.Add(new ActionForServer(server.Id,
                    actionName: context.LanguageService.Translate("server_card_operate_copy_password"),
                    action: (id) =>
                    {
                        var pb = context.AppData.VmItemList.First(x => x.Server.Id == id);
                        if (pb.Server is ProtocolServerWithAddrPortUserPwdBase tmp)
                            try
                            {
                                Clipboard.SetText(context.DataService.DecryptOrReturnOriginalString(tmp.Password));
                            }
                            catch (Exception)
                            {
                                // ignored
                            }
                    }));
            }

            actions.Add(new ActionForServer(server.Id, context.LanguageService.Translate("Delete"), (id) =>
            {
                GlobalEventHelper.OnRequestDeleteServer?.Invoke(id);
            }));

            #endregion Build Actions

            return actions;
        }
    }
}
