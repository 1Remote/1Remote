using Windows.Security.Credentials;

namespace _1RM.Utils.WindowsSdk.PasswordVaultManager
{
    /// <summary>
    /// Ref: https://learn.microsoft.com/en-us/windows/uwp/security/credential-locker
    /// </summary>
    public class PasswordVaultManagerWindowsApi : IPasswordManager
    {
        private readonly string _resourceName;

        public PasswordVaultManagerWindowsApi(string resourceName)
        {
            _resourceName = resourceName;
        }

        public string? Retrieve(string key)
        {
            var vault = new PasswordVault();
            try
            {
                var credential = vault.Retrieve(_resourceName, key);
                return credential.Password;
            }
            catch (System.Exception)
            {
                return null;
            }
        }

        public void Add(string key, string password)
        {
            var vault = new PasswordVault();
            vault.Add(new PasswordCredential(_resourceName, key, password));
        }
    }
}
