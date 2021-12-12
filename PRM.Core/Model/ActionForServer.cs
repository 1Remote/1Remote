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
        private string _actionName = "";

        public string ActionName
        {
            get => _actionName;
            set => SetAndNotifyIfChanged(ref _actionName, value);
        }

        public Action<int> Run;

    }

    public static class ActionForServerEx
    {
        public static List<ActionForServer> GetActions(this ProtocolServerBase server, PrmContext context, int tabWindowCount)
        {
            #region Build Actions
            var actions = new List<ActionForServer>();
            {
                if (tabWindowCount > 0)
                    actions.Add(new ActionForServer()
                    {
                        ActionName = context.LanguageService.Translate("Connect (New window)"),
                        Run = (id) => { GlobalEventHelper.OnRequestServerConnect?.Invoke(id, DateTime.Now.Ticks.ToString()); },
                    });

                // external runners
                if (context.ProtocolConfigurationService.ProtocolConfigs.ContainsKey(server.Protocol)
                && context.ProtocolConfigurationService.ProtocolConfigs[server.Protocol].Runners.Count > 1)
                {
                    actions.Add(new ActionForServer()
                    {
                        ActionName = context.LanguageService.Translate("Connect") + $" (Internal)",
                        Run = (id) => { GlobalEventHelper.OnRequestServerConnect?.Invoke(id, assignRunnerName: context.ProtocolConfigurationService.ProtocolConfigs[server.Protocol].Runners.First().Name); },
                    });
                    foreach (var runner in context.ProtocolConfigurationService.ProtocolConfigs[server.Protocol].Runners)
                    {
                        if (runner is InternalDefaultRunner) continue;
                        actions.Add(new ActionForServer()
                        {
                            ActionName = context.LanguageService.Translate("Connect") + $" ({runner.Name})",
                            Run = (id) => { GlobalEventHelper.OnRequestServerConnect?.Invoke(id, assignRunnerName: runner.Name); },
                        });
                    }
                }
                else
                {
                    actions.Add(new ActionForServer()
                    {
                        ActionName = context.LanguageService.Translate("Connect"),
                        Run = (id) => { GlobalEventHelper.OnRequestServerConnect?.Invoke(id); },
                    });
                }

                actions.Add(new ActionForServer()
                {
                    ActionName = context.LanguageService.Translate("Edit"),
                    Run = (id) => { GlobalEventHelper.OnRequestGoToServerEditPage?.Invoke(id, false, false); },
                });
                actions.Add(new ActionForServer()
                {
                    ActionName = context.LanguageService.Translate("server_card_operate_duplicate"),
                    Run = (id) => { GlobalEventHelper.OnRequestGoToServerEditPage?.Invoke(id, true, false); },
                });
            };
            if (server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortBase)))
            {
                actions.Add(new ActionForServer()
                {
                    ActionName = context.LanguageService.Translate("server_card_operate_copy_address"),
                    Run = (id) =>
                    {
                        var pb = context.AppData.VmItemList.First(x => x.Server.Id == id);
                        if (pb.Server is ProtocolServerWithAddrPortBase server)
                            try
                            {
                                Clipboard.SetText($"{server.Address}:{server.GetPort()}");
                            }
                            catch (Exception)
                            {
                                // ignored
                            }
                    },
                });
            }
            if (server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortUserPwdBase)))
            {
                actions.Add(new ActionForServer()
                {
                    ActionName = context.LanguageService.Translate("server_card_operate_copy_username"),
                    Run = (id) =>
                    {
                        var pb = context.AppData.VmItemList.First(x => x.Server.Id == id);
                        if (pb.Server is ProtocolServerWithAddrPortUserPwdBase server)
                            try
                            {
                                Clipboard.SetText(server.UserName);
                            }
                            catch (Exception)
                            {
                                // ignored
                            }
                    },
                });
            }
            if (server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortUserPwdBase)))
            {
                actions.Add(new ActionForServer()
                {
                    ActionName = context.LanguageService.Translate("server_card_operate_copy_password"),
                    Run = (id) =>
                    {
                        var pb = context.AppData.VmItemList.First(x => x.Server.Id == id);
                        if (pb.Server is ProtocolServerWithAddrPortUserPwdBase server)
                            try
                            {
                                Clipboard.SetText(context.DataService.DecryptOrReturnOriginalString(server.Password));
                            }
                            catch (Exception)
                            {
                                // ignored
                            }
                    },
                });
            }

            #endregion Build Actions

            return actions;
        }
    }
}
