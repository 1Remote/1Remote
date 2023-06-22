using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using _1RM.Model;
using _1RM.Model.Protocol.Base;
using _1RM.View;
using _1RM.View.Settings.General;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using WindowsShortcutFactory;
using SetSelfStartingHelper = _1RM.Utils.SetSelfStartingHelper;

namespace _1RM.Service
{
    internal static class AppStartupHelper
    {
        private const string Separator = "+++!+!+++";
        public const string ACTIVATE = "activate";
        public const string APP_START_MINIMIZED = "start-minimized";


        public const string INSTALL = "install";
        public const string UNINSTALL = "uninstall";
        public const string DESKTOP_SHORTCUT_INSTALL = "install-desktop-shortcut";
        public const string DESKTOP_SHORTCUT_UNINSTALL = "uninstall-desktop-shortcut";
        public const string RUN_AUTO_AT_OS_START_INSTALL = "install-startup";
        public const string RUN_AUTO_AT_OS_START_UNINSTALL = "uninstall-startup";
        private static HashSet<string> _args = new HashSet<string>();

        public static bool IsStartMinimized { get; private set; } = false;
        private static readonly NamedPipeHelper NamedPipeHelper = new NamedPipeHelper(Assert.APP_DISPLAY_NAME + "_" + MD5Helper.GetMd5Hash16BitString(Environment.UserName));

        public static void Init(List<string> args)
        {
            _args = new HashSet<string>(args.Select(x => x.TrimStart('-')).Where(x => string.IsNullOrWhiteSpace(x) == false).Distinct());

            if (NamedPipeHelper.IsServer == false)
            {
                if (_args.Count == 0) _args.Add(ACTIVATE);
                try
                {
                    NamedPipeHelper.NamedPipeSendMessage(string.Join(Separator, _args));
                    Environment.Exit(0);
                }
                catch
                {
                    Environment.Exit(1);
                }
                finally
                {
                    NamedPipeHelper?.Dispose();
                }
            }
            else
            {
                ProcessCfgArgs(ref _args);
                if (args.Count > 0 && _args.Count == 0)
                {
                    // Exit when all args are processed
                    Environment.Exit(0);
                }
                IsStartMinimized = _args.Contains(APP_START_MINIMIZED);
                if (_args.Contains(APP_START_MINIMIZED)) _args.Remove(APP_START_MINIMIZED);
            }
        }

        private static bool IsCfgArg(string arg)
        {
            return arg is UNINSTALL or INSTALL
                or DESKTOP_SHORTCUT_INSTALL or DESKTOP_SHORTCUT_UNINSTALL
                or RUN_AUTO_AT_OS_START_INSTALL or RUN_AUTO_AT_OS_START_UNINSTALL;
        }


        private static GeneralSettingViewModel? _generalSettingViewModel = null;
        public static void ProcessWhenDataLoaded(GeneralSettingViewModel generalViewModel)
        {
            _generalSettingViewModel = generalViewModel;
            ProcessArg(_args);
            _args.Clear();
            if (NamedPipeHelper.IsServer)
            {
                NamedPipeHelper.OnMessageReceived += message =>
                {
                    SimpleLogHelper.Debug("NamedPipeServerStream get: " + message);
                    var strings = new HashSet<string>(message.Split(new[] { Separator }, StringSplitOptions.RemoveEmptyEntries).Distinct());
                    ProcessArg(strings);
                };
            }
        }


        private static void ProcessCfgArgs(ref HashSet<string> args)
        {
            if (args.Contains(INSTALL) || args.Contains(DESKTOP_SHORTCUT_INSTALL))
            {
                InstallDesktopShortcut(true);
            }
            else if (args.Contains(UNINSTALL) || args.Contains(DESKTOP_SHORTCUT_UNINSTALL))
            {
                InstallDesktopShortcut(false);
            }
            if (args.Contains(INSTALL) || args.Contains(RUN_AUTO_AT_OS_START_INSTALL))
            {
                if (_generalSettingViewModel != null) // update ui
                    _generalSettingViewModel.AppStartAutomatically = true;
                else
                {
                    ConfigurationService.SetSelfStart(true);
                    Thread.Sleep(500);
                }
            }
            else if (args.Contains(UNINSTALL) || args.Contains(RUN_AUTO_AT_OS_START_UNINSTALL))
            {
                if (_generalSettingViewModel != null) // update ui
                    _generalSettingViewModel.AppStartAutomatically = false;
                else
                {
                    ConfigurationService.SetSelfStart(false);
                    Thread.Sleep(500);
                }
            }

            foreach (var arg in args.ToArray())
            {
                if (IsCfgArg(arg)) args.Remove(arg);
            }
        }

        private static void ProcessArg(HashSet<string> args)
        {
            Debug.Assert(IoC.TryGet<MainWindowViewModel>() != null);

            ProcessCfgArgs(ref args);

            if (args.Contains(ACTIVATE))
            {
                IoC.Get<MainWindowViewModel>()?.ShowMe(true);
                args.Remove(ACTIVATE);
            }


            // OPEN SERVER CONNECTION
            var servers = new List<ProtocolBase>();
            foreach (var arg in args)
            {
                if (arg.StartsWith("#"))
                {
                    // tag connect
                    var tagName = arg.Substring(1);
                    foreach (var server in IoC.Get<GlobalData>().VmItemList.ToArray())
                    {
                        if (server.Server.Tags.Any(x => string.Equals(x, tagName, StringComparison.CurrentCultureIgnoreCase))
                            && servers.Contains(server.Server) == false)
                        {
                            servers.Add(server.Server);
                        }
                    }
                }
                else if (arg.StartsWith("ULID:", StringComparison.OrdinalIgnoreCase))
                {
                    var id = arg.Substring("ULID:".Length).Trim();
                    var server = IoC.Get<GlobalData>().VmItemList.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.CurrentCultureIgnoreCase));
                    if (server != null && servers.Contains(server.Server) == false)
                    {
                        servers.Add(server.Server);
                    }
                }
                else
                {
                    var ss = IoC.Get<GlobalData>().VmItemList.Where(x => string.Equals(x.DisplayName, arg, StringComparison.CurrentCultureIgnoreCase));
                    foreach (var server in ss)
                    {
                        if (servers.Contains(server.Server) == false)
                        {
                            servers.Add(server.Server);
                        }
                    }
                }
            }

            GlobalEventHelper.OnRequestServersConnect?.Invoke(servers, fromView: "CLI");
        }

        private static void InstallDesktopShortcut(bool isInstall)
        {
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var shortcutPath = System.IO.Path.Combine(desktopPath, Assert.APP_DISPLAY_NAME + ".lnk");
            if (System.IO.File.Exists(shortcutPath))
            {
                System.IO.File.Delete(shortcutPath);
            }
            if (isInstall)
            {
                using var shortcut = new WindowsShortcut
                {
                    Path = Process.GetCurrentProcess().MainModule!.FileName!,
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                    IconLocation = Process.GetCurrentProcess().MainModule!.FileName!,
                    Arguments = "",
                };
                shortcut.Save(shortcutPath);
            }
        }
    }
}
