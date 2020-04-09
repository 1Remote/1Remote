using System.Windows.Controls;
using PRM.Core.Protocol;
using PRM.Core.UI.VM;
using PRM.View;
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
            set
            {
                SetAndNotifyIfChanged(nameof(SysOptionsMenuIsOpen), ref _sysOptionsMenuIsOpen, value);
            }
        }

        public VmMain()
        {
            PageServerList = new ServerListPage(this);
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
                            Page = new SysOptionsPage(),
                        };
                        SysOptionsMenuIsOpen = false;

                    }, o => DispPage?.Page?.GetType() != typeof(SysOptionsPage));
                }
                return _cmdGoSysOptionsPage;
            }
        }
        #endregion
    }
}
