using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using PRM.Core.Model;
using PRM.Core.Protocol;
using PRM.Core.Protocol.RDP;
using Shawn.Utils;
using Shawn.Utils.PageHost;
using PRM.View;

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
            set => SetAndNotifyIfChanged(nameof(TbFilterVisible), ref _tbFilterVisible, value);
        }

        private readonly AboutPage _aboutPage;

        public AnimationPage ServersShownPage { get; }

        private AnimationPage _bottomPage = null;

        public AnimationPage BottomPage
        {
            get => _bottomPage;
            set
            {
                SetAndNotifyIfChanged(nameof(BottomPage), ref _bottomPage, value);
                CalcTbFilterVisible();
            }
        }

        private AnimationPage _dispPage = null;

        public AnimationPage DispPage
        {
            get => _dispPage;
            set
            {
                SetAndNotifyIfChanged(nameof(DispPage), ref _dispPage, value);
                CalcTbFilterVisible();
            }
        }

        private AnimationPage _topPage = null;

        public AnimationPage TopPage
        {
            get => _topPage;
            set
            {
                SetAndNotifyIfChanged(nameof(TopPage), ref _topPage, value);
                CalcTbFilterVisible();
            }
        }

        private int _progressBarValue = 0;

        public int ProgressBarValue
        {
            get => _progressBarValue;
            set => SetAndNotifyIfChanged(nameof(ProgressBarValue), ref _progressBarValue, value);
        }

        private int _progressBarMaximum = 0;

        public int ProgressBarMaximum
        {
            get => _progressBarMaximum;
            set
            {
                if (value != _progressBarMaximum)
                {
                    SetAndNotifyIfChanged(nameof(ProgressBarMaximum), ref _progressBarMaximum, value);
                }
            }
        }

        private string _progressBarInfo = "";

        public string ProgressBarInfo
        {
            get => _progressBarInfo;
            set
            {
                if (value != _progressBarInfo)
                {
                    SetAndNotifyIfChanged(nameof(ProgressBarInfo), ref _progressBarInfo, value);
                }
            }
        }

        #endregion Properties

        public readonly MainWindow Window;
        public PrmContext Context { get; }

        public VmMain(PrmContext context, MainWindow window)
        {
            Context = context;
            Window = window;
            this.VmAboutPage = new VmAboutPage();
            GlobalEventHelper.OnLongTimeProgress += (arg1, arg2, arg3) =>
            {
                ProgressBarValue = arg1;
                ProgressBarMaximum = arg2;
                ProgressBarInfo = arg2 > 0 ? arg3 : "";
            };
            GlobalEventHelper.OnRequestGoToServerEditPage += new GlobalEventHelper.OnRequestGoToServerEditPageDelegate((id, isDuplicate, isInAnimationShow) =>
            {
                if (id <= 0) return;
                Debug.Assert(Context.AppData.VmItemList.Any(x => x.Server.Id == id));
                var server = Context.AppData.VmItemList.First(x => x.Server.Id == id).Server;
                DispPage = new AnimationPage()
                {
                    InAnimationType = isInAnimationShow ? AnimationPage.InOutAnimationType.SlideFromRight : AnimationPage.InOutAnimationType.None,
                    OutAnimationType = AnimationPage.InOutAnimationType.SlideToRight,
                    Page = new ServerEditorPage(new VmServerEditorPage(Context, server, isDuplicate)),
                };

                Window.ActivateMe();
            });

            GlobalEventHelper.OnGoToServerAddPage += new GlobalEventHelper.OnGoToServerAddPageDelegate((groupName, isInAnimationShow) =>
            {
                var server = new ProtocolServerRDP { GroupName = groupName };
                DispPage = new AnimationPage()
                {
                    InAnimationType = isInAnimationShow ? AnimationPage.InOutAnimationType.SlideFromRight : AnimationPage.InOutAnimationType.None,
                    OutAnimationType = AnimationPage.InOutAnimationType.SlideToRight,
                    Page = new ServerEditorPage(new VmServerEditorPage(Context, server)),
                };
            });

            GlobalEventHelper.OnRequestGoToServerMultipleEditPage += (servers, isInAnimationShow) =>
            {
                DispPage = new AnimationPage()
                {
                    InAnimationType = isInAnimationShow ? AnimationPage.InOutAnimationType.SlideFromRight : AnimationPage.InOutAnimationType.None,
                    OutAnimationType = AnimationPage.InOutAnimationType.SlideToRight,
                    Page = new ServerEditorPage(new VmServerEditorPage(Context, servers)),
                };
            };

            ServersShownPage = new AnimationPage()
            {
                InAnimationType = AnimationPage.InOutAnimationType.None,
                OutAnimationType = AnimationPage.InOutAnimationType.None,
                Page = new ServerListPage(Context),
            };
            //_managementPage = new ServerManagementPage(Context);
            _aboutPage = new AboutPage(VmAboutPage, this);
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
                if (_cmdGoSysOptionsPage == null)
                {
                    _cmdGoSysOptionsPage = new RelayCommand((o) =>
                    {
                        DispPage = new AnimationPage()
                        {
                            InAnimationType = AnimationPage.InOutAnimationType.SlideFromRight,
                            OutAnimationType = AnimationPage.InOutAnimationType.SlideToRight,
                            Page = new SystemConfigPage(this, Context, (Type)o),
                        };
                        Window.PopupMenu.IsOpen = false;
                    }, o => TopPage == null && DispPage?.Page?.GetType() != typeof(SystemConfigPage));
                }
                return _cmdGoSysOptionsPage;
            }
        }

        private RelayCommand _cmdGoAboutPage;

        public RelayCommand CmdGoAboutPage
        {
            get
            {
                if (_cmdGoAboutPage == null)
                {
                    _cmdGoAboutPage = new RelayCommand((o) =>
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
                return _cmdGoAboutPage;
            }
        }

        #endregion CMD
    }
}