using System;
using System.Windows.Controls;
using PRM.Core.Model;
using PRM.Core.Protocol;
using PRM.View;
using Shawn.Ulits;
using Shawn.Ulits.PageHost;
using NotifyPropertyChangedBase = PRM.Core.NotifyPropertyChangedBase;

namespace PRM.ViewModel
{
    public class VmMain : NotifyPropertyChangedBase
    {
        private string _dispNameFilter = "";
        public string DispNameFilter
        {
            get => _dispNameFilter;
            set
            {
                SetAndNotifyIfChanged(nameof(DispNameFilter), ref _dispNameFilter, value);
                if (PageServerList?.VmDataContext?.DispNameFilter != null)
                    PageServerList.VmDataContext.DispNameFilter = value;
            }
        }

        private AnimationPage _dispPage = null;

        public AnimationPage DispPage
        {
            get => _dispPage;
            set => SetAndNotifyIfChanged(nameof(DispPage), ref _dispPage, value);
        }

        private ServerListPage _pageServerList;
        public ServerListPage PageServerList
        {
            get => _pageServerList;
            set => SetAndNotifyIfChanged(nameof(PageServerList), ref _pageServerList, value);
        }



        private bool _sysOptionsMenuIsOpen = false;
        public bool SysOptionsMenuIsOpen
        {
            get => _sysOptionsMenuIsOpen;
            set => SetAndNotifyIfChanged(nameof(SysOptionsMenuIsOpen), ref _sysOptionsMenuIsOpen, value);
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




        public VmMain()
        {
            PageServerList = new ServerListPage(this);

            GlobalEventHelper.OnLongTimeProgress += (arg1, arg2, arg3) =>
            {
                ProgressBarValue = arg1;
                ProgressBarMaximum = arg2;
                ProgressBarInfo = arg2 > 0 ? arg3 : "";
            };
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
                        SysOptionsMenuIsOpen = false;
                    }, o => DispPage?.Page?.GetType() != typeof(SystemConfigPage));
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
                        DispPage = new AnimationPage()
                        {
                            InAnimationType = AnimationPage.InOutAnimationType.SlideFromRight,
                            OutAnimationType = AnimationPage.InOutAnimationType.SlideToRight,
                            Page = new AboutPage(this),
                        };
                        SysOptionsMenuIsOpen = false;

                    }, o => DispPage?.Page?.GetType() != typeof(AboutPage));
                }
                return _cmdGoAboutPage;
            }
        }




        private RelayCommand _cmdGoServerListPage;
        public RelayCommand CmdGoServerListPage
        {
            get
            {
                if (_cmdGoServerListPage == null)
                {
                    _cmdGoServerListPage = new RelayCommand((o) =>
                    {
                        if (DispPage?.Page?.GetType() != null)
                            DispPage = null;
                    }, o => DispPage?.Page?.GetType() != null && DispPage?.Page?.GetType() != typeof(ServerListPage));
                }
                return _cmdGoServerListPage;
            }
        }
        #endregion
    }
}
