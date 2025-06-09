using System;
using _1RM.Model.Protocol.Base;
using Newtonsoft.Json;
using Shawn.Utils;

namespace _1RM.Service.DataSource.DAO.Dapper
{
    public class TableCredential
    {
        public const string TABLE_NAME = "Credentials";
        
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Json { get; set; } = "";
        public string Hash { get; set; } = "";

        public Credential? ToCredential()
        {
            try
            {
                var c = JsonConvert.DeserializeObject<Credential>(Json);
                if (c != null)
                {
                    c.DatabaseId = Id;
                    c.Name = Name;
                    c.Hash = Hash;
                }
                return c;
            }
            catch (Exception e)
            {
                SimpleLogHelper.Error($"Failed to deserialize Credential from JSON: {Json}", e);
                return null;
            }
        }
    }
}