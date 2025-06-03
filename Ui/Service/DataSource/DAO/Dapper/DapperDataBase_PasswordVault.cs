using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using _1RM.Model.Protocol.Base;
using NUlid;
using Newtonsoft.Json;

// ReSharper disable InconsistentNaming

namespace _1RM.Service.DataSource.DAO.Dapper
{
    public partial class DapperDatabase
    {
        private void InitPasswordVault()
        {
            _dbConnection?.Execute(NormalizedSql(@$"
CREATE TABLE IF NOT EXISTS `{TablePasswordVault.TABLE_NAME}` (
    `{nameof(TablePasswordVault.Name)}`       VARCHAR (128) PRIMARY KEY
                                                    NOT NULL
                                                    UNIQUE,
    `{nameof(TablePasswordVault.Hash)}` VARCHAR (32) NOT NULL,
    `{nameof(TablePasswordVault.Json)}`     TEXT         NOT NULL
);
"));
        }

        public override ResultSelects<Credential> GetPasswords()
        {
            string info = IoC.Translate("We can not select from database:");

            lock (this)
            {
                var result = OpenConnection(info);
                if (!result.IsSuccess) return ResultSelects<Credential>.Fail(result.ErrorInfo);
                try
                {
                    var ps = _dbConnection.Query<TablePasswordVault>(NormalizedSql($"SELECT * FROM `{TablePasswordVault.TABLE_NAME}`"))
                                                            .Select(x => x.ToCredential())
                                                            .Where(x => x != null).ToList();
                    return ResultSelects<Credential>.Success((ps as List<Credential>)!);
                }
                catch (Exception e)
                {
                    return ResultSelects<Credential>.Fail(info, DatabaseName, e.Message);
                }
            }
        }

        private string SqlInsertPasswordVault => NormalizedSql($@"INSERT INTO `{TablePasswordVault.TABLE_NAME}`
(`{nameof(TablePasswordVault.Name)}`,`{nameof(TablePasswordVault.Hash)}`, `{nameof(TablePasswordVault.Json)}`)
VALUES
(@{nameof(TablePasswordVault.Name)}, @{nameof(TablePasswordVault.Hash)}, @{nameof(TablePasswordVault.Json)});");

        public override Result AddPassword(Credential credential)
        {
            string info = IoC.Translate("We can not insert into database:");
            lock (this)
            {
                var result = OpenConnection(info);
                if (!result.IsSuccess) return result;

                try
                {
                    // TODO: 检查 credential.Name 是否已经存在
                    var tpv = new TablePasswordVault()
                    {
                        Name = credential.Name,
                        Hash = credential.GetHash(),
                        Json = JsonConvert.SerializeObject(credential),
                    };
                    int affCount = _dbConnection?.Execute(SqlInsertPasswordVault, tpv) ?? 0;
                    if (affCount > 0)
                        SetDataUpdateTimestamp();
                    return Result.Success();
                }
                catch (Exception e)
                {
                    return Result.Fail(info, DatabaseName, e.Message);
                }
            }
        }

        private string SqlUpdatePasswordVault => NormalizedSql($@"UPDATE `{TablePasswordVault.TABLE_NAME}` SET
`{nameof(TablePasswordVault.Hash)}` = @{nameof(TablePasswordVault.Hash)},
`{nameof(TablePasswordVault.Json)}` = @{nameof(TablePasswordVault.Json)}
WHERE `{nameof(TablePasswordVault.Name)}`= @{nameof(TablePasswordVault.Name)};");
        public override Result UpdatePassword(Credential credential)
        {
            string info = IoC.Translate("We can not update on database:");
            lock (this)
            {
                var result = OpenConnection(info);
                if (!result.IsSuccess) return result;
                try
                {
                    var tpv = new TablePasswordVault()
                    {
                        Name = credential.Name,
                        Hash = credential.GetHash(),
                        Json = JsonConvert.SerializeObject(credential),
                    };
                    var ret = _dbConnection?.Execute(SqlUpdatePasswordVault, tpv) > 0;
                    if (ret)
                    {
                        SetDataUpdateTimestamp();
                    }

                    return Result.Success();
                }
                catch (Exception e)
                {
                    return ResultString.Fail(info, DatabaseName, e.Message);
                }
            }
        }

        public override Result UpdatePassword(IEnumerable<Credential> credentials)
        {
            string info = IoC.Translate("We can not update on database:");
            lock (this)
            {
                var result = OpenConnection(info);
                if (!result.IsSuccess) return result;
                try
                {
                    var items = new List<TablePasswordVault>();
                    foreach (var credential in credentials)
                    {
                        var tpv = new TablePasswordVault()
                        {
                            Name = credential.Name,
                            Hash = credential.GetHash(),
                            Json = JsonConvert.SerializeObject(credential),
                        };
                        items.Add(tpv);
                    }
                    var ret = _dbConnection?.Execute(SqlUpdate, items) > 0;
                    if (ret)
                    {
                        SetDataUpdateTimestamp();
                    }
                    return Result.Success();
                }
                catch (Exception e)
                {
                    return ResultString.Fail(info, DatabaseName, e.Message);
                }
            }
        }

        public override Result DeletePassword(IEnumerable<string> names)
        {
            var info = IoC.Translate("We can not delete from database:");
            lock (this)
            {
                var result = OpenConnection(info);
                if (!result.IsSuccess) return result;
                try
                {
                    // special case: dapper does not support IN operator for postgresql
                    var sql = DatabaseType == DatabaseType.PostgreSQL
                        ? $@"DELETE FROM `{TablePasswordVault.TABLE_NAME}` WHERE `{nameof(TablePasswordVault.Name)}` = ANY(@{nameof(TablePasswordVault.Name)});"
                        : $@"DELETE FROM `{TablePasswordVault.TABLE_NAME}` WHERE `{nameof(TablePasswordVault.Name)}` IN @{nameof(TablePasswordVault.Name)};";
                    var ret = _dbConnection?.Execute(NormalizedSql(sql), new { Id = names }) > 0;
                    if (ret)
                        SetDataUpdateTimestamp();
                    return Result.Success();
                }
                catch (Exception e)
                {
                    return Result.Fail(info, DatabaseName, e.Message);
                }
            }
        }
    }
}