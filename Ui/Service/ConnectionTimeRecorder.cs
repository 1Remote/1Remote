using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using _1RM.Model.DAO.Dapper;
using _1RM.Model.Protocol.Base;
using _1RM.View;
using Newtonsoft.Json;

namespace _1RM.Service
{
    public static class ConnectTimeRecorder
    {
        public static string Path { get; private set; } = "";
        public static ConcurrentDictionary<string, long> ConnectTimeData { get; private set; } = new ConcurrentDictionary<string, long>();

        public static void Init(string path)
        {
            Path = path;
            Load();
        }

        private static void Load()
        {
            if (File.Exists(Path))
            {
                ConnectTimeData = JsonConvert.DeserializeObject<ConcurrentDictionary<string, long>>(File.ReadAllText(Path)) ?? new ConcurrentDictionary<string, long>();
            }
        }

        public static void Save()
        {
            File.WriteAllText(Path, JsonConvert.SerializeObject(ConnectTimeData));
        }

        /// <summary>
        /// Update the connect time of id
        /// </summary>
        public static void UpdateConnectTime(this ProtocolBaseViewModel vmServer, long time = 0)
        {
            //var serverId = $"{server.Id}_From{server.DataSourceName}";
            if (time == 0)
                time = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            ConnectTimeData.AddOrUpdate(vmServer.Id, time, (s, l) => time);
            Save();
            vmServer.LastConnectTime = ConnectTimeRecorder.Get(vmServer.Server);
        }

        public static DateTime Get(ProtocolBase server)
        {
            //var serverId = $"{server.Id}_src{server.DataSourceId}";
            if (ConnectTimeData.TryGetValue(server.Id, out var ut))
            {
                DateTimeOffset dto = DateTimeOffset.FromUnixTimeMilliseconds(ut);
                return dto.LocalDateTime;
            }
            return DateTime.MinValue;
        }

        public static void Cleanup(IEnumerable<string> existedIds)
        {
            var junks = ConnectTimeData.Where(x => existedIds.All(y => y != x.Key));
            foreach (var junk in junks)
            {
                ConnectTimeData.TryRemove(junk.Key, out _);
            }
        }

        public static void Cleanup()
        {
            if (ConnectTimeData.Count > 100)
            {
                var ordered = ConnectTimeData.OrderByDescending(x => x.Value).ToList();
                var junks = ordered.Where(x => x.Value > ordered[100].Value);
                foreach (var junk in junks)
                {
                    ConnectTimeData.TryRemove(junk.Key, out _);
                }
            }
        }
    }
}
