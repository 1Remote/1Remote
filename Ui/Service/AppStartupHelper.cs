using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using _1RM.Model;
using _1RM.Model.Protocol.Base;
using _1RM.Service.DataSource.DAO.Dapper;
using _1RM.View;
using Shawn.Utils;
using Shawn.Utils.Wpf;

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

        public static async Task Init(List<string> args)
        {
            args = args.Select(x => x.TrimStart('-')).Where(x => string.IsNullOrWhiteSpace(x) == false).Distinct().ToList();
            _args = new HashSet<string>(args);




            if (args.Count > 0 && _args.Count == 0)
            {
                // Exit when all args are processed
                Environment.Exit(0);
            }

            #region APP_START_WITH_MINIMIZED
            IsStartMinimized = _args.Contains(APP_START_MINIMIZED);
            if (_args.Contains(APP_START_MINIMIZED)) _args.Remove(APP_START_MINIMIZED);

            #endregion


            if (NamedPipeHelper.IsServer == false)
            {
                // Send message to server
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
        }

        private static bool IsCfgArg(string arg)
        {
            return arg == UNINSTALL
                       || arg == INSTALL
                       || arg == DESKTOP_SHORTCUT_INSTALL
                       || arg == DESKTOP_SHORTCUT_UNINSTALL
                       || arg == RUN_AUTO_AT_OS_START_INSTALL
                       || arg == RUN_AUTO_AT_OS_START_UNINSTALL;
        }

        private static bool IsAnyArgNeedAppInstance(IEnumerable<string> args)
        {
            return _args.Any(x=> IsCfgArg(x) == false);
        }


        private static ConfigurationService? _configuration = null;
        public static void ProcessWhenDataLoaded(ConfigurationService cfg)
        {
            _configuration = cfg;
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
            if (args.Contains(UNINSTALL))
            {
                args.Remove(UNINSTALL);
                if (!args.Contains(DESKTOP_SHORTCUT_UNINSTALL)) args.Add(DESKTOP_SHORTCUT_UNINSTALL);
                if (!args.Contains(RUN_AUTO_AT_OS_START_UNINSTALL)) args.Add(RUN_AUTO_AT_OS_START_UNINSTALL);
            }
            if (args.Contains(INSTALL))
            {
                args.Remove(INSTALL);
                if (!args.Contains(DESKTOP_SHORTCUT_INSTALL)) args.Add(DESKTOP_SHORTCUT_INSTALL);
                if (!args.Contains(RUN_AUTO_AT_OS_START_INSTALL)) args.Add(RUN_AUTO_AT_OS_START_INSTALL);
            }

            #region DESKTOP_SHORTCUT
#if !FOR_MICROSOFT_STORE_ONLY
            if (args.Contains(DESKTOP_SHORTCUT_INSTALL))
            {
                InstallDesktopShortcut(true);
            }
            if (args.Contains(DESKTOP_SHORTCUT_UNINSTALL))
            {
                InstallDesktopShortcut(false);
            }
#endif
            if (args.Contains(DESKTOP_SHORTCUT_INSTALL)) args.Remove(DESKTOP_SHORTCUT_INSTALL);
            if (args.Contains(DESKTOP_SHORTCUT_UNINSTALL)) args.Remove(DESKTOP_SHORTCUT_UNINSTALL);
            #endregion

            #region RUN_AUTO_AT_OS_START
            if (args.Contains(RUN_AUTO_AT_OS_START_INSTALL))
            {
                Task.Factory.StartNew(() => ConfigurationService.SetSelfStart(true)).Wait();
            }
            else if (args.Contains(RUN_AUTO_AT_OS_START_UNINSTALL))
            {
                Task.Factory.StartNew(() => ConfigurationService.SetSelfStart(false)).Wait();
            }
            if (args.Contains(RUN_AUTO_AT_OS_START_INSTALL)) args.Remove(RUN_AUTO_AT_OS_START_INSTALL);
            if (args.Contains(RUN_AUTO_AT_OS_START_UNINSTALL)) args.Remove(RUN_AUTO_AT_OS_START_UNINSTALL);
            #endregion
            foreach (var arg in args.ToArray())
            {
                if (IsCfgArg(arg))
                {
                    args.Remove(arg);
                }
            }
        }

        private static void ProcessArg(ICollection<string> args)
        {
            Debug.Assert(IoC.TryGet<MainWindowViewModel>() != null);
            if (args.Contains(ACTIVATE))
            {
                IoC.Get<MainWindowViewModel>()?.ShowMe(true);
                args.Remove(ACTIVATE);
            }


            // OPEN SERVER CONNECTION
            var servers = new List<ProtocolBase>();
            var foRm = new List<string>();
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
                            foRm.Add("CLI tag");
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
                        foRm.Add("CLI id");
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
                            foRm.Add("CLI name");
                        }
                    }
                }
            }

            foreach (var server in servers)
            {
                GlobalEventHelper.OnRequestServerConnect?.Invoke(server, fromView: $"{nameof(MainWindowView)}");
                Thread.Sleep(100);
            }
        }

#if !FOR_MICROSOFT_STORE_ONLY
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
                var shortcut = new IWshRuntimeLibrary.WshShell().CreateShortcut(shortcutPath);
                shortcut.IconLocation =
                shortcut.TargetPath = Process.GetCurrentProcess().MainModule!.FileName!;
                shortcut.WorkingDirectory = Environment.CurrentDirectory;
                shortcut.Arguments = "";
                shortcut.Description = Assert.APP_DISPLAY_NAME;
                shortcut.Save();
            }
        }
#endif

    }
}
