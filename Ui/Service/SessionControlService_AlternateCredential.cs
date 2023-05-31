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
using _1RM.View.Utils;

namespace _1RM.Service
{
    public partial class SessionControlService
    {
        public static void CredentialTest()
        {
            {
                var cts = new CancellationTokenSource();
                var tasks = new List<Task>();

                for (int i = 0; i < 5; i++)
                {
                    int taskNumber = i + 1;
                    var task = Task.Run(() =>
                    {
                        SimpleLogHelper.Debug($"Task {taskNumber} started");
                        // Simulate some work
                        Thread.Sleep(taskNumber * 1000);
                        SimpleLogHelper.Debug($"Task {taskNumber} finished");
                        return taskNumber;
                    }, cts.Token);
                    tasks.Add(task);
                }

                // Wait for any task to complete
                int completedTaskIndex = Task.WaitAny(tasks.ToArray());

                // Cancel the remaining tasks
                cts.Cancel();

                SimpleLogHelper.Debug(tasks.ToArray());

                Console.WriteLine($"Task {completedTaskIndex + 1} completed first. Cancelling remaining tasks.");

                foreach (var task in tasks)
                {
                    if (!task.IsCompleted)
                    {
                        task.ContinueWith(t => Console.WriteLine($"Task cancelled."));
                    }
                    else
                    {
                        Console.WriteLine($"Task done");
                    }
                }
            }

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

            {
                const int maxWaitSeconds = 10;
                var items = new List<PingTestItem>();
                foreach (var credential in pingCredentials)
                {
                    items.Add(new PingTestItem(credential.Address)
                    {
                        Status = PingTestItem.PingStatus.None,
                    });
                }

                var dlg = new AlternateAddressSwitchingViewModel() { Title = DateTime.Now.ToString(), PingTestItems = items };
                IoC.Get<IWindowManager>().ShowWindow(dlg);


                var cts = new CancellationTokenSource();
                var tasks = new List<Task<Credential?>>();
                for (int i = 0; i < pingCredentials.Count; i++)
                {
                    var sleep = (i + 1) * 50;
                    var x = pingCredentials[i];
                    var y = items[i];
                    var i1 = i;
                    tasks.Add(new Task<Credential?>(() =>
                    {
                        var ii = i1;
                        // 根据排序休息一段时间，排在越后面休息时间越长，实现带优先级的检测
                        if (sleep > 0)
                            Thread.Sleep(sleep);
                        y.Status = PingTestItem.PingStatus.Testing;
                        if (Credential.TestAddressPortIsAvailable(x.Address, x.Port, maxWaitSeconds * 1000))
                        {
                            y.Status = PingTestItem.PingStatus.Success;
                            SimpleLogHelper.Debug($"task {ii} done");
                            return x;
                        }

                        y.Status = PingTestItem.PingStatus.Failed;
                        SimpleLogHelper.Debug($"task {ii} done");
                        return null;
                    }, cts.Token));
                }

                tasks.Add(new Task<Credential?>(() =>
                {
                    for (int i = 0; i < maxWaitSeconds; i++)
                    {
                        if (dlg != null)
                        {
                            dlg.Eta = (maxWaitSeconds - i) > 0 ? (maxWaitSeconds - i) : 0;
                        }

                        Thread.Sleep(1000);
                    }

                    return null;
                }, cts.Token));

                foreach (var task in tasks)
                {
                    task.Start();
                }


                var t = Task.WaitAny(tasks.ToArray());
                cts.Cancel();
                SimpleLogHelper.Debug($"cts.Cancel();");
                dlg.Eta = 0;
                Task.WaitAll(tasks.ToArray());
                if (tasks[t].Result != null)
                {
                }
                else
                {

                }


                foreach (var task in tasks)
                {
                    if (!task.IsCompleted)
                    {
                        task.ContinueWith(t => Console.WriteLine($"Task {t.Result} cancelled."));
                    }
                    else
                    {
                        Console.WriteLine($"Task done");
                    }
                }
            }
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
            // pingCredentials 不为空 说明需要 ping
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