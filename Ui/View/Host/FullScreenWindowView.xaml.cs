using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using _1RM.Model;
using _1RM.Model.Protocol;
using _1RM.Service;
using _1RM.View.Host.ProtocolHosts;
using Shawn.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;
using Shawn.Utils.WpfResources.Theme.Styles;
using Stylet;

namespace _1RM.View.Host
{
    public partial class FullScreenWindowView : WindowBase
    {
        private HostBase? _host = null;
        public HostBase? Host
        {
            get => _host;
            private set
            {
                if (Equals(_host, value) == false)
                {
                    _host = value;
                    SetContent();
                }
            }
        }

        public static FullScreenWindowView Create(string token, HostBase host, TabWindowBase? fromTab)
        {
            FullScreenWindowView? view = null;
            Execute.OnUIThreadSync(() =>
            {
                view = new FullScreenWindowView();
                view.LastTabToken = token;

                // full screen placement
                ScreenInfoEx? screenEx;
                if (fromTab != null)
                    screenEx = ScreenInfoEx.GetCurrentScreen(fromTab);
                else if (host.ProtocolServer is RDP rdp
                         && rdp.RdpFullScreenFlag == ERdpFullScreenFlag.EnableFullScreen
                         && IoC.Get<LocalityService>().RdpLocalityGet(rdp.Id) is { } setting
                         && setting.FullScreenLastSessionScreenIndex >= 0
                         && setting.FullScreenLastSessionScreenIndex < System.Windows.Forms.Screen.AllScreens.Length)
                    screenEx = ScreenInfoEx.GetCurrentScreen(setting.FullScreenLastSessionScreenIndex);
                else
                    screenEx = ScreenInfoEx.GetCurrentScreen(IoC.Get<MainWindowView>());
                if (screenEx != null)
                {
                    view.Top = screenEx.VirtualWorkingAreaCenter.Y - view.Height / 2;
                    view.Left = screenEx.VirtualWorkingAreaCenter.X - view.Width / 2;
                }
            });
            return view!;
        }

        public const int DESIGN_WIDTH = 600;
        public const int DESIGN_HEIGHT = 480;
        private FullScreenWindowView()
        {
            InitializeComponent();

            WindowStartupLocation = WindowStartupLocation.Manual;

            Loaded += (sender, args) =>
            {
                this.Width = DESIGN_WIDTH;
                this.Height = DESIGN_HEIGHT;
                SetContent();
            };

            Closed += (sender, args) =>
            {
                if (Host != null)
                {
                    IoC.Get<SessionControlService>().CloseProtocolHostAsync(Host.ConnectionId);
                }
            };
        }

        private void SetContent()
        {
            Execute.OnUIThread(() =>
            {
                // !set content first
                if (this.IsLoaded && Equals(this.Content, Host) == false)
                {
                    this.Content = Host;
                }
                // !then set host
                if (this.IsLoaded && Host != null)
                {
                    this.Title = Host.ProtocolServer.DisplayName + " - " + Host.ProtocolServer.SubTitle;
                    this.Icon = IoC.Get<ConfigurationService>().General.ShowSessionIconInSessionWindow ?
                        Host.ProtocolServer.IconImg : null;
                    Host.SetParentWindow(this);
                }
            });
        }

        public void ShowOrHide(HostBase? host)
        {
            Execute.OnUIThreadSync(() =>
            {
                Host = null;
                Host = host;
                if(host == null)
                    this.Hide();
                else
                    this.Show();
            });
        }

        public string LastTabToken { get; set; } = "";
    }
}
