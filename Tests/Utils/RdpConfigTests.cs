using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using _1RM.Utils.RdpFile;

namespace Tests.Utils
{
    [TestClass()]
    public class RdpConfigTests
    {
        private const string PasswordLinePattern = @"^password 51:b:[0-9A-F]+$";

        private static string? GetPasswordLine(string rdpContent)
        {
            return rdpContent.Split('\r', '\n')
                .Select(l => l.Trim())
                .FirstOrDefault(l => l.StartsWith("password 51:b:"));
        }

        [TestMethod()]
        public void EmptyPasswordIsWrittenAsEncryptedField()
        {
            var cfg = new RdpConfig("test", "127.0.0.1:3389", "user", "");
            var line = GetPasswordLine(cfg.ToString());
            Assert.IsNotNull(line, "empty password must produce a 'password 51:b:' line so mstsc can auto-logon");
            Assert.IsTrue(Regex.IsMatch(line!, PasswordLinePattern), "the DPAPI ciphertext of an empty string must be non-empty upper-case hex");
        }

        [TestMethod()]
        public void NullPasswordIsTreatedAsEmpty()
        {
            var cfg = new RdpConfig("test", "127.0.0.1:3389", "user", null!);
            var line = GetPasswordLine(cfg.ToString());
            Assert.IsNotNull(line, "null password must be treated as empty and still produce the field");
            Assert.IsTrue(Regex.IsMatch(line!, PasswordLinePattern));
        }

        [TestMethod()]
        public void NonEmptyPasswordIsStillWritten()
        {
            var cfg = new RdpConfig("test", "127.0.0.1:3389", "user", "secret");
            var line = GetPasswordLine(cfg.ToString());
            Assert.IsNotNull(line);
            Assert.IsTrue(Regex.IsMatch(line!, PasswordLinePattern));
        }
    }
}
