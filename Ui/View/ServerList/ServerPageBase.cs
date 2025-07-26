using _1RM.Model;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.Model;
using _1RM.Utils;
using _1RM.View.Settings.Launcher;
using Shawn.Utils.Wpf;
using Stylet;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace _1RM.View.ServerList
{
    public abstract partial class ServerPageBase : NotifyPropertyChangedBaseScreen
    {
        protected ServerPageBase(DataSourceService sourceService, GlobalData appData)
        {
            SourceService = sourceService;
            AppData = appData;
            TagsPanelViewModel = IoC.Get<TagsPanelViewModel>();

            AppData.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(GlobalData.TagList))
                {
                    OnGlobalDataTagListChanged();
                }
            };
            OnGlobalDataTagListChanged();
        }

        public DataSourceService SourceService { get; }
        public GlobalData AppData { get; }
        public LauncherSettingViewModel LauncherSettingViewModel => IoC.Get<LauncherSettingViewModel>();


    }
}
