using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Service.DataSource.DAO;
using _1RM.Service.DataSource.DAO.Dapper;
using _1RM.Service;
using _1RM.Utils;
using Dapper;
using Newtonsoft.Json;
using Shawn.Utils;
using Xunit;

namespace Tests.Service
{
    /// <summary>
    /// End-to-end tests for the Dapper credential vault update and batch import paths, on a real temporary SQLite file.
    /// Covers: unreferenced-credential updates must commit; related servers must store the new state; batch imports must encrypt.
    /// </summary>
    public class DapperUpdateCredentialTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly DapperDatabase _db;

        public DapperUpdateCredentialTests()
        {
            UnSafeStringEncipher.Init("unit-test-salt");
            _dbPath = Path.Combine(Path.GetTempPath(), "1rm-test-" + Guid.NewGuid().ToString("N") + ".db");
            _db = new DapperDatabase("test-db", DatabaseType.Sqlite);
            var open = _db.OpenNewConnection(DbExtensions.GetSqliteConnectionString(_dbPath));
            Assert.True(open.IsSuccess, "failed to open sqlite db: " + open.ErrorInfo);
            var init = _db.InitTables();
            Assert.True(init.IsSuccess, "failed to init tables: " + init.ErrorInfo);
        }

        public void Dispose()
        {
            try { _db.CloseConnection(); } catch { /* ignore */ }
            try { File.Delete(_dbPath); } catch { /* ignore */ }
        }

        [Fact]
        public void UpdateCredential_WithNoRelatedServers_MustPersist()
        {
            // arrange: a credential not referenced by any server
            var credential = new Credential { Name = "c1", UserName = "root", Password = "pw1" };
            Assert.True(_db.AddCredential(ref credential).IsSuccess);

            // act: edit it (password change) with no related protocols - the C-1 scenario
            var edited = credential.CloneMe();
            edited.Password = UnSafeStringEncipher.EncryptOnce("pw2");
            edited.DatabaseId = credential.DatabaseId;
            var result = _db.UpdateCredential(edited, relatedProtocols: null);

            // assert: the UI saw success, so the database must contain the new value after re-read
            Assert.True(result.IsSuccess, "UpdateCredential reported failure: " + result.ErrorInfo);
            var reloaded = _db.GetCredentials();
            Assert.True(reloaded.IsSuccess);
            var fromDb = reloaded.Items.Single();
            Assert.Equal("c1", fromDb.Name);
            var decryptedPw = UnSafeStringEncipher.DecryptOrReturnOriginalString(fromDb.Password);
            Assert.True(decryptedPw == "pw2",
                $"the edit was rolled back on dispose because the transaction was never committed (actual password: '{decryptedPw}')");
        }
        /// <summary>Reads raw server rows straight from the sqlite file, bypassing ToProtocolServerBase (needs full IoC).</summary>
        private List<SSH> ReadServersRawFromDisk()
        {
            System.Data.SQLite.SQLiteConnection.ClearAllPools();
            using var con = new System.Data.SQLite.SQLiteConnection(DbExtensions.GetSqliteConnectionString(_dbPath));
            con.Open();
            var jsons = con.Query<string>(NormalizedSqlHolder.SelectServersJson).ToList();
            return jsons.Select(JsonConvert.DeserializeObject<SSH>).ToList();
        }

        private static class NormalizedSqlHolder
        {
            public const string SelectServersJson = "SELECT Json FROM Servers";
        }

        [Fact]
        public void UpdateCredential_WithRelatedServers_MustStoreNewSnapshotInServers()
        {
            // arrange: credential c1(pw1) + a server inheriting it with the pw1 snapshot
            var credential = new Credential { Name = "c1", UserName = "root", Password = "pw1" };
            Assert.True(_db.AddCredential(ref credential).IsSuccess);

            var server = new SSH { Address = "192.0.2.10", Port = "22", UserName = "root" };
            server.InheritedCredentialName = "c1";
            server.Password = UnSafeStringEncipher.EncryptOnce("pw1");
            ProtocolBase serverBase = server;
            Assert.True(_db.AddServer(ref serverBase).IsSuccess);

            // act: rename c1 -> c2 and change the password (the C-2 scenario)
            var edited = credential.CloneMe();
            edited.Name = "c2";
            edited.Password = UnSafeStringEncipher.EncryptOnce("pw2");
            edited.DatabaseId = credential.DatabaseId;
            var result = _db.UpdateCredential(edited, new List<ProtocolBaseWithAddressPortUserPwd> { server });

            // assert: after the UI reloads from the database, the server must reference the
            // NEW credential name and carry the NEW password snapshot (finding C-2: the old
            // code serialized the servers BEFORE applying the new state, so the reload
            // resurrected the old name/password and the vault link was lost)
            Assert.True(result.IsSuccess, "UpdateCredential reported failure: " + result.ErrorInfo);
            var fromDb = ReadServersRawFromDisk().Single();
            Assert.True(fromDb.InheritedCredentialName == "c2",
                $"server still references the old credential name '{fromDb.InheritedCredentialName}' after rename");
            var serverPw = UnSafeStringEncipher.DecryptOrReturnOriginalString(fromDb.Password);
            Assert.True(serverPw == "pw2",
                $"server still carries the old password snapshot ('{serverPw}') after the credential password changed");
        }
        [Fact]
        public void AddServer_Batch_MustStoreEncryptedSecrets()
        {
            // arrange: mimic DataSourceBase.Database_InsertServer(List): the caller passes
            // ALREADY-ENCRYPTED clones into the batch AddServer
            var server = new SSH { Address = "192.0.2.20", Port = "22", UserName = "root" };
            server.InheritedCredentialName = "c1";
            server.Password = "plain-secret-pw";
            ProtocolBase encryptedClone = (ProtocolBase)server.Clone();
            encryptedClone.SetNotifyPropertyChangedEnabled(false);
            encryptedClone.EncryptToDatabaseLevel();
            Assert.True(_db.AddServer(new[] { encryptedClone }).IsSuccess);

            // assert server row: password must round-trip AND must not be stored as plaintext
            // (finding C-3: the batch path called DecryptToConnectLevel for the fingerprint
            // math and then serialized the DECRYPTED objects straight into the database)
            var fromDb = ReadServersRawFromDisk().Single();
            var roundTrip = UnSafeStringEncipher.DecryptOrReturnOriginalString(fromDb.Password);
            Assert.True(roundTrip == "plain-secret-pw",
                $"stored password no longer round-trips to the original (got '{roundTrip}')");
            Assert.True(fromDb.Password != "plain-secret-pw",
                "the batch-import path stores the password as PLAINTEXT in the database");

            // assert credential row: the credential derived from the server must be encrypted too
            System.Data.SQLite.SQLiteConnection.ClearAllPools();
            using (var con = new System.Data.SQLite.SQLiteConnection(DbExtensions.GetSqliteConnectionString(_dbPath)))
            {
                con.Open();
                var credJson = con.Query<string>("SELECT Json FROM Credentials").Single();
                var credential = JsonConvert.DeserializeObject<Credential>(credJson);
                var credRoundTrip = UnSafeStringEncipher.DecryptOrReturnOriginalString(credential.Password);
                Assert.True(credRoundTrip == "plain-secret-pw",
                    $"stored credential password no longer round-trips (got '{credRoundTrip}')");
                Assert.True(credential.Password != "plain-secret-pw",
                    "the batch-import path stores the derived credential as PLAINTEXT in the database");
            }
        }
    }
}
