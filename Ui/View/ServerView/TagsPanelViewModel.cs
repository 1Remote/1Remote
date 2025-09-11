using System.Windows.Data;
using _1RM.Model;
using _1RM.Utils;
using Shawn.Utils.Wpf;
using Stylet;

namespace _1RM.View.ServerView
{
    public class TagsPanelViewModel : NotifyPropertyChangedBaseScreen
    {
        public GlobalData GlobalData => IoC.Get<GlobalData>();

        private bool _filterIsFocused = false;
        public bool FilterIsFocused
        {
            get => _filterIsFocused;
            set => SetAndNotifyIfChanged(ref _filterIsFocused, value);
        }

        private readonly DebounceDispatcher _debounceDispatcher = new();
        private string _filterString = "";
        public string FilterString
        {
            get => _filterString;
            set
            {
                // can only be called by the Ui
                if (SetAndNotifyIfChanged(ref _filterString, value))
                {
                    _debounceDispatcher.Debounce(150, (obj) =>
                    {
                        if (_filterString == FilterString)
                        {
                            if (this.View is TagsPanelView v)
                            {
                                Execute.OnUIThread(() => { CollectionViewSource.GetDefaultView(v.ListBoxTags.ItemsSource).Refresh(); });
                            }
                        }
                    });
                }
            }
        }

        private RelayCommand? _cmdTagDelete;
        public RelayCommand CmdTagDelete
        {
            get
            {
                return _cmdTagDelete ??= new RelayCommand(TagActionHelper.CmdTagDelete);
            }
        }





        private RelayCommand? _cmdTagRename;
        public RelayCommand CmdTagRename
        {
            get
            {
                return _cmdTagRename ??= new RelayCommand(TagActionHelper.CmdTagRename);
            }
        }



        private RelayCommand? _cmdTagConnect;
        public RelayCommand CmdTagConnect
        {
            get
            {
                return _cmdTagConnect ??= new RelayCommand(TagActionHelper.CmdTagConnect);
            }
        }



        private RelayCommand? _cmdTagConnectToNewTab;
        public RelayCommand CmdTagConnectToNewTab
        {
            get
            {
                return _cmdTagConnectToNewTab ??= new RelayCommand(TagActionHelper.CmdTagConnectToNewTab);
            }
        }



        private RelayCommand? _cmdTagPin;
        public RelayCommand CmdTagPin
        {
            get
            {
                return _cmdTagPin ??= new RelayCommand(TagActionHelper.CmdTagPin);
            }
        }



        private RelayCommand? _cmdCreateDesktopShortcut;
        public RelayCommand CmdCreateDesktopShortcut
        {
            get
            {
                return _cmdCreateDesktopShortcut ??= new RelayCommand(TagActionHelper.CmdCreateDesktopShortcut);
            }
        }
    }
}
