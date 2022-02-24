using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Threading;
using PRM.Core.DB.Dapper;
using PRM.Core.Model;
using PRM.Core.Protocol;
using PRM.Core.Protocol.RDP;
using PRM.Core.Service;
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
        private readonly ServerListPage _serverListPage;

        #region Properties


        public AnimationPage AnimationPageServerList { get; }


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

        private int _progressBarValue = 0;

        public int ProgressBarValue
        {
            get => _progressBarValue;
            set => SetAndNotifyIfChanged(ref _progressBarValue, value);
        }

        private int _progressBarMaximum = 0;

        public int ProgressBarMaximum
        {
            get => _progressBarMaximum;
            set => SetAndNotifyIfChanged(ref _progressBarMaximum, value);
        }

        private string _progressBarInfo = "";

        public string ProgressBarInfo
        {
            get => _progressBarInfo;
            set => SetAndNotifyIfChanged(ref _progressBarInfo, value);
        }

        #endregion Properties

        public readonly MainWindow Window;
        public PrmContext Context { get; }

        public VmMain(PrmContext context, ConfigurationViewModel configurationVm, MainWindow window)
        {
            Context = context;
            Window = window;
            ConfigurationViewModel.Init(context);
            ConfigurationVm = configurationVm;
            ConfigurationVm.Host = this;
            VmAboutPage = new VmAboutPage();
            GlobalEventHelper.OnLongTimeProgress += (arg1, arg2, arg3) =>
            {
                Window.Dispatcher.Invoke(() =>
                {
                    ProgressBarValue = arg1;
                    ProgressBarMaximum = arg2;
                    ProgressBarInfo = arg2 > 0 ? arg3 : "";
                });
            };
            GlobalEventHelper.OnRequestGoToServerEditPage += new GlobalEventHelper.OnRequestGoToServerEditPageDelegate((id, isDuplicate, isInAnimationShow) =>
            {
                if (id <= 0) return;
                Debug.Assert(Context.AppData.VmItemList.Any(x => x.Server.Id == id));
                var server = Context.AppData.VmItemList.First(x => x.Server.Id == id).Server;
                Window.Dispatcher.Invoke(() =>
                {
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

            _serverListPage = new ServerListPage(Context, configurationVm);
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