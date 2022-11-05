using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using _1RM.Model;
using _1RM.Model.DAO;
using _1RM.Model.DAO.Dapper;
using _1RM.Model.Protocol.Base;
using _1RM.Service.DataSource;
using _1RM.Utils;
using _1RM.View;
using com.github.xiangyuecn.rsacsharp;
using Dapper;
using Shawn.Utils;
using Shawn.Utils.Interface;

namespace _1RM.Service
{
    internal class PrmTransferHelper
    {
        internal class PrmServer : IDataBaseServer
        {
            public int Id { get; set; }
            public string Protocol { get; set; } = "";
            public string ClassVersion { get; set; } = "";
            public string JsonConfigString { get; set; } = "";

            public ProtocolBase? ToProtocolServerBase()
            {
                return ItemCreateHelper.CreateFromDbOrm(this);
            }

            public string GetId()
            {
                return Id.ToString();
            }

            public string GetProtocol()
            {
                return Protocol;
            }

            public string GetClassVersion()
            {
                return ClassVersion;
            }

            public string GetJson()
            {
                return JsonConfigString;
            }
        }

        public static void Run()
        {
            GlobalEventHelper.ShowProcessingRing?.Invoke(Visibility.Visible, IoC.Get<ILanguageService>().Translate("system_options_data_security_info_data_processing"));
            Task.Factory.StartNew(StartTask);
            //StartTask();
        }

        private static void StartTask()
        {
            var dataBase = new DapperDataBaseFree();
            try
            {
                // TODO what if transfer from PRM (store)?
                var dbPath = Path.Join(AppPathHelper.Instance.BaseDirPath, "PRemoteM.db");
                if (File.Exists(dbPath) == false)
                {
                    dbPath = Path.Join(AppPathHelper.Instance.BaseDirPath, "PRemoteM_Debug.db");
                    if (File.Exists(dbPath) == false)
                        return;
                }

                dataBase.OpenNewConnection(DatabaseType.Sqlite, DbExtensions.GetSqliteConnectionString(dbPath));

                // check connection
                if (dataBase.IsConnected() != true)
                    return;


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

                var localSource = IoC.Get<DataSourceService>().LocalDataSource;
                Debug.Assert(localSource != null);
                // read from PRemoteM db
                var dbServers = dataBase.Connection?.Query<PrmServer>($"SELECT * FROM `Server`").Select(x => x?.ToProtocolServerBase()).Where(x => x != null).ToList();
                if (dbServers?.Count > 0)
                {
                    var servers = new List<ProtocolBase>();
                    foreach (var server in dbServers)
                    {
                        if (server is { } p)
                        {
                            rsa.DecryptToConnectLevel(ref p);
                            servers.Add(p);
                        }
                    }
                    localSource.Database_InsertServer(servers);
                }
            }
            catch (Exception e)
            {
                SimpleLogHelper.Fatal(e);
            }
            finally
            {
                dataBase.CloseConnection();
                IoC.Get<GlobalData>().ReloadServerList(true);
                GlobalEventHelper.ShowProcessingRing?.Invoke(Visibility.Collapsed, "");
            }
        }
    }
}
