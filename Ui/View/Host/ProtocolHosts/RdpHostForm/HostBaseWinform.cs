using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Navigation;
using _1RM.Model;
using _1RM.Model.Protocol.Base;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.Controls;
using Shawn.Utils.WpfResources.Theme.Styles;
using Stylet;
using Binding = System.Windows.Data.Binding;
using CheckBox = System.Windows.Controls.CheckBox;

namespace _1RM.View.Host.ProtocolHosts;

//public enum ProtocolHostStatus
//{
//    NotInit,
//    Initializing,
//    Initialized,
//    Connecting,
//    Connected,
//    Disconnected,
//    WaitingForReconnect
//}
//public enum ProtocolHostType
//{
//    Native,
//    Integrate
//}

public abstract class HostBaseWinform : Form, IHostBase
{
    public bool IsClosing { get; private set; } = false;
    public bool IsClosed { get; private set; } = false;

    public ProtocolBase ProtocolServer { get; }
    public virtual ProtocolBase GetProtocolServer()
    {
        return ProtocolServer;
    }

    private WindowBase? _parentWindow;
    [Obsolete]
    public WindowBase? ParentWindow => _parentWindow;
    public IntPtr HWND { get; private set; }

    public bool IsLoaded { get; private set; }

    public string LastTabToken { get; set; } = "";


    public virtual void SetParentWindow(WindowBase? value)
    {
        if (_parentWindow == value) return;
        _parentWindow = value;
        ParentWindowHandle = IntPtr.Zero;

        if (null == value) return;
        var window = Window.GetWindow(value);
        if (window != null)
        {
            var wih = new WindowInteropHelper(window);
            ParentWindowHandle = wih.Handle;
        }
    }

    public IntPtr ParentWindowHandle { get; private set; } = IntPtr.Zero;



    private ProtocolHostStatus _status = ProtocolHostStatus.NotInit;
    public virtual ProtocolHostStatus GetStatus()
    {
        return _status;
    }
    public virtual void SetStatus(ProtocolHostStatus value)
    {
        if (_status != value)
        {
            SimpleLogHelper.Debug(this.GetType().Name + ": Status => " + value);
            _status = value;
            OnCanResizeNowChanged?.Invoke();
        }
    }



    protected HostBaseWinform(ProtocolBase protocolServer, bool canFullScreen = false)
    {
        FormBorderStyle = FormBorderStyle.Sizable;
        ProtocolServer = protocolServer;
        CanFullScreen = canFullScreen;
        
        Load += (sender, args) =>
        {
            IsLoaded = true;
            HWND = this.Handle;
        };
    }

    public string ConnectionId
    {
        get
        {
            if (ProtocolServer.IsOnlyOneInstance())
            {
                return ProtocolServer.BuildConnectionId();
            }
            else
            {
                return ProtocolServer.BuildConnectionId() + "_" + this.GetHashCode();
            }
        }
    }

    public bool CanFullScreen { get; protected set; }

    /// <summary>
    /// special menu for tab
    /// </summary>
    public List<System.Windows.Controls.Control> MenuItems { get; set; } = new List<System.Windows.Controls.Control>();

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
    public new virtual void Close()
    {
        this.OnProtocolClosed?.Invoke(ConnectionId);
    }

    public abstract void GoFullScreen();

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
    public virtual IntPtr GetHostHwnd()
    {
        if (this.HWND == IntPtr.Zero)
        {
            Execute.OnUIThreadSync(() =>
            {
                HWND = this.Handle;
            });
        }
        return this.HWND;
    }

    public Action<string>? OnProtocolClosed { get; set; } = null;
    public Action<string>? OnFullScreen2Window { get; set; } = null;

    /// <summary>
    /// 为了在 tab 中显示，必须把这个 winfrom 窗口托管到 IntegrateHostForWinFrom
    /// </summary>
    public IntegrateHostForWinFrom? AttachedHost { get; private set; } = null;
    public IntegrateHostForWinFrom AttachToHostBase()
    {
        FormBorderStyle = FormBorderStyle.None;
        WindowState = FormWindowState.Maximized;
        ShowInTaskbar = false;
        AttachedHost ??= new IntegrateHostForWinFrom(this);
        return AttachedHost;
    }


    //public void DetachFromHostBase()
    //{
    //    if (AttachedHost == null)
    //        return;

    //    FormBorderStyle = FormBorderStyle.Sizable;
    //    WindowState = FormWindowState.Normal;
    //    ShowInTaskbar = true;
    //    Handle.SetParentEx(IntPtr.Zero);

    //    AttachedHost.Dispose();
    //    AttachedHost = null;
    //}

}
