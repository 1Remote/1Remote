using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using PRM.Core.Model;

#if FOR_MICROSOFT_STORE
using Windows.ApplicationModel;
#endif

namespace Shawn.Utils
{
#if !FOR_MICROSOFT_STORE_ONLY
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
#endif


    public static class SetSelfStartingHelper
    {
#if !FOR_MICROSOFT_STORE_ONLY
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
        private static string GetShortCutPath()
        {
            var startUpPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);
            var exePath = Process.GetCurrentProcess().MainModule.FileName;
            string md5 = MD5EncryptString(exePath);
            var shortcutPath = System.IO.Path.Combine(startUpPath, string.Format("{0}_{1}.lnk", SystemConfig.AppName, md5));
            return shortcutPath;
        }
        /// <summary>
        /// 用于实现提权操作的类
        /// Elevated Permission 后，杀死原进程
        /// </summary>
        private class AppElvatedHelper
        {
            public static bool IsElvated()
            {
                var wi = WindowsIdentity.GetCurrent();
                var wp = new WindowsPrincipal(wi);
                var runAsAdmin = wp.IsInRole(WindowsBuiltInRole.Administrator);
                return runAsAdmin;
            }
            /// <summary>
            /// 判断app是否以管理员权限运行，不是的话提升权限重启app
            /// </summary>
            /// <param name="startupMode"></param>
            public static void ElvateApp(StartupMode startupMode)
            {
                if (!IsElvated())
                {
                    // It is not possible to launch a ClickOnce app as administrator directly,  
                    // so instead we launch the app as administrator in a new process.  
                    var processInfo = new ProcessStartInfo(Assembly.GetExecutingAssembly().CodeBase);

                    // The following properties run the new process as administrator  
                    processInfo.UseShellExecute = true;
                    processInfo.Verb = "runas";
                    processInfo.Arguments = startupMode.ToString();
                    // Start the new process  
                    try
                    {
                        Process.Start(processInfo);
                    }
                    catch (Exception)
                    {
                    }

                    // Shut down the current process  
                    Environment.Exit(0);
                }
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
        }
#endif















        public static string StartupTaskId = "PremoteM";
        public static bool IsSetSelfStart()
        {
            Task<bool> t = Task.Factory.StartNew<bool>(() =>
            {
                try
                {
                    var result = StartupTask.GetAsync(StartupTaskId).GetResults();
                    switch (result.State)
                    {
                        case StartupTaskState.Disabled:
                        case StartupTaskState.DisabledByUser:
                        case StartupTaskState.DisabledByPolicy:
                            return false;
                        case StartupTaskState.Enabled:
                        case StartupTaskState.EnabledByPolicy:
                            return true;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                catch (Exception)
                {
#if !FOR_MICROSOFT_STORE_ONLY
                    if (File.Exists(GetShortCutPath()))
                        return true;
                    else
                        return false;
#else
                    throw;
#endif
                }
            });
            t.Wait();
            return t.Result;
        }

        public static void SetSelfStart()
        {
            var t = Task.Factory.StartNew(() =>
            {
                try
                {
                    var result = StartupTask.GetAsync(StartupTaskId).GetResults();
                    switch (result.State)
                    {
                        case StartupTaskState.Disabled:
                        case StartupTaskState.DisabledByUser:
                        case StartupTaskState.DisabledByPolicy:
                            result.RequestEnableAsync().GetResults();
                            break;
                        case StartupTaskState.Enabled:
                        case StartupTaskState.EnabledByPolicy:
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                catch (Exception)
                {
#if !FOR_MICROSOFT_STORE_ONLY
                    if (IsSetSelfStart())
                        return;

                    if (AppElvatedHelper.IsElvated())
                    {
                        try
                        {
                            var exePath = Process.GetCurrentProcess().MainModule.FileName;
                            var shortcutPath = GetShortCutPath();
                            if (File.Exists(shortcutPath))
                                File.Delete(shortcutPath);
                            var shell = new IWshRuntimeLibrary.WshShell();
                            var shortcut = (IWshRuntimeLibrary.IWshShortcut)shell.CreateShortcut(shortcutPath);
                            shortcut.TargetPath = exePath; // exe路径
                            shortcut.Arguments = ""; // 启动参数
                            shortcut.IconLocation = exePath;
                            shortcut.WorkingDirectory = System.IO.Path.GetDirectoryName(exePath);
                            shortcut.Description = "";
                            shortcut.Save();
                            // 取消其他自启动
                            var di = new FileInfo(shortcutPath).Directory;
                            var fis = di.GetFiles(SystemConfig.AppName + "_*");
                            if (fis?.Length > 0)
                            {
                                foreach (var fi in fis)
                                {
                                    if (fi.FullName != shortcutPath)
                                        File.Delete(fi.FullName);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message);
                        }
                    }
                    else
                    {
                        // 以高操作权限执行
                        AppElvatedHelper.RunElvatedTask(StartupMode.SetSelfStart);
                    }
#else
                    throw;
#endif
                }
            });
            t.Wait();
        }

        public static void UnsetSelfStart()
        {
            var t = Task.Factory.StartNew(() =>
            {
                try
                {
                    var result = StartupTask.GetAsync(StartupTaskId).GetResults();
                    switch (result.State)
                    {
                        case StartupTaskState.Disabled:
                        case StartupTaskState.DisabledByUser:
                        case StartupTaskState.DisabledByPolicy:
                            break;
                        case StartupTaskState.Enabled:
                        case StartupTaskState.EnabledByPolicy:
                            result.Disable();
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                catch (Exception e)
                {
#if !FOR_MICROSOFT_STORE_ONLY
                    var shortcutPath = GetShortCutPath();
                    if (File.Exists(shortcutPath))
                    {
                        if (AppElvatedHelper.IsElvated())
                        {
                            try
                            {
                                File.Delete(shortcutPath);
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(ex.Message);
                            }
                        }
                        else
                        {
                            AppElvatedHelper.RunElvatedTask(StartupMode.UnsetSelfStart);
                        }
                    }
#else
                    throw;
#endif
                }
            });
            t.Wait();
        }

    }
}
