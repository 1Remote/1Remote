using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using _1RM.View;
using Shawn.Utils;
using Shawn.Utils.Wpf;

namespace _1RM.Service
{
    internal static class AppStartupHelper
    {
        private const string Seperator = "+++!+!+++";
        public const string ACTIVATE = "activate";
        public const string DESKTOP_SHORTCUT_INSTALL = "install-desktop-shortcut";
        public const string DESKTOP_SHORTCUT_UNINSTALL = "uninstall-desktop-shortcut";
        public const string APP_START_MINIMIZED = "start-minimized";
        public const string RUN_AUTO_AT_OS_START_INSTALL = "install-startup";
        public const string RUN_AUTO_AT_OS_START_UNINSTALL = "uninstall-startup";
        private static HashSet<string> _args = new HashSet<string>();

#if FOR_MICROSOFT_STORE_ONLY
        public static bool IsStartMinimized => true;
#else
        public static bool IsStartMinimized { get; private set; } = false;
#endif
        private static readonly NamedPipeHelper NamedPipeHelper = new NamedPipeHelper(Assert.APP_DISPLAY_NAME + "_" + MD5Helper.GetMd5Hash16BitString(Environment.UserName));

        public static void Init(string[] args)
        {
            args = args.Select(x => x.TrimStart('-')).Where(x => string.IsNullOrWhiteSpace(x) == false).Distinct().ToArray();
            _args = new HashSet<string>(args);

            #region DESKTOP_SHORTCUT
            if (_args.Contains(DESKTOP_SHORTCUT_INSTALL))
            {
                InstallDesktopShortcut(true);
            }
            if (_args.Contains(DESKTOP_SHORTCUT_UNINSTALL))
            {
                InstallDesktopShortcut(false);
            }
            if (_args.Contains(DESKTOP_SHORTCUT_INSTALL)) _args.Remove(DESKTOP_SHORTCUT_INSTALL);
            if (_args.Contains(DESKTOP_SHORTCUT_UNINSTALL)) _args.Remove(DESKTOP_SHORTCUT_UNINSTALL);
            #endregion

            #region RUN_AUTO_AT_OS_START
            if (_args.Contains(RUN_AUTO_AT_OS_START_INSTALL))
            {
                ConfigurationService.SetSelfStart(true);
            }
            else if (_args.Contains(RUN_AUTO_AT_OS_START_UNINSTALL))
            {
                ConfigurationService.SetSelfStart(false);
            }
            if (_args.Contains(RUN_AUTO_AT_OS_START_INSTALL)) _args.Remove(RUN_AUTO_AT_OS_START_INSTALL);
            if (_args.Contains(RUN_AUTO_AT_OS_START_UNINSTALL)) _args.Remove(RUN_AUTO_AT_OS_START_UNINSTALL);
            #endregion

            if (args.Length > 0 && _args.Count == 0)
            {
                // Exit when all args are processed
                Environment.Exit(0);
            }

            #region APP_START_WITH_MINIMIZED
#if !FOR_MICROSOFT_STORE_ONLY
            IsStartMinimized = _args.Contains(APP_START_MINIMIZED);
#endif
            if (_args.Contains(APP_START_MINIMIZED)) _args.Remove(APP_START_MINIMIZED);

            #endregion


            if (NamedPipeHelper.IsServer == false)
            {
                // Send message to server
                if (_args.Count == 0) _args.Add(ACTIVATE);
                try
                {
                    NamedPipeHelper.NamedPipeSendMessage(string.Join(" ", _args));
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


        public static void ProcessWhenLaunch()
        {
            ProcessArg(_args);
            _args.Clear();
            if (NamedPipeHelper.IsServer)
            {
                NamedPipeHelper.OnMessageReceived += message =>
                {
                    SimpleLogHelper.Debug("NamedPipeServerStream get: " + message);
                    var strings = new HashSet<string>(message.Split(Seperator, StringSplitOptions.RemoveEmptyEntries).Distinct());
                    ProcessArg(strings);
                };
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

            // TODO: process other args like "id:xasd123123" "tag:21312"
        }

#if !FOR_MICROSOFT_STORE_ONLY
        private static void InstallDesktopShortcut(bool isInstall)
        {
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var shortcutPath = System.IO.Path.Combine(desktopPath, Assert.APP_DISPLAY_NAME + ".lnk");
            if (isInstall)
            {
                if (System.IO.File.Exists(shortcutPath))
                {
                    System.IO.File.Delete(shortcutPath);
                }
                var shortcut = new IWshRuntimeLibrary.WshShell().CreateShortcut(shortcutPath);
                shortcut.IconLocation =
                shortcut.TargetPath = Process.GetCurrentProcess().MainModule!.FileName!;
                shortcut.WorkingDirectory = Environment.CurrentDirectory;
                shortcut.Arguments = "";
                shortcut.Description = Assert.APP_DISPLAY_NAME;
                shortcut.Save();
            }
            else
            {
                if (System.IO.File.Exists(shortcutPath))
                {
                    System.IO.File.Delete(shortcutPath);
                }
            }
        }
#endif

    }
}
