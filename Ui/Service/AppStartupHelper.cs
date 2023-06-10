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
        public const string ACTIVATE = "activate";
        public const string DESKTOP_SHORTCUT_INSTALL = "install-desktop-shortcut";
        public const string DESKTOP_SHORTCUT_UNINSTALL = "uninstall-desktop-shortcut";
        public const string APP_START_MINIMIZED = "start-minimized";
        public const string RUN_AUTO_AT_OS_START_INSTALL = "install-startup";
        public const string RUN_AUTO_AT_OS_START_UNINSTALL = "uninstall-startup";
        private static string[] _args = Array.Empty<string>();

#if FOR_MICROSOFT_STORE_ONLY
        public static bool IsStartMinimized => true;
#else
        public static bool IsStartMinimized { get; private set; } = false;
#endif

        public static void Init(string[] args)
        {
            args = args.Select(x => x.TrimStart('-')).ToArray();
            if (NamedPipeHelper.IsServer)
            {
                _args = args;
                NamedPipeHelper.OnMessageReceived += message =>
                {
                    SimpleLogHelper.Debug("NamedPipeServerStream get: " + message);
                    var strings = message.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    ProcessArg(strings);
                };
            }
            else
            {
                // Send message to server
                NamedPipeHelper.NamedPipeSendMessage(string.Join(" ", args));
            }
        }


        public static void ProcessArg()
        {
            ProcessArg(_args);
        }
        private static void ProcessArg(string[] args)
        {
            Debug.Assert(IoC.TryGet<MainWindowViewModel>() != null);
            // run after IoC init
            _args = _args.Select(x => x.TrimStart('-')).ToArray();

            if (_args.Contains(AppStartupHelper.ACTIVATE))
            {
                IoC.Get<MainWindowViewModel>()?.ShowMe(true);
            }

#if !FOR_MICROSOFT_STORE_ONLY
            IsStartMinimized = _args.Contains(APP_START_MINIMIZED);
            if (_args.Contains(DESKTOP_SHORTCUT_INSTALL))
            {
                InstallDesktopShortcut(true);
            }
            else if (_args.Contains(DESKTOP_SHORTCUT_UNINSTALL))
            {
                InstallDesktopShortcut(false);
            }

            if (_args.Contains(RUN_AUTO_AT_OS_START_INSTALL))
            {
                IoC.Get<ConfigurationService>().SetSelfStart(true);
            }
            else if (_args.Contains(RUN_AUTO_AT_OS_START_UNINSTALL))
            {
                IoC.Get<ConfigurationService>().SetSelfStart(false);
            }
#endif
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

        #region NamedPipe
        private static readonly NamedPipeHelper NamedPipeHelper = new NamedPipeHelper(Assert.APP_DISPLAY_NAME + "_" + MD5Helper.GetMd5Hash16BitString(Environment.CurrentDirectory + Environment.UserName));
        private static void OnlyOneAppInstanceCheck(string[] args)
        {
            args = args.Select(x => x.TrimStart('-')).ToArray();
            if (NamedPipeHelper.IsServer == false)
            {
                try
                {
                    NamedPipeHelper.NamedPipeSendMessage(AppStartupHelper.ACTIVATE);
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
        #endregion
    }
}
