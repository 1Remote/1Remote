﻿ using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using Dragablz;
using _1RM.Service;
using _1RM.Utils;
using _1RM.View.Host.ProtocolHosts;
 using _1RM.View.Utils;
 using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;
using Stylet;
using _1RM.Model.DAO.Dapper;

namespace _1RM.View.Host
{
    public class TabWindowViewModel : MaskLayerContainerScreenBase, IDisposable
    {
        public readonly string Token;
        private readonly TabWindowBase _windowView;

        public TabWindowViewModel(string token, TabWindowBase windowView)
        {
            _windowView = windowView;
            Token = token;
            Items.CollectionChanged += ItemsOnCollectionChanged;
        }

        private void ItemsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RaisePropertyChanged(nameof(BtnCloseAllVisibility));
            if (Items.Count == 0)
            {
                _windowView.Hide();
            }
        }

        public void Dispose()
        {
            Execute.OnUIThread(() =>
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
            });
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
                if (SelectedItem?.Content == null
                    || SelectedItem.Content.CanResizeNow() == false)
                    return ResizeMode.NoResize;
                return ResizeMode.CanResize;
            }
        }

        public bool LauncherEnabled => IoC.Get<ConfigurationService>().Launcher.LauncherEnabled;

        public ObservableCollection<TabItemViewModel> Items { get; } = new ObservableCollection<TabItemViewModel>();

        public Visibility BtnCloseAllVisibility => Items.Count > 1 ? Visibility.Visible : Visibility.Collapsed;

        private TabItemViewModel? _selectedItem = null;
        public TabItemViewModel? SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (_selectedItem != null)
                {
                    _selectedItem.Content.OnCanResizeNowChanged -= OnCanResizeNowChanged;
                }

                if (SetAndNotifyIfChanged(ref _selectedItem, value))
                {
                    if (_selectedItem != null)
                    {
                        SetTitle();
                        _selectedItem.Content.OnCanResizeNowChanged += OnCanResizeNowChanged;
                    }
                    RaisePropertyChanged(nameof(WindowResizeMode));
                }
            }
        }

        private void OnCanResizeNowChanged()
        {
            RaisePropertyChanged(nameof(WindowResizeMode));
        }


        #region drag drop tab

        public IInterTabClient InterTabClient { get; } = new InterTabClient();

        #endregion drag drop tab

        private void SetTitle()
        {
            if (SelectedItem != null)
            {
                this.Title = SelectedItem.Header + " - " + Assert.APP_DISPLAY_NAME;
            }
        }

        public bool TryRemoveItem(string connectionId)
        {
            var item = Items.FirstOrDefault(x => x.Content.ConnectionId == connectionId);
            if (item != null)
            {
                Execute.OnUIThreadSync(() =>
                {
                    Items.Remove(item);
                    SelectedItem = Items.FirstOrDefault();
                });
            }
            return false;
        }

        public void AddItem(TabItemViewModel newItem)
        {
            if (Items.Any(x => x.Content?.ConnectionId == newItem.Content.ConnectionId))
            {
                SelectedItem = Items.First(x => x.Content.ConnectionId == newItem.Content.ConnectionId);
                return;
            }
            Items.Add(newItem);
            newItem.Content.SetParentWindow(_windowView);
            SelectedItem = newItem;
        }

        public TabItemViewModel? GetItem(string connectionId)
        {
            return Items.FirstOrDefault(x => x.Content.ConnectionId == connectionId);
        }


        #region CMD

        private RelayCommand? _cmdHostGoFullScreen;
        public RelayCommand CmdHostGoFullScreen
        {
            get
            {
                return _cmdHostGoFullScreen ??= new RelayCommand((o) =>
                {
                    if (this.SelectedItem?.Content?.CanResizeNow() ?? false)
                        IoC.Get<SessionControlService>().MoveSessionToFullScreen(SelectedItem.Content.ConnectionId);
                }, o => this.SelectedItem != null && (this.SelectedItem.Content?.CanFullScreen ?? false));
            }
        }

        private RelayCommand? _cmdInvokeLauncher;
        public RelayCommand CmdInvokeLauncher
        {
            get
            {
                return _cmdInvokeLauncher ??= new RelayCommand((o) => { IoC.Get<LauncherWindowViewModel>().ShowMe(); }, o => this.SelectedItem != null);
            }
        }

        private RelayCommand? _cmdShowTabByIndex;
        public RelayCommand CmdShowTabByIndex
        {
            get
            {
                return _cmdShowTabByIndex ??= new RelayCommand((o) =>
                {
                    if (int.TryParse(o?.ToString() ?? "0", out int i))
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
                    if (_windowView.WindowState != WindowState.Maximized)
                    {
                        _windowView.WindowState = WindowState.Maximized;
                    }

                    else
                    {
                        _windowView.WindowStyle = WindowStyle.SingleBorderWindow;
                        _windowView.WindowState = WindowState.Normal;
                    }
                });
            }
        }


        private RelayCommand? _cmdGoMaximizeF11;
        public RelayCommand CmdGoMaximizeF11
        {
            get
            {
                return _cmdGoMaximizeF11 ??= new RelayCommand((o) =>
                {
                    if (_windowView.WindowState != WindowState.Maximized)
                    {
                        _windowView.WindowStyle = WindowStyle.None;
                        _windowView.WindowState = WindowState.Maximized;
                    }
                    else
                    {
                        _windowView.WindowStyle = WindowStyle.SingleBorderWindow;
                        _windowView.WindowState = WindowState.Normal;
                    }
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
                    if (_canCmdClose)
                    {
                        _canCmdClose = false;
                        if (IoC.Get<ConfigurationService>().General.ConfirmBeforeClosingSession == true
                            && this.Items.Count > 0
                            && App.ExitingFlag == false
                            && false == MessageBoxHelper.Confirm(IoC.Get<ILanguageService>().Translate("Are you sure you want to close the connection?"), ownerViewModel: this))
                        {
                        }
                        else
                        {
                            IoC.Get<SessionControlService>().CloseProtocolHostAsync(
                                Items
                                .Where(x => x.Host.ProtocolServer.IsTmpSession() == false)
                                .Select(x => x.Host.ConnectionId).ToArray());
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
                    if (_canCmdClose)
                    {
                        _canCmdClose = false;
                        if (IoC.Get<ConfigurationService>().General.ConfirmBeforeClosingSession == true
                            && App.ExitingFlag == false
                            && false == MessageBoxHelper.Confirm(IoC.Get<ILanguageService>().Translate("Are you sure you want to close the connection?"), ownerViewModel: this))
                        {
                        }
                        else
                        {
                            HostBase? host = null;
                            if (o is string connectionId)
                            {
                                host = Items.FirstOrDefault(x => x.Host.ConnectionId == connectionId)?.Host;
                            }
                            else
                            {
                                host = SelectedItem?.Content;
                            }

                            if (host != null)
                            {
                                IoC.Get<SessionControlService>().CloseProtocolHostAsync(host.ConnectionId);
                            }
                        }

                        _canCmdClose = true;
                    }
                }, o => this.SelectedItem != null);
            }
        }

        #endregion CMD
    }

    public class InterTabClient : IInterTabClient
    {
        /// <summary>
        /// split tab window
        /// </summary>
        /// <param name="interTabClient"></param>
        /// <param name="partition"></param>
        /// <param name="source"></param>
        /// <returns></returns>
        public INewTabHost<Window> GetNewHost(IInterTabClient interTabClient, object partition, TabablzControl source)
        {
            string token = DateTime.Now.Ticks.ToString();
            var v = new TabWindowView(token, IoC.Get<LocalityService>());
            IoC.Get<SessionControlService>().AddTab(v);
            return new NewTabHost<Window>(v, v.TabablzControl);
        }

        /// <summary>
        /// merge tab window
        /// </summary>
        /// <param name="tabControl"></param>
        /// <param name="window"></param>
        /// <returns></returns>
        public TabEmptiedResponse TabEmptiedHandler(TabablzControl tabControl, Window window)
        {
            if (window is TabWindowBase tab)
            {
                tab.GetViewModel().Items.Clear();
                IoC.Get<SessionControlService>().CleanupProtocolsAndWindows();
            }
            return TabEmptiedResponse.CloseWindowOrLayoutBranch;
        }
    }
}