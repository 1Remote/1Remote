using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Dragablz;
using PRM.Core.Model;
using PRM.Core.Protocol;
using PRM.Core.Ulits.DragablzTab;
using PRM.Model;
using PRM.View;
using Shawn.Ulits;
using Shawn.Ulits.RDP;
using NotifyPropertyChangedBase = PRM.Core.NotifyPropertyChangedBase;

namespace PRM.ViewModel
{
    public class VmTabWindow : NotifyPropertyChangedBase
    {
        public readonly string Token;
        public VmTabWindow(string token)
        {
            Token = token;
            Items.CollectionChanged += (sender, args) =>
                RaisePropertyChanged(nameof(BtnCloseAllVisibility));
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
        #endregion


        private void SetTitle()
        {
            if (SelectedItem != null)
            {
                if (!string.IsNullOrEmpty(Tag))
                    this.Title = Tag + " - " + SelectedItem.Header;
                else
                    this.Title = SelectedItem.Header + " - " + SystemConfig.AppName;
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

        private RelayCommand _cmdClose;
        public RelayCommand CmdClose
        {
            get
            {
                if (_cmdClose == null)
                {
                    _cmdClose = new RelayCommand((o) =>
                    {
                        RemoteWindowPool.Instance.DelTabWindow(Token);
                    }, o => this.SelectedItem != null);
                }
                return _cmdClose;
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
        #endregion
    }


    public class InterTabClient : IInterTabClient
    {
        public INewTabHost<Window> GetNewHost(IInterTabClient interTabClient, object partition, TabablzControl source)
        {
            string token = DateTime.Now.Ticks.ToString();
            var v = new TabWindow(token);
            RemoteWindowPool.Instance.AddTab(v);
            return new NewTabHost<Window>(v, v.TabablzControl);
        }
        public TabEmptiedResponse TabEmptiedHandler(TabablzControl tabControl, Window window)
        {
            if (window is TabWindow tab)
            {
                RemoteWindowPool.Instance.DelTabWindow(tab.Vm.Token);
            }
            return TabEmptiedResponse.CloseWindowOrLayoutBranch;
        }
    }
}
