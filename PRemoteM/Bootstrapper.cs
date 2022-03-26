using System;
using System.IO;
using PRM.Model;
using PRM.Service;
using PRM.View;
using PRM.View.Settings;
using Stylet;
using StyletIoC;

namespace PRM
{
    public class Bootstrapper : Bootstrapper<MainWindowView>
    {
        protected override void ConfigureIoC(IStyletIoCBuilder builder)
        {
            //builder.Bind<IApplicationState>().ToInstance(new ApplicationState(this.Application));
            // Configure the IoC container in here
            builder.Bind<ConfigurationService>().ToSelf().InSingletonScope();
            builder.Bind<LocalityService>().ToSelf().InSingletonScope();
            builder.Bind<PrmContext>().ToInstance(App.Context);
            builder.Bind<SettingsPageViewModel>().ToInstance(App.SettingsPageVm);
            builder.Bind<ILanguageService>().And<LanguageService>().ToInstance(App.LanguageService);
            base.ConfigureIoC(builder);
        }

        protected override void Configure()
        {
            // This is called after Stylet has created the IoC container, so this.Container exists, but before the
            // Root ViewModel is launched.
            // Configure your services, etc, in here

            IoC.GetInstance = this.Container.Get;
            IoC.BuildUp = this.Container.BuildUp;
            IoC.Instances = this.Container;

            //Container.Get<ConfigurationService>().Init(true);
            //var appDateFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ConfigurationService.AppName);
            //Container.Get<LocalityService>().Init(Path.Combine(appDateFolder, "locality.json"));
            //Container.Get<LocalityService>().Init("locality.json");
        }


        protected override void OnStart()
        {
            // This is called just after the application is started, but before the IoC container is set up.
            // Set up things like logging, etc
            base.OnStart();
        }


        protected override void OnLaunch()
        {
            base.OnLaunch();
        }
    }
}
