using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using Dragablz;
using PRM.Model;
using PRM.Service;
using PRM.View.Host.ProtocolHosts;
using Shawn.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;

namespace PRM.View.Host
{
    public class TabWindowViewModel : NotifyPropertyChangedBase, IDisposable
    {
        public readonly string Token;

        public TabWindowViewModel(string token)
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

        private ObservableCollection<string> _tags = new ObservableCollection<string>();

        /// <summary>
        /// tag of the Tab, e.g. tag = Group1 then the servers in Group1 will be shown on this Tab.
        /// </summary>
        public ObservableCollection<string> Tags
        {
            get => _tags;
            set
            {
                SetAndNotifyIfChanged(ref _tags, value);
                SetTitle();
            }
        }

        private string _title = "";

        public string Title
        {
            get => _title;
            set => SetAndNotifyIfChanged(ref _title, value);
        }



        public ResizeMode WindowResizeMode
        {
            get
            {
                if (_isLocked
                    || SelectedItem?.Content == null
                    || (SelectedItem?.Content is AxMsRdpClient09Host && SelectedItem?.CanResizeNow == false))
                    return ResizeMode.NoResize;
                return ResizeMode.CanResize;
            }
        }


        private bool _isLocked = false;
        public bool IsLocked
        {
            get => _isLocked;
            set
            {
                SetAndNotifyIfChanged(ref _isLocked, value);
                RaisePropertyChanged(nameof(WindowResizeMode));
            }
        }


        public ObservableCollection<TabItemViewModel> Items { get; } = new ObservableCollection<TabItemViewModel>();

        public Visibility BtnCloseAllVisibility => Items.Count > 1 ? Visibility.Visible : Visibility.Collapsed;

        private bool _isTagEditing = false;

        public bool IsTagEditing
        {
            get => _isTagEditing;
            set
            {
                SetAndNotifyIfChanged(ref _isTagEditing, value);
                RaisePropertyChanged(nameof(TitleTextVisibility));
                RaisePropertyChanged(nameof(TitleTagEditorVisibility));
            }
        }

        public Visibility TitleTextVisibility => !IsTagEditing ? Visibility.Visible : Visibility.Collapsed;
        public Visibility TitleTagEditorVisibility => IsTagEditing ? Visibility.Visible : Visibility.Collapsed;

        private TabItemViewModel? _selectedItem = null;
        public TabItemViewModel? SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (_selectedItem != null)
                    try
                    {
                        _selectedItem.PropertyChanged -= SelectedItemOnPropertyChanged;
                    }
                    catch (Exception e)
                    {
                        Console.Write(e);
                    }

                if (SetAndNotifyIfChanged(ref _selectedItem, value))
                {
                    RaisePropertyChanged(nameof(WindowResizeMode));

                    if (_selectedItem != null)
                    {
                        SetTitle();
                        _selectedItem.PropertyChanged += SelectedItemOnPropertyChanged;
                    }
                }
            }
        }

        private void SelectedItemOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TabItemViewModel.CanResizeNow))
            {
                RaisePropertyChanged(nameof(WindowResizeMode));
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
                this.Title = SelectedItem.Header + " - " + ConfigurationService.AppName;
            }
        }

        #region CMD

        private RelayCommand? _cmdHostGoFullScreen;
        public RelayCommand CmdHostGoFullScreen
        {
            get
            {
                if (_cmdHostGoFullScreen == null)
                {
                    _cmdHostGoFullScreen = new RelayCommand((o) =>
                    {
                        if (IsLocked) return;
                        if (this.SelectedItem?.Content?.CanResizeNow() ?? false)
                            IoC.Get<RemoteWindowPool>().MoveProtocolHostToFullScreen(SelectedItem.Content.ConnectionId);
                    }, o => this.SelectedItem != null && (this.SelectedItem.Content?.CanFullScreen ?? false));
                }
                return _cmdHostGoFullScreen;
            }
        }

        private RelayCommand? _cmdIsTagEditToggle;
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

        private RelayCommand? _cmdInvokeLauncher;
        public RelayCommand CmdInvokeLauncher
        {
            get
            {
                if (_cmdInvokeLauncher == null)
                {
                    _cmdInvokeLauncher = new RelayCommand((o) =>
                    {
                        IoC.Get<LauncherWindowViewModel>().ShowMe();
                    }, o => this.SelectedItem != null);
                }
                return _cmdInvokeLauncher;
            }
        }

        private RelayCommand? _cmdShowTabByIndex;
        public RelayCommand CmdShowTabByIndex
        {
            get
            {
                return _cmdShowTabByIndex ??= new RelayCommand((o) =>
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
        }

        private RelayCommand? _cmdGoMinimize;
        public RelayCommand CmdGoMinimize
        {
            get
            {
                return _cmdGoMinimize ??= new RelayCommand((o) =>
                {
                    if (o is Window window)
                    {
                        window.WindowState = WindowState.Minimized;
                        if (SelectedItem?.Content != null)
                            SelectedItem.Content.ToggleAutoResize(false);
                    }
                });
            }
        }

        private RelayCommand? _cmdGoMaximize;
        public RelayCommand CmdGoMaximize
        {
            get
            {
                return _cmdGoMaximize ??= new RelayCommand((o) =>
                {
                    if (o is Window window)
                        window.WindowState = (window.WindowState == WindowState.Normal) ? WindowState.Maximized : WindowState.Normal;
                });
            }
        }

        private bool _canCmdClose = true;

        private RelayCommand? _cmdCloseAll;
        public RelayCommand CmdCloseAll
        {
            get
            {
                return _cmdCloseAll ??= new RelayCommand((o) =>
                {
                    if (IsLocked) return;
                    if (_canCmdClose)
                    {
                        _canCmdClose = false;
                        if (IoC.Get<ConfigurationService>().General.ConfirmBeforeClosingSession == true
                            && this.Items.Count > 0
                            && MessageBox.Show(IoC.Get<ILanguageService>().Translate("Are you sure you want to close the connection?"), IoC.Get<ILanguageService>().Translate("messagebox_title_warning"), MessageBoxButton.YesNo) !=
                            MessageBoxResult.Yes)
                        {
                        }
                        else
                        {
                            IoC.Get<RemoteWindowPool>().DelTabWindow(Token);
                        }
                        _canCmdClose = true;
                    }
                });
            }
        }

        private RelayCommand? _cmdClose;
        public RelayCommand CmdClose
        {
            get
            {
                return _cmdClose ??= new RelayCommand((o) =>
                {
                    if (IsLocked) return;
                    if (_canCmdClose)
                    {
                        _canCmdClose = false;
                        var cid = SelectedItem?.Content?.ConnectionId;
                        IoC.Get<RemoteWindowPool>().DelProtocolHostInSyncContext(cid, true);
                        _canCmdClose = true;
                    }
                }, o => this.SelectedItem != null);
            }
        }

        #endregion CMD
    }

    public class InterTabClient : IInterTabClient
    {
        public INewTabHost<Window> GetNewHost(IInterTabClient interTabClient, object partition, TabablzControl source)
        {
            string token = DateTime.Now.Ticks.ToString();
            var v = new TabWindowView(token, IoC.Get<PrmContext>().LocalityService);
            IoC.Get<RemoteWindowPool>().AddTab(v);
            return new NewTabHost<Window>(v, v.TabablzControl);
        }

        public TabEmptiedResponse TabEmptiedHandler(TabablzControl tabControl, Window window)
        {
            if (window is TabWindowBase tab)
            {
                IoC.Get<RemoteWindowPool>().DelTabWindow(tab.GetViewModel().Token);
            }
            return TabEmptiedResponse.CloseWindowOrLayoutBranch;
        }
    }
}