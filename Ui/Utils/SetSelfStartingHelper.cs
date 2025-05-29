#define SHORTCUT_METHOD
#define REGISTRY_METHOD

#if FOR_MICROSOFT_STORE_ONLY
#undef SHORTCUT_METHOD
#undef REGISTRY_METHOD
#define STORE_UWP_METHOD
#endif

using System;
using System.Diagnostics;
#if STORE_UWP_METHOD
using System.Threading.Tasks;
using Windows.ApplicationModel;
#else
using System.IO;
using System.Linq;
using _1RM.Service;
using _1RM.Utils.WindowsApi.WindowsShortcutFactory;
using Shawn.Utils;
#endif


namespace _1RM.Utils
{
    public static class SetSelfStartingHelper
    {
#if SHORTCUT_METHOD
        public enum StartupMode
        {
            /// <summary>
            /// 正常启动
            /// </summary>
            Normal,

            /// <summary>
            /// 高权限启动，并设置软件自启动
            /// </summary>
            SetSelfStart,

            /// <summary>
            /// 高权限启动，并取消软件自启动
            /// </summary>
            UnsetSelfStart,
        }

        //public static bool IsElvated()
        //{
        //    var wi = WindowsIdentity.GetCurrent();
        //    var wp = new WindowsPrincipal(wi);
        //    var runAsAdmin = wp.IsInRole(WindowsBuiltInRole.Administrator);
        //    return runAsAdmin;
        //}

        ///// <summary>
        ///// 以高权限执行某些任务
        ///// </summary>
        ///// <param name="startupMode"></param>
        //public static void RunElvatedTask(StartupMode startupMode)
        //{
        //    // It is not possible to launch a ClickOnce app as administrator directly,
        //    // so instead we launch the app as administrator in a new process.
        //    var processInfo = new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName);
        //    // The following properties run the new process as administrator
        //    processInfo.UseShellExecute = true;
        //    processInfo.Verb = "runas";
        //    processInfo.Arguments = startupMode.ToString();
        //    // Start the new process
        //    try
        //    {
        //        Process.Start(processInfo);
        //    }
        //    catch
        //    {
        //    }
        //}

        private static string GetStartupShortcutPath(string appName)
        {
            var startUpPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            var shortcutPath = System.IO.Path.Combine(startUpPath, $"{appName}.lnk");
            return shortcutPath;
        }

        //private static void CleanUpShortcut(string exceptionShortcutPath, string appName)
        //{
        //    if (IsElvated())
        //    {
        //        var di = new FileInfo(exceptionShortcutPath).Directory;
        //        var fis = di.GetFiles(appName + "_*");
        //        if (fis?.Length > 0)
        //        {
        //            foreach (var fi in fis)
        //            {
        //                if (fi.FullName != exceptionShortcutPath)
        //                    File.Delete(fi.FullName);
        //            }
        //        }
        //    }
        //}

        //private static async void UnsetSelfStartByStartupByShortcut(string shortcutPath, string appName)
        //{
        //    var hasStartup = await IsSelfStartByShortcut(appName);
        //    if (!hasStartup) return;
        //    if (IsElvated())
        //        File.Delete(shortcutPath);
        //    else
        //        RunElvatedTask(StartupMode.UnsetSelfStart);
        //}

        //private static async void SetSelfStartByStartupByShortcut(string shortcutPath, string appName)
        //{
        //    var hasStartup = await IsSelfStartByShortcut(appName);
        //    if (hasStartup) return;

        //    if (IsElvated())
        //    {
        //        var exePath = Process.GetCurrentProcess().MainModule.FileName;
        //        if (File.Exists(shortcutPath))
        //            File.Delete(shortcutPath);
        //        var shell = new IWshRuntimeLibrary.WshShell();
        //        var shortcut = (IWshRuntimeLibrary.IWshShortcut)shell.CreateShortcut(shortcutPath);
        //        shortcut.TargetPath = exePath;
        //        shortcut.Arguments = "";
        //        shortcut.IconLocation = exePath;
        //        shortcut.WorkingDirectory = System.IO.Path.GetDirectoryName(exePath);
        //        shortcut.Description = "";
        //        shortcut.Save();
        //    }
        //    else
        //    {
        //        RunElvatedTask(StartupMode.SetSelfStart);
        //    }
        //}

        //public static async Task<bool> IsSelfStartByShortcut(string appName)
        //{
        //    return File.Exists(GetStartupShortcutPath(appName));
        //}

        private static bool IsSelfStartByShortcut(string appName)
        {
            return File.Exists(GetStartupShortcutPath(appName));
        }

        private static void SetSelfStartByShortcut(bool isSetSelfStart, string appName)
        {
            var shortcutPath = GetStartupShortcutPath(appName);

            try
            {
                if (System.IO.File.Exists(shortcutPath))
                {
                    System.IO.File.Delete(shortcutPath);
                }
                if (isSetSelfStart)
                {
                    using var shortcut = new WindowsShortcut
                    {
                        Path = Process.GetCurrentProcess().MainModule!.FileName!,
                        WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                        Arguments = $" --{AppStartupHelper.APP_START_MINIMIZED}",
                    };
                    shortcut.Save(shortcutPath);
                }
            }
            catch (Exception e)
            {
                SentryIoHelper.Error(e);
            }
        }

#endif

#if REGISTRY_METHOD

        private static bool IsSelfStartByRegistryKey(string appName)
        {
            var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            return key?.GetValueNames().Contains(appName) == true;
        }

        private static void SetSelfStartByRegistryKey(bool isSetSelfStart, string appName)
        {
            var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            if (key?.GetValueNames().Contains(appName) == true)
            {
                key?.DeleteValue(appName, false);
            }

            if (isSetSelfStart)
            {
                key?.SetValue(appName, $@"""{Process.GetCurrentProcess().MainModule!.FileName!}"" --{AppStartupHelper.APP_START_MINIMIZED}");
            }
        }

#endif


#if STORE_UWP_METHOD
        private static StartupTask? _startupTask = null;
        private static bool _isStartupTaskStateEnable = false;
        public static async void SetSelfStartByStartupTask(string appName, bool? isSetSelfStart = null)
        {
#if !DEBUG
            _startupTask ??= await StartupTask.GetAsync(appName); // Pass the task ID you specified in the appxmanifest file
            switch (_startupTask.State)
            {
                case StartupTaskState.Disabled:
                    if (isSetSelfStart == true)
                    {
                        // Task is disabled but can be enabled.
                        var newState = await _startupTask.RequestEnableAsync(); // ensure that you are on a UI thread when you call RequestEnableAsync()
                        Debug.WriteLine("Request to enable startup, result = {0}", newState);
                        _isStartupTaskStateEnable = true;
                    }
                    else
                    {
                        _isStartupTaskStateEnable = false;
                    }

                    break;
                case StartupTaskState.DisabledByUser:
                case StartupTaskState.DisabledByPolicy:
                    _isStartupTaskStateEnable = false;
                    if (isSetSelfStart == true)
                    {
                        // Task is disabled and user must enable it manually.
                        MessageBoxHelper.Warning("You have disabled this app's ability to run " +
                                        "as soon as you sign in, but if you change your mind, " +
                                        "you can enable this in the Startup tab in Task Manager.",
                            "Warning");
                    }
                    break;
                case StartupTaskState.Enabled:
                    if (isSetSelfStart == false)
                    {
                        _startupTask.Disable();
                        _isStartupTaskStateEnable = false;
                    }
                    else
                    {
                        _isStartupTaskStateEnable = true;
                    }
                    break;
            }
#endif
        }


#endif

        public static void SetSelfStart(bool isInstall, string appName)
        {
#if REGISTRY_METHOD
            try
            {
                SetSelfStartByRegistryKey(isInstall, appName);
                if (isInstall)
                    return;
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
            }
#endif

#if SHORTCUT_METHOD
            try
            {
                SetSelfStartByShortcut(isInstall, appName);
                if (isInstall)
                    return;
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
            }
#endif

#if STORE_UWP_METHOD
            Task.Factory.StartNew(() =>
            {
                SetSelfStartingHelper.SetSelfStartByStartupTask(appName, isInstall);
            }).Wait();
            if (isInstall)
                return;
#endif
            return;
        }

        public static bool IsSelfStart(string appName)
        {
            bool flag = false;
#if REGISTRY_METHOD
            try
            {
                flag |= IsSelfStartByRegistryKey(appName);
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
            }
#endif

#if SHORTCUT_METHOD
            try
            {
                flag |= IsSelfStartByShortcut(appName);
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error(e);
            }
#endif

#if STORE_UWP_METHOD
            flag |= _isStartupTaskStateEnable;
#endif
            return flag;
        }
    }
}