using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Media.Animation;
using PRM.Core.Protocol;
using PRM.Core.Protocol.RDP;
using Shawn.Ulits.RDP;
using SQLite;
using SQLiteConnection = System.Data.SQLite.SQLiteConnection;

namespace PRM.Core.DB
{
    public class ServerOrm : SimpleOrmBase
    {
        public ServerOrm() : base(tableName: "Server")
        {
        }

        [PrimaryKey, AutoIncrement]
        public uint Id { get; set; }

        public string ServerType { get; set; } = "";

        public string ClassVersion { get; set; } = "";

        public string DispName { get; set; } = "";

        public string GroupName { get; set; } = "";


        public void SetLastConnTime(DateTime dt)
        {
            LastConnTime = dt.ToString("yyyyMMdd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        }

        public string LastConnTime { get; private set; } = "";

        public string JsonConfigString { get; set; } = "";


        public static ServerOrm ConvertFrom(ProtocolServerBase org)
        {
            var ret = new ServerOrm();
            ret.Id = org.Id;
            ret.ServerType = org.ServerType;
            ret.ClassVersion = org.ClassVersion;
            ret.DispName = org.DispName;
            ret.GroupName = org.GroupName;
            ret.JsonConfigString = org.ToJsonString();
            return ret;
        }


        public override string SQLCreateTable()
        {
            return $@"
CREATE TABLE IF NOT EXISTS {TableName}(
    Id                       INTEGER     PRIMARY KEY AUTOINCREMENT,
    Protocol               CHAR(50)    NOT NULL,
    ClassVersion             CHAR(50)    NOT NULL,
    DispName                 CHAR(250)   NOT NULL,
    GroupName                CHAR(250)   NOT NULL,
    LastConnTime             CHAR(50)    NOT NULL,
    JsonConfigString         TEXT   NOT NULL
)
";
        }

        public override bool Insert(SQLiteConnection connection)
        {
            try
            {
                using (var tr = connection.BeginTransaction())
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = $@"
INSERT INTO 
{TableName}(
    Protocol      ,
    ClassVersion    ,
    DispName        ,
    GroupName       ,
    LastConnTime    ,
    JsonConfigString
) 
VALUES(
    @Protocol,
    @ClassVersion,
    @DispName,
    @GroupName,
    @LastConnTime,
    @JsonConfigString
)
            ";
                        command.Parameters.AddWithValue("@Protocol", this.ServerType);
                        command.Parameters.AddWithValue("@ClassVersion", this.ClassVersion);
                        command.Parameters.AddWithValue("@DispName", this.DispName);
                        command.Parameters.AddWithValue("@GroupName", this.GroupName);
                        command.Parameters.AddWithValue("@LastConnTime", this.LastConnTime);
                        command.Parameters.AddWithValue("@JsonConfigString", this.JsonConfigString);
                        command.ExecuteNonQuery();
                    }

                    // get new id
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "Select Id FROM Server ORDER BY Id DESC Limit 1";
                        var id = command.ExecuteScalar();
                        this.Id = (uint)Convert.ToInt32(id);
                    }

                    tr.Commit();
                }
                return true;
            }
            catch (Exception e)
            {
            }
            return false;
        }

        public override bool Update(SQLiteConnection connection, uint id = 0)
        {
            if (id <= 0)
                id = this.Id;
            try
            {
                using (var tr = connection.BeginTransaction())
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = $@"
UPDATE {TableName} SET
    Protocol        = @Protocol      ,
    ClassVersion      = @ClassVersion    ,
    DispName          = @DispName        ,
    GroupName          = @GroupName        ,
    LastConnTime      = @LastConnTime    ,
    JsonConfigString  = @JsonConfigString
WHERE Id = @Id
            ";
                        command.Parameters.AddWithValue("@Id", id);
                        command.Parameters.AddWithValue("@Protocol", this.ServerType);
                        command.Parameters.AddWithValue("@ClassVersion", this.ClassVersion);
                        command.Parameters.AddWithValue("@DispName", this.DispName);
                        command.Parameters.AddWithValue("@GroupName", this.GroupName);
                        command.Parameters.AddWithValue("@LastConnTime", this.LastConnTime);
                        command.Parameters.AddWithValue("@JsonConfigString", this.JsonConfigString);
                        command.ExecuteNonQuery();
                    }
                    tr.Commit();
                }
                this.Id = id;
                return true;
            }
            catch (Exception e)
            {
            }
            return false;
        }

        public override bool Delete(SQLiteConnection connection, uint id = 0)
        {
            if (id <= 0)
                id = this.Id;
            try
            {
                using (var tr = connection.BeginTransaction())
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = $@"DELETE FROM {TableName} WHERE Id = @Id";
                        command.Parameters.AddWithValue("@Id", id);
                        command.ExecuteNonQuery();
                    }
                    tr.Commit();
                }
                this.Id = 0;
                return true;
            }
            catch (Exception e)
            {
            }
            return false;
        }

        public static List<ServerOrm> ListAll(SQLiteConnection connection)
        {
            var t = typeof(ServerOrm);
            var ps = t.GetProperties();

            List<ServerOrm> ret = new List<ServerOrm>();
            using (var tr = connection.BeginTransaction())
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $@"SELECT * FROM Server";
                    var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var obj = new ServerOrm();
                        for (int i = 0; i < ps.Length; i++)
                        {
                            try
                            {
                                object v = Convert.ChangeType(reader[i], ps[i].PropertyType);
                                t.GetProperty(ps[i].Name).SetValue(obj, v);
                            }
                            catch (Exception e)
                            {
                                t.GetProperty(ps[i].Name).SetValue(obj, null);
                            }
                        }
                        ret.Add(obj);
                    }
                }
                tr.Commit();
            }
            return ret;
        }
    }
}