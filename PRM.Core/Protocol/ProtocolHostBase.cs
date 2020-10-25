using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using PRM.Core.Model;
using Shawn.Utils;

namespace PRM.Core.Protocol
{
    public abstract class ProtocolHostBase : UserControl
    {
        public readonly ProtocolServerBase ProtocolServer;

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

        protected ProtocolHostBase(ProtocolServerBase protocolServer, bool canFullScreen = false)
        {
            ProtocolServer = protocolServer;
            CanFullScreen = canFullScreen;

            // Add right click menu
            MenuItems.Add(new System.Windows.Controls.MenuItem()
            {
                Header = SystemConfig.Instance.Language.GetText("button_close"),
                Command = new RelayCommand((o) =>
                {
                    DisConn();
                })
            });
        }


        public string ConnectionId
        {
            get
            {
                if (ProtocolServer.OnlyOneInstance)
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


        public abstract void Conn();

        /// <summary>
        /// disconnect the session and close host window
        /// </summary>
        public virtual void DisConn()
        {
            OnClosed?.Invoke(ConnectionId);
        }
        public abstract void GoFullScreen();
        public abstract bool IsConnected();
        public abstract bool IsConnecting();



        protected static readonly object MakeItFocusLocker1 = new object();
        protected static readonly object MakeItFocusLocker2 = new object();

        /// <summary>
        /// call to focus the AxRdp or putty
        /// </summary>
        public virtual void MakeItFocus()
        {
            // do nothing
        }



        public Action<string> OnClosed { get; set; } = null;
        public Action<string> OnFullScreen2Window { get; set; } = null;
    }
}
