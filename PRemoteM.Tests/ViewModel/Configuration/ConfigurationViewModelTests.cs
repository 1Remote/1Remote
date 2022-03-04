using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PRemoteM.Tests.Service;
using PRM.Model;
using PRM.Model.DAO;
using PRM.Model.Protocol;
using PRM.Resources.Icons;
using PRM.Service;
using PRM.View;
using PRM.View.Settings;
using Shawn.Utils;

namespace PRemoteM.Tests.ViewModel.Configuration
{
    [TestClass()]
    public class ConfigurationViewModelTests
    {
        private DataService _dataService = null;
        private RDP _rdp = null;
        private SSH _ssh = null;
        private VNC _vnc = null;
        private LocalApp _app = null;
        private string _dbPath;
        private string _ppkPath;

        [TestMethod()]
        public void ConfigurationViewModelTest()
        {
            UriParser.Register(new GenericUriParser(GenericUriParserOptions.GenericAuthority), "pack", -1);

            Init();
            _dataService.Database_OpenConnection(DatabaseType.Sqlite, DbExtensions.GetSqliteConnectionString(_dbPath));
            Assert.IsTrue(_dataService.Database_SelfCheck() == EnumDbStatus.OK);
            _dataService.Database_InsertServer(_rdp);
            _dataService.Database_InsertServer(_ssh);
            _dataService.Database_InsertServer(_vnc);
            _dataService.Database_InsertServer(_app);
            _dataService.Database_CloseConnection();

            var ctx = new PrmContext(true, null);
            if(File.Exists(ctx.ConfigurationService.JsonPath))
                File.Delete(ctx.ConfigurationService.JsonPath);
            ctx.ConfigurationService.Database.SqliteDatabasePath = _dbPath;
            ctx.InitSqliteDb();
            SettingsPageViewModel.Init(ctx);
            var vm = SettingsPageViewModel.GetInstance();
            vm.GenRsa(_ppkPath)?.Wait();
            vm.CleanRsa().Wait();
        }


        public void MockData()
        {
            var r = new Random(DateTime.Now.Millisecond);
            _rdp = new RDP()
            {
                CommandAfterDisconnected = "rdp",
                CommandBeforeConnected = "zzzz",
                DisplayName = "RDP test",
                UserName = "username",
                Password = "password",
                Address = "123.123.123.123",
                IconBase64 = ServerIcons.Instance.Icons[r.Next(0, ServerIcons.Instance.Icons.Count)].ToBase64(),
                Tags = new List<string>() { "t1", "t2", "rdp" },
            };
            _ssh = new SSH()
            {
                CommandAfterDisconnected = "zxcvdafg",
                CommandBeforeConnected = "fhjfgj",
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
                CommandAfterDisconnected = "asdasd123",
                CommandBeforeConnected = "xczcasdas",
                DisplayName = "VNC test",
                UserName = "username",
                Password = "password",
                IconBase64 = ServerIcons.Instance.Icons[r.Next(0, ServerIcons.Instance.Icons.Count)].ToBase64(),
                Tags = new List<string>() { "t1", "t2", "vnc" },
            };
            _app = new LocalApp()
            {
                Arguments = "123",
                CommandAfterDisconnected = "cxxxx",
                CommandBeforeConnected = "zzzz",
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
                if (_dataService != null) return;
                if (Directory.Exists(nameof(ConfigurationViewModelTests)))
                {
                    Directory.Delete(nameof(ConfigurationViewModelTests), true);
                }

                Directory.CreateDirectory(nameof(ConfigurationViewModelTests));
                _dbPath = nameof(ConfigurationViewModelTests) + "/test.db";
                _ppkPath = new FileInfo(nameof(ConfigurationViewModelTests) + "/test.ppk").FullName;
                if (File.Exists(_dbPath)) File.Delete(_dbPath);
                if (File.Exists(_ppkPath)) File.Delete(_ppkPath);
                _dataService = new DataService();
                _dataService.Database_OpenConnection(DatabaseType.Sqlite, DbExtensions.GetSqliteConnectionString(_dbPath));
                MockData();
                _dataService.Database_CloseConnection();
            }
        }
    }
}