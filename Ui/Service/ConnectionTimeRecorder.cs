using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        /// <param name="id"></param>
        /// <param name="time"></param>
        public static void UpdateAndSave(string id, long time = 0)
        {
            if (time == 0)
                time = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            ConnectTimeData.AddOrUpdate(id, time, (s, l) => time);
            Save();
        }

        public static DateTime Get(string id)
        {
            if (ConnectTimeData.TryGetValue(id, out var ut))
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
                ConnectTimeData.Remove(junk.Key, out _);
            }
        }
    }
}
