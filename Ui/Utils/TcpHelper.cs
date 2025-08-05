using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Shawn.Utils;
using System.Net.NetworkInformation;

namespace _1RM.Utils
{
    public static class TcpHelper
    {
        public static (bool, IPAddress?) IsIPv6(string address)
        {
            if (IPAddress.TryParse(address, out var ipAddress))
            {
                return (ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6, ipAddress);
            }
            return (false, null);
        }

        /// <summary>
        /// return true if connected, false if not connected, null if timeout or cancelled.
        /// </summary>
        public static async Task<bool?> TestConnectionAsync(string address, int port, CancellationToken? cancellationToken = null, int timeOutMillisecond = 0)
        {
            var (isIPv6, iPv6) = IsIPv6(address);
            using var client = isIPv6 ? new TcpClient(iPv6!.AddressFamily) : new TcpClient();
            try
            {
                var cts = new CancellationTokenSource();
                cancellationToken ??= cts.Token;
#if NETCOREAPP
                var connectTask = client.ConnectAsync(address, port, (CancellationToken)cancellationToken).AsTask();
#else
                var connectTask = isIPv6 ? client.ConnectAsync(iPv6, port) : client.ConnectAsync(address, port);
#endif
                if (timeOutMillisecond <= 0)
                    timeOutMillisecond = 30 * 1000;
                var timeoutTask = Task.Delay(TimeSpan.FromMilliseconds(timeOutMillisecond), (CancellationToken)cancellationToken);
                var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                if (completedTask == timeoutTask && completedTask.IsCanceled != true)
                {
                    SimpleLogHelper.Debug("TcpHelper: timeoutTask is completed, cancel connectTask.");
                    cts.Cancel();
                    connectTask.Dispose();
                    return null;
                }

                if (completedTask.IsCanceled)
                {
                    SimpleLogHelper.Debug("TcpHelper: Connection cancelled.");
                    cts.Cancel();
                    connectTask.Dispose();
                    return null;
                }

                await connectTask;

                SimpleLogHelper.Debug($"TcpHelper: Connected to {address}:{port}");
                return true;
            }
            catch (OperationCanceledException)
            {
                SimpleLogHelper.Debug("TcpHelper: Connection cancelled.");
                return null;
            }
            catch (Exception ex)
            {
                SimpleLogHelper.Debug($"TcpHelper: Error connecting to {address}:{port}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// try access the address and port, if connect success return true, cancel return null
        /// </summary>
        public static async Task<bool?> TestConnectionAsync(string address, string portStr, CancellationToken? cancellationToken = null, int timeOutMillisecond = 0)
        {
            using var client = new TcpClient();
            try
            {
                var port = int.Parse(portStr);
                return await TestConnectionAsync(address, port, cancellationToken, timeOutMillisecond);
            }
            catch (Exception ex)
            {
                SimpleLogHelper.Debug("TcpHelper: Error connecting to {0}:{1}: {2}", address, portStr, ex.Message);
                return false;
            }
        }
    }
}
