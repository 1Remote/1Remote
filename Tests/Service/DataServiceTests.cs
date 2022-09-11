using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using com.github.xiangyuecn.rsacsharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PRM.Model.DAO;
using PRM.Model.Protocol;
using PRM.Model.Protocol.Base;
using PRM.Resources.Icons;
using PRM.Service;
using Shawn.Utils.Wpf.Image;

namespace Tests.Service
{
    [TestClass()]
    public class DataServiceTests
    {
        private DataService _dataService = null;
        private RDP _rdp = null;
        private SSH _ssh = null;
        private VNC _vnc = null;
        private LocalApp _app = null;
        private string _dbPath;
        private string _ppkPath;

        [TestMethod()]
        public void DataServiceTest()
        {
            Init();
            _dataService.Database_OpenConnection(DatabaseType.Sqlite, DbExtensions.GetSqliteConnectionString(_dbPath));
            Assert.IsTrue(_dataService.Database_SelfCheck() == EnumDbStatus.OK);


            _dataService.Database_InsertServer(_rdp);
            _dataService.Database_InsertServer(_ssh);
            _dataService.Database_InsertServer(_vnc);
            _dataService.Database_InsertServer(_app);


            // Database_GetServers
            {
                var data = _dataService.Database_GetServers();
                Assert.IsTrue(data.Count == 4);
                var first = data.First(x => x is RDP) as RDP;
                Assert.IsTrue(first != null);
                Assert.IsTrue(_rdp.DisplayName == first.DisplayName);
                Assert.IsTrue(_rdp.UserName == first.UserName);
                Assert.IsTrue(_rdp.Password == first.Password);
                Assert.IsTrue(_rdp.Address == first.Address);
                Assert.IsTrue(_rdp.CommandAfterDisconnected == first.CommandAfterDisconnected);
            }


            // Database_EncryptionKey
            {
                // gen rsa
                var rsa = new RSA(2048);
                // save key file
                var privateKeyContent = rsa.ToPEM_PKCS1();
                File.WriteAllText(_ppkPath, privateKeyContent);

                _dataService.Database_SetEncryptionKey(_ppkPath, privateKeyContent, new List<ProtocolBase>());
                var path = _dataService.Database_GetPrivateKeyPath();
                var pk = _dataService.Database_GetPublicKey();
                Assert.IsTrue(_ppkPath == path);
                Assert.IsTrue(RSA.CheckPrivatePublicKeyMatch(_ppkPath, pk) == RSA.EnumRsaStatus.NoError);
                Assert.IsTrue(string.IsNullOrEmpty(_dataService.Database_GetPrivateKeyPath()) == false);
                Assert.IsTrue(_dataService.Database_SelfCheck() == EnumDbStatus.OK);

                // data encrypt
                Assert.IsTrue("test" != _dataService.Encrypt("test"));
                Assert.IsTrue("test" == _dataService.DecryptOrReturnOriginalString(_dataService.Encrypt("test")));
                var data = _dataService.Database_GetServers();
                Assert.IsTrue(data.Count == 4);
                var first = data.First(x => x is SSH);
                Assert.IsTrue(first != null);
                {
                    _dataService.EncryptToDatabaseLevel(ref first);
                    if (first is SSH r)
                    {
                        Assert.IsTrue(_ssh.DisplayName == r.DisplayName);
                        Assert.IsTrue(_ssh.UserName != r.UserName);
                        Assert.IsTrue(_ssh.Password != r.Password);
                        Assert.IsTrue(_ssh.Address != r.Address);
                        Assert.IsTrue(_ssh.PrivateKey != r.PrivateKey);
                        Assert.IsTrue(_ssh.CommandAfterDisconnected == r.CommandAfterDisconnected);
                        Assert.IsTrue(_ssh.PrivateKey == _dataService.DecryptOrReturnOriginalString(r.PrivateKey));
                        Assert.IsTrue(_ssh.Password == _dataService.DecryptOrReturnOriginalString(r.Password));
                    }
                }
                {
                    _dataService.DecryptToRamLevel(ref first);
                    if (first is SSH r)
                    {
                        Assert.IsTrue(_ssh.DisplayName == r.DisplayName);
                        Assert.IsTrue(_ssh.UserName == r.UserName);
                        Assert.IsTrue(_ssh.Password != r.Password);
                        Assert.IsTrue(_ssh.Address == r.Address);
                        Assert.IsTrue(_ssh.PrivateKey != r.PrivateKey);
                        Assert.IsTrue(_ssh.CommandAfterDisconnected == r.CommandAfterDisconnected);
                    }
                }
                {
                    _dataService.DecryptToConnectLevel(ref first);
                    if (first is SSH r)
                    {
                        Assert.IsTrue(_ssh.DisplayName == r.DisplayName);
                        Assert.IsTrue(_ssh.UserName == r.UserName);
                        Assert.IsTrue(_ssh.Password == r.Password);
                        Assert.IsTrue(_ssh.Address == r.Address);
                        Assert.IsTrue(_ssh.PrivateKey == r.PrivateKey);
                        Assert.IsTrue(_ssh.CommandAfterDisconnected == r.CommandAfterDisconnected);
                    }
                }


                _dataService.Database_SetEncryptionKey("", "", new List<ProtocolBase>());
                path = _dataService.Database_GetPrivateKeyPath();
                pk = _dataService.Database_GetPublicKey();
                Assert.IsTrue("" == path);
                Assert.IsTrue("" == pk);
                Assert.IsTrue(string.IsNullOrEmpty(_dataService.Database_GetPrivateKeyPath()) == true);
                Assert.IsTrue(_dataService.Database_SelfCheck() == EnumDbStatus.OK);
            }


            // Database_UpdateDelete
            {
                Init();

                var data = _dataService.Database_GetServers();
                Assert.IsTrue(data.Count == 4);
                var first = data.First(x => x is RDP) as RDP;
                Assert.IsTrue(first != null);
                _rdp.Id = first.Id;
                _rdp.DisplayName = "1";
                _rdp.Address = "2";
                _rdp.Password = "3";
                _rdp.UserName = "4";
                _dataService.Database_UpdateServer(_rdp);
                data = _dataService.Database_GetServers();
                Assert.IsTrue(data.Count == 4);
                first = data.First(x => x is RDP) as RDP;
                Assert.IsTrue(first != null);
                Assert.IsTrue(_rdp.DisplayName == first.DisplayName);
                Assert.IsTrue(_rdp.UserName == first.UserName);
                Assert.IsTrue(_rdp.Password == first.Password);
                Assert.IsTrue(_rdp.Address == first.Address);
                Assert.IsTrue(_rdp.CommandAfterDisconnected == first.CommandAfterDisconnected);
            }


            {
                Init();

                var data = _dataService.Database_GetServers();
                Assert.IsTrue(data.Count == 4);
                var first = data.First(x => x is RDP) as RDP;
                Assert.IsTrue(first != null);
                _rdp.Id = first.Id;
                _rdp.DisplayName = "2";
                _rdp.Address = "3";
                _rdp.Password = "4";
                _rdp.UserName = "5";
                var second = data.First(x => x is SSH) as SSH;
                _ssh.Id = second.Id;
                _ssh.DisplayName = "SS2";
                _ssh.Address = "XXXXX";
                Assert.IsTrue(_dataService.Database_UpdateServer(new List<ProtocolBase>(){ _rdp, _ssh }));
                data = _dataService.Database_GetServers();
                Assert.IsTrue(data.Count == 4);
                first = data.First(x => x is RDP) as RDP;
                Assert.IsTrue(first != null);
                Assert.IsTrue(_rdp.DisplayName == first.DisplayName);
                Assert.IsTrue(_rdp.UserName == first.UserName);
                Assert.IsTrue(_rdp.Password == first.Password);
                Assert.IsTrue(_rdp.Address == first.Address);
                Assert.IsTrue(_rdp.CommandAfterDisconnected == first.CommandAfterDisconnected);
                second = data.First(x => x is SSH) as SSH;
                Assert.IsTrue(second != null);
                Assert.IsTrue(_ssh.DisplayName == second.DisplayName);
                Assert.IsTrue(_ssh.Address == second.Address);

                _dataService.Database_DeleteServer(_rdp.Id);
                data = _dataService.Database_GetServers();
                Assert.IsTrue(data.Count == 3);
                Assert.IsTrue(data.All(x => x.Id != _rdp.Id));


                _dataService.Database_DeleteServer(new List<int>(){_rdp.Id, _ssh.Id});
                data = _dataService.Database_GetServers();
                Assert.IsTrue(data.Count == 2);
                Assert.IsTrue(data.All(x => x.Id != _ssh.Id));
            }

            {
                _dataService.Database_InsertServer(new List<ProtocolBase>() { _rdp, _ssh });
                var data = _dataService.Database_GetServers();
                Assert.IsTrue(data.Count == 4);
            }

            _dataService.Database_CloseConnection();

            if (Directory.Exists(nameof(DataServiceTests)))
            {
                Directory.Delete(nameof(DataServiceTests), true);
            }
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
                if (_dataService != null) return;
                if (Directory.Exists(nameof(DataServiceTests)))
                {
                    Directory.Delete(nameof(DataServiceTests), true);
                }

                Directory.CreateDirectory(nameof(DataServiceTests));
                _dbPath = nameof(DataServiceTests) + "/test.db";
                _ppkPath = new FileInfo(nameof(DataServiceTests) + "/test.ppk").FullName;
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