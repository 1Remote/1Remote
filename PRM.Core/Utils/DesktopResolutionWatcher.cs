using System;
using Microsoft.Win32;

namespace Shawn.Utils
{
    public class DesktopResolutionWatcher
    {
        public Action OnDesktopResolutionChanged;
        private int _lastScreenCount = 0;
        private System.Drawing.Rectangle _lastScreenRectangle = System.Drawing.Rectangle.Empty;

        public DesktopResolutionWatcher()
        {
            _lastScreenCount = System.Windows.Forms.Screen.AllScreens.Length;
            _lastScreenRectangle = ScreenInfoEx.GetAllScreensSize();
            SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
        }

        ~DesktopResolutionWatcher()
        {
            SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;
        }

        private void SystemEvents_DisplaySettingsChanged(object sender, EventArgs e)
        {
            SimpleLogHelper.Debug($"Resolution Changed: {e}");
            var newScreenCount = System.Windows.Forms.Screen.AllScreens.Length;
            var newScreenRectangle = ScreenInfoEx.GetAllScreensSize();
            if (newScreenCount != _lastScreenCount
                || newScreenRectangle.Width != _lastScreenRectangle.Width
                || newScreenRectangle.Height != _lastScreenRectangle.Height)
                OnDesktopResolutionChanged?.Invoke();
            _lastScreenCount = newScreenCount;
            _lastScreenRectangle = newScreenRectangle;
        }
    }
}