namespace _1RM.Utils.WindowsSdk.PasswordVaultManager
{
    /// <summary>
    /// Provide methods to get or set user's password.<para/>
    /// We should not store the user's password directly, but we can use platform-specified method to store them.
    /// So there must be a password manager interface so that different platform can have it's own security solution.
    /// </summary>
    public interface IPasswordManager
    {
        /// <summary>
        /// Retrieve a user's password by a key. The key is commonly the users account id of mail address.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string? Retrieve(string key);

        /// <summary>
        /// Add to store a new password in a secure method. The key is commonly the users account id of mail address.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="password"></param>
        public void Add(string key, string password);
    }
}