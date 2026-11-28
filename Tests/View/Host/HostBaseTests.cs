using System;
using _1RM;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.View.Host.ProtocolHosts;
using _1RM.View.Settings;
using Xunit;

namespace Tests.View.Host
{
    /// <summary>
    /// The connection id of a session is the key of the session table and of the tab close button,
    /// but it used to be computed again on every read from the address and the credential.
    /// A reconnect that switches the address changed it, so the session could no longer be found
    /// and closed by that id.
    /// </summary>
    public class HostBaseTests
    {
        private sealed class TestHost : HostBase
        {
            public TestHost(ProtocolBase protocol) : base(protocol)
            {
            }

            public override void Conn()
            {
            }

            public override void ReConn()
            {
            }

            public override ProtocolHostType GetProtocolHostType()
            {
                return ProtocolHostType.Integrate;
            }

            public override IntPtr GetHostHwnd()
            {
                return IntPtr.Zero;
            }
        }

        private static TestHost NewHost(ProtocolBase protocol)
        {
            // HostBase reads the settings view model from the container in its constructor
            var saved = IoC.GetByType;
            IoC.GetByType = (type, key) => type == typeof(SettingsPageViewModel) ? new SettingsPageViewModel(null!, null!) : null;
            try
            {
                return new TestHost(protocol);
            }
            finally
            {
                IoC.GetByType = saved;
            }
        }

        private static RDP NewServer(string address)
        {
            return new RDP { Id = "server", Address = address, Port = "3389", UserName = "user", Password = "pwd" };
        }

        [Fact]
        public void ConnectionId_ContainsTheAddressOfTheMomentOfCreation()
        {
            StaThread.Run(() =>
            {
                var host = NewHost(NewServer("10.0.0.1"));

                Assert.Contains("10.0.0.1:3389", host.ConnectionId);
            });
        }

        [Fact]
        public void ConnectionId_StaysTheSame_WhenTheAddressChangesAfterwards()
        {
            StaThread.Run(() =>
            {
                var rdp = NewServer("10.0.0.1");
                var host = NewHost(rdp);
                var idBefore = host.ConnectionId;

                rdp.Address = "10.0.0.2";

                Assert.Equal(idBefore, host.ConnectionId);
            });
        }
    }
}
