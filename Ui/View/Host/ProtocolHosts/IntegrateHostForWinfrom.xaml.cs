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
        public IntPtr PanelHandle => _panel.Handle;

        public IntegrateHostForWinFrom(HostBaseWinform form) : base(form.GetProtocolServer(), false)
        {
            _form = form;
            InitializeComponent();

            _panel = new System.Windows.Forms.Panel
            {
                BackColor = System.Drawing.Color.Blue,
                Dock = System.Windows.Forms.DockStyle.Fill,
                BorderStyle = BorderStyle.None
            };
            _panel.SizeChanged += PanelOnSizeChanged;
            FormsHost.Child = _panel;
            //_form.Closed += FormOnClosed;

            _form.OnCanResizeNowChanged += () =>
            {
                OnCanResizeNowChanged?.Invoke();
            };

            Loaded += (sender, args) =>
            {
                _form.Show();
                //_form.Parent = _panel;
                _form.Handle.SetParentEx(_panel.Handle);
                SetToPanelSize();
            };
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
            _form?.SetWidthHeight((int)(_panel.Width), (int)(_panel.Height));
        }

        private void PanelOnSizeChanged(object? sender, EventArgs e)
        {
            if (IsLoaded)
            {
                SimpleLogHelper.Warning($"IntegrateHostForWinFrom PanelOnSizeChanged -> _panel.Size = {_panel.Width}, {_panel.Height}");
                SetToPanelSize();
            }
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
                    _form.Hide();
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

        private Action<string>? _onFullScreen2Window = null;
        public override Action<string>? OnFullScreen2Window
        {
            get => _onFullScreen2Window;
            set
            {
                _onFullScreen2Window = value;
                if (Form != null) 
                    Form.OnFullScreen2Window = value;
            }
        }

        private Action<string>? _onProtocolClosed = null;
        public override Action<string>? OnProtocolClosed
        {
            get => _onProtocolClosed;
            set
            {
                _onProtocolClosed = value;
                if (Form != null) 
                    Form.OnProtocolClosed = value;
            }
        }
    }
}