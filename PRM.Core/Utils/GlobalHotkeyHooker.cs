using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;

namespace Shawn.Utils
{
    /*
        HOW TO USE:
        public MainWindow()
        {
            InitializeComponent();
            Loaded += (sender, args) =>
            {
                var r = GlobalHotkeyHooker.Instance.Regist(this,(uint)GlobalHotkeyHooker.HotkeyModifiers.Ctrl | (uint)GlobalHotkeyHooker.HotkeyModifiers.Alt, Key.M,
                    () => { MessageBox.Show("hook"); });
                switch (r.Item1)
                {
                    case GlobalHotkeyHooker.RetCode.Success:
                        break;

                    case GlobalHotkeyHooker.RetCode.ERROR_HOTKEY_NOT_REGISTERED:
                        MessageBox.Show("快捷键注册失败" + ": " + r.Item2);
                        break;

                    case GlobalHotkeyHooker.RetCode.ERROR_HOTKEY_ALREADY_REGISTERED:
                        MessageBox.Show("快捷键已被占用" + ": " + r.Item2);
                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            };
        }
     */

    /// <summary>
    /// for wpf window global hotkey regist
    /// </summary>
    public class GlobalHotkeyHooker
    {
        #region user32 api

        private const int WM_HOTKEY = 0x312;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint hotkeyModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("kernel32.dll")]
        public static extern uint GetLastError();

        #endregion user32 api

        #region 单例

        private static GlobalHotkeyHooker uniqueInstance;
        private static readonly object InstanceLock = new object();

        private GlobalHotkeyHooker()
        {
        }

        public static GlobalHotkeyHooker GetInstance()
        {
            if (uniqueInstance == null)
            {
                lock (InstanceLock)
                {
                    if (uniqueInstance == null)
                    {
                        uniqueInstance = new GlobalHotkeyHooker();
                    }
                }
            }
            return uniqueInstance;
        }

        public static GlobalHotkeyHooker Instance => GetInstance();

        #endregion 单例

        ~GlobalHotkeyHooker()
        {
            Unregist();
        }

        public delegate void HotKeyCallBackHandler();

        private int _hotKeyId = 1000;
        private object _locker = new object();
        private HashSet<IntPtr> _hookedhWnd = new HashSet<IntPtr>();
        private Dictionary<int, IntPtr> _dictHotKeyId2hWnd = new Dictionary<int, IntPtr>();
        private Dictionary<int, HotKeyCallBackHandler> _dictHotKeyId2CallBack = new Dictionary<int, HotKeyCallBackHandler>();

        /// <summary>
        /// The keys that must be pressed in combination with the key specified by the uVirtKey parameter in order to generate the WM_HOTKEY message.
        /// ref: https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey
        /// </summary>
        public enum HotkeyModifiers
        {
            Alt = 0x0001,
            Ctrl = 0x0002,
            Shift = 0x0004,
            Win = 0x0008
        }

        public enum RetCode
        {
            Success = 0,
            ERROR_HOTKEY_NOT_REGISTERED = 1419,
            ERROR_HOTKEY_ALREADY_REGISTERED = 1409,
        }

        /// <summary>
        /// Regist a hotkey, it will return: status code + hot key in string + hot key id
        /// </summary>
        public Tuple<RetCode, string, int> Regist(Window window, ModifierKeys hotkeyModifiers, System.Windows.Input.Key key, HotKeyCallBackHandler callBack)
        {
            return Register(window, (uint)hotkeyModifiers, key, callBack);
        }

        /// <summary>
        /// Regist a hotkey, it will return: status code + hot key in string + hot key id
        /// </summary>
        public Tuple<RetCode, string, int> Regist(Window window, HotkeyModifiers hotkeyModifiers, System.Windows.Input.Key key, HotKeyCallBackHandler callBack)
        {
            return Register(window, (uint)hotkeyModifiers, key, callBack);
        }

        /// <summary>
        /// Regist a hotkey, it will return: status code + hot key in string + hot key id
        /// </summary>
        /// <param name="window">wpf window</param>
        /// <param name="hotkeyModifiers"></param>
        /// <param name="key"></param>
        /// <param name="callBack"></param>
        public Tuple<RetCode, string, int> Register(Window window, uint hotkeyModifiers, System.Windows.Input.Key key, HotKeyCallBackHandler callBack)
        {
            lock (_locker)
            {
                var hotKeyString = GetHotKeyString(hotkeyModifiers, key);
                var hWnd = IntPtr.Zero;
                if (window != null)
                {
                    var win = new System.Windows.Interop.WindowInteropHelper(window);
                    hWnd = win.Handle;
                    if (!_hookedhWnd.Contains(hWnd) && hWnd != IntPtr.Zero)
                    {
                        var source = System.Windows.Interop.HwndSource.FromHwnd(hWnd);
                        source.RemoveHook(HookHandel);
                        source.AddHook(HookHandel);
                    }
                }

                while (_dictHotKeyId2hWnd.ContainsKey(_hotKeyId))
                {
                    ++_hotKeyId;
                }

                // ref https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey
                var vk = System.Windows.Input.KeyInterop.VirtualKeyFromKey(key);
                if (!RegisterHotKey(hWnd, _hotKeyId, hotkeyModifiers, (uint)vk))    // If the function succeeds, the return value is nonzero.
                {
                    var errorCode = GetLastError();
                    var code = errorCode == (int)RetCode.ERROR_HOTKEY_ALREADY_REGISTERED ? RetCode.ERROR_HOTKEY_ALREADY_REGISTERED : RetCode.ERROR_HOTKEY_NOT_REGISTERED;
                    return new Tuple<RetCode, string, int>(code, hotKeyString, 0);
                }

                _hookedhWnd.Add(hWnd);
                _dictHotKeyId2hWnd[_hotKeyId] = hWnd;
                _dictHotKeyId2CallBack[_hotKeyId] = callBack;

                return new Tuple<RetCode, string, int>(RetCode.Success, hotKeyString, _hotKeyId);
            }
        }

        private IntPtr HookHandel(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (_dictHotKeyId2CallBack.TryGetValue(id, out var callback))
                {
                    callback();
                    //handled = true;
                }
            }
            return IntPtr.Zero;
        }

        public void Unregist(Window window)
        {
            var win = new System.Windows.Interop.WindowInteropHelper(window);
            var hWnd = win.Handle;
            lock (_locker)
            {
                foreach (var pair in _dictHotKeyId2hWnd.Where(var => var.Value == hWnd).ToArray())
                {
                    Unregist(pair.Key);
                }
            }
        }

        public void Unregist(HotKeyCallBackHandler callBack)
        {
            lock (_locker)
            {
                foreach (var pair in _dictHotKeyId2CallBack.Where(pair => pair.Value == callBack).ToArray())
                {
                    Unregist(pair.Key);
                }
            }
        }

        public void Unregist(int hotKeyId)
        {
            try
            {
                if (!_dictHotKeyId2hWnd.ContainsKey(hotKeyId)) return;
                lock (_locker)
                {
                    if (!_dictHotKeyId2hWnd.ContainsKey(hotKeyId)) return;
                    var hWnd = _dictHotKeyId2hWnd[hotKeyId];
                    UnregisterHotKey(hWnd, hotKeyId);
                    if (_hookedhWnd.Contains(hWnd))
                        _hookedhWnd.Remove(hWnd);
                    if (_dictHotKeyId2hWnd.ContainsKey(hotKeyId))
                        _dictHotKeyId2hWnd.Remove(hotKeyId);
                    if (_dictHotKeyId2CallBack.ContainsKey(hotKeyId))
                        _dictHotKeyId2CallBack.Remove(hotKeyId);
                    _hotKeyId = 1000;
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        public void Unregist()
        {
            lock (_locker)
            {
                foreach (var id in _dictHotKeyId2CallBack.Keys.ToArray())
                {
                    Unregist(id);
                }
            }
        }

        public static string GetHotKeyString(uint hotkeyModifiers, System.Windows.Input.Key key)
        {
            var hotKeyString = "";
            if ((hotkeyModifiers & (uint)GlobalHotkeyHooker.HotkeyModifiers.Shift) > 0)
                hotKeyString += GlobalHotkeyHooker.HotkeyModifiers.Shift.ToString() + " + ";
            if ((hotkeyModifiers & (uint)GlobalHotkeyHooker.HotkeyModifiers.Ctrl) > 0)
                hotKeyString += GlobalHotkeyHooker.HotkeyModifiers.Ctrl.ToString() + " + ";
            if ((hotkeyModifiers & (uint)GlobalHotkeyHooker.HotkeyModifiers.Alt) > 0)
                hotKeyString += GlobalHotkeyHooker.HotkeyModifiers.Alt.ToString() + " + ";
            if ((hotkeyModifiers & (uint)GlobalHotkeyHooker.HotkeyModifiers.Win) > 0)
                hotKeyString += GlobalHotkeyHooker.HotkeyModifiers.Win.ToString() + " + ";

            if (
                key == Key.LeftCtrl ||
                key == Key.RightCtrl ||
                key == Key.LeftAlt ||
                key == Key.RightAlt ||
                key == Key.LeftShift ||
                key == Key.RightShift ||
                key == Key.LWin ||
                key == Key.RWin ||
                key == Key.Clear ||
                key == Key.OemClear ||
                key == Key.Escape ||
                key == Key.Apps)
            {
            }
            else
                hotKeyString += key.ToString();
            return hotKeyString;
        }
    }
}