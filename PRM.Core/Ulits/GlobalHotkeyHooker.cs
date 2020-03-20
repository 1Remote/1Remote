using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Shawn.Ulits
{
    /*
        HOW TO USE:
        public MainWindow()
        {
            InitializeComponent();
            Loaded += (sender, args) =>
            {
                var r = GlobalHotkeyHooker.GetInstance().Regist(this, GlobalHotkeyHooker.HotkeyModifiers.MOD_CONTROL, Key.M, 
                    () => { MessageBox.Show("hook"); });
                switch (r)
                {
                    case GlobalHotkeyHooker.RetCode.Success:
                        break;
                    case GlobalHotkeyHooker.RetCode.ERROR_HOTKEY_NOT_REGISTERED:
                        MessageBox.Show("快捷键注册失败"); 
                        break;
                    case GlobalHotkeyHooker.RetCode.ERROR_HOTKEY_ALREADY_REGISTERED:
                        MessageBox.Show("快捷键已被占用"); 
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
        const int WM_HOTKEY = 0x312;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, HotkeyModifiers hotkeyModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("kernel32.dll")]
        public static extern uint GetLastError();
        #endregion

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
        #endregion

        ~GlobalHotkeyHooker()
        {
            Unregist();
        }

        public delegate void HotKeyCallBackHandler();
        private int _hotKeyId = 9000;
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
            MOD_ALT = 0x0001,
            MOD_CONTROL = 0x0002,
            MOD_SHIFT = 0x0004,
            MOD_WIN = 0x0008
        }

        public enum RetCode
        {
            Success = 0,
            ERROR_HOTKEY_NOT_REGISTERED = 1419,
            ERROR_HOTKEY_ALREADY_REGISTERED = 1409,
        }

        /// <summary>
        /// Regist a hotkey. If the function fails, the return value is *false*
        /// </summary>
        /// <param name="window">wpf window</param>
        /// <param name="hotkeyModifiers"></param>
        /// <param name="key"></param>
        /// <param name="callBack"></param>
        public RetCode Regist(Window window, HotkeyModifiers hotkeyModifiers, System.Windows.Input.Key key, HotKeyCallBackHandler callBack)
        {
            lock (_locker)
            {
                var win = new System.Windows.Interop.WindowInteropHelper(window);
                var hWnd = win.Handle;
                if (!_hookedhWnd.Contains(hWnd))
                {
                    var source = System.Windows.Interop.HwndSource.FromHwnd(hWnd);
                    source.AddHook(HookHandel);
                }

                ++_hotKeyId;

                // ref https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey
                var vk = System.Windows.Input.KeyInterop.VirtualKeyFromKey(key);
                if (!RegisterHotKey(hWnd, _hotKeyId, hotkeyModifiers, (uint)vk))    // If the function succeeds, the return value is nonzero.
                {
                    var errorCode = GetLastError();
                    return errorCode == (int)RetCode.ERROR_HOTKEY_ALREADY_REGISTERED ? RetCode.ERROR_HOTKEY_ALREADY_REGISTERED : RetCode.ERROR_HOTKEY_NOT_REGISTERED;
                }

                _hookedhWnd.Add(hWnd);
                _dictHotKeyId2hWnd[_hotKeyId] = hWnd;
                _dictHotKeyId2CallBack[_hotKeyId] = callBack;

                return RetCode.Success;
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
                if (_dictHotKeyId2hWnd.ContainsKey(hotKeyId))
                {
                    lock (_locker)
                    {
                        if (_dictHotKeyId2hWnd.ContainsKey(hotKeyId))
                        {
                            var hWnd = _dictHotKeyId2hWnd[hotKeyId];
                            UnregisterHotKey(hWnd, hotKeyId);
                            if (_hookedhWnd.Contains(hWnd))
                                _hookedhWnd.Remove(hWnd);
                            if (_dictHotKeyId2hWnd.ContainsKey(hotKeyId))
                                _dictHotKeyId2hWnd.Remove(hotKeyId);
                            if (_dictHotKeyId2CallBack.ContainsKey(hotKeyId))
                                _dictHotKeyId2CallBack.Remove(hotKeyId);
                        }
                    }
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
    }
}
