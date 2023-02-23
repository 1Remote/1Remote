using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using _1RM.Model;
using _1RM.Model.DAO;
using _1RM.Model.DAO.Dapper;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Service;
using _1RM.Service.DataSource;
using _1RM.View;
using _1RM.View.Utils;
using com.github.xiangyuecn.rsacsharp;
using Dapper;
using Newtonsoft.Json;
using Shawn.Utils;

namespace _1RM.Utils.PRemoteM
{
    internal class PRemoteMTransferHelper
    {
        private static string _dbPath = "";
        private static List<string>? _dbPathList = null;
        private static List<ProtocolBase> _servers = new List<ProtocolBase>();
        public static bool IsReading = false;

        public static bool IsNeedTransfer()
        {
            _dbPathList ??= FindDatabase();
            return _dbPathList.Count > 0;
        }

        public static void ReadAsync()
        {
            IsReading = true;
            Task.Factory.StartNew(StartTask);
        }

        public static void TransAsync()
        {
            if (_servers?.Any() == true)
            {
                if (MessageBoxHelper.Confirm($"Do you want to transfer sessions from `{_dbPath}`?", "Data transfer", ownerViewModel: IoC.Get<MainWindowViewModel>()))
                {
                    var id = MaskLayerController.ShowProcessingRing(msg: "Data transfer in progress", assignLayerContainer: IoC.Get<MainWindowViewModel>());
                    Task.Factory.StartNew(() =>
                    {
                        try
                        {
                            var now = DateTime.Now;
                            while (IsReading)
                            {
                                Thread.Sleep(100);
                                if (DateTime.Now - now > TimeSpan.FromSeconds(60))
                                {
                                    return;
                                }
                            }

                            var localSource = IoC.Get<DataSourceService>().LocalDataSource;
                            Debug.Assert(localSource != null);
                            localSource.Database_InsertServer(_servers);
                            IoC.Get<GlobalData>().ReloadServerList(true);
                        }
                        catch (Exception e)
                        {
                            SimpleLogHelper.Error(e);
                        }
                        finally
                        {
                            MaskLayerController.HideMask(IoC.Get<MainWindowViewModel>());
                            Clear();
                        }
                    });
                }
            }
        }

        public static void Clear()
        {
            _dbPathList = null;
            _servers.Clear();
        }

        private static List<string> FindDatabase()
        {
            var dbPaths = new List<string>();
            var appNames = new string[] {
                "PRemoteM",
#if DEBUG
                "PRemoteM_Debug",
#endif
            };


            foreach (var appName in appNames)
            {
                var basePaths = new List<string>()
                {
                    Environment.CurrentDirectory,
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appName),
                };

                foreach (var basePath in basePaths)
                {
                    if (Directory.Exists(basePath))
                    {
                        try
                        {
                            string profileJsonPath = Path.Combine(basePath, appName + ".json");
                            if (File.Exists(profileJsonPath))
                            {
                                var tmp = JsonConvert.DeserializeObject<dynamic>(File.ReadAllText(profileJsonPath));
                                if (tmp != null)
                                {
                                    string dbPath = (tmp["Database"]["SqliteDatabasePath"]).ToString();
                                    if (File.Exists(dbPath) && dbPaths.Contains(dbPath) == false)
                                    {
                                        dbPaths.Add(dbPath);
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // ignored
                        }

                        {
                            var dbPath = Path.Combine(basePath, "PRemoteM.db");
                            if (File.Exists(dbPath) && dbPaths.Contains(dbPath) == false)
                            {
                                dbPaths.Add(dbPath);
                            }
                        }
                    }
                }
            }

            return dbPaths;
        }


        private static string DecryptOrReturnOriginalString(RSA? ras, string originalString)
        {
            return ras?.DecodeOrNull(originalString) ?? originalString;
        }

        private static void StartTask()
        {
            var dataBase = new DapperDatabaseFree();
            try
            {
                if (_dbPathList?.Count > 0 == false)
                {
                    return;
                }

                foreach (var dbPath in _dbPathList)
                {
                    _dbPath = dbPath;

                    dataBase.OpenNewConnection(DatabaseType.Sqlite, DbExtensions.GetSqliteConnectionString(dbPath));

                    // check connection
                    if (dataBase.IsConnected() != true)
                    {
                        return;
                    }


                    RSA? rsa = null;
                    // check database rsa encrypt
                    var privateKeyPath = dataBase.Connection?.QueryFirstOrDefault<Config>($"SELECT * FROM `Config` WHERE `Key` = @Key", new { Key = "RSA_PrivateKeyPath", })?.Value ?? "";


                    if (!string.IsNullOrWhiteSpace(privateKeyPath)
                        && File.Exists(privateKeyPath))
                    {
                        // validate encryption
                        var publicKey = dataBase.Connection?.QueryFirstOrDefault<Config>($"SELECT * FROM `Config` WHERE `Key` = @Key", new { Key = "RSA_PublicKey", })?.Value ?? "";
                        var pks = RSA.CheckPrivatePublicKeyMatch(privateKeyPath, publicKey);
                        if (pks != RSA.EnumRsaStatus.NoError)
                        {
                            return;
                        }

                        rsa = new RSA(File.ReadAllText(privateKeyPath), true);
                    }
                    else
                    {
                        rsa = null;
                    }

                    // read from PRemoteM db
                    var dbServers = dataBase.Connection?.Query<PRemoteMServer>($"SELECT * FROM `Server`").Select(x => x?.ToProtocolServerBase()).Where(x => x != null).ToList();
                    if (dbServers?.Count > 0)
                    {
                        _servers.Clear();
                        foreach (var server in dbServers)
                        {
                            if (server is { })
                            {
                                // DecryptToRamLevel
                                if(rsa != null)
                                {
                                    server.DisplayName = DecryptOrReturnOriginalString(rsa, server.DisplayName);
                                    if (server.GetType().IsSubclassOf(typeof(ProtocolBaseWithAddressPort)))
                                    {
                                        var p = (ProtocolBaseWithAddressPort)server;
                                        p.Address = DecryptOrReturnOriginalString(rsa, p.Address);
                                        p.Port = DecryptOrReturnOriginalString(rsa, p.Port);
                                    }

                                    if (server.GetType().IsSubclassOf(typeof(ProtocolBaseWithAddressPortUserPwd)))
                                    {
                                        var p = (ProtocolBaseWithAddressPortUserPwd)server;
                                        p.UserName = DecryptOrReturnOriginalString(rsa, p.UserName);
                                    }
                                }

                                // DecryptToConnectLevel
                                {
                                    if (server.GetType().IsSubclassOf(typeof(ProtocolBaseWithAddressPortUserPwd)))
                                    {
                                        var s = (ProtocolBaseWithAddressPortUserPwd)server;
                                        s.Password = DecryptOrReturnOriginalString(rsa, s.Password);
                                    }

                                    switch (server)
                                    {
                                        case SSH ssh when !string.IsNullOrWhiteSpace(ssh.PrivateKey):
                                            ssh.PrivateKey = DecryptOrReturnOriginalString(rsa, ssh.PrivateKey);
                                            break;

                                        case RDP rdp when !string.IsNullOrWhiteSpace(rdp.GatewayPassword):
                                            rdp.GatewayPassword = DecryptOrReturnOriginalString(rsa, rdp.GatewayPassword);
                                            break;
                                    }
                                }
                                _servers.Add(server);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                SimpleLogHelper.Fatal(e);
            }
            finally
            {
                dataBase.CloseConnection();
                IsReading = false;
            }
        }
    }
}
