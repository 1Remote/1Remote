using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.WindowsAPICodePack.Shell.PropertySystem;

namespace _1RM.Utils.WindowsSdk.PasswordVaultManager
{
    internal class PasswordVaultManagerFileSystem : IPasswordManager
    {
        private readonly string _localFolder;
        public PasswordVaultManagerFileSystem(string localFolder)
        {
            _localFolder = localFolder;
        }
        //private static readonly string LocalFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".1Remote");

        public string? Retrieve(string key)
        {
            try
            {
                var passwordFile = Path.Combine(_localFolder, key, "token");
                if (File.Exists(passwordFile))
                {
                    var encrypted = File.ReadAllText(passwordFile);
                    var password = DataProtectionForLocal.Unprotect(encrypted).Result;
                    return password;
                }
            }
            catch (Exception)
            {
            }
            return null;
        }

        public void Add(string key, string password)
        {
            var passwordFile = Path.Combine(_localFolder, key);
            if (!Directory.Exists(passwordFile))
            {
                Directory.CreateDirectory(passwordFile);
            }
            passwordFile = Path.Combine(passwordFile, "token");
            Task.Factory.StartNew(async () =>
            {
                var encrypted = await DataProtectionForLocal.Protect(password);
                if (encrypted != null)
                    File.WriteAllText(passwordFile, encrypted);
            });

            var retrieved = Retrieve(key);
        }

        public void Remove(string key)
        {
            var passwordFile = Path.Combine(_localFolder, key);
            if (Directory.Exists(passwordFile))
            {
                Directory.Delete(passwordFile, true);
            }
        }
    }
}