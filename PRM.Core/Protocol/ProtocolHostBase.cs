using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using PRM.Core.Model;
using Shawn.Utils;

namespace PRM.Core.Protocol
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

    public abstract class ProtocolHostBase : UserControl
    {
        public ProtocolServerBase ProtocolServer { get; }
        protected PrmContext Context;

        private Window _parentWindow = null;

        public Window ParentWindow
        {
            get => _parentWindow;
            set
            {
                ParentWindowHandle = IntPtr.Zero;
                if (value != null)
                {
                    var window = Window.GetWindow(value);
                    if (window != null)
                    {
                        var wih = new WindowInteropHelper(window);
                        ParentWindowHandle = wih.Handle;
                    }
                }
                _parentWindow = value;
            }
        }

        public IntPtr ParentWindowHandle { get; private set; } = IntPtr.Zero;

        private ProtocolHostStatus _status = ProtocolHostStatus.NotInit;

        public ProtocolHostStatus Status
        {
            get => _status;
            protected set
            {
                SimpleLogHelper.Debug(this.GetType().Name + ": Status => " + value);
                if (_status != value)
                {
                    _status = value;
                    OnCanResizeNowChanged?.Invoke();
                }
            }
        }

        protected ProtocolHostBase(PrmContext context, ProtocolServerBase protocolServer, bool canFullScreen = false)
        {
            Context = context;
            ProtocolServer = protocolServer;
            CanFullScreen = canFullScreen;

            // Add right click menu
            {
                var tb = new TextBlock();
                tb.SetResourceReference(TextBlock.TextProperty, "word_reconnect");
                MenuItems.Add(new System.Windows.Controls.MenuItem()
                {
                    Header = tb,
                    Command = new RelayCommand((o) => { ReConn(); })
                });
            }
            {
                var tb = new TextBlock();
                tb.SetResourceReference(TextBlock.TextProperty, "word_close");
                MenuItems.Add(new System.Windows.Controls.MenuItem()
                {
                    Header = tb,
                    Command = new RelayCommand((o) => { Close(); })
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
        public Action OnCanResizeNowChanged { get; set; } = null;

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
            OnClosed?.Invoke(ConnectionId);
        }

        public abstract void GoFullScreen();

        /// <summary>
        /// call to focus the AxRdp or putty
        /// </summary>
        public virtual void MakeItFocus()
        {
            // do nothing
        }

        public abstract ProtocolHostType GetProtocolHostType();

        /// <summary>
        /// if it is a Integrate host, then return process's hwnd.
        /// </summary>
        /// <returns></returns>
        public abstract IntPtr GetHostHwnd();

        public Action<string> OnClosed { get; set; } = null;
        public Action<string> OnFullScreen2Window { get; set; } = null;
    }
}