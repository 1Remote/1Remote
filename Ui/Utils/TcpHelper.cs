using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace _1RM.Utils
{
    public static class TcpHelper
    {
        public static async Task<bool?> TestConnectionAsync(string address, int port, CancellationToken? cancellationToken = null, int timeOutMillisecond = 0)
        {
            using var client = new TcpClient();
            try
            {
                var connectTask = cancellationToken == null ? client.ConnectAsync(address, port) : client.ConnectAsync(address, port, (CancellationToken)cancellationToken).AsTask();
                if (timeOutMillisecond <= 0)
                    timeOutMillisecond = 30 * 1000;
                var timeoutTask = cancellationToken == null ? Task.Delay(TimeSpan.FromMilliseconds(timeOutMillisecond)) : Task.Delay(TimeSpan.FromMilliseconds(timeOutMillisecond), (CancellationToken)cancellationToken);
                var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                if (completedTask == timeoutTask)
                {
                    Console.WriteLine("Connection timed out.");
                    return false;
                }

                await connectTask;

                Console.WriteLine("Connected to {0}:{1}", address, port);
                return true;
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Connection cancelled.");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error connecting to {0}:{1}: {2}", address, port, ex.Message);
                return false;
            }
        }

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
                Console.WriteLine("Error connecting to {0}:{1}: {2}", address, portStr, ex.Message);
                return false;
            }
        }
    }
}
