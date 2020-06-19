using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using System.Windows.Interop;

namespace Shawn.Ulits
{
    /*How to use:

            foreach (var screen in Screen.AllScreens)
            {
                var si = new ScreenInfoEx(screen);
                Console.WriteLine($@"Device: {si.Screen.DeviceName}");
                Console.WriteLine($@"Real Resolution: {si.PhysicalPixWidth}x{si.PhysicalPixHeight}");
                Console.WriteLine($@"Screen.Bounds T: {si.Screen.Bounds.Top} L:{si.Screen.Bounds.Left}");
                Console.WriteLine($@"Screen.Bounds Resolution: {si.Screen.Bounds.Width}x{si.Screen.Bounds.Height}");
                Console.WriteLine($@"Virtual T: {si.VirtualBounds.Top} L:{si.VirtualBounds.Left}");
                Console.WriteLine($@"Virtual Resolution: {si.VirtualBounds.Width}x{si.VirtualBounds.Height}");
                Console.WriteLine($@"ScaleFactor: {si.ScaleFactor * 100}%");
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
            const int ENUM_CURRENT_SETTINGS = -1;
            var mainRealScaleFactor = (100.0 * System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width / SystemParameters.PrimaryScreenWidth);


            var dmMain = new DEVMODE { dmSize = (short)Marshal.SizeOf(typeof(DEVMODE)) };
            EnumDisplaySettings(Screen.PrimaryScreen.DeviceName, ENUM_CURRENT_SETTINGS, ref dmMain);
            var mainLogicScaleFactor = 100.0 * dmMain.dmPelsWidth / Screen.PrimaryScreen.Bounds.Width;

            double k = mainRealScaleFactor / mainLogicScaleFactor;


            Screen = screen;
            var dm = new DEVMODE { dmSize = (short)Marshal.SizeOf(typeof(DEVMODE)) };
            EnumDisplaySettings(screen.DeviceName, ENUM_CURRENT_SETTINGS, ref dm);
            DevMode = dm;

            VirtualBounds = new Rectangle(
                (int)(screen.Bounds.Left / k),
                (int)(screen.Bounds.Top / k),
                (int)(screen.Bounds.Width / k),
                (int)(screen.Bounds.Height / k)
                );
            VirtualWorkingArea = new Rectangle(
                (int)(screen.WorkingArea.Left / k),
                (int)(screen.WorkingArea.Top / k),
                (int)(screen.WorkingArea.Width / k),
                (int)(screen.WorkingArea.Height / k)
            );
        }



        public double ScaleFactor => PhysicalPixWidth * 1.0 / VirtualBounds.Width;
        public int Index { get; private set; }
        public Screen Screen
        {
            get => Screen.AllScreens[Index];
            private set
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
        public int PhysicalPixHeight => DevMode.dmPelsHeight;

        public Rectangle VirtualBounds { get; }
        public Rectangle VirtualWorkingArea { get; }
        public System.Drawing.Point VirtualCenter => new System.Drawing.Point(VirtualBounds.X + VirtualBounds.Width / 2, VirtualBounds.Y + VirtualBounds.Height / 2);
        public System.Drawing.Point VirtualWorkingAreaCenter => new System.Drawing.Point(VirtualWorkingArea.X + VirtualWorkingArea.Width / 2,
            VirtualWorkingArea.Y + VirtualWorkingArea.Height / 2);


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
