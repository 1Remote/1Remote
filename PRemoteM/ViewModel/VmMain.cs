using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using PRM.Core.Model;
using PRM.Core.Protocol;
using PRM.Core.Protocol.RDP;
using Shawn.Utils;
using Shawn.Utils.PageHost;
using PRM.View;
using PRM.ViewModel.Configuration;

using NotifyPropertyChangedBase = PRM.Core.NotifyPropertyChangedBase;

namespace PRM.ViewModel
{
    public class VmMain : NotifyPropertyChangedBase
    {
        public ConfigurationViewModel ConfigurationVm { get; }
        public VmAboutPage VmAboutPage { get; }
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

        public readonly MainWindow Window;

        public VmMain(PrmContext context, ConfigurationViewModel configurationVm, MainWindow window)
        {
            Context = context;
            Window = window;
            ConfigurationViewModel.Init(context);
            ConfigurationVm = configurationVm;
            ConfigurationVm.Host = this;
            VmAboutPage = new VmAboutPage();
            GlobalEventHelper.ShowProcessingRing += (visibility, msg) =>
            {
                Window.Dispatcher.Invoke(() =>
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
                Window.Dispatcher.Invoke(() =>
                {
                    AnimationPageAbout = null;
                    AnimationPageSettings = null;
                    AnimationPageEditor = new AnimationPage()
                    {
                        InAnimationType = isInAnimationShow ? AnimationPage.InOutAnimationType.SlideFromRight : AnimationPage.InOutAnimationType.None,
                        OutAnimationType = AnimationPage.InOutAnimationType.SlideToRight,
                        Page = new ServerEditorPage(Context, new VmServerEditorPage(Context.AppData, Context.DataService, Context.LanguageService, server, isDuplicate)),
                    };
                    Window.ActivateMe();
                });
            });

            GlobalEventHelper.OnGoToServerAddPage += new GlobalEventHelper.OnGoToServerAddPageDelegate((tagNames, isInAnimationShow) =>
            {
                var server = new ProtocolServerRDP
                {
                    Tags = new List<string>(tagNames)
                };
                Window.Dispatcher.Invoke(() =>
                {
                    AnimationPageEditor = new AnimationPage()
                    {
                        InAnimationType = isInAnimationShow ? AnimationPage.InOutAnimationType.SlideFromRight : AnimationPage.InOutAnimationType.None,
                        OutAnimationType = AnimationPage.InOutAnimationType.SlideToRight,
                        Page = new ServerEditorPage(Context, new VmServerEditorPage(Context.AppData, Context.DataService, Context.LanguageService, server)),
                    };
                    Window.ActivateMe();
                });
            });

            GlobalEventHelper.OnRequestGoToServerMultipleEditPage += (servers, isInAnimationShow) =>
            {
                Window.Dispatcher.Invoke(() =>
                {
                    var page = new AnimationPage()
                    {
                        InAnimationType = isInAnimationShow ? AnimationPage.InOutAnimationType.SlideFromRight : AnimationPage.InOutAnimationType.None,
                        OutAnimationType = AnimationPage.InOutAnimationType.SlideToRight,
                    };
                    var serverBases = servers as ProtocolServerBase[] ?? servers.ToArray();
                    if (serverBases.Count() > 1)
                        page.Page = new ServerEditorPage(Context, new VmServerEditorPage(Context.AppData, Context.DataService, Context.LanguageService, serverBases));
                    else
                        page.Page = new ServerEditorPage(Context, new VmServerEditorPage(Context.AppData, Context.DataService, Context.LanguageService, serverBases.First()));
                    AnimationPageEditor = page;
                    Window.ActivateMe();
                });
            };
        }

        public void ShowListPage()
        {
            _serverListPage = new ServerListPage(Context, ConfigurationVm, this);
            AnimationPageServerList = new AnimationPage()
            {
                InAnimationType = AnimationPage.InOutAnimationType.None,
                OutAnimationType = AnimationPage.InOutAnimationType.None,
                Page = _serverListPage,
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
                        Page = new SystemConfigPage(Context, ConfigurationVm, o?.ToString()),
                    };
                    Window.PopupMenu.IsOpen = false;
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
                    Window.PopupMenu.IsOpen = false;
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
                        Page = new AboutPage(VmAboutPage, this),
                    };
                    Window.PopupMenu.IsOpen = false;
                }, o => AnimationPageAbout?.Page?.GetType() != typeof(AboutPage));
            }
        }

        #endregion CMD
    }
}