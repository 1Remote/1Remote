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
using _1RM.Model;

namespace _1RM.Service
{
    public partial class SessionControlService
    {
#if DEBUG
        public static void CredentialTest()
        {
            var pingCredentials = new List<Credential>
            {
                new Credential()
                {
                    Name = "asfasdas12312312312",
                    Address = "127.012311.131231231", Port = "5000",
                },
                new Credential()
                {
                    Name = "asfasdas",
                    Address = "127.0.1.1", Port = "5000",
                },
                new Credential()
                {
                    Address = "127.0.0.1", Port = "5000",
                },
                new Credential()
                {
                    Name = "xcv1",
                    Address = "192.168.100.1", Port = "3389",
                },
                new Credential()
                {
                    Name = "asfasdxxxxxxxxxxxxxxxxas12312312312",
                    Address = "172.20.65.31", Port = "3389",
                },
                new Credential()
                {
                    Name = "98vs",
                    Address = "172.20.65.64", Port = "3389",
                },
                new Credential()
                {
                    Name = "ggg232",
                    Address = "172.20.65.65", Port = "3389",
                },
                new Credential()
                {
                    Name = "zxd11",
                    Address = "172.20.65.66", Port = "3389",
                },
            };
            Task.Factory.StartNew(async () =>
            {
                var c = await FindFirstConnectableAddressAsync(pingCredentials, "test");
                if (c != null)
                {
                    SimpleLogHelper.Debug($"Connected to {c.Address}:{c.Port}");
                }
            });
        }
#endif

        /// <summary>
        /// Find the first connectable address from the given credentials. if return null then no address is connectable.
        /// </summary>
        public static async Task<Credential?> FindFirstConnectableAddressAsync(IEnumerable<Credential> pingCredentials, string protocolDisplayName)
        {
            var credentials = pingCredentials.Select(x => x.CloneMe()).ToList();
            const int maxWaitSeconds = 5;
            using var cts = new CancellationTokenSource();

            var uiPingItems = new List<PingTestItem>();
            foreach (var credential in credentials)
            {
                // Normalize credentials - ensure all have address and port
                credential.Address = string.IsNullOrEmpty(credential.Address) ? credentials.First().Address : credential.Address;
                credential.Port = string.IsNullOrEmpty(credential.Port) ? credentials.First().Port : credential.Port;
                uiPingItems.Add(new PingTestItem(credential.Name, credential.Address + ":" + credential.Port)
                {
                    Status = PingStatus.None,
                });
            }

            var dlg = new AlternateAddressSwitchingViewModel(cts)
            {
                Title = protocolDisplayName + ": " + IoC.Translate("Availability detection"),
                PingTestItems = uiPingItems
            };
            SimpleLogHelper.Debug($"FindFirstConnectableAddressAsync in {uiPingItems.Count} address, showing dlg...");
            await Execute.OnUIThreadAsync(() => { IoC.Get<IWindowManager>().ShowWindow(dlg); });

            var taskList = new List<(Task<bool?> Task, Credential Credential)>();
            for (int i = 0; i < credentials.Count; i++)
            {
                // add tasks to ping each credential
                var credential = credentials[i];
                var pingTestItem = uiPingItems[i];
                var task = Task.Run(async () =>
                {
                    try
                    {
                        await Execute.OnUIThreadAsync(() => pingTestItem.Status = PingStatus.Pinging);
                        var startTime = DateTime.UtcNow;
                        var ret = await TcpHelper.TestConnectionAsync(credential.Address, credential.Port, cts.Token, maxWaitSeconds * 1000);
                        await Execute.OnUIThreadAsync(() =>
                        {
                            pingTestItem.Ping = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
                            pingTestItem.Status = ret switch
                            {
                                null => PingStatus.Canceled,
                                true => PingStatus.Success,
                                _ => PingStatus.Failed
                            };
                        });
                        await Task.Delay(200, cts.Token); // To avoid the UI closing too quickly, making it hard to see.
                        return ret;
                    }
                    catch (OperationCanceledException)
                    {
                        await Execute.OnUIThreadAsync(() => pingTestItem.Status = PingStatus.Canceled);
                        return null;
                    }
                    catch (Exception ex)
                    {
                        SimpleLogHelper.Error($"Error testing connection: {ex.Message}");
                        await Execute.OnUIThreadAsync(() => pingTestItem.Status = PingStatus.Failed);
                        return false;
                    }
                }, cts.Token);
                taskList.Add((task, credential));
            }

            // start a task to update UI countdown
            var countDownTask = Task.Run(async () =>
            {
                try
                {
                    for (int i = 0; i < maxWaitSeconds; i++)
                    {
                        await Execute.OnUIThreadAsync(() => dlg.Eta = maxWaitSeconds - i);
                        if (cts.Token.IsCancellationRequested)
                        {
                            break;
                        }
                        await Task.Delay(1000, cts.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception e)
                {
                    SimpleLogHelper.Error($"Error in countdown task: {e.Message}");
                }
            }, cts.Token);


            Credential? successfulCredential = null;
            try
            {
                var remainingTasks = taskList.Select(t => t.Task).ToList();
                while (remainingTasks.Any())
                {
                    var completedTask = await Task.WhenAny(remainingTasks);
                    remainingTasks.Remove(completedTask);

                    // if success then get credential
                    var taskInfo = taskList.FirstOrDefault(t => t.Task == completedTask);
                    if (taskInfo != default && await taskInfo.Task == true)
                    {
                        successfulCredential = taskInfo.Credential;
                        SimpleLogHelper.DebugInfo($"Task for credential {successfulCredential.Name} completed successfully. Cancelling remaining tasks.");
                        break;
                    }
                }
            }
            finally
            {
                await Execute.OnUIThreadAsync(() => dlg.Eta = 0);
                cts.Cancel();
            }


            try
            {
                await Task.WhenAny(countDownTask, Task.Delay(maxWaitSeconds * 1000));
            }
            catch (Exception)
            {
                // ignored
            }


            if (dlg.IsCanceled)
            {
                return null;
            }

            // return the first credential when ping success
            if (successfulCredential != null)
            {
                // close the pop window
                await Execute.OnUIThreadAsync(() => { dlg.RequestClose(); });
                return successfulCredential.CloneMe();
            }
            else
            {
                // none of the address is connectable
                // show error message
                await Execute.OnUIThreadAsync(() =>
                {
                    dlg.Message = IoC.Translate("We are not able to connect to xxx", protocolDisplayName);
                    dlg.StartAutoCloseCounter();
                });
            }
            return null;
        }


        /// <summary>
        /// Find the first connectable address from the given credentials. if return null then no address is connectable.
        /// </summary>
        public static async Task<Credential?> FindFirstConnectableAddressAsyncWithoutUI(IEnumerable<Credential> pingCredentials, string protocolDisplayName)
        {
            const int maxWaitSeconds = 5;
            var credentials = pingCredentials.Select(x => x.CloneMe()).ToList();
            if (!credentials.Any())
            {
                return null;
            }

            using var cts = new CancellationTokenSource();

            // Normalize credentials - ensure all have address and port
            foreach (var credential in credentials)
            {
                credential.Address = string.IsNullOrEmpty(credential.Address) ? credentials.First().Address : credential.Address;
                credential.Port = string.IsNullOrEmpty(credential.Port) ? credentials.First().Port : credential.Port;
            }

            SimpleLogHelper.Debug($"FindFirstConnectableAddressSimpleAsync testing {credentials.Count} addresses...");

            var taskList = new List<(Task<bool?> Task, Credential Credential)>();
            for (int i = 0; i < credentials.Count; i++)
            {
                // add tasks to ping each credential
                var credential = credentials[i];
                var task = Task.Run(async () =>
                {
                    try
                    {
                        var ret = await TcpHelper.TestConnectionAsync(credential.Address, credential.Port, cts.Token, maxWaitSeconds * 1000);
                        return ret;
                    }
                    catch (OperationCanceledException)
                    {
                        return null;
                    }
                    catch (Exception ex)
                    {
                        SimpleLogHelper.Debug($"Error testing connection: {ex.Message}");
                        return false;
                    }
                }, cts.Token);
                taskList.Add((task, credential));
            }

            Credential? successfulCredential = null;
            try
            {
                var remainingTasks = taskList.Select(t => t.Task).ToList();
                while (remainingTasks.Any())
                {
                    var completedTask = await Task.WhenAny(remainingTasks);
                    remainingTasks.Remove(completedTask);

                    // if success then get credential
                    var taskInfo = taskList.FirstOrDefault(t => t.Task == completedTask);
                    if (taskInfo != default && await taskInfo.Task == true)
                    {
                        successfulCredential = taskInfo.Credential;
                        SimpleLogHelper.DebugInfo($"Task for credential {successfulCredential.Name} completed successfully. Cancelling remaining tasks.");
                        break;
                    }
                }
            }
            finally
            {
                // Cancel all remaining tasks
                cts.Cancel();
            }

            // Return the first credential when ping success
            if (successfulCredential != null)
            {
                return successfulCredential.CloneMe();
            }

            // None of the addresses are connectable
            SimpleLogHelper.Debug("No connectable address found");
            return null;
        }


        /// <summary>
        /// if return null then no address is connectable.
        /// </summary>
        public static async Task<Credential?> GetCredential(ProtocolBaseWithAddressPort protocol, string assignCredentialName = "")
        {
            // set the credential from the raw protocol (for reconnection since the credential may be changed when first connection)
            if (IoC.Get<GlobalData>().VmItemList.FirstOrDefault(x => x.Id == protocol.Id) is { Server: ProtocolBaseWithAddressPort swap })
            {
                var swap2 = (ProtocolBaseWithAddressPort)swap.Clone();
                swap2.DecryptToConnectLevel();
                protocol.DisplayName = swap2.DisplayName;
                protocol.SetCredential(swap2.GetCredential(), true);
            }

            var newCredential = protocol.GetCredential();
            newCredential.Name = protocol.DisplayName;
            // use assign credential 应用指定的 credential
            var assignCredential = protocol.AlternateCredentials.FirstOrDefault(x => x.Name == assignCredentialName);
            if (assignCredential != null)
            {
                SimpleLogHelper.Debug("using assign credential: " + assignCredentialName);
                newCredential.SetCredential(assignCredential);
                if (protocol.DisplayName != assignCredentialName)
                    newCredential.Name = $"{protocol.DisplayName} ({assignCredentialName})";
            }

            // check if it needs to ping before connect
            bool isPingBeforeConnect = protocol.IsPingBeforeConnect == true
                                       // do not ping if rdp protocol and gateway is used
                                       && protocol is not RDP { GatewayMode: EGatewayMode.UseTheseGatewayServerSettings };
            // check if it needs to auto switch address
            var isAutoAlternateAddressSwitching = protocol.IsAutoAlternateAddressSwitching == true
                                                  // if any host or port in assignCredential，then disabled `AutoAlternateAddressSwitching`
                                                  && string.IsNullOrEmpty(assignCredential?.Address) && string.IsNullOrEmpty(assignCredential?.Port)
                                                  // if none of the alternate credential has host or port，then disabled `AutoAlternateAddressSwitching`
                                                  && protocol.AlternateCredentials.Any(x => !string.IsNullOrEmpty(x.Address) || !string.IsNullOrEmpty(x.Port));

            if (
                // if both `IsPingBeforeConnect` and `IsAutoAlternateAddressSwitching` are false, then return directly
                (isPingBeforeConnect == false && isAutoAlternateAddressSwitching == false)
                // if LocalApp protocol are not showing address input, then return directly
                || (protocol is LocalApp app && app.ShowAddressInput() == false))
            {
                return newCredential;
            }

            // a quick test for the first credential, if pass return directly to avoid window pop
            var ret = await TcpHelper.TestConnectionAsync(newCredential.Address, newCredential.Port, null, 100);
            if (ret == true)
                return newCredential;

            var credentials = new List<Credential> { newCredential };
            // if `IsAutoAlternateAddressSwitching` is true, then add all alternate credentials, else only add the main address to ping
            if (isAutoAlternateAddressSwitching)
                credentials.AddRange(protocol.AlternateCredentials.Where(x => !string.IsNullOrEmpty(x.Address) || !string.IsNullOrEmpty(x.Port)));

            // find the first response address from `credentials`
            var connectableAddress = await FindFirstConnectableAddressAsync(credentials, protocol.DisplayName);
            if (connectableAddress != null)
            {
                newCredential = connectableAddress.CloneMe();
                if (protocol.DisplayName != connectableAddress.Name)
                    newCredential.Name = $"{protocol.DisplayName} ({connectableAddress.Name})";
                return newCredential;
            }
            return null;
        }
    }
}