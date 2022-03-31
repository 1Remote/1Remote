using System;
using System.Globalization;
using System.IO;
using System.Runtime.Remoting.Contexts;
using System.Windows;
using PRM.Model;
using PRM.Model.DAO;
using PRM.Model.DAO.Dapper;
using PRM.Service;
using PRM.View;
using PRM.View.Guidance;
using PRM.View.Settings;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf.FileSystem;
using Stylet;
using StyletIoC;

namespace PRM
{
    public class Bootstrapper : Bootstrapper<MainWindowViewModel>
    {
        protected override void ConfigureIoC(IStyletIoCBuilder builder)
        {
            // Configure the IoC container in here
            //builder.Bind<ConfigurationService>().ToSelf().InSingletonScope();
            builder.Bind<ResourceDictionary>().ToInstance(App.ResourceDictionary);
            builder.Bind<ILanguageService>().And<LanguageService>().To<LanguageService>().InSingletonScope();
            builder.Bind<LocalityService>().ToSelf().InSingletonScope();
            builder.Bind<ThemeService>().ToSelf().InSingletonScope();
            builder.Bind<KeywordMatchService>().ToSelf().InSingletonScope();
            builder.Bind<ConfigurationService>().ToSelf().InSingletonScope();
            builder.Bind<PrmContext>().ToSelf().InSingletonScope();

            builder.Bind<MainWindowView>().ToSelf().InSingletonScope();
            builder.Bind<MainWindowViewModel>().ToSelf().InSingletonScope();
            builder.Bind<LauncherWindowView>().ToSelf().InSingletonScope();
            builder.Bind<LauncherWindowViewModel>().ToSelf().InSingletonScope();
            builder.Bind<SettingsPageViewModel>().ToSelf().InSingletonScope();
            base.ConfigureIoC(builder);
        }

        protected override void Configure()
        {
            // This is called after Stylet has created the IoC container, so this.Container exists, but before the
            // Root ViewModel is launched.
            // Configure your services, etc, in here
            
            IoC.Init(this.Container);

            //Container.Get<ConfigurationService>().Init(true);
            //var appDateFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ConfigurationService.AppName);
            //Container.Get<LocalityService>().Init(Path.Combine(appDateFolder, "locality.json"));
            //Container.Get<LocalityService>().Init("locality.json");
        }

        private bool _canPortable = false;

        protected override void OnStart()
        {
            // This is called just after the application is started, but before the IoC container is set up.
            // Set up things like logging, etc

            // TODO 确定当前软件是 Portable 模式还是多用户模式 

            #region Check permissions
#if FOR_MICROSOFT_STORE_ONLY
            CanPortable = false;
#else
            _canPortable = true;
#endif

            // Check for permissions based on CanPortable
            var tmp = new ConfigurationService(null);
            tmp.Init(_canPortable);
            var dbDir = new FileInfo(tmp.Database.SqliteDatabasePath).Directory;
            {
                var languageService = new LanguageService(App.ResourceDictionary);
                languageService.SetLanguage(CultureInfo.CurrentCulture.Name.ToLower());
                if (IoPermissionHelper.HasWritePermissionOnDir(dbDir.FullName) == false)
                {
                    MessageBox.Show(languageService.Translate("write permissions alert", dbDir.FullName), languageService.Translate("messagebox_title_warning"), MessageBoxButton.OK);
                    Environment.Exit(1);
                }

                if (IoPermissionHelper.HasWritePermissionOnFile(tmp.JsonPath) == false)
                {
                    MessageBox.Show($"We don't have write permissions for the `{tmp.JsonPath}` file!\r\nPlease try:\r\n1. `run as administrator`\r\n2. change file permissions \r\n3. move PRemoteM to another folder.",
                        languageService.Translate("messagebox_title_warning"), MessageBoxButton.OK);
                    Environment.Exit(1);
                }
            }

            #endregion


            AppInit.InitLog(_canPortable);
            AppInit.OnlyOneAppInstanceCheck();
            base.OnStart();
        }

        protected override void OnLaunch()
        {
            IoC.Get<ConfigurationService>().Init(_canPortable);
            IoC.Get<LanguageService>().SetLanguage(IoC.Get<ConfigurationService>().General.CurrentLanguageCode);
            var context = IoC.Get<PrmContext>();
            context.Init(_canPortable);

            RemoteWindowPool.Init(context);

            // if cfg is not existed, then it would be a new user
            var isNewUser = !File.Exists(context.ConfigurationService.JsonPath);
            if (isNewUser)
            {
                var gw = new GuidanceWindow(context, IoC.Get<SettingsPageViewModel>());
                gw.ShowDialog();
            }

            // init Database here after ui init, to show alert if db connection goes wrong.
            var connStatus = context.InitSqliteDb();
            if (connStatus != EnumDbStatus.OK)
            {
                string error = connStatus.GetErrorInfo();
                MessageBox.Show(error, IoC.Get<LanguageService>().Translate("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                IoC.Get<MainWindowViewModel>().CmdGoSysOptionsPage.Execute("Data");
                IoC.Get<MainWindowView>().ActivateMe();
            }
            else
            {
                context.AppData.ReloadServerList();
            }


            if (IoC.Get<ConfigurationService>().General.AppStartMinimized == false
                || isNewUser)
            {
                IoC.Get<MainWindowView>().ActivateMe();
            }


            base.OnLaunch();
        }
    }
}
