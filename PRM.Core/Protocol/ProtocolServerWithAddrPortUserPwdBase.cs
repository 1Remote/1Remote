using PRM.Core.Model;

namespace PRM.Core.Protocol
{
    public abstract class ProtocolServerWithAddrPortUserPwdBase : ProtocolServerWithAddrPortBase
    {
        protected ProtocolServerWithAddrPortUserPwdBase(string protocol, string classVersion, string protocolDisplayName, string protocolDisplayNameInShort = "") : base(protocol, classVersion, protocolDisplayName, protocolDisplayNameInShort)
        {
        }

        #region Conn

        private string _userName = "Administrator";
        public string UserName
        {
            get => _userName;
            set => SetAndNotifyIfChanged(nameof(UserName), ref _userName, value);
        }

        private string _password = "";
        public string Password
        {
            get => _password;
            set => SetAndNotifyIfChanged(nameof(Password), ref _password, value);
        }

        //public string GetDecryptedPassWord()
        //{
        //    if (SystemConfig.Instance.DataSecurity.Rsa != null)
        //    {
        //        return SystemConfig.Instance.DataSecurity.Rsa.DecodeOrNull(_password) ?? "";
        //    }
        //    return _password;
        //}

        protected override string GetSubTitle()
        {
            return $"@{Address}:{Port} ({UserName})";
        }

        #endregion
    }
}
