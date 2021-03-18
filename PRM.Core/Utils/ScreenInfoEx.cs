using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;

namespace Shawn.Utils
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

        public static ScreenInfoEx GetCurrentScreenBySystemPosition(System.Drawing.Point systemPosition)
        {
            var screen = System.Windows.Forms.Screen.FromPoint(systemPosition);
            return new ScreenInfoEx(screen);
        }

        public static ScreenInfoEx GetCurrentScreenByVirtualPosition(System.Drawing.Point virtualPosition)
        {
            var k = new ScreenInfoEx(Screen.PrimaryScreen).ScaleFactor;
            var systemPosition = new System.Drawing.Point((int)(virtualPosition.X * k), (int)(virtualPosition.Y * k));
            var screen = System.Windows.Forms.Screen.FromPoint(systemPosition);
            return new ScreenInfoEx(screen);
        }

        public static System.Drawing.Rectangle GetAllScreensSize()
        {
            var entireSize = System.Drawing.Rectangle.Empty;
            foreach (var screen in System.Windows.Forms.Screen.AllScreens)
                entireSize = System.Drawing.Rectangle.Union(entireSize, screen.Bounds);
            return entireSize;
        }

        private const int ENUM_CURRENT_SETTINGS = -1;

        /// <summary>
        /// On win10, if your PrimaryScreen is 200%, then GlobalScaleFactor would be 200%, your 2nd screen's real bounds = screen.Bounds / 200%.
        /// But if on a remote desk(local machine screen scale > 100%),  we can't read the real logic pix width, so GetGlobalScaleFactor will return a wrong value witch is lower than 1.0.
        /// </summary>
        /// <returns></returns>
        public static double GetGlobalScaleFactor()
        {
            var mainRealScaleFactor = 100.0 * Screen.PrimaryScreen.Bounds.Width / SystemParameters.PrimaryScreenWidth;
            var dmMain = new DEVMODE { dmSize = (short)Marshal.SizeOf(typeof(DEVMODE)) };
            EnumDisplaySettings(Screen.PrimaryScreen.DeviceName, ENUM_CURRENT_SETTINGS, ref dmMain);
            var mainLogicScaleFactor = 100.0 * dmMain.dmPelsWidth / Screen.PrimaryScreen.Bounds.Width;
            double k = mainRealScaleFactor / mainLogicScaleFactor;
            //#if DEV
            //            Console.WriteLine($@"{nameof(ScreenInfoEx)}: k = {mainRealScaleFactor } / {mainLogicScaleFactor} = {k}");
            //#endif
            return k;
        }

        public ScreenInfoEx(Screen screen)
        {
            Screen = screen;
            var dm = new DEVMODE { dmSize = (short)Marshal.SizeOf(typeof(DEVMODE)) };
            EnumDisplaySettings(screen.DeviceName, ENUM_CURRENT_SETTINGS, ref dm);
            DevMode = dm;

            double k = GetGlobalScaleFactor();
            if (k < 1)
            {
#if DEV
                Console.WriteLine($@"
// in this case , something goes wrong and let the VirtualBounds.Width > screen.Bounds.Width.
// ScaleFactor >= 100%, so VirtualBounds.Width = PhysicalPixWidth / ScaleFactor must <= screen.Bounds.Width
// i am not sure ↓ is right or wrong, BUT it did work on my case： 4k remote to win2016, then print ScreenInfoEx on win2016");
#endif
                // in this case , something goes wrong and let the VirtualBounds.Width > screen.Bounds.Width.
                // we can't read the real logic pix width(Screen.PrimaryScreen.Bounds.Width), 4k + 200% it should be 1920, and return 3840
                // ScaleFactor >= 100%, so VirtualBounds.Width = PhysicalPixWidth / ScaleFactor must <= screen.Bounds.Width
                // i am not sure ↓ is right or wrong, BUT it did work on my case： 4k remote to win2016, then print ScreenInfoEx on win2016
                VirtualBounds = new Rectangle(
                    (int)(screen.Bounds.Left),
                    (int)(screen.Bounds.Top),
                    (int)(screen.Bounds.Width),
                    (int)(screen.Bounds.Height)
                );
                VirtualWorkingArea = new Rectangle(
                    (int)(screen.WorkingArea.Left),
                    (int)(screen.WorkingArea.Top),
                    (int)(screen.WorkingArea.Width),
                    (int)(screen.WorkingArea.Height)
                );
            }
            else
            {
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

        /// <summary>
        /// GetCursorPos return the cursor position witch is on a system resolution, not on real resolution.
        /// if cursor is on PrimaryScreen, then return the cursor position on physical PrimaryScreen resolution(with out scale)
        /// else return cursor position on other screen system resolution(physical resolution * PrimaryScreen scale factor)
        /// </summary>
        /// <param name="pt"></param>
        /// <returns></returns>
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(ref Win32Point pt);

        [StructLayout(LayoutKind.Sequential)]
        internal struct Win32Point
        {
            public Int32 X;
            public Int32 Y;
        };

        public static System.Drawing.Point GetMouseVirtualPosition()
        {
            var w32Mouse = new Win32Point();
            GetCursorPos(ref w32Mouse);
            if (GetGlobalScaleFactor() < 1)
            {
                // on remote machine
                return new System.Drawing.Point((int)(w32Mouse.X), (int)(w32Mouse.Y));
            }
            else
            {
                // on local machine
                var k = new ScreenInfoEx(Screen.PrimaryScreen).ScaleFactor;
                return new System.Drawing.Point((int)(w32Mouse.X / k), (int)(w32Mouse.Y / k));
            }
        }

        public static System.Drawing.Point GetMouseSystemPosition()
        {
            var w32Mouse = new Win32Point();
            GetCursorPos(ref w32Mouse);
            if (GetGlobalScaleFactor() < 1)
            {
                // on remote machine
                var k = new ScreenInfoEx(Screen.PrimaryScreen).ScaleFactor;
                return new System.Drawing.Point((int)(w32Mouse.X * k), (int)(w32Mouse.Y * k));
            }
            else
            {
                // on local machine
                return new System.Drawing.Point((int)(w32Mouse.X), (int)(w32Mouse.Y));
            }
        }
    }
}