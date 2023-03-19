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
        private static bool ApplyAlternateCredential(ref ProtocolBase serverClone, string assignCredentialName)
        {
            if (serverClone is not ProtocolBaseWithAddressPort protocol)
                return true;

            bool ret = false;


            bool canAddressAutoSwitch = protocol.IsAutoAlternateAddressSwitching == true;
            var newCredential = protocol.GetCredential();


            // use assign credential
            var assignCredentialNameClone = assignCredentialName;
            var assignCredential = protocol.AlternateCredentials.FirstOrDefault(x => x.Name == assignCredentialNameClone);
            if (assignCredential != null)
            {
                // if any host or port in assignCredential，then disabled `AutoAlternateAddressSwitching`
                if (string.IsNullOrEmpty(assignCredential.Address) == false
                    || string.IsNullOrEmpty(assignCredential.Port) == false
                   )
                {
                    canAddressAutoSwitch = false;
                }
                newCredential.SetCredential(assignCredential);
            }

            // auto switching address
            if (canAddressAutoSwitch || protocol.IsPingBeforeConnect == true)
            {
                var credentials = new List<Credential>()
                {
                    newCredential,
                };
                credentials.AddRange(protocol.AlternateCredentials.Where(x => !string.IsNullOrEmpty(x.Address) || !string.IsNullOrEmpty(x.Port)));

                WaitingViewModel? dlg = null;
                var title = serverClone.DisplayName + " - " + IoC.Get<LanguageService>().Translate((credentials.Count > 1 ? "Automatic address switching" : "Availability detection"));
                var message = IoC.Get<LanguageService>().Translate("Detecting available host...") + $" form {credentials.Count} {(credentials.Count > 1 ? "addresses" : "address")}";
                Execute.OnUIThreadSync(() =>
                {
                    // show a progress window
                    dlg = new WaitingViewModel
                    {
                        Title = title,
                        Message = message
                    };
                    IoC.Get<IWindowManager>().ShowWindow(dlg);
                });

                int maxWaitSeconds = 5;
                var cts = new CancellationTokenSource();
                var tasks = new List<Task<Credential?>>();
                for (int i = 0; i < credentials.Count; i++)
                {
                    var sleep = i * 50;
                    var x = credentials[i];
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
                            dlg.Message = $"{message} (eta {maxWaitSeconds - i}s)";
                        }
                        Thread.Sleep(1000);
                    }
                    return null as Credential;
                }, cts.Token));

                var t = Task.WaitAny(tasks.ToArray());
                cts.Cancel(false);
                if (tasks[t].Result != null)
                {
                    SimpleLogHelper.Info("Auto switching address.");
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