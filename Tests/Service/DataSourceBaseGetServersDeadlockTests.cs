using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Service.DataSource.DAO;
using _1RM.Service.DataSource.Model;
using Stylet;
using Xunit;

namespace Tests.Service
{
    /// <summary>
    /// GetServers used to dispatch view-model creation to the UI thread while holding lock(this);
    /// a UI thread waiting for that same lock (server editor -> GetCredentials(true)) froze the app.
    /// This simulates the exact interleaving: the old code times out, the fixed code completes.
    /// </summary>
    public class DataSourceBaseGetServersDeadlockTests
    {
        private sealed class FakeDatabase : IDatabase
        {
            public Func<List<ProtocolBase>> ServersProvider { get; set; } = () => new List<ProtocolBase>();

            public FakeDatabase() : base("fake-test-db", DatabaseType.Sqlite) { }

            public override ResultSelects<ProtocolBase> GetServers()
            {
                return ResultSelects<ProtocolBase>.Success(ServersProvider());
            }

            public override ResultSelects<Credential> GetCredentials(bool closeInEnd = true)
            {
                return ResultSelects<Credential>.Success(new List<Credential>());
            }

            public override ResultLong GetTableUpdateTimestamp(string tableName, bool closeInEnd = true)
            {
                return ResultLong.Success(0);
            }

            private static Result Unused() => throw new NotSupportedException("not used by this test");

            public override void CloseConnection() { /* no-op */ }
            public override Result OpenNewConnection(string newConnectionString) => Result.Success();
            public override bool IsConnected() => true;
            public override Result InitTables() => Unused();
            public override Result TableExists(string tableName) => Unused();
            public override Result AddServer(ref ProtocolBase protocolBase) => Unused();
            public override Result AddServer(IEnumerable<ProtocolBase> protocolBases) => Unused();
            public override Result UpdateServer(ProtocolBase server) => Unused();
            public override Result UpdateServer(IEnumerable<ProtocolBase> servers) => Unused();
            public override Result DeleteServer(IEnumerable<string> ids) => Unused();
            public override Result AddCredential(ref Credential credential) => Unused();
            public override Result UpdateCredential(Credential credential, List<ProtocolBaseWithAddressPortUserPwd>? relatedProtocols = null) => Unused();
            public override Result DeleteCredential(IEnumerable<string> names, List<ProtocolBaseWithAddressPortUserPwd>? relatedProtocols = null) => Unused();
            public override ResultString GetConfig(string key, bool closeInEnd = true) => throw new NotSupportedException("not used by this test");
            public override Result SetConfig(string key, string? value, bool closeInEnd = true) => Unused();
            public override Result SetTableUpdateTimestamp(string tableName, long time = -1, bool closeInEnd = true) => Unused();
        }

        private sealed class TestDataSource : DataSourceBase
        {
            public readonly FakeDatabase Db = new FakeDatabase();
            public override IDatabase GetDataBase() => Db;
            public override string GetConnectionString(int connectTimeOutSeconds = 5) => "";
            public override DatabaseType DatabaseType => DatabaseType.Sqlite;
            public override string Description => "unit test source";
        }

        /// <summary>
        /// A dedicated thread acting as the UI thread; Send() blocks the caller until the action runs (like Dispatcher.Invoke).
        /// ArmContend() makes it fight for lock(this) right before the next dispatched action.
        /// </summary>
        private sealed class FakeUiThread : IDisposable
        {
            private readonly BlockingCollection<(Action action, TaskCompletionSource<bool> tcs)> _queue
                = new BlockingCollection<(Action, TaskCompletionSource<bool>)>();
            private readonly Thread _thread;
            private volatile bool _contend;

            /// <summary>
            /// Once armed, before running each dispatched action the "UI thread" first runs
            /// <paramref name="contendAction"/> - e.g. contend for lock(this). Lets the test
            /// decide WHICH dispatch starts the fight.
            /// </summary>
            public void ArmContend() => _contend = true;

            public FakeUiThread(Action contendAction)
            {
                _thread = new Thread(() =>
                {
                    foreach (var (action, tcs) in _queue.GetConsumingEnumerable())
                    {
                        try
                        {
                            if (_contend) contendAction();
                            action();
                            tcs.TrySetResult(true);
                        }
                        catch (Exception e)
                        {
                            tcs.TrySetException(e);
                        }
                    }
                })
                { IsBackground = true, Name = "FakeUiThread" };
                _thread.Start();
            }

            public bool IsCurrent => Thread.CurrentThread.ManagedThreadId == _thread.ManagedThreadId;

            /// <summary>called from other threads via IDispatcher.Send; waits forever, like Dispatcher.Invoke</summary>
            public void Invoke(Action action)
            {
                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _queue.Add((action, tcs));
                tcs.Task.Wait();
            }

            public void Dispose() => _queue.CompleteAdding();
        }

        private sealed class FakeDispatcher : IDispatcher
        {
            private readonly FakeUiThread _ui;
            public FakeDispatcher(FakeUiThread ui) => _ui = ui;
            public void Post(Action action) => _ui.Invoke(action);
            public void Send(Action action) => _ui.Invoke(action);
            public bool IsCurrent => _ui.IsCurrent;
        }

        [Fact]
        public void GetServers_MustNotDeadlock_WhenUiThreadWaitsForSameLock()
        {
            var source = new TestDataSource();
            source.Db.ServersProvider = () => new List<ProtocolBase>
            {
                new SSH { Address = "192.0.2.1", Port = "22", UserName = "root" },
            };

            using var uiThread = new FakeUiThread(contendAction: () =>
            {
                // Simulate the user opening the server editor page on the UI thread
                // (CredentialViewModel ctor) while a background reload is in flight:
                // GetCredentials(force:true) takes lock(this).
                source.GetCredentials(force: true);
            });

            var original = Execute.Dispatcher;
            Execute.Dispatcher = new FakeDispatcher(uiThread);
            try
            {
                // Warm-up: bring Status up to OK, without contention
                source.Database_OpenConnection();
                Assert.Equal(EnumDatabaseStatus.OK, source.Status);
                source.GetServers(force: true);

                // The deadlock scenario (finding B-1): the "UI thread" contends for lock(this)
                // from inside the first dispatched action of the reload below.
                uiThread.ArmContend();
                var reload = Task.Run(() => source.GetServers(force: true));
                bool finished = Task.WaitAll(new[] { reload }, TimeSpan.FromSeconds(15));
                Assert.True(finished,
                    "GetServers deadlocked: the reload thread waits for the UI thread while holding lock(this), " +
                    "while the UI thread waits for lock(this) inside GetCredentials");
            }
            finally
            {
                Execute.Dispatcher = original;
            }
        }
    }
}
