using FreeSql.DataAnnotations;
using Newtonsoft.Json;

namespace PRM.Core.DB.freesql
{
    [Index("uk_key", "Key", true)]
    [Table(Name = "Config")]
    public class DbConfig
    {
        [Column(IsIdentity = true, IsPrimary = true)]
        public int Id { get; set; }

        [JsonProperty, Column(DbType = "VARCHAR")]
        public string Key { get; set; } = "";

        [JsonProperty, Column(DbType = "VARCHAR")]
        public string Value { get; set; }
    }
}
