using System.Windows.Controls;
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
            set => SetAndNotifyIfChanged(nameof(DispNameFilter), ref _dispNameFilter, value);
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

        public VmMain()
        {
            PageServerList = new ServerListPage(this);
        }
    }
}
