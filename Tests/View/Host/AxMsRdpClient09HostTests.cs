using System.Reflection;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.View.Host.ProtocolHosts;
using Xunit;

namespace Tests.View.Host
{
    /// <summary>
    /// Cancelling the reconnect screen of the RDP control used to start a second retry loop
    /// of 1Remote's own. After such a cancel the loop is skipped, the only thing left to try
    /// is another address of the server.
    /// </summary>
    public class AxMsRdpClient09HostTests
    {
        private const int ReasonLocalNotError = 1;
        private const int ReasonSocketConnectFailed = 516;

        private static bool HasSwitchableAddress(RDP rdp)
        {
            var method = GetPrivateStaticMethod("HasSwitchableAddress");
            return (bool)method.Invoke(null, new object[] { rdp })!;
        }

        private static string DecideAfterDisconnect(int discReason, bool controlIsReconnecting, bool hasEverConnected, bool canSwitchAddress)
        {
            var method = GetPrivateStaticMethod("DecideAfterDisconnect");
            var decision = method.Invoke(null, new object[] { discReason, controlIsReconnecting, hasEverConnected, canSwitchAddress });
            return decision!.ToString()!;
        }

        private static MethodInfo GetPrivateStaticMethod(string name)
        {
            var method = typeof(AxMsRdpClient09Host).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.True(method != null, $"AxMsRdpClient09Host.{name} was not found");
            return method!;
        }

        private static RDP NewServer()
        {
            return new RDP { Id = "server", Address = "10.0.0.1", Port = "3389", UserName = "user", Password = "pwd" };
        }

        private static RDP NewServerWithAlternateAddress()
        {
            var rdp = NewServer();
            rdp.AlternateCredentials.Add(new Credential { Address = "10.0.0.2", Port = "3389" });
            return rdp;
        }

        [Fact]
        public void HasSwitchableAddress_IsFalse_WhenNoAlternateCredentialIsConfigured()
        {
            Assert.False(HasSwitchableAddress(NewServer()));
        }

        [Fact]
        public void HasSwitchableAddress_IsTrue_WhenAnAlternateCredentialHasAnAddress()
        {
            Assert.True(HasSwitchableAddress(NewServerWithAlternateAddress()));
        }

        [Fact]
        public void HasSwitchableAddress_IsTrue_WhenAnAlternateCredentialHasOnlyAPort()
        {
            var rdp = NewServer();
            rdp.AlternateCredentials.Add(new Credential { Port = "3390" });
            Assert.True(HasSwitchableAddress(rdp));
        }

        [Fact]
        public void HasSwitchableAddress_IsFalse_WhenTheAlternateCredentialHasNeitherAddressNorPort()
        {
            var rdp = NewServer();
            rdp.AlternateCredentials.Add(new Credential { Name = "other user only" });
            Assert.False(HasSwitchableAddress(rdp));
        }

        [Fact]
        public void HasSwitchableAddress_IsFalse_WhenAutoSwitchingIsOff()
        {
            var rdp = NewServerWithAlternateAddress();
            rdp.IsAutoAlternateAddressSwitching = false;
            Assert.False(HasSwitchableAddress(rdp));
        }

        [Fact]
        public void HasSwitchableAddress_IsFalse_WhenAutoSwitchingIsUnset()
        {
            var rdp = NewServerWithAlternateAddress();
            rdp.IsAutoAlternateAddressSwitching = null;
            Assert.False(HasSwitchableAddress(rdp));
        }

        [Fact]
        public void DecideAfterDisconnect_ClosesTheSession_WhenTheUserCancelledAndNoAddressCanSwitch()
        {
            var decision = DecideAfterDisconnect(ReasonLocalNotError, true, true, false);
            Assert.Equal("Close", decision);
        }

        [Fact]
        public void DecideAfterDisconnect_ProbesThenCloses_WhenTheUserCancelledAndAnAddressCanSwitch()
        {
            var decision = DecideAfterDisconnect(ReasonLocalNotError, true, true, true);
            Assert.Equal("ProbeThenClose", decision);
        }

        [Fact]
        public void DecideAfterDisconnect_Retries_WhenTheControlWasNotReconnecting()
        {
            var decision = DecideAfterDisconnect(ReasonLocalNotError, false, true, false);
            Assert.Equal("Retry", decision);
        }

        [Fact]
        public void DecideAfterDisconnect_Retries_ForOtherReasons()
        {
            var decision = DecideAfterDisconnect(ReasonSocketConnectFailed, true, true, false);
            Assert.Equal("Retry", decision);
        }

        [Fact]
        public void DecideAfterDisconnect_Retries_WhenTheSessionNeverConnected()
        {
            var decision = DecideAfterDisconnect(ReasonLocalNotError, true, false, false);
            Assert.Equal("Retry", decision);
        }

        [Fact]
        public void DecideAfterDisconnect_Retries_WhenTheSessionNeverConnectedEvenIfAnAddressCanSwitch()
        {
            var decision = DecideAfterDisconnect(ReasonLocalNotError, true, false, true);
            Assert.Equal("Retry", decision);
        }

        [Fact]
        public void DecideAfterDisconnect_Retries_ForOtherReasonsEvenIfAnAddressCanSwitch()
        {
            var decision = DecideAfterDisconnect(ReasonSocketConnectFailed, true, true, true);
            Assert.Equal("Retry", decision);
        }
    }
}
