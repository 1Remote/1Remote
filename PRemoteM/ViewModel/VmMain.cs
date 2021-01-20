using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using PRM.Core.Model;
using PRM.Core.Protocol;
using PRM.Core.Protocol.RDP;
using PRM.View;
using Shawn.Utils;
using Shawn.Utils.PageHost;
using NotifyPropertyChangedBase = PRM.Core.NotifyPropertyChangedBase;

namespace PRM.ViewModel
{
    public class VmMain : NotifyPropertyChangedBase
    {
        private object _listViewPageForServerList;
        public object ListViewPageForServerList
        {
            get => _listViewPageForServerList;
            set => SetAndNotifyIfChanged(nameof(ListViewPageForServerList), ref _listViewPageForServerList, value);
        }


        private Visibility _tbFilterVisible = Visibility.Visible;
        public Visibility TbFilterVisible
        {
            get => _tbFilterVisible;
            set => SetAndNotifyIfChanged(nameof(TbFilterVisible), ref _tbFilterVisible, value);
        }

        private readonly ServerManagementPage _managementPage = null;
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

        public readonly MainWindow Window;


        public VmMain(MainWindow window)
        {
            Window = window;
            GlobalEventHelper.OnLongTimeProgress += (arg1, arg2, arg3) =>
            {
                ProgressBarValue = arg1;
                ProgressBarMaximum = arg2;
                ProgressBarInfo = arg2 > 0 ? arg3 : "";
            };
            GlobalEventHelper.OnGoToServerEditPage += new GlobalEventHelper.OnGoToServerEditPageDelegate((id, isDuplicate, isInAnimationShow) =>
            {
                ProtocolServerBase server;
                if (id <= 0)
                {
                    return;
                }
                else
                {
                    Debug.Assert(GlobalData.Instance.VmItemList.Any(x => x.Server.Id == id));
                    server = GlobalData.Instance.VmItemList.First(x => x.Server.Id == id).Server;
                }
                DispPage = new AnimationPage()
                {
                    InAnimationType = isInAnimationShow ? AnimationPage.InOutAnimationType.SlideFromRight : AnimationPage.InOutAnimationType.None,
                    OutAnimationType = AnimationPage.InOutAnimationType.SlideToRight,
                    Page = new ServerEditorPage(new VmServerEditorPage(server, isDuplicate)),
                };

                Window.ActivateMe();
            });

            GlobalEventHelper.OnGoToServerAddPage += new GlobalEventHelper.OnGoToServerAddPageDelegate((groupName, isInAnimationShow) =>
            {
                var server = new ProtocolServerRDP();
                server.GroupName = groupName;
                DispPage = new AnimationPage()
                {
                    InAnimationType = isInAnimationShow ? AnimationPage.InOutAnimationType.SlideFromRight : AnimationPage.InOutAnimationType.None,
                    OutAnimationType = AnimationPage.InOutAnimationType.SlideToRight,
                    Page = new ServerEditorPage(new VmServerEditorPage(server)),
                };
            });

            ListViewPageForServerList = new ServerListPage();
            _managementPage = new ServerManagementPage();
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
                            Page = new SystemConfigPage(this, (Type)o),
                        };
                        Window.PopupMenu.IsOpen = false;
                    }, o => TopPage == null && DispPage?.Page?.GetType() != typeof(SystemConfigPage));
                }
                return _cmdGoSysOptionsPage;
            }
        }


        private RelayCommand _cmdGoManagementPage;
        public RelayCommand CmdGoManagementPage
        {
            get
            {
                if (_cmdGoManagementPage == null)
                {
                    _cmdGoManagementPage = new RelayCommand((o) =>
                    {
                        if (DispPage != null)
                        {
                            DispPage = null;
                        }
                        BottomPage = new AnimationPage()
                        {
                            InAnimationType = AnimationPage.InOutAnimationType.SlideFromRight,
                            OutAnimationType = AnimationPage.InOutAnimationType.SlideToRight,
                            Page = _managementPage,
                        };
                        Window.PopupMenu.IsOpen = false;
                    }, o => TopPage == null && BottomPage?.Page?.GetType() != typeof(ServerManagementPage));
                }
                return _cmdGoManagementPage;
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
                            Page = new AboutPage(this),
                        };
                        Window.PopupMenu.IsOpen = false;
                    }, o => TopPage?.Page?.GetType() != typeof(AboutPage));
                }
                return _cmdGoAboutPage;
            }
        }
        #endregion
    }
}
