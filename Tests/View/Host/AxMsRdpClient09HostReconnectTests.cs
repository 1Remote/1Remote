using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using _1RM.View.Host.ProtocolHosts;
using Xunit;

namespace Tests.View.Host
{
    /// <summary>
    /// Closing the RDP tab between two reconnect attempts disposed the session host, but the
    /// reconnect that was already under way went on and created a new control inside the closed
    /// tab. A disposed host must not reconnect.
    /// </summary>
    public class AxMsRdpClient09HostReconnectTests
    {
        private static FieldInfo GetPrivateField(Type type, string name)
        {
            var field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.True(field != null, $"{type.Name}.{name} was not found");
            return field!;
        }

        [Fact]
        public void ReConn_DoesNothing_OnceTheHostIsDisposed()
        {
            // the host is built without running a constructor, only its own flag is needed
            var host = RuntimeHelpers.GetUninitializedObject(typeof(AxMsRdpClient09Host));
            GC.SuppressFinalize(host);
            var status = GetPrivateField(typeof(HostBase), "_status");
            status.SetValue(host, ProtocolHostStatus.Disconnected);
            GetPrivateField(typeof(AxMsRdpClient09Host), "_isDisposed").SetValue(host, true);

            // without the flag the call needs controls that were never created, so it runs apart
            var reConn = typeof(AxMsRdpClient09Host).GetMethod("ReConn")!;
            var failure = CallWithTimeout(() => reConn.Invoke(host, null));

            Assert.True(failure == null, $"ReConn did not stay silent on a disposed host: {failure}");
            Assert.Equal(ProtocolHostStatus.Disconnected, status.GetValue(host));
        }

        private static Exception? CallWithTimeout(Action action)
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    failure = e;
                }
            }) { IsBackground = true };
            thread.Start();
            if (!thread.Join(TimeSpan.FromSeconds(10)))
                return new TimeoutException("the call did not return within 10 seconds");
            return failure;
        }
    }
}
