using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using PRM.Model;
using PRM.Model.Protocol;
using PRM.Model.Protocol.Base;
using PRM.Utils;
using PRM.View.Editor;
using PRM.View.Settings;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.PageHost;
using Stylet;

namespace PRM.View
{
    public enum EnumPage
    {
        List,
        Setting,
        About,
    }

    public class MainWindowViewModel : NotifyPropertyChangedBaseScreen
    {
        public PrmContext Context { get; }
        public ServerListPageViewModel ServerListViewModel { get; } = IoC.Get<ServerListPageViewModel>();
        public SettingsPageViewModel SettingViewModel { get; } = IoC.Get<SettingsPageViewModel>();
        public AboutPageViewModel AboutViewModel { get; } = IoC.Get<AboutPageViewModel>();

        #region Properties


        private INotifyPropertyChanged _topLevelViewModel;
        public INotifyPropertyChanged TopLevelViewModel
        {
            get => _topLevelViewModel;
            set => SetAndNotifyIfChanged(ref _topLevelViewModel, value);
        }
        private ServerEditorPageViewModel _contentViewModel = null;
        public ServerEditorPageViewModel ContentViewModel
        {
            get => _contentViewModel;
            set => SetAndNotifyIfChanged(ref _contentViewModel, value);
        }

        private EnumPage _displayPage = EnumPage.List;
        public EnumPage DisplayPage
        {
            get => _displayPage;
            set => SetAndNotifyIfChanged(ref _displayPage, value);
        }

        #region FilterString
        public Action OnFilterStringChangedByUi;
        public Action OnFilterStringChangedByBackend;

        private string _filterString = "";
        public string FilterString
        {
            get => _filterString;
            set
            {
                if (SetAndNotifyIfChanged(ref _filterString, value))
                {
                    // can only be called by the Ui
                    OnFilterStringChangedByUi?.Invoke();
                }
            }
        }

        public void SetFilterStringByBackend(string newValue)
        {
            // 区分关键词的来源，若是从后端设置关键词，则需要把搜索框的 CaretIndex 设置到末尾，以方便用户输入其他关键词。 
            // Distinguish the source of keywords, if the keyword is set from the backend, we need to set the CaretIndex of the search box to the end to facilitate the user to enter other keywords.
            if (_filterString == newValue)
                return;
            _filterString = newValue;
            RaisePropertyChanged(nameof(FilterString));
            OnFilterStringChangedByBackend?.Invoke();
        }
        #endregion

        #endregion Properties

        public MainWindowView WindowView { get; private set; }

        public MainWindowViewModel(PrmContext context)
        {
            Context = context;
            ServerListViewModel.Init(this);
            ShowList();
        }

        public void Init(MainWindowView windowView)
        {
            WindowView = windowView;
            GlobalEventHelper.ShowProcessingRing += (visibility, msg) =>
            {
                Execute.OnUIThread(() =>
                {
                    if (visibility == Visibility.Visible)
                    {
                        var pvm = IoC.Get<ProcessingRingViewModel>();
                        pvm.ProcessingRingMessage = msg;
                        TopLevelViewModel = pvm;
                    }
                    else
                    {
                        TopLevelViewModel = null;
                    }
                });
            };
            GlobalEventHelper.OnRequestGoToServerEditPage += new GlobalEventHelper.OnRequestGoToServerEditPageDelegate((id, isDuplicate, isInAnimationShow) =>
            {
                if (id <= 0) return;
                Debug.Assert(Context.AppData.VmItemList.Any(x => x.Server.Id == id));
                var server = Context.AppData.VmItemList.First(x => x.Server.Id == id).Server;
                ContentViewModel = new ServerEditorPageViewModel(Context.AppData, Context.DataService, server, isDuplicate);
                WindowView.ActivateMe();
            });

            GlobalEventHelper.OnGoToServerAddPage += new GlobalEventHelper.OnGoToServerAddPageDelegate((tagNames, isInAnimationShow) =>
            {
                var server = new RDP
                {
                    Tags = new List<string>(tagNames)
                };
                ContentViewModel = new ServerEditorPageViewModel(Context.AppData, Context.DataService, server);
                WindowView.ActivateMe();
            });

            GlobalEventHelper.OnRequestGoToServerMultipleEditPage += (servers, isInAnimationShow) =>
            {
                var serverBases = servers as ProtocolBase[] ?? servers.ToArray();
                if (serverBases.Count() > 1)
                    ContentViewModel = new ServerEditorPageViewModel(Context.AppData, Context.DataService, serverBases);
                else
                    ContentViewModel = new ServerEditorPageViewModel(Context.AppData, Context.DataService, serverBases.First());
                WindowView.ActivateMe();
            };
        }

        public void ShowList()
        {
            ContentViewModel = null;
            DisplayPage = EnumPage.List;
        }

        public bool IsShownList()
        {
            return ContentViewModel is null && DisplayPage == EnumPage.List;
        }


        #region CMD

        private RelayCommand _cmdGoSysOptionsPage;
        public RelayCommand CmdGoSysOptionsPage
        {
            get
            {
                return _cmdGoSysOptionsPage ??= new RelayCommand((o) =>
                {
                    DisplayPage = EnumPage.Setting;
                    WindowView.PopupMenu.IsOpen = false;
                }, o => IsShownList());
            }
        }

        private RelayCommand _cmdGoAboutPage;
        public RelayCommand CmdGoAboutPage
        {
            get
            {
                return _cmdGoAboutPage ??= new RelayCommand((o) =>
                {
                    DisplayPage = EnumPage.About;
                    WindowView.PopupMenu.IsOpen = false;
                }, o => IsShownList());
            }
        }

        private RelayCommand _cmdToggleCardList;

        public RelayCommand CmdToggleCardList
        {
            get
            {
                return _cmdToggleCardList ??= new RelayCommand((o) =>
                {
                    this.ServerListViewModel.ListPageIsCardView = !this.ServerListViewModel.ListPageIsCardView;
                    WindowView.PopupMenu.IsOpen = false;
                }, o => IsShownList());
            }
        }

        #endregion CMD
    }
}