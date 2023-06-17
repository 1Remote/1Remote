//#define SHORTCUT_METHOD
#define REGISTRY_METHOD

#if FOR_MICROSOFT_STORE_ONLY
#undef REGISTRY_METHOD
#define STORE_UWP_METHOD
#endif

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using _1RM.Service;
#if STORE_UWP_METHOD
using Windows.ApplicationModel;
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

        public static bool IsElvated()
        {
            var wi = WindowsIdentity.GetCurrent();
            var wp = new WindowsPrincipal(wi);
            var runAsAdmin = wp.IsInRole(WindowsBuiltInRole.Administrator);
            return runAsAdmin;
        }

        /// <summary>
        /// 以高权限执行某些任务
        /// </summary>
        /// <param name="startupMode"></param>
        public static void RunElvatedTask(StartupMode startupMode)
        {
            // It is not possible to launch a ClickOnce app as administrator directly,
            // so instead we launch the app as administrator in a new process.
            var processInfo = new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName);

            // The following properties run the new process as administrator
            processInfo.UseShellExecute = true;
            processInfo.Verb = "runas";
            processInfo.Arguments = startupMode.ToString();
            // Start the new process
            try
            {
                Process.Start(processInfo);
            }
            catch
            {
            }
        }

        private static string MD5EncryptString(string str)
        {
            var md5 = MD5.Create();
            // 将字符串转换成字节数组
            var byteOld = Encoding.UTF8.GetBytes(str);
            // 调用加密方法
            var byteNew = md5.ComputeHash(byteOld);
            // 将加密结果转换为字符串
            var sb = new StringBuilder();
            foreach (var b in byteNew)
            {
                // 将字节转换成16进制表示的字符串，
                sb.Append(b.ToString("x2"));
            }
            // 返回加密的字符串
            return sb.ToString();
        }

        private static string GetStartupShortcutPath(string appName)
        {
            var startUpPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            var exePath = Process.GetCurrentProcess().MainModule.FileName;
            string md5 = MD5EncryptString(exePath);
            var shortcutPath = System.IO.Path.Combine(startUpPath, $"{appName}_{md5}.lnk");
            return shortcutPath;
        }

        private static void CleanUpShortcut(string exceptionShortcutPath, string appName)
        {
            if (IsElvated())
            {
                var di = new FileInfo(exceptionShortcutPath).Directory;
                var fis = di.GetFiles(appName + "_*");
                if (fis?.Length > 0)
                {
                    foreach (var fi in fis)
                    {
                        if (fi.FullName != exceptionShortcutPath)
                            File.Delete(fi.FullName);
                    }
                }
            }
        }

        private static async void UnsetSelfStartByStartupByShortcut(string shortcutPath, string appName)
        {
            var hasStartup = await IsSelfStartByShortcut(appName);
            if (!hasStartup) return;
            if (IsElvated())
                File.Delete(shortcutPath);
            else
                RunElvatedTask(StartupMode.UnsetSelfStart);
        }

        private static async void SetSelfStartByStartupByShortcut(string shortcutPath, string appName)
        {
            var hasStartup = await IsSelfStartByShortcut(appName);
            if (hasStartup) return;

            if (IsElvated())
            {
                var exePath = Process.GetCurrentProcess().MainModule.FileName;
                if (File.Exists(shortcutPath))
                    File.Delete(shortcutPath);
                var shell = new IWshRuntimeLibrary.WshShell();
                var shortcut = (IWshRuntimeLibrary.IWshShortcut)shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = exePath;
                shortcut.Arguments = "";
                shortcut.IconLocation = exePath;
                shortcut.WorkingDirectory = System.IO.Path.GetDirectoryName(exePath);
                shortcut.Description = "";
                shortcut.Save();
            }
            else
            {
                RunElvatedTask(StartupMode.SetSelfStart);
            }
        }

        public static async Task<bool> IsSelfStartByShortcut(string appName)
        {
            return File.Exists(GetStartupShortcutPath(appName));
        }

        public static async void SetSelfStartByShortcut(bool isSetSelfStart, string appName)
        {
            var shortcutPath = GetStartupShortcutPath(appName);
            CleanUpShortcut(shortcutPath, appName);

            if (isSetSelfStart)
                SetSelfStartByStartupByShortcut(shortcutPath, appName);
            else
                UnsetSelfStartByStartupByShortcut(shortcutPath, appName);
        }

#endif

#if REGISTRY_METHOD

        public static bool IsSelfStartByRegistryKey(string appName)
        {
            var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            return key?.GetValueNames().Contains(appName) == true;
        }

        public static void SetSelfStartByRegistryKey(bool isSetSelfStart, string appName)
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
        public static bool IsStartupTaskStateEnable = false;
        public static async void SetSelfStartByStartupTask(string appName, bool? isSetSelfStart = null)
        {
            _startupTask ??= await StartupTask.GetAsync(appName); // Pass the task ID you specified in the appxmanifest file
            switch (_startupTask.State)
            {
                case StartupTaskState.Disabled:
                    if (isSetSelfStart == true)
                    {
                        // Task is disabled but can be enabled.
                        var newState = await _startupTask.RequestEnableAsync(); // ensure that you are on a UI thread when you call RequestEnableAsync()
                        Debug.WriteLine("Request to enable startup, result = {0}", newState);
                        IsStartupTaskStateEnable = true;
                    }
                    else
                    {
                        IsStartupTaskStateEnable = false;
                    }

                    break;
                case StartupTaskState.DisabledByUser:
                case StartupTaskState.DisabledByPolicy:
                    IsStartupTaskStateEnable = false;
                    if (isSetSelfStart == true)
                    {
                        // Task is disabled and user must enable it manually.
                        MessageBox.Show("You have disabled this app's ability to run " +
                                        "as soon as you sign in, but if you change your mind, " +
                                        "you can enable this in the Startup tab in Task Manager.",
                            "Warning");
                    }
                    break;
                //Debug.WriteLine("Startup disabled by group policy, or not supported on this device");
                //break;
                case StartupTaskState.Enabled:
                    if (isSetSelfStart == false)
                    {
                        _startupTask.Disable();
                        IsStartupTaskStateEnable = false;
                    }
                    else
                    {
                        IsStartupTaskStateEnable = true;
                    }
                    break;
            }
        }

#endif
    }
}