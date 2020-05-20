using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;

namespace Shawn.Ulits
{
    /*How to use:

            foreach (var screen in Screen.AllScreens)
            {
                var si = new ScreenInfoEx(screen);
                Console.WriteLine($"Device: {si.Screen.DeviceName}");
                Console.WriteLine($"Real Resolution: {si.PhysicalPixWidth}x{si.PhysicalPixHeigth}");
                Console.WriteLine($"Virtual T: {si.Screen.Bounds.Top} L:{si.Screen.Bounds.Left}");
                Console.WriteLine($"Virtual Resolution: {si.Screen.Bounds.Width}x{si.Screen.Bounds.Height}");
                Console.WriteLine($"ScaleFactor: {si.ScaleFactor * 100}%");
                Console.WriteLine();
            }
*/


    public class ScreenInfoEx
    {
        public static ScreenInfoEx GetCurrentScreen(int screenIndex)
        {
            if (screenIndex < 0
                || screenIndex >= System.Windows.Forms.Screen.AllScreens.Length)
            {
                return null;
            }
            return new ScreenInfoEx(System.Windows.Forms.Screen.AllScreens[screenIndex]);
        }
        public static ScreenInfoEx GetCurrentScreen(Window win)
        {
            var screen = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(win).Handle);
            return new ScreenInfoEx(screen);
        }
        public static ScreenInfoEx GetCurrentScreen(System.Drawing.Point point)
        {
            var screen = System.Windows.Forms.Screen.FromPoint(point);
            return new ScreenInfoEx(screen);
        }

        public ScreenInfoEx(Screen screen)
        {
            Screen = screen;
            var dm = new DEVMODE { dmSize = (short)Marshal.SizeOf(typeof(DEVMODE)) };
            const int ENUM_CURRENT_SETTINGS = -1;
            EnumDisplaySettings(screen.DeviceName, ENUM_CURRENT_SETTINGS, ref dm);
            DevMode = dm;
        }


        public int Index;

        public double ScaleFactor => PhysicalPixWidth * 1.0 / this.Screen.Bounds.Width;

        public System.Drawing.Point Center => new System.Drawing.Point(Screen.Bounds.X + Screen.Bounds.Width / 2, Screen.Bounds.Y + Screen.Bounds.Height / 2);
        public System.Drawing.Point WorkingAreaCenter => new System.Drawing.Point(Screen.WorkingArea.X + Screen.WorkingArea.Width / 2, Screen.WorkingArea.Y + Screen.WorkingArea.Height / 2);

        public Screen Screen
        {
            get => Screen.AllScreens[Index];
            set
            {
                for (int i = 0; i < Screen.AllScreens.Length; i++)
                {
                    var screen = Screen.AllScreens[i];
                    if (Equals(screen, value))
                    {
                        Index = i;
                        return;
                    }
                }
            }
        }

        public int PhysicalPixWidth => DevMode.dmPelsWidth;
        public int PhysicalPixHeigth => DevMode.dmPelsHeight;

        public DEVMODE DevMode { get; private set; }


        [DllImport("user32.dll")]
        public static extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

        [StructLayout(LayoutKind.Sequential)]
        public struct DEVMODE
        {
            private const int CCHDEVICENAME = 0x20;
            private const int CCHFORMNAME = 0x20;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x20)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public ScreenOrientation dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x20)]
            public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }


        
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(ref Win32Point pt);

        [StructLayout(LayoutKind.Sequential)]
        internal struct Win32Point
        {
            public Int32 X;
            public Int32 Y;
        };
        public static System.Drawing.Point GetMouseScreenPosition()
        {
            Win32Point w32Mouse = new Win32Point();
            GetCursorPos(ref w32Mouse);
            return new System.Drawing.Point(w32Mouse.X, w32Mouse.Y);
        }
    }
}
