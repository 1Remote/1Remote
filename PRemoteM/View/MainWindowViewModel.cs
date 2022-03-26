using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using PRM.Model;
using PRM.Model.Protocol;
using PRM.Model.Protocol.Base;
using PRM.View.Editor;
using PRM.View.Settings;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.PageHost;

namespace PRM.View
{
    public class MainWindowViewModel : NotifyPropertyChangedBase
    {
        public SettingsPageViewModel SettingsPageVm { get; }
        public AboutPageViewModel AboutPageViewModel { get; }
        private ServerListPage _serverListPage;

        #region Properties

        public PrmContext Context { get; }

        public AnimationPage AnimationPageServerList { get; private set; }


        private AnimationPage _animationPageEditor = null;
        public AnimationPage AnimationPageEditor
        {
            get => _animationPageEditor;
            set => SetAndNotifyIfChanged(ref _animationPageEditor, value);
        }

        private AnimationPage _animationPageSettings = null;
        public AnimationPage AnimationPageSettings
        {
            get => _animationPageSettings;
            set => SetAndNotifyIfChanged(ref _animationPageSettings, value);
        }



        private AnimationPage _animationPageAbout = null;
        public AnimationPage AnimationPageAbout
        {
            get => _animationPageAbout;
            set => SetAndNotifyIfChanged(ref _animationPageAbout, value);
        }


        private Visibility _processingRingVisibility = Visibility.Collapsed;
        public Visibility ProcessingRingVisibility
        {
            get => _processingRingVisibility;
            set => SetAndNotifyIfChanged(ref _processingRingVisibility, value);
        }

        private Visibility _requestRatingPopupVisibility = Visibility.Collapsed;
        public Visibility RequestRatingPopupVisibility
        {
            get => _requestRatingPopupVisibility;
            set => SetAndNotifyIfChanged(ref _requestRatingPopupVisibility, value);
        }

        private string _processingRingMessage = "";
        public string ProcessingRingMessage
        {
            get => _processingRingMessage;
            set => SetAndNotifyIfChanged(ref _processingRingMessage, value);
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

        public readonly MainWindowView WindowView;

        public MainWindowViewModel(PrmContext context, SettingsPageViewModel settingsPageVm, MainWindowView windowView)
        {
            Context = context;
            WindowView = windowView;
            SettingsPageViewModel.Init(context);
            SettingsPageVm = settingsPageVm;
            SettingsPageVm.Host = this;
            AboutPageViewModel = new AboutPageViewModel();
            GlobalEventHelper.ShowProcessingRing += (visibility, msg) =>
            {
                WindowView.Dispatcher.Invoke(() =>
                {
                    if (visibility == Visibility.Visible)
                    {
                        ProcessingRingVisibility = Visibility.Visible;
                        ProcessingRingMessage = msg;
                    }
                    else
                    {
                        ProcessingRingVisibility = Visibility.Collapsed;
                    }
                });
            };
            GlobalEventHelper.OnRequestGoToServerEditPage += new GlobalEventHelper.OnRequestGoToServerEditPageDelegate((id, isDuplicate, isInAnimationShow) =>
            {
                if (id <= 0) return;
                Debug.Assert(Context.AppData.VmItemList.Any(x => x.Server.Id == id));
                var server = Context.AppData.VmItemList.First(x => x.Server.Id == id).Server;
                WindowView.Dispatcher.Invoke(() =>
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
                WindowView.Dispatcher.Invoke(() =>
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
                WindowView.Dispatcher.Invoke(() =>
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

        public void ShowListPage()
        {
            _serverListPage = new ServerListPage(Context, SettingsPageVm, this);
            AnimationPageServerList = new AnimationPage()
            {
                InAnimationType = AnimationPage.InOutAnimationType.None,
                OutAnimationType = AnimationPage.InOutAnimationType.None,
                Content = _serverListPage,
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
                    this._serverListPage.Vm.ListPageIsCardView = !this._serverListPage.Vm.ListPageIsCardView;
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