using System;
using System.Collections.Generic;
using _1RM.Model.Protocol.Base;
using Shawn.Utils.WpfResources.Theme.Styles;

namespace _1RM.View.Host.ProtocolHosts
{

    public interface IHostBase
    {
        public ProtocolBase ProtocolServer { get; }
        public WindowBase? ParentWindow { get; }
        public void SetParentWindow(WindowBase? value);

        public IntPtr ParentWindowHandle { get; }

        public ProtocolHostStatus GetStatus();
        public void SetStatus(ProtocolHostStatus value);

        public string ConnectionId { get; }


        public Action<string>? OnProtocolClosed { get; set; }
        public Action<string>? OnFullScreen2Window { get; set; }

        public bool CanFullScreen { get; }

        public List<System.Windows.Controls.Control> MenuItems { get; }
        public bool CanResizeNow();
        /// <summary>
        /// in rdp, tab window cannot resize until rdp is connected. or rdp will not fit window size.
        /// </summary>
        public Action? OnCanResizeNowChanged { get; set; }

        public void ToggleAutoResize(bool isEnable);
        public abstract void Conn();

        public abstract void ReConn();

        /// <summary>
        /// disconnect the session and close host window
        /// </summary>
        public void Close();

        public void GoFullScreen();
        public void FocusOnMe();

        public ProtocolHostType GetProtocolHostType();

        public IntPtr GetHostHwnd();
    }
}
