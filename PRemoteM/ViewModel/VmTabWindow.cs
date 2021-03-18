using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using Dragablz;
using PRM.Core.Model;
using Shawn.Utils;
using PRM.Model;
using PRM.View.TabWindow;

using Shawn.Utils;

using NotifyPropertyChangedBase = PRM.Core.NotifyPropertyChangedBase;

namespace PRM.ViewModel
{
    public class VmTabWindow : NotifyPropertyChangedBase, IDisposable
    {
        public readonly string Token;

        public VmTabWindow(string token)
        {
            Token = token;
            Items.CollectionChanged += ItemsOnCollectionChanged;
        }

        private void ItemsOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            RaisePropertyChanged(nameof(BtnCloseAllVisibility));
        }

        public void Dispose()
        {
            SelectedItem = null;
            foreach (var item in Items.ToArray())
            {
                if (item.Content is IDisposable dp)
                {
                    dp.Dispose();
                }
            }
            Items.CollectionChanged -= ItemsOnCollectionChanged;
            Items.Clear();
        }

        private string _tag = "";

        /// <summary>
        /// tag of the Tab, e.g. tag = Group1 then the servers in Group1 will be shown on this Tab.
        /// </summary>
        public string Tag
        {
            get => _tag;
            set
            {
                SetAndNotifyIfChanged(nameof(Tag), ref _tag, value);
                SetTitle();
            }
        }

        private string _title = "";

        public string Title
        {
            get => _title;
            set => SetAndNotifyIfChanged(nameof(Title), ref _title, value);
        }

        public ObservableCollection<TabItemViewModel> Items { get; } = new ObservableCollection<TabItemViewModel>();

        public Visibility BtnCloseAllVisibility => Items.Count > 1 ? Visibility.Visible : Visibility.Collapsed;

        private bool _isTagEditing = false;

        public bool IsTagEditing
        {
            get => _isTagEditing;
            set
            {
                SetAndNotifyIfChanged(nameof(IsTagEditing), ref _isTagEditing, value);
                RaisePropertyChanged(nameof(TitleTextVisibility));
                RaisePropertyChanged(nameof(TitleTagEditorVisibility));
            }
        }

        public Visibility TitleTextVisibility => !IsTagEditing ? Visibility.Visible : Visibility.Collapsed;

        public Visibility TitleTagEditorVisibility => IsTagEditing ? Visibility.Visible : Visibility.Collapsed;

        private TabItemViewModel _selectedItem = new TabItemViewModel();

        public TabItemViewModel SelectedItem
        {
            get => _selectedItem;
            set
            {
                SetAndNotifyIfChanged(nameof(SelectedItem), ref _selectedItem, value);
                SetTitle();
            }
        }

        #region drag drop tab

        private readonly IInterTabClient _interTabClient = new InterTabClient();
        public IInterTabClient InterTabClient => _interTabClient;

        #endregion drag drop tab

        private void SetTitle()
        {
            if (SelectedItem != null)
            {
                if (!string.IsNullOrEmpty(Tag))
                    this.Title = Tag + " - " + SelectedItem.Header;
                else
                    this.Title = SelectedItem.Header + " - " + SystemConfig.AppName;
#if DEV
                if (!string.IsNullOrEmpty(Tag))
                    this.Title = Tag + " - " + SelectedItem.Header;
                else
                    this.Title = SelectedItem.Header + " - PRemoteM";
#endif
            }
        }

        #region CMD

        private RelayCommand _cmdHostGoFullScreen;

        public RelayCommand CmdHostGoFullScreen
        {
            get
            {
                if (_cmdHostGoFullScreen == null)
                {
                    _cmdHostGoFullScreen = new RelayCommand((o) =>
                    {
                        if (this.SelectedItem?.Content?.CanResizeNow() ?? false)
                            RemoteWindowPool.Instance.MoveProtocolHostToFullScreen(SelectedItem.Content.ConnectionId);
                    }, o => this.SelectedItem != null && (this.SelectedItem.Content?.CanFullScreen ?? false));
                }
                return _cmdHostGoFullScreen;
            }
        }

        private RelayCommand _cmdIsTagEditToggle;

        public RelayCommand CmdIsTagEditToggle
        {
            get
            {
                if (_cmdIsTagEditToggle == null)
                {
                    _cmdIsTagEditToggle = new RelayCommand((o) =>
                    {
                        IsTagEditing = !IsTagEditing;
                    }, o => this.SelectedItem != null);
                }
                return _cmdIsTagEditToggle;
            }
        }

        private RelayCommand _cmdInvokeLauncher;

        public RelayCommand CmdInvokeLauncher
        {
            get
            {
                if (_cmdInvokeLauncher == null)
                {
                    _cmdInvokeLauncher = new RelayCommand((o) =>
                    {
                        App.SearchBoxWindow?.ShowMe(this.Token);
                    }, o => this.SelectedItem != null);
                }
                return _cmdInvokeLauncher;
            }
        }

        private RelayCommand _cmdShowTabByIndex;

        public RelayCommand CmdShowTabByIndex
        {
            get
            {
                if (_cmdShowTabByIndex == null)
                {
                    _cmdShowTabByIndex = new RelayCommand((o) =>
                    {
                        if (int.TryParse(o.ToString(), out int i))
                        {
                            if (i > 0 && i <= Items.Count)
                            {
                                SelectedItem = Items[i - 1];
                            }
                        }
                    }, o => this.SelectedItem != null);
                }
                return _cmdShowTabByIndex;
            }
        }

        private RelayCommand _cmdGoMinimize;

        public RelayCommand CmdGoMinimize
        {
            get
            {
                if (_cmdGoMinimize == null)
                {
                    _cmdGoMinimize = new RelayCommand((o) =>
                    {
                        if (o is Window window)
                        {
                            window.WindowState = WindowState.Minimized;
                            SelectedItem.Content.ToggleAutoResize(false);
                        }
                    });
                }
                return _cmdGoMinimize;
            }
        }

        private RelayCommand _cmdGoMaximize;

        public RelayCommand CmdGoMaximize
        {
            get
            {
                if (_cmdGoMaximize == null)
                {
                    _cmdGoMaximize = new RelayCommand((o) =>
                    {
                        if (o is Window window)
                            window.WindowState = (window.WindowState == WindowState.Normal) ? WindowState.Maximized : WindowState.Normal;
                    });
                }
                return _cmdGoMaximize;
            }
        }

        private RelayCommand _cmdCloseAll;

        public RelayCommand CmdCloseAll
        {
            get
            {
                if (_cmdCloseAll == null)
                {
                    _cmdCloseAll = new RelayCommand((o) =>
                    {
                        RemoteWindowPool.Instance.DelTabWindow(Token);
                    });
                }
                return _cmdCloseAll;
            }
        }

        private RelayCommand _cmdClose;

        public RelayCommand CmdClose
        {
            get
            {
                if (_cmdClose == null)
                {
                    _cmdClose = new RelayCommand((o) =>
                    {
                        if (SelectedItem != null)
                        {
                            RemoteWindowPool.Instance.DelProtocolHostInSyncContext(SelectedItem?.Content?.ConnectionId);
                        }
                        else
                        {
                            CmdCloseAll.Execute();
                        }
                    }, o => this.SelectedItem != null);
                }
                return _cmdClose;
            }
        }

        #endregion CMD
    }

    public class InterTabClient : IInterTabClient
    {
        public INewTabHost<Window> GetNewHost(IInterTabClient interTabClient, object partition, TabablzControl source)
        {
            string token = DateTime.Now.Ticks.ToString();
            if (SystemConfig.Instance.Theme.TabUI == EnumTabUI.ChromeLike)
            {
                var v = new TabWindowChrome(token);
                RemoteWindowPool.Instance.AddTab(v);
                return new NewTabHost<Window>(v, v.TabablzControl);
            }
            else
            {
                var v = new TabWindowClassical(token);
                RemoteWindowPool.Instance.AddTab(v);
                return new NewTabHost<Window>(v, v.TabablzControl);
            }
        }

        public TabEmptiedResponse TabEmptiedHandler(TabablzControl tabControl, Window window)
        {
            if (window is TabWindowBase tab)
            {
                RemoteWindowPool.Instance.DelTabWindow(tab.GetViewModel().Token);
            }
            return TabEmptiedResponse.CloseWindowOrLayoutBranch;
        }
    }
}