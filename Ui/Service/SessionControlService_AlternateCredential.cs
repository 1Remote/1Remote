using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Utils;
using Shawn.Utils;
using Stylet;
using _1RM.View.Utils;

namespace _1RM.Service
{
    public partial class SessionControlService
    {
        public static void CredentialTest()
        {
            var pingCredentials = new List<Credential>
            {
                new Credential()
                {
                    Address = "127.0.0.1", Port = "5000",
                },
                new Credential()
                {
                    Address = "127.0.1.1", Port = "5000",
                },
                new Credential()
                {
                    Address = "192.168.100.1", Port = "3389",
                },
                new Credential()
                {
                    Address = "172.20.65.31", Port = "3389",
                },
                new Credential()
                {
                    Address = "172.20.65.64", Port = "3389",
                },
                new Credential()
                {
                    Address = "172.20.65.65", Port = "3389",
                },
                new Credential()
                {
                    Address = "172.20.65.66", Port = "3389",
                },
            };
            Task.Factory.StartNew(async () =>
            {
                await FindFirstConnectableAddressAsync(pingCredentials, "test");
            });
        }

        private static async Task<Credential?> FindFirstConnectableAddressAsync(IEnumerable<Credential> pingCredentials, string title)
        {
            // credentials 不为空 说明需要 ping
            var credentials = pingCredentials.ToList();
            if (!credentials.Any()) return null;

            const int maxWaitSeconds = 10;
            var pingTestItems = new List<PingTestItem>();
            foreach (var credential in credentials)
            {
                pingTestItems.Add(new PingTestItem(credential.Address)
                {
                    Status = PingTestItem.PingStatus.None,
                });
            }
            title = title + ": " + IoC.Get<LanguageService>().Translate("Detecting available host...") + $" form {credentials.Count()} {(credentials.Count() > 1 ? "addresses" : "address")}";
            var dlg = new AlternateAddressSwitchingViewModel() { Title = title, PingTestItems = pingTestItems };
            await Execute.OnUIThreadAsync(() =>
            {
                IoC.Get<IWindowManager>().ShowWindow(dlg);
            });


            var cts = new CancellationTokenSource();
            var tasks = new List<Task<bool?>>();
            for (int i = 0; i < credentials.Count; i++)
            {
                var sleep = i * 100;
                var credential = credentials[i];
                var pingTestItem = pingTestItems[i];
                tasks.Add(Task.Factory.StartNew(() =>
                {
                    // 根据排序休息一段时间，排在越后面休息时间越长，实现带优先级的检测
                    if (sleep > 0)
                        Task.Delay(sleep, cts.Token).Wait(cts.Token);
                    var ret = TcpHelper.TestConnectionAsync(credential.Address, credential.Port, cts.Token, maxWaitSeconds * 1000).Result;
                    pingTestItem.Status = ret switch
                    {
                        null => PingTestItem.PingStatus.Canceled,
                        true => PingTestItem.PingStatus.Success,
                        _ => PingTestItem.PingStatus.Failed
                    };
                    Task.Delay(500, cts.Token).Wait(cts.Token); // 避免界面关闭太快，根本看不清
                    return ret;
                }, cts.Token));
            }
            tasks.Add(Task.Factory.StartNew(() =>
            {
                for (int i = 0; i < maxWaitSeconds; i++)
                {
                    if (dlg != null)
                    {
                        //dlg.Message = $"{message} (eta {maxWaitSeconds - i}s)";
                    }
                    Task.Delay(1000, cts.Token).Wait(cts.Token);
                }
                bool? ret = null;
                return ret;
            }, cts.Token));

            int completedTaskIndex = -1;
            var ts = tasks.ToArray();
            while (ts.Any())
            {
                var completedTask = await Task.WhenAny(ts);
                Console.WriteLine(completedTask.Result);
                if (completedTask?.Result == true)
                {
                    completedTaskIndex = tasks.IndexOf(completedTask);
                    Console.WriteLine($"Task{completedTaskIndex} completed first. Cancelling remaining tasks.");
                    break;
                }
                ts = ts.Where(t => t != completedTask).ToArray();
            }

            dlg.Eta = 0;
            if (ts.Any())
            {
                try
                {
                    cts.Cancel();
                    Task.WaitAll(tasks.ToArray());
                }
                catch (Exception e)
                {
                    // ignored
                }
            }
            await Execute.OnUIThreadAsync(() =>
            {
                dlg.RequestClose();
            });
            if (completedTaskIndex >= 0 && completedTaskIndex < tasks.Count)
            {
                SimpleLogHelper.DebugInfo("Auto switching address.");
                return credentials[completedTaskIndex].CloneMe();
            }
            return null;
        }

        /// <summary>
        /// 为指定的 protocol 应用指定的 credential，并 ping 一下，如果 ping 失败则返回 false
        /// </summary>
        private static bool ApplyAlternateCredentialAndPingIfNeeded(ref ProtocolBase serverClone, string assignCredentialName)
        {
            if (serverClone is not ProtocolBaseWithAddressPort protocol)
                return true;
            if (serverClone is RDP rdp && rdp.GatewayMode != EGatewayMode.DoNotUseGateway)
                return true;


            var newCredential = protocol.GetCredential();


            // use assign credential 应用指定的 credential
            var assignCredentialNameClone = assignCredentialName;
            var assignCredential = protocol.AlternateCredentials.FirstOrDefault(x => x.Name == assignCredentialNameClone);
            if (assignCredential != null)
            {
                newCredential.SetCredential(assignCredential);
            }

            // 判断是否需要  ping
            // ping before connect
            var pingCredentials = new List<Credential>();
            if (protocol.IsPingBeforeConnect == true)
            {
                pingCredentials.Add(newCredential);
            }

            // 判断是否需要自动切换地址
            if (protocol.IsAutoAlternateAddressSwitching == true)
            {
                // if any host or port in assignCredential，then disabled `AutoAlternateAddressSwitching`
                // 如果 assignCredential 中有 host 或 port，说明用户指定了连接的地址，因此禁用 `AutoAlternateAddressSwitching`
                var canAddressAutoSwitch = string.IsNullOrEmpty(assignCredential?.Address) && string.IsNullOrEmpty(assignCredential?.Port);

                // if none of the alternate credential has host or port，then disabled `AutoAlternateAddressSwitching`
                // 如果所有的 alternate credential 中都没有 host 或 port，则禁用 `AutoAlternateAddressSwitching`
                if (canAddressAutoSwitch
                    && protocol.AlternateCredentials.All(x => string.IsNullOrEmpty(x.Address) && string.IsNullOrEmpty(x.Port)))
                {
                    canAddressAutoSwitch = false;
                }

                if (canAddressAutoSwitch)
                {
                    if (pingCredentials.Count == 0)
                        pingCredentials.Add(newCredential);
                    pingCredentials.AddRange(protocol.AlternateCredentials.Where(x => !string.IsNullOrEmpty(x.Address) || !string.IsNullOrEmpty(x.Port)));
                }
            }

            var ret = true;
            // credentials 不为空 说明需要 ping
            if (pingCredentials.Any())
            {
                AlternateAddressSwitchingViewModel? dlg = null;
                var title = serverClone.DisplayName + " - " + IoC.Get<LanguageService>().Translate((pingCredentials.Count > 1 ? "Automatic address switching" : "Availability detection"));
                var message = IoC.Get<LanguageService>().Translate("Detecting available host...") + $" form {pingCredentials.Count} {(pingCredentials.Count > 1 ? "addresses" : "address")}";
                Execute.OnUIThreadSync(() =>
                {
                    // show a progress window
                    dlg = new AlternateAddressSwitchingViewModel
                    {
                        Title = title,
                    };
                    IoC.Get<IWindowManager>().ShowWindow(dlg);
                });

                const int maxWaitSeconds = 5;
                var cts = new CancellationTokenSource();
                var tasks = new List<Task<Credential?>>();
                for (int i = 0; i < pingCredentials.Count; i++)
                {
                    var sleep = i * 50;
                    var x = pingCredentials[i];
                    tasks.Add(Task.Factory.StartNew(() =>
                    {
                        // 根据排序休息一段时间，排在越后面休息时间越长，实现带优先级的检测
                        if (sleep > 0)
                            Thread.Sleep(sleep);
                        if (Credential.TestAddressPortIsAvailable(protocol, x, maxWaitSeconds * 1000))
                        {
                            return x;
                        }
                        return null;
                    }, cts.Token));
                }

                tasks.Add(Task.Factory.StartNew(() =>
                {
                    for (int i = 0; i < maxWaitSeconds; i++)
                    {
                        if (dlg != null)
                        {
                            //dlg.Message = $"{message} (eta {maxWaitSeconds - i}s)";
                        }
                        Thread.Sleep(1000);
                    }
                    return null as Credential;
                }, cts.Token));

                var t = Task.WaitAny(tasks.ToArray());
                cts.Cancel(false);
                if (tasks[t].Result != null)
                {
                    SimpleLogHelper.DebugInfo("Auto switching address.");
                    newCredential.SetAddress(tasks[t].Result!);
                    newCredential.SetPort(tasks[t].Result!);
                    ret = true;
                }
                else
                {
                    // none of this address is connectable
                    ret = false;
                    MessageBoxHelper.ErrorAlert(IoC.Get<LanguageService>().Translate("We are not able to connet to xxx", protocol.DisplayName), ownerViewModel: dlg);
                }

                Execute.OnUIThreadSync(() =>
                {
                    dlg?.RequestClose();
                });
            }


            protocol.SetCredential(newCredential);
            return ret;
        }
    }
}