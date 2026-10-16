using System;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Service;
using _1RM.Utils;
using Newtonsoft.Json;
using Xunit;
using static _1RM.Service.DataService;

namespace Tests.Service
{
    /// <summary>
    /// Regression tests for issue https://github.com/1Remote/1Remote/issues/1118
    ///
    /// The SSH private key path was wiped after save/restart:
    /// 1. EncryptToDatabaseLevel() always assigned EncryptOnce(Password), so an EMPTY
    ///    password became a 130+ char non-empty cipher string;
    /// 2. the Password setter treated any non-empty assignment as "user switched to
    ///    password auth" and cleared PrivateKey / UsePrivateKeyForConnect.
    /// => every save of a key-only SSH server erased the private key from the database.
    /// </summary>
    public class DataServiceEncryptionTests
    {
        public DataServiceEncryptionTests()
        {
            UnSafeStringEncipher.Init("unit-test-salt");
        }

        private static SSH NewKeyOnlySshServer()
        {
            var ssh = new SSH
            {
                Address = "192.168.0.101",
                Port = "22",
                UserName = "root",
            };
            // Setting PrivateKey switches the server to key auth (password stays empty);
            // UsePrivateKeyForConnect is set explicitly the same way the editor UI does
            // (CredentialViewModel.IsUsePrivateKey / the checkbox binding).
            ssh.PrivateKey = @"C:\keys\my-key.ppk";
            ssh.Password = "";
            ssh.UsePrivateKeyForConnect = true;
            return ssh;
        }

        // ============================================================
        // Main bug: private key must survive EncryptToDatabaseLevel
        // ============================================================

        [Fact]
        public void EncryptToDatabaseLevel_ShouldNotClearPrivateKey_WhenPasswordIsEmpty()
        {
            var server = NewKeyOnlySshServer();

            var tmp = (SSH)server.Clone();
            tmp.SetNotifyPropertyChangedEnabled(false);
            tmp.EncryptToDatabaseLevel();

            Assert.False(string.IsNullOrWhiteSpace(tmp.PrivateKey),
                "issue #1118: private key must survive EncryptToDatabaseLevel");
            Assert.True(tmp.UsePrivateKeyForConnect,
                "issue #1118: UsePrivateKeyForConnect must survive EncryptToDatabaseLevel");
        }

        [Fact]
        public void EncryptDecryptRoundTrip_ShouldKeepPrivateKey()
        {
            var server = NewKeyOnlySshServer();

            // simulate save-to-database
            var tmp = (SSH)server.Clone();
            tmp.SetNotifyPropertyChangedEnabled(false);
            tmp.EncryptToDatabaseLevel();
            var json = JsonConvert.SerializeObject(tmp);

            // simulate load-from-database
            var loaded = new SSH().CreateFromJsonString(json)!;
            loaded.DecryptToConnectLevel();

            var loadedSsh = (SSH)loaded;
            Assert.Equal(@"C:\keys\my-key.ppk", loadedSsh.PrivateKey);
            Assert.True(loadedSsh.UsePrivateKeyForConnect);
            Assert.Equal("", loadedSsh.Password);
        }

        // ============================================================
        // Data-layer assignments must not have UI side effects
        // (legacy 1.2.x databases may contain BOTH password and key)
        // ============================================================

        [Fact]
        public void JsonDeserialize_ShouldKeepBothPasswordAndPrivateKey_WhenBothExist()
        {
            // a pre-1.3 database could hold both fields (1.2 setters had no side effects)
            var json = "{\"$type\":\"_1RM.Model.Protocol.SSH, Ui\",\"Address\":\"1.2.3.4\",\"Port\":\"22\"," +
                       "\"UserName\":\"root\",\"Password\":\"plain-pwd\",\"PrivateKey\":\"C:\\\\keys\\\\k.ppk\"," +
                       "\"UsePrivateKeyForConnect\":true}";

            var loaded = (SSH)new SSH().CreateFromJsonString(json)!;

            Assert.Equal("plain-pwd", loaded.Password);
            Assert.Equal(@"C:\keys\k.ppk", loaded.PrivateKey);
        }

        // ============================================================
        // Regression guards: encryption itself must keep working
        // ============================================================

        [Fact]
        public void EncryptToDatabaseLevel_ShouldStillEncryptNonEmptyPassword()
        {
            var ssh = new SSH { Address = "1.2.3.4", Port = "22", UserName = "root", Password = "secret-pwd" };

            var tmp = (SSH)ssh.Clone();
            tmp.SetNotifyPropertyChangedEnabled(false);
            tmp.EncryptToDatabaseLevel();

            Assert.NotEqual("secret-pwd", tmp.Password);
            Assert.True(string.IsNullOrWhiteSpace(tmp.PrivateKey));
            tmp.DecryptToConnectLevel();
            Assert.Equal("secret-pwd", tmp.Password);
        }

        [Fact]
        public void EncryptToDatabaseLevel_ShouldStillEncryptNonEmptyPrivateKey()
        {
            var server = NewKeyOnlySshServer();

            var tmp = (SSH)server.Clone();
            tmp.SetNotifyPropertyChangedEnabled(false);
            tmp.EncryptToDatabaseLevel();

            Assert.NotEqual(@"C:\keys\my-key.ppk", tmp.PrivateKey);
            tmp.DecryptToConnectLevel();
            Assert.Equal(@"C:\keys\my-key.ppk", tmp.PrivateKey);
        }
    }
}
