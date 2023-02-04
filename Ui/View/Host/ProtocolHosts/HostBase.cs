using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using PRM.Model;
using PRM.Model.Protocol;
using PRM.Model.Protocol.Base;
using PRM.Service;
using PRM.View.Settings;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Shawn.Utils.WpfResources.Theme.Styles;

namespace PRM.View.Host.ProtocolHosts
{
    public enum ProtocolHostStatus
    {
        NotInit,
        Initializing,
        Initialized,
        Connecting,
        Connected,
        Disconnected,
        WaitingForReconnect
    }

    public enum ProtocolHostType
    {
        Native,
        Integrate
    }

    public abstract class HostBase : UserControl
    {
        public ProtocolBase ProtocolServer { get; }

        private WindowBase? _parentWindow;
        public WindowBase? ParentWindow => _parentWindow;

        public virtual void SetParentWindow(WindowBase? value)
        {
            if (_parentWindow == value) return;
            ParentWindowHandle = IntPtr.Zero;
            var window = Window.GetWindow(value);
            if (window != null)
            {
                var wih = new WindowInteropHelper(window);
                ParentWindowHandle = wih.Handle;
            }
            _parentWindow = value;
        }

        public IntPtr ParentWindowHandle { get; private set; } = IntPtr.Zero;

        private ProtocolHostStatus _status = ProtocolHostStatus.NotInit;

        public ProtocolHostStatus Status
        {
            get => _status;
            protected set
            {
                if (_status != value)
                {
                    SimpleLogHelper.Debug(this.GetType().Name + ": Status => " + value);
                    _status = value;
                    OnCanResizeNowChanged?.Invoke();
                }
            }
        }

        protected HostBase(ProtocolBase protocolServer, bool canFullScreen = false)
        {
            ProtocolServer = protocolServer;
            CanFullScreen = canFullScreen;

            // Add right click menu
            {
                var tb = new TextBlock();
                tb.SetResourceReference(TextBlock.TextProperty, "Reconnect");
                MenuItems.Add(new System.Windows.Controls.MenuItem()
                {
                    Header = tb,
                    Command = new RelayCommand((o) => { ReConn(); })
                });
            }

            {
                var tb = new TextBlock();
                tb.SetResourceReference(TextBlock.TextProperty, "Close");
                MenuItems.Add(new System.Windows.Controls.MenuItem()
                {
                    Header = tb,
                    Command = new RelayCommand((o) => { Close(); }),
                });
            }

            {
                var cb = new CheckBox
                {
                    Content = IoC.Get<LanguageService>().Translate("Show XXX button", IoC.Get<LanguageService>().Translate("Reconnect")),
                    IsHitTestVisible = false,
                };

                var binding = new Binding("TabHeaderShowReConnectButton")
                {
                    Source = SettingsPage,
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                };
                cb.SetBinding(CheckBox.IsCheckedProperty, binding);
                MenuItems.Add(new System.Windows.Controls.MenuItem()
                {
                    Header = cb,
                    Command = new RelayCommand((_) => { SettingsPage.TabHeaderShowReConnectButton = !SettingsPage.TabHeaderShowReConnectButton; }),
                });
            }

            {
                var cb = new CheckBox
                {
                    Content = IoC.Get<LanguageService>().Translate("Show XXX button", IoC.Get<LanguageService>().Translate("Close")),
                    IsHitTestVisible = false,
                };

                var binding = new Binding("TabHeaderShowCloseButton")
                {
                    Source = SettingsPage,
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                };
                cb.SetBinding(CheckBox.IsCheckedProperty, binding);
                MenuItems.Add(new System.Windows.Controls.MenuItem()
                {
                    Header = cb,
                    Command = new RelayCommand((_) => { SettingsPage.TabHeaderShowCloseButton = !SettingsPage.TabHeaderShowCloseButton; }),
                });
            }
        }

        public string ConnectionId
        {
            get
            {
                if (ProtocolServer.IsOnlyOneInstance())
                    return ProtocolServer.Id.ToString();
                else
                    return ProtocolServer.Id.ToString() + "_" + this.GetHashCode().ToString();
            }
        }
        public SettingsPageViewModel SettingsPage => IoC.Get<SettingsPageViewModel>();

        public bool CanFullScreen { get; protected set; }

        /// <summary>
        /// special menu for tab
        /// </summary>
        public List<MenuItem> MenuItems { get; set; } = new List<MenuItem>();

        /// <summary>
        /// since resizing when rdp is connecting would not tiger the rdp size change event
        /// then I let rdp host return false when rdp is on connecting to prevent TabWindow resize or maximize.
        /// </summary>
        /// <returns></returns>
        public virtual bool CanResizeNow()
        {
            return true;
        }

        /// <summary>
        /// in rdp, tab window cannot resize until rdp is connected. or rdp will not fit window size.
        /// </summary>
        public Action? OnCanResizeNowChanged { get; set; } = null;

        public virtual void ToggleAutoResize(bool isEnable)
        {
        }

        public abstract void Conn();

        public abstract void ReConn();

        /// <summary>
        /// disconnect the session and close host window
        /// </summary>
        public virtual void Close()
        {
            this.OnClosed?.Invoke(ConnectionId);
        }

        protected virtual void GoFullScreen()
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// call to focus the AxRdp or putty
        /// </summary>
        public virtual void FocusOnMe()
        {
            // do nothing
        }

        public abstract ProtocolHostType GetProtocolHostType();

        /// <summary>
        /// if it is a Integrate host, then return process's hwnd.
        /// </summary>
        /// <returns></returns>
        public abstract IntPtr GetHostHwnd();

        public Action<string>? OnClosed { get; set; } = null;
        public Action<string>? OnFullScreen2Window { get; set; } = null;
    }
}