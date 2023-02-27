using CredentialManagement;
using Shawn.Utils;
using System.Collections.ObjectModel;

namespace _1RM.Model.Protocol.Base
{
    public abstract class ProtocolBaseWithAddressPortUserPwd : ProtocolBaseWithAddressPort
    {
        protected ProtocolBaseWithAddressPortUserPwd(string protocol, string classVersion, string protocolDisplayName) : base(protocol, classVersion, protocolDisplayName)
        {
        }

        #region Conn

        private string _userName = "Administrator";
        [OtherName(Name = "USERNAME")]
        public string UserName
        {
            get => _userName;
            set => SetAndNotifyIfChanged(ref _userName, value);
        }

        private string _password = "";
        [OtherName(Name = "PASSWORD")]
        public string Password
        {
            get => _password;
            set => SetAndNotifyIfChanged(ref _password, value);
        }

        protected override string GetSubTitle()
        {
            return string.IsNullOrEmpty(UserName) ? base.GetSubTitle() : $"{Address}:{Port} ({UserName})";
        }



        public override Credential GetCredential()
        {
            var c = new Credential()
            {
                Address = Address,
                Port = Port,
                Password = Password,
                UserName = UserName,
            };
            return c;
        }

        public override void SetCredential(in Credential credential)
        {
            base.SetCredential(credential);

            if (!string.IsNullOrEmpty(credential.UserName))
            {
                UserName = credential.UserName;
            }

            if (!string.IsNullOrEmpty(credential.Password))
            {
                Password = credential.Password;
            }
        }

        #endregion
    }
}
