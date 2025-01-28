using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Media.Imaging;
using _1RM.Model;
using _1RM.Model.Protocol.Base;
using _1RM.Utils;
using _1RM.Utils.WindowsApi.WindowsShortcutFactory;
using _1RM.View;
using _1RM.View.Settings.General;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.Image;

namespace _1RM.Service
{
    internal static class AppStartupHelper
    {
        private const string SEPARATOR = "+++!+!+++";
        private const string ACTIVATE = "activate";
        public const string APP_START_MINIMIZED = "start-minimized";


        public const string INSTALL = "install";
        public const string UNINSTALL = "uninstall";
        public const string DESKTOP_SHORTCUT_INSTALL = "install-desktop-shortcut";
        public const string DESKTOP_SHORTCUT_UNINSTALL = "uninstall-desktop-shortcut";
        public const string RUN_AUTO_AT_OS_START_INSTALL = "install-startup";
        public const string RUN_AUTO_AT_OS_START_UNINSTALL = "uninstall-startup";

        public const string ULID_PREFIX = "ULID:";
        public const string TAG_PREFIX = "#";

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
                    NamedPipeHelper.NamedPipeSendMessage(string.Join(SEPARATOR, _args));
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
                    SimpleLogHelper.Info("NamedPipeServerStream get: " + message);
                    var strings = new HashSet<string>(message.Split(new[] { SEPARATOR }, StringSplitOptions.RemoveEmptyEntries).Distinct());
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
                if (arg.StartsWith(TAG_PREFIX))
                {
                    // tag connect
                    var tagName = arg.Substring(1);

                    var ss = IoC.Get<GlobalData>().VmItemList
                        .Where(x => x.Server.Tags.Any(x => string.Equals(x, tagName, StringComparison.CurrentCultureIgnoreCase))
                                                    && servers.Contains(x.Server) == false)
                        .Select(x => x.Server)
                        .ToArray();
                    servers.AddRange(ss);
                }
                else if (arg.StartsWith(ULID_PREFIX, StringComparison.OrdinalIgnoreCase))
                {
                    var id = arg.Substring(ULID_PREFIX.Length).Trim();
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

            if (servers.Count > 0)
                GlobalEventHelper.OnRequestServersConnect?.Invoke(servers, fromView: "CLI");
        }


        private static Bitmap? MargeBitmap(List<Bitmap?> bitmapsRaw)
        {
            List<Bitmap?> bitmaps = bitmapsRaw.Where(x => x != null).ToList();
            if (bitmaps.Count == 0)
                return null;
            if (bitmaps.Count == 1)
                return bitmaps.First();
            if (bitmaps.Count == 2)
            {
                var first = bitmaps.First()!;
                var second = bitmaps.Last()!;
                // 水平拼接
                int width = first.Width + second.Width;
                int height = Math.Max(first.Height, second.Height);
                Bitmap result = new Bitmap(width, height, first.PixelFormat);
                Graphics gr = Graphics.FromImage(result);
                gr.DrawImage(first, 0, 0);
                gr.DrawImage(second, first.Width, 0);
                gr.Dispose();
                return result;
            }

            if (bitmaps.Count == 3)
            {
                var first = MargeBitmap(new List<Bitmap?>() { bitmaps[0], bitmaps[1] });
                var second = bitmaps.Last();
                // 竖直拼接
                int width = Math.Max(first!.Width, second!.Width);
                int height = first.Height + second.Height;
                Bitmap result = new Bitmap(width, height, first.PixelFormat);
                Graphics gr = Graphics.FromImage(result);
                gr.DrawImage(first, first.Width < width ? (width - first.Width) / 2 : 0, 0);
                gr.DrawImage(second, second.Width < width ? (width - second.Width) / 2 : 0, first.Height);
                gr.Dispose();
                return result;
            }
            else
            {
                var first = MargeBitmap(new List<Bitmap?>() { bitmaps[0], bitmaps[1] })!;
                var second = MargeBitmap(new List<Bitmap?>() { bitmaps[2], bitmaps[3] })!;
                // 竖直拼接
                int width = Math.Max(first.Width, second.Width);
                int height = first.Height + second.Height;
                Bitmap result = new Bitmap(width, height, first.PixelFormat);
                Graphics gr = Graphics.FromImage(result);
                gr.DrawImage(first, first.Width < width ? (width - first.Width) / 2 : 0, 0);
                gr.DrawImage(second, second.Width < width ? (width - second.Width) / 2 : 0, first.Height);
                gr.Dispose();
                return result;
            }
        }

        public static string? MakeIcon(string name, List<BitmapSource?> bitmapSources)
        {
            var list = bitmapSources.Select(x => x?.ToBitmap()).Where(x => x != null).ToList();
            var bitmap = MargeBitmap(list);
            return MakeIcon(name, bitmap);
        }

        public static string? MakeIcon(string name, BitmapSource? bitmapSource)
        {
            return MakeIcon(name, bitmapSource?.ToBitmap());
        }

        public static string? MakeIcon(string name, Bitmap? bitmap)
        {
            if (bitmap == null) return null;
            MakeFilenameValid(ref name);
            string? iconPath = null;
            iconPath = System.IO.Path.Combine(AppPathHelper.Instance.LocalityIconDirPath, $"{name}.ico");
            Executor.TryCatch(() =>
            {
                if (File.Exists(iconPath)) File.Delete(iconPath);
            });
            if (IcoHelper.ConvertToIcon(bitmap, iconPath, 64)
                && File.Exists(iconPath))
            {
                //File.SetAttributes(iconPath, FileAttributes.ReadOnly);
                //File.SetAttributes(iconPath, FileAttributes.Hidden);
                return iconPath;
            }
            return null;
        }

        private static void MakeFilenameValid(ref string name)
        {
            name = name.Replace('\\', '_')
                       .Replace('/', '_')
                       .Replace(':', '_')
                       .Replace('*', '_')
                       .Replace('?', '_')
                       .Replace('"', '_')
                       .Replace('<', '_')
                       .Replace('>', '_')
                       .Replace('|', '_');
        }

        public static void InstallDesktopShortcutByTag(string name, string tag, string? iconLocation = null)
        {
            if (name.StartsWith("Tag=") == false)
                name = "Tag=" + name;
            InstallDesktopShortcut(true, name, TAG_PREFIX + tag + $" --{APP_START_MINIMIZED}", iconLocation);
        }
        public static void InstallDesktopShortcutByUlid(string name, IEnumerable<string> ulids, string? iconLocation = null)
        {
            if (ulids.Any())
                InstallDesktopShortcut(true, name, ULID_PREFIX + string.Join($" & {ULID_PREFIX}", ulids) + $" --{APP_START_MINIMIZED}", iconLocation);
        }

        public static void InstallDesktopShortcut(bool isInstall, string name = Assert.APP_DISPLAY_NAME, string parameter = "", string? iconLocation = null)
        {
            MakeFilenameValid(ref name);
            try
            {
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                var shortcutPath = System.IO.Path.Combine(desktopPath, name + ".lnk");
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
                        IconLocation = iconLocation ?? Process.GetCurrentProcess().MainModule!.FileName!,
                        Arguments = parameter,
                    };
                    shortcut.Save(shortcutPath);
                }
            }
            catch (Exception e)
            {
                MsAppCenterHelper.Error(e);
            }
        }
    }
}
