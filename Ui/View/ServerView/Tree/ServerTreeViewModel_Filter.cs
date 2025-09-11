using _1RM.View.ServerView;

namespace _1RM.View.ServerView.Tree
{
    public partial class ServerTreeViewModel : ServerPageViewModelBase
    {
        public sealed override void CalcServerVisibleAndRefresh(bool force = false, bool matchSubTitle = true)
        {
            base.CalcServerVisibleAndRefresh(force, false);
            BuildView();
        }
    }
}