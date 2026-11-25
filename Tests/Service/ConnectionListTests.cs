using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Service.DataSource.DAO;
using _1RM.Service.DataSource.DAO.Dapper;
using _1RM.Service.Locality;
using Dapper;
using Xunit;

namespace Tests.Service
{
    public class ConnectionListTests
    {
        [Fact]
        public void FilteredMoveKeepsOfflineAndHiddenRowsAcrossSources()
        {
            string a = LocalityListViewSettings.ConnectionKey("Office", "same-id");
            string b = LocalityListViewSettings.ConnectionKey("Test", "same-id");
            var settings = new LocalityListViewSettings();
            settings.ServerCustomOrder["same-id"] = 8;
            Assert.Equal(8, settings.GetConnectionOrder("Office", "same-id"));

            var before = new[] { a, "offline", "hidden", b, "other" };
            settings.ConnectionOrder = before.Select((key, i) => (key, i)).ToDictionary(x => x.key, x => x.i);
            var reordered = settings.MergeVisibleOrder(new[] { a, "hidden", b, "other" }, new[] { b, a });
            Assert.Equal(new[] { b, "offline", "hidden", a, "other" }, reordered.OrderBy(x => x.Value).Select(x => x.Key));
            settings.ConnectionOrder = reordered;
            Assert.Equal(0, settings.GetConnectionOrder("Test", "same-id"));
            Assert.Equal(3, settings.GetConnectionOrder("Office", "same-id"));
        }

        [Fact]
        public void SharedPinsSurviveEditsAndFailedBatches()
        {
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db");
            var writer = new DapperDatabase("writer", DatabaseType.Sqlite);
            var reader = new DapperDatabaseFree("reader", DatabaseType.Sqlite);
            try
            {
                string connection = new SQLiteConnectionStringBuilder { DataSource = path, Pooling = false }.ConnectionString;
                Assert.True(writer.OpenNewConnection(connection).IsSuccess);
                Assert.True(writer.InitTables().IsSuccess);
                Assert.True(reader.OpenNewConnection(connection).IsSuccess);
                ProtocolBase first = new SSH { Id = new string('a', 64), DisplayName = "original" };
                ProtocolBase second = new SSH { Id = "second", DisplayName = "second" };
                Assert.True(writer.AddServer(ref first).IsSuccess);
                Assert.True(writer.AddServer(ref second).IsSuccess);

                var ids = new[] { first.Id, second.Id };
                Assert.True(writer.SetServerPins(ids, true).IsSuccess);
                long version = reader.GetTableUpdateTimestamp(IDatabase.ListPinsRevision).Result;
                Assert.True(version > 0);
                // A stale editor from the other client must not write the pin as part of the connection JSON.
                first.DisplayName = "renamed";
                Assert.True(reader.UpdateServer(first).IsSuccess);
                var reloaded = reader.GetServers();
                Assert.True(reloaded.IsSuccess, reloaded.ErrorInfo);
                Assert.Equal(2, reloaded.Items.Count(row => row.IsPinnedInList));
                Assert.Equal("renamed", reloaded.Items.Single(row => row.Id == first.Id).DisplayName);
                Assert.Contains("renamed", writer.Connection.QuerySingle<string>("SELECT Json FROM Servers WHERE Id=@Id", new { first.Id }));

                Assert.True(reader.SetServerPins(ids, false).IsSuccess);
                Assert.True(reader.GetTableUpdateTimestamp(IDatabase.ListPinsRevision).Result > version);
                version = reader.GetTableUpdateTimestamp(IDatabase.ListPinsRevision).Result;
                Assert.False(writer.SetServerPins(new[] { first.Id, "missing" }, true).IsSuccess);
                Assert.Equal(version, reader.GetTableUpdateTimestamp(IDatabase.ListPinsRevision).Result);
                Assert.Equal(0, writer.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Configs WHERE Key LIKE 'ListPin:%'"));
                Assert.True(writer.SetServerPins(ids, true).IsSuccess);
                Assert.True(reader.SetServerPins(ids, true).IsSuccess);
                Assert.True(writer.DeleteServer(new[] { first.Id }).IsSuccess);
                Assert.Equal(second.Id, writer.Connection.QuerySingle<string>("SELECT Value FROM Configs WHERE Key LIKE 'ListPin:%'"));
            }
            finally
            {
                reader.CloseConnection();
                writer.CloseConnection();
                File.Delete(path);
            }
        }
    }
}
