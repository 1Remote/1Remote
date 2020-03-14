using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SQLite;
using System.Windows.Media.Animation;
using PRM.Core.Base;
using PRM.RDP;

namespace PRM.Core.DB
{
    public class ServerOrm : SimpleOrmBase
    {
        public ServerOrm() : base(tableName: "Server")
        {
        }

        public uint Id { get; set; }

        public string ServerType { get; set; } = "";

        public string ClassVersion { get; set; } = "";

        public string DispName { get; set; } = "";

        public string GroupName { get; set; } = "";

        public string Address { get; set; } = "";

        public void SetLastConnTime(DateTime dt)
        {
            LastConnTime = dt.ToString("yyyyMMdd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        }

        public string LastConnTime { get; private set; } = "";

        public string JsonConfigString { get; set; } = "";


        public static ServerOrm ConvertFrom(ServerAbstract org)
        {
            var ret = new ServerOrm();
            var rdp = (ServerRDP)Convert.ChangeType(org, typeof(ServerRDP));
            ret.Id = rdp.Id;
            ret.ServerType = rdp.ServerType;
            ret.ClassVersion = rdp.ClassVersion;
            ret.DispName = rdp.DispName;
            ret.GroupName = rdp.GroupName;
            ret.Address = rdp.Address;
            ret.SetLastConnTime(rdp.LassConnTime);
            ret.JsonConfigString = rdp.GetConfigJsonString();
            return ret;
        }







        public override string SQLCreateTable()
        {
            return $@"
CREATE TABLE IF NOT EXISTS {TableName}(
    Id                       INTEGER     PRIMARY KEY AUTOINCREMENT,
    ServerType               CHAR(50)    NOT NULL,
    ClassVersion             CHAR(50)    NOT NULL,
    DispName                 CHAR(250)   NOT NULL,
    GroupName                CHAR(250)   NOT NULL,
    Address                  CHAR(250)   NOT NULL,
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
    ServerType      ,
    ClassVersion    ,
    DispName        ,
    GroupName       ,
    Address         ,
    LastConnTime    ,
    JsonConfigString
) 
VALUES(
    @ServerType,
    @ClassVersion,
    @DispName,
    @GroupName,
    @Address,
    @LastConnTime,
    @JsonConfigString
)
            ";
                        command.Parameters.AddWithValue("@ServerType", this.ServerType);
                        command.Parameters.AddWithValue("@ClassVersion", this.ClassVersion);
                        command.Parameters.AddWithValue("@DispName", this.DispName);
                        command.Parameters.AddWithValue("@GroupName", this.GroupName);
                        command.Parameters.AddWithValue("@Address", this.Address);
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
    ServerType        = @ServerType      ,
    ClassVersion      = @ClassVersion    ,
    DispName          = @DispName        ,
    GroupName          = @GroupName        ,
    Address           = @Address        ,
    LastConnTime      = @LastConnTime    ,
    JsonConfigString  = @JsonConfigString
WHERE Id = @Id
            ";
                        command.Parameters.AddWithValue("@Id", id);
                        command.Parameters.AddWithValue("@ServerType", this.ServerType);
                        command.Parameters.AddWithValue("@ClassVersion", this.ClassVersion);
                        command.Parameters.AddWithValue("@DispName", this.DispName);
                        command.Parameters.AddWithValue("@GroupName", this.GroupName);
                        command.Parameters.AddWithValue("@Address", this.Address);
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
                        //var r4 = reader[4];
                        ////var tmp = reader.GetString(4);
                        //object tmp = reader.GetStream(4);
                        for (int i = 0; i < ps.Length; i++)
                        {
                            var field = reader[ps[i].Name];
                            //var value = ps[i].PropertyType
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