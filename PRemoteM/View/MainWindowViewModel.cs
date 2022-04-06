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
    public class MainWindowViewModel : NotifyPropertyChangedBaseScreen
    {
        public SettingsPageViewModel SettingsPageVm { get; }
        public AboutPageViewModel AboutPageViewModel { get; }
        public ServerListPageViewModel ServerListViewModel { get; }

        #region Properties

        public PrmContext Context { get; }



        //private AnimationPage _animationPageEditor = null;
        //public AnimationPage AnimationPageEditor
        //{
        //    get => _animationPageEditor;
        //    set => SetAndNotifyIfChanged(ref _animationPageEditor, value);
        //}

        //private AnimationPage _animationPageSettings = null;
        //public AnimationPage AnimationPageSettings
        //{
        //    get => _animationPageSettings;
        //    set => SetAndNotifyIfChanged(ref _animationPageSettings, value);
        //}



        //private AnimationPage _animationPageAbout = null;
        //public AnimationPage AnimationPageAbout
        //{
        //    get => _animationPageAbout;
        //    set => SetAndNotifyIfChanged(ref _animationPageAbout, value);
        //}

        private INotifyPropertyChanged _contentViewModel;
        public INotifyPropertyChanged ContentViewModel
        {
            get => _contentViewModel;
            set => SetAndNotifyIfChanged(ref _contentViewModel, value);
        }

        private INotifyPropertyChanged _topLevelViewModel;
        public INotifyPropertyChanged TopLevelViewModel
        {
            get => _topLevelViewModel;
            set => SetAndNotifyIfChanged(ref _topLevelViewModel, value);
        }

        #endregion Properties

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

        public MainWindowView WindowView { get; private set; }

        public MainWindowViewModel(PrmContext context, SettingsPageViewModel settingsPageVm)
        {
            Context = context;
            SettingsPageVm = settingsPageVm;
            SettingsPageVm.Host = this;
            AboutPageViewModel = new AboutPageViewModel();
            ServerListViewModel = new ServerListPageViewModel(Context, SettingsPageVm, this);
            ContentViewModel = ServerListViewModel;
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
                Execute.OnUIThread(() =>
                {
                    AnimationPageAbout = null;
                    AnimationPageSettings = null;
                    AnimationPageEditor = new AnimationPage()
                    {
                        InAnimationType = isInAnimationShow ? AnimationPage.InOutAnimationType.SlideFromRight : AnimationPage.InOutAnimationType.None,
                        OutAnimationType = AnimationPage.InOutAnimationType.SlideToRight,
                        Content = new ServerEditorPage(Context, new ServerEditorPageViewModel(Context.AppData, Context.DataService, Context.LanguageService, server, isDuplicate)),
                    };
                    WindowView.ActivateMe();
                });
            });

            GlobalEventHelper.OnGoToServerAddPage += new GlobalEventHelper.OnGoToServerAddPageDelegate((tagNames, isInAnimationShow) =>
            {
                var server = new RDP
                {
                    Tags = new List<string>(tagNames)
                };
                Execute.OnUIThread(() =>
                {
                    AnimationPageEditor = new AnimationPage()
                    {
                        InAnimationType = isInAnimationShow ? AnimationPage.InOutAnimationType.SlideFromRight : AnimationPage.InOutAnimationType.None,
                        OutAnimationType = AnimationPage.InOutAnimationType.SlideToRight,
                        Content = new ServerEditorPage(Context, new ServerEditorPageViewModel(Context.AppData, Context.DataService, Context.LanguageService, server)),
                    };
                    WindowView.ActivateMe();
                });
            });

            GlobalEventHelper.OnRequestGoToServerMultipleEditPage += (servers, isInAnimationShow) =>
            {
                Execute.OnUIThread(() =>
                {
                    var page = new AnimationPage()
                    {
                        InAnimationType = isInAnimationShow ? AnimationPage.InOutAnimationType.SlideFromRight : AnimationPage.InOutAnimationType.None,
                        OutAnimationType = AnimationPage.InOutAnimationType.SlideToRight,
                    };
                    var serverBases = servers as ProtocolBase[] ?? servers.ToArray();
                    if (serverBases.Count() > 1)
                        page.Content = new ServerEditorPage(Context, new ServerEditorPageViewModel(Context.AppData, Context.DataService, Context.LanguageService, serverBases));
                    else
                        page.Content = new ServerEditorPage(Context, new ServerEditorPageViewModel(Context.AppData, Context.DataService, Context.LanguageService, serverBases.First()));
                    AnimationPageEditor = page;
                    WindowView.ActivateMe();
                });
            };
        }

        #region CMD

        private RelayCommand _cmdGoSysOptionsPage;

        public RelayCommand CmdGoSysOptionsPage
        {
            get
            {
                return _cmdGoSysOptionsPage ??= new RelayCommand((o) =>
                {
                    AnimationPageSettings = new AnimationPage()
                    {
                        InAnimationType = AnimationPage.InOutAnimationType.SlideFromRight,
                        OutAnimationType = AnimationPage.InOutAnimationType.SlideToRight,
                        Content = new SettingsPage(Context, SettingsPageVm, o?.ToString()),
                    };
                    WindowView.PopupMenu.IsOpen = false;
                }, o => AnimationPageAbout == null && AnimationPageSettings == null && AnimationPageEditor == null);
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
                }, o => AnimationPageAbout == null && AnimationPageEditor == null && AnimationPageSettings == null);
            }
        }

        private RelayCommand _cmdGoAboutPage;

        public RelayCommand CmdGoAboutPage
        {
            get
            {
                return _cmdGoAboutPage ??= new RelayCommand((o) =>
                {
                    AnimationPageAbout = new AnimationPage()
                    {
                        InAnimationType = AnimationPage.InOutAnimationType.SlideFromRight,
                        OutAnimationType = AnimationPage.InOutAnimationType.SlideToRight,
                        Content = new AboutPage(AboutPageViewModel, this),
                    };
                    WindowView.PopupMenu.IsOpen = false;
                }, o => AnimationPageAbout?.Content?.GetType() != typeof(AboutPage));
            }
        }

        #endregion CMD
    }
}