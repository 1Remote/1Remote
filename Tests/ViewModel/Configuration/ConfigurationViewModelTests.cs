using System;
using System.Collections.Generic;
using System.IO;
using _1RM;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using _1RM.Model;
using _1RM.Model.DAO;
using _1RM.Model.Protocol;
using _1RM.Resources.Icons;
using _1RM.Service;
using _1RM.Service.DataSource;
using _1RM.View.Settings;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf.Image;

namespace Tests.ViewModel.Configuration
{
    [TestClass()]
    public class ConfigurationViewModelTests
    {
        private DataService _dataService = null!;
        private _1RM.Service.Configuration _cfg = null!;
        private ConfigurationService _configurationService = null!;
        private RDP _rdp = null!;
        private SSH _ssh = null!;
        private VNC _vnc = null!;
        private LocalApp _app = null!;
        private string _ppkPath = "";

        [TestMethod()]
        public void ConfigurationViewModelTest()
        {
            UriParser.Register(new GenericUriParser(GenericUriParserOptions.GenericAuthority), "pack", -1);

            Init();
            _dataService.Database_OpenConnection(DatabaseType.Sqlite, DbExtensions.GetSqliteConnectionString(AppPathHelper.Instance.SqliteDbDefaultPath));
            Assert.IsTrue(_dataService.Database_SelfCheck() == EnumDbStatus.OK);
            _dataService.Database_InsertServer(_rdp);
            _dataService.Database_InsertServer(_ssh);
            _dataService.Database_InsertServer(_vnc);
            _dataService.Database_InsertServer(_app);
            _dataService.Database_CloseConnection();

            var gd = new GlobalData(_configurationService);
            var ctx = new DataSourceService(new ProtocolConfigurationService(), gd);



            if (File.Exists(AppPathHelper.Instance.ProfileJsonPath))
                File.Delete(AppPathHelper.Instance.ProfileJsonPath);
            _configurationService.DataSource.LocalDataSourceConfig = AppPathHelper.Instance.SqliteDbDefaultPath;
            ctx.InitSqliteDb(_configurationService.DataSource.LocalDataSourceConfig, new DataService());
            SettingsPageViewModel vm = new SettingsPageViewModel(ctx, gd);
            vm.GenRsa(_ppkPath);
            vm.CleanRsa().Wait();
        }


        public void MockData()
        {
            var r = new Random(DateTime.Now.Millisecond);
            _rdp = new RDP()
            {
                DisplayName = "RDP test",
                UserName = "username",
                Password = "password",
                Address = "123.123.123.123",
                IconBase64 = ServerIcons.Instance.Icons[r.Next(0, ServerIcons.Instance.Icons.Count)].ToBase64(),
                Tags = new List<string>() { "t1", "t2", "rdp" },
            };
            _ssh = new SSH()
            {
                DisplayName = "Ssh test",
                UserName = "username",
                Password = "password",
                Address = "123.123.123.123",
                PrivateKey = "PrivateKey",
                IconBase64 = ServerIcons.Instance.Icons[r.Next(0, ServerIcons.Instance.Icons.Count)].ToBase64(),
                Tags = new List<string>() { "t1", "t2", "ssh" },
            };
            _vnc = new VNC()
            {
                DisplayName = "VNC test",
                UserName = "username",
                Password = "password",
                IconBase64 = ServerIcons.Instance.Icons[r.Next(0, ServerIcons.Instance.Icons.Count)].ToBase64(),
                Tags = new List<string>() { "t1", "t2", "vnc" },
            };
            _app = new LocalApp()
            {
                Arguments = "123",
                DisplayName = "AppTest",
                ExePath = "xxxx.exe",
                IconBase64 = ServerIcons.Instance.Icons[r.Next(0, ServerIcons.Instance.Icons.Count)].ToBase64(),
                Tags = new List<string>() { "t1", "t2" },
            };
        }

        public void Init()
        {
            if (_dataService != null) return;
            lock (this)
            {
                TestInit.Init();
                if (_dataService != null) return;
                if (Directory.Exists(nameof(ConfigurationViewModelTests)))
                {
                    Directory.Delete(nameof(ConfigurationViewModelTests), true);
                }
                Directory.CreateDirectory(nameof(ConfigurationViewModelTests));
                _ppkPath = new FileInfo(nameof(ConfigurationViewModelTests) + "/test.ppk").FullName;
                if (File.Exists(AppPathHelper.Instance.SqliteDbDefaultPath)) File.Delete(AppPathHelper.Instance.SqliteDbDefaultPath);
                if (File.Exists(_ppkPath)) File.Delete(_ppkPath);
                _dataService = new DataService();
                _dataService.Database_OpenConnection(DatabaseType.Sqlite, DbExtensions.GetSqliteConnectionString(AppPathHelper.Instance.SqliteDbDefaultPath));
                MockData();
                _dataService.Database_CloseConnection();
                _cfg = new _1RM.Service.Configuration();
                _configurationService = new ConfigurationService(_cfg, new KeywordMatchService());
            }
        }
    }
}