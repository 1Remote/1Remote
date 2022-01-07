using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using PRM.Core.Model;
using PRM.Core.Protocol;
using PRM.Core.Protocol.RDP;
using Shawn.Utils;
using Shawn.Utils.PageHost;
using PRM.View;
using PRM.ViewModel.Configuration;
using Shawn.Utils;

using NotifyPropertyChangedBase = PRM.Core.NotifyPropertyChangedBase;

namespace PRM.ViewModel
{
    public class VmMain : NotifyPropertyChangedBase
    {
        public VmAboutPage VmAboutPage { get; }

        #region Properties

        private Visibility _tbFilterVisible = Visibility.Visible;

        public Visibility TbFilterVisible
        {
            get => _tbFilterVisible;
            set => SetAndNotifyIfChanged(ref _tbFilterVisible, value);
        }

        private readonly AboutPage _aboutPage;

        public AnimationPage ServersShownPage { get; }

        private AnimationPage _bottomPage = null;

        public AnimationPage BottomPage
        {
            get => _bottomPage;
            set
            {
                SetAndNotifyIfChanged(ref _bottomPage, value);
                CalcTbFilterVisible();
            }
        }

        private AnimationPage _dispPage = null;

        public AnimationPage DispPage
        {
            get => _dispPage;
            set
            {
                SetAndNotifyIfChanged(ref _dispPage, value);
                CalcTbFilterVisible();
            }
        }

        private AnimationPage _topPage = null;

        public AnimationPage TopPage
        {
            get => _topPage;
            set
            {
                SetAndNotifyIfChanged(ref _topPage, value);
                CalcTbFilterVisible();
            }
        }

        private int _progressBarValue = 0;

        public int ProgressBarValue
        {
            get => _progressBarValue;
            set => SetAndNotifyIfChanged(ref _progressBarValue, value);
        }

        private int _progressBarMaximum = 0;

        public int ProgressBarMaximum
        {
            get => _progressBarMaximum;
            set => SetAndNotifyIfChanged(ref _progressBarMaximum, value);
        }

        private string _progressBarInfo = "";

        public string ProgressBarInfo
        {
            get => _progressBarInfo;
            set => SetAndNotifyIfChanged(ref _progressBarInfo, value);
        }

        #endregion Properties

        public readonly MainWindow Window;
        public PrmContext Context { get; }

        public VmMain(PrmContext context, MainWindow window)
        {
            Context = context;
            Window = window;
            this.VmAboutPage = new VmAboutPage();
            _aboutPage = new AboutPage(VmAboutPage, this);
            ConfigurationViewModel.Init(context);
            ConfigurationViewModel.GetInstance().Host = this;

            GlobalEventHelper.OnLongTimeProgress += (arg1, arg2, arg3) =>
            {
                Window.Dispatcher.Invoke(() =>
                {
                    ProgressBarValue = arg1;
                    ProgressBarMaximum = arg2;
                    ProgressBarInfo = arg2 > 0 ? arg3 : "";
                });
            };
            GlobalEventHelper.OnRequestGoToServerEditPage += new GlobalEventHelper.OnRequestGoToServerEditPageDelegate((id, isDuplicate, isInAnimationShow) =>
            {
                if (id <= 0) return;
                Debug.Assert(Context.AppData.VmItemList.Any(x => x.Server.Id == id));
                var server = Context.AppData.VmItemList.First(x => x.Server.Id == id).Server;
                Window.Dispatcher.Invoke(() =>
                {
                    DispPage = new AnimationPage()
                    {
                        InAnimationType = isInAnimationShow ? AnimationPage.InOutAnimationType.SlideFromRight : AnimationPage.InOutAnimationType.None,
                        OutAnimationType = AnimationPage.InOutAnimationType.SlideToRight,
                        Page = new ServerEditorPage(Context, new VmServerEditorPage(Context, server, isDuplicate)),
                    };
                    Window.ActivateMe();
                });
            });

            GlobalEventHelper.OnGoToServerAddPage += new GlobalEventHelper.OnGoToServerAddPageDelegate((tagName, isInAnimationShow) =>
            {
                var server = new ProtocolServerRDP();
                if (string.IsNullOrWhiteSpace(tagName) == false)
                    server.Tags = new List<string>() { tagName };

                Window.Dispatcher.Invoke(() =>
                {
                    DispPage = new AnimationPage()
                    {
                        InAnimationType = isInAnimationShow ? AnimationPage.InOutAnimationType.SlideFromRight : AnimationPage.InOutAnimationType.None,
                        OutAnimationType = AnimationPage.InOutAnimationType.SlideToRight,
                        Page = new ServerEditorPage(Context, new VmServerEditorPage(Context, server)),
                    };
                    Window.ActivateMe();
                });
            });

            GlobalEventHelper.OnRequestGoToServerMultipleEditPage += (servers, isInAnimationShow) =>
            {
                Window.Dispatcher.Invoke(() =>
                {
                    var page = new AnimationPage()
                    {
                        InAnimationType = isInAnimationShow ? AnimationPage.InOutAnimationType.SlideFromRight : AnimationPage.InOutAnimationType.None,
                        OutAnimationType = AnimationPage.InOutAnimationType.SlideToRight,
                    };
                    var serverBases = servers as ProtocolServerBase[] ?? servers.ToArray();
                    if (serverBases.Count() > 1)
                        page.Page = new ServerEditorPage(Context, new VmServerEditorPage(Context, serverBases));
                    else
                        page.Page = new ServerEditorPage(Context, new VmServerEditorPage(Context, serverBases.First()));
                    DispPage = page;
                    Window.ActivateMe();
                });
            };

            ServersShownPage = new AnimationPage()
            {
                InAnimationType = AnimationPage.InOutAnimationType.None,
                OutAnimationType = AnimationPage.InOutAnimationType.None,
                Page = new ServerListPage(Context),
            };
            //_managementPage = new ServerManagementPage(Context);
        }

        private void CalcTbFilterVisible()
        {
            if (TopPage == null && DispPage == null)
                TbFilterVisible = Visibility.Visible;
            else
                TbFilterVisible = Visibility.Collapsed;
        }

        #region CMD

        private RelayCommand _cmdGoSysOptionsPage;

        public RelayCommand CmdGoSysOptionsPage
        {
            get
            {
                return _cmdGoSysOptionsPage ??= new RelayCommand((o) =>
                {
                    DispPage = new AnimationPage()
                    {
                        InAnimationType = AnimationPage.InOutAnimationType.SlideFromRight,
                        OutAnimationType = AnimationPage.InOutAnimationType.SlideToRight,
                        Page = new SystemConfigPage(Context, o?.ToString()),
                    };
                    Window.PopupMenu.IsOpen = false;
                }, o => TopPage == null && DispPage?.Page?.GetType() != typeof(SystemConfigPage));
            }
        }

        private RelayCommand _cmdToggleCardList;

        public RelayCommand CmdToggleCardList
        {
            get
            {
                return _cmdToggleCardList ??= new RelayCommand((o) =>
                {
                    ConfigurationViewModel.GetInstance().ListPageIsCardView = !ConfigurationViewModel.GetInstance().ListPageIsCardView;
                    Window.PopupMenu.IsOpen = false;
                }, o => TopPage == null && DispPage?.Page?.GetType() != typeof(SystemConfigPage));
            }
        }

        private RelayCommand _cmdGoAboutPage;

        public RelayCommand CmdGoAboutPage
        {
            get
            {
                return _cmdGoAboutPage ??= new RelayCommand((o) =>
                {
                    TopPage = new AnimationPage()
                    {
                        InAnimationType = AnimationPage.InOutAnimationType.SlideFromRight,
                        OutAnimationType = AnimationPage.InOutAnimationType.SlideToRight,
                        Page = _aboutPage,
                    };
                    Window.PopupMenu.IsOpen = false;
                }, o => TopPage?.Page?.GetType() != typeof(AboutPage));
            }
        }

        #endregion CMD
    }
}