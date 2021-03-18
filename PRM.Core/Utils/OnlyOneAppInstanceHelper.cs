using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace Shawn.Utils
{
    public class OnlyOneAppInstanceHelper : IDisposable
    {
        private readonly string _appName;
        private readonly Mutex _singleAppMutex = null;
        private readonly bool _isFirstStart;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public delegate void OnMessageReceivedDelegate(string message);

        public OnMessageReceivedDelegate OnMessageReceived;

        public OnlyOneAppInstanceHelper(string appName)
        {
            this._appName = appName;
            _singleAppMutex = new Mutex(true, appName, out var isFirst);
            _isFirstStart = isFirst;
            if (isFirst)
                StartNamedPipeServer();
        }

        public bool IsFirstInstance()
        {
            return _isFirstStart;
        }

        public void NamedPipeSendMessage(string message)
        {
            var client = new NamedPipeClientStream(_appName);
            try
            {
                client.Connect(1 * 1000);
                var writer = new StreamWriter(client);
                writer.WriteLine(message);
                writer.Flush();
            }
            finally
            {
                client.Close();
                client.Dispose();
            }
        }

        private void StartNamedPipeServer()
        {
            NamedPipeServerStream server = null;
            Task.Factory.StartNew(() =>
            {
                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                    server?.Dispose();
                    server = new NamedPipeServerStream(_appName);
                    server.WaitForConnection();
                    var reader = new StreamReader(server);
                    var line = reader.ReadLine();
                    OnMessageReceived?.Invoke(line);
                }
            }, _cancellationTokenSource.Token);
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel(false);
            _singleAppMutex?.Dispose();
            _cancellationTokenSource?.Dispose();
        }
    }
}