using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Forms;
using Shawn.Utils;
using Shawn.Utils.Wpf.Controls;
using Stylet;

/*
 * Note:

We should add <UseWindowsForms>true</UseWindowsForms> in the csproj.

<PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>netcoreapp3.0</TargetFramework>
    <UseWpf>true</UseWpf>
    <UseWindowsForms>true</UseWindowsForms>
</PropertyGroup>


 */

namespace _1RM.View.Host.ProtocolHosts
{
    public partial class IntegrateHostForWinFrom : HostBase
    {
        private HostBaseWinform? _form;
        public HostBaseWinform? Form => _form;
        private readonly System.Windows.Forms.Panel _panel;

        public IntegrateHostForWinFrom(HostBaseWinform form) : base(form.GetProtocolServer(), false)
        {
            _form = form;
            InitializeComponent();

            _panel = new System.Windows.Forms.Panel
            {
                BackColor = System.Drawing.Color.Transparent,
                Dock = System.Windows.Forms.DockStyle.Fill,
                BorderStyle = BorderStyle.None
            };
            _panel.SizeChanged += PanelOnSizeChanged;
            FormsHost.Child = _panel;
            //_form.Closed += FormOnClosed;


            _form.FormBorderStyle = FormBorderStyle.None;
            _form.WindowState = FormWindowState.Maximized;
            _form.Handle.SetParentEx(_panel.Handle);
            _form.OnCanResizeNowChanged += () =>
            {
                OnCanResizeNowChanged?.Invoke();
            };

            if (form.IsLoaded)
            {
                _form.Show();
            }
            else
            {
                Loaded += (sender, args) =>
                {
                    _form.Show();
                };
            }
            SimpleLogHelper.Debug($"IntegrateHostForWinFrom({this.GetHashCode()}) Created");

            // TODO: _form.CanFullScreen change invoke CanFullScreen changed
            CanFullScreen = _form.CanFullScreen;
        }

        ~IntegrateHostForWinFrom()
        {
            SimpleLogHelper.Debug($"IntegrateHostForWinFrom({this.GetHashCode()}) Released");
        }

        //public override ProtocolHostStatus GetStatus()
        //{
        //    return _form?.GetStatus() ?? ProtocolHostStatus.NotInit;
        //}
        //public override void SetStatus(ProtocolHostStatus value)
        //{
        //    _form?.SetStatus(value);
        //}

        public override bool CanResizeNow()
        {
            return _form?.CanResizeNow() ?? false;
        }



        #region Resize

        private void SetToPanelSize()
        {
            if (_form == null) return;
            if (_form.IsLoaded)
                WindowExtensions.MoveWindow(_form.Handle, 0, 0, (int)(_panel.Width), (int)(_panel.Height), true);
        }

        private void PanelOnSizeChanged(object? sender, EventArgs e)
        {
            SetToPanelSize();
        }
        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            this.InvalidateVisual();
            base.OnRenderSizeChanged(sizeInfo);
        }

        #endregion


        public override void Conn()
        {
            if (_form == null) return;
            _form.FormBorderStyle = FormBorderStyle.None;
            _form.WindowState = FormWindowState.Maximized;
            _form.Handle.SetParentEx(_panel.Handle);
            //ShowWindow(_form.Handle, (int)ShowWindowStyles.SW_SHOWMAXIMIZED);
            //_panel.Controls.Add(_form);
            Debug.Assert(ParentWindow != null);
            _form.Show();
            _form.Conn();
        }

        public override void ReConn()
        {
            _form?.ReConn();
        }


        public override void Close()
        {
            Execute.OnUIThread(() =>
            {
                if (_form != null)
                {
                    _form.Handle.SetParentEx(IntPtr.Zero);
                    _form.Close();
                    _form = null;
                }
                _panel?.Dispose();
                FormsHost?.Dispose();
                GC.SuppressFinalize(this);
            });
        }

        public override void FocusOnMe()
        {
            WindowExtensions.SetForegroundWindow(this.GetHostHwnd());
        }

        public override ProtocolHostType GetProtocolHostType()
        {
            return ProtocolHostType.Integrate;
        }

        public override IntPtr GetHostHwnd()
        {
            return _form?.GetHostHwnd() ?? IntPtr.Zero;
        }
    }
}