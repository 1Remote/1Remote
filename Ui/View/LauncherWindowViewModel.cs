using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using _1RM.Service;
using _1RM.Utils;
using Shawn.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;
using Stylet;
using _1RM.View.Launcher;


namespace _1RM.View
{
    public class LauncherWindowViewModel : NotifyPropertyChangedBaseScreen
    {
        public const double LAUNCHER_LIST_AREA_WIDTH = 400;
        public const double LAUNCHER_GRID_KEYWORD_HEIGHT = 46;
        public const double LAUNCHER_SERVER_LIST_ITEM_HEIGHT = 40;
        public const double LAUNCHER_ACTION_LIST_ITEM_HEIGHT = 34;
        public const double LAUNCHER_OUTLINE_CORNER_RADIUS = 8;
        public static readonly CornerRadius LauncherOutlineCornerRadiusObj = new CornerRadius(LAUNCHER_OUTLINE_CORNER_RADIUS, LAUNCHER_OUTLINE_CORNER_RADIUS, LAUNCHER_OUTLINE_CORNER_RADIUS, LAUNCHER_OUTLINE_CORNER_RADIUS);


        public const int MAX_SERVER_COUNT = 8;
        public const double MAX_SELECTION_HEIGHT = LauncherWindowViewModel.LAUNCHER_SERVER_LIST_ITEM_HEIGHT * MAX_SERVER_COUNT;
        public const double MAX_WINDOW_HEIGHT = LauncherWindowViewModel.LAUNCHER_GRID_KEYWORD_HEIGHT + MAX_SELECTION_HEIGHT;

        private Visibility _serverSelectionsViewVisibility = Visibility.Visible;
        public Visibility ServerSelectionsViewVisibility
        {
            get => _serverSelectionsViewVisibility;
            set => SetAndNotifyIfChanged(ref _serverSelectionsViewVisibility, value);
        }

        public ServerSelectionsViewModel ServerSelectionsViewModel { get; } = IoC.Get<ServerSelectionsViewModel>();
        public QuickConnectionViewModel QuickConnectionViewModel { get; } = IoC.Get<QuickConnectionViewModel>();
        private ConfigurationService _configurationService => IoC.Get<ConfigurationService>();

        #region properties


        private double _gridMainHeight;
        public double GridMainHeight
        {
            get => _gridMainHeight;
            set
            {
                if (SetAndNotifyIfChanged(ref _gridMainHeight, value))
                {
                    GridMainClip = new RectangleGeometry(new Rect(new Size(LAUNCHER_LIST_AREA_WIDTH, GridMainHeight)), LAUNCHER_OUTLINE_CORNER_RADIUS, LAUNCHER_OUTLINE_CORNER_RADIUS);
                }
            }
        }


        private RectangleGeometry? _gridMainClip = null;
        public RectangleGeometry? GridMainClip
        {
            get => _gridMainClip;
            set => SetAndNotifyIfChanged(ref _gridMainClip, value);
        }


        public double GridNoteHeight { get; }

        private double _noteWidth = 500;

        public double NoteWidth
        {
            get => _noteWidth;
            private set => SetAndNotifyIfChanged(ref _noteWidth, value);
        }

        #endregion

        public LauncherWindowViewModel()
        {
            GridNoteHeight = MAX_WINDOW_HEIGHT + 20;
        }

        protected override void OnViewLoaded()
        {
            HideMe();
            if (this.View is LauncherWindowView { IsClosing: false } window)
            {
                ServerSelectionsViewVisibility = Visibility.Visible;
                ReSetWindowHeight();

                SetHotKey(_configurationService.Launcher.LauncherEnabled,
                    _configurationService.Launcher.HotKeyModifiers, _configurationService.Launcher.HotKeyKey);
                window.Deactivated += (s, a) => { HideMe(); };
                window.KeyDown += (s, a) => { if (a.Key == Key.Escape) HideMe(); };
                ServerSelectionsViewModel.CalcNoteFieldVisibility();
            }
        }


        public void ReSetWindowHeight()
        {
            if (IoC.TryGet<LauncherWindowView>()?.IsClosing != false) return;
            Execute.OnUIThread(() =>
            {
                double height;
                if (ServerSelectionsViewVisibility == Visibility.Visible)
                {
                    height = ServerSelectionsViewModel.ReCalcGridMainHeight();
                }
                else
                {
                    height = QuickConnectionViewModel.ReCalcGridMainHeight();
                }
                GridMainHeight = height;
            });
        }


        public void ShowMe()
        {
            if (this.View is LauncherWindowView { IsClosing: false } window)
            {
                SimpleLogHelper.Debug($"Call shortcut to invoke launcher Visibility = {window.Visibility}");
                if (IoC.Get<MainWindowViewModel>().TopLevelViewModel != null) return;
                if (IoC.Get<ConfigurationService>().Launcher.LauncherEnabled == false) return;

                lock (this)
                {
                    if (window.Visibility != Visibility.Visible)
                    {
                        MsAppCenterHelper.TraceView(nameof(LauncherWindowView), true);
                    }
                    window.WindowState = WindowState.Normal;
                    QuickConnectionViewModel.SelectedProtocol = QuickConnectionViewModel.Protocols.First();
                    ReSetWindowHeight();

                    // show position
                    var p = ScreenInfoEx.GetMouseSystemPosition();
                    var screenEx = ScreenInfoEx.GetCurrentScreenBySystemPosition(p);
                    window.Top = screenEx.VirtualWorkingAreaCenter.Y - GridMainHeight / 2 - 40; // 40: margin of BorderMainContent
                    window.Left = screenEx.VirtualWorkingAreaCenter.X - window.BorderMainContent.ActualWidth / 2;

                    var noteWidth = (screenEx.VirtualWorkingArea.Width - window.BorderMainContent.ActualWidth - 100) / 2;
                    if (noteWidth < 100)
                        noteWidth = 100;
                    NoteWidth = Math.Min(noteWidth, NoteWidth);

                    window.Show();
                    window.Visibility = Visibility.Visible;
                    window.Activate();
                    window.Topmost = true; // important
                    window.Topmost = false; // important
                    window.Topmost = true; // important
                    window.Focus(); // important
                    ServerSelectionsViewModel.Show();
                    ServerSelectionsViewModel.SelectedIndex = 0;
                }
            }
            else if (this.View is not LauncherWindowView)
            {
                IoC.Get<IWindowManager>().ShowWindow(this);
            }
        }


        public void HideMe()
        {
            if (this.View is LauncherWindowView { IsClosing: false } window)
            {
                lock (this)
                {
                    Execute.OnUIThread(() =>
                    {
                        if (window.Visibility == Visibility.Visible)
                        {
                            MsAppCenterHelper.TraceView(nameof(LauncherWindowView), false);
                        }
                        window.Hide();
                        ServerSelectionsViewModel.Show();
                        // After startup and initalizing our application and when closing our window and minimize the application to tray we free memory with the following line:
                        System.Diagnostics.Process.GetCurrentProcess().MinWorkingSet = System.Diagnostics.Process.GetCurrentProcess().MinWorkingSet;
                    });
                }
            }
        }


        public bool SetHotKey(bool launcherEnabled, HotkeyModifierKeys hotKeyModifierKeys, Key hotKeyKey)
        {
            if (this.View is LauncherWindowView window)
            {
                GlobalHotkeyHooker.Instance.Unregist(window);
                if (launcherEnabled == false)
                    return false;
                var r = GlobalHotkeyHooker.Instance.Register(window, (uint)hotKeyModifierKeys, hotKeyKey, this.ShowMe);
                switch (r.Item1)
                {
                    case GlobalHotkeyHooker.RetCode.Success:
                        return true;
                    case GlobalHotkeyHooker.RetCode.ERROR_HOTKEY_NOT_REGISTERED:
                        {
                            var msg = $"{IoC.Get<ILanguageService>().Translate("hotkey_registered_fail")}: {r.Item2}";
                            SimpleLogHelper.Warning(msg);
                            MessageBoxHelper.Warning(msg);
                            break;
                        }
                    case GlobalHotkeyHooker.RetCode.ERROR_HOTKEY_ALREADY_REGISTERED:
                        {
                            var msg = $"{IoC.Get<ILanguageService>().Translate("hotkey_already_registered")}: {r.Item2}";
                            SimpleLogHelper.Warning(msg);
                            MessageBoxHelper.Warning(msg);
                            break;
                        }
                    default:
                        throw new ArgumentOutOfRangeException(r.Item1.ToString());
                }
            }
            return false;
        }

        public void ToggleQuickConnection()
        {
            if (ServerSelectionsViewVisibility == Visibility.Collapsed)
            {
                ServerSelectionsViewModel.Show();
            }
            else
            {
                QuickConnectionViewModel.Show();
            }
            ServerSelectionsViewModel.CalcNoteFieldVisibility();
            ReSetWindowHeight();
        }
    }
}