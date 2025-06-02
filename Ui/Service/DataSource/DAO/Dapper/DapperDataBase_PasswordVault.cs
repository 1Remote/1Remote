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
    `{nameof(TablePasswordVault.Id)}`       VARCHAR (64) PRIMARY KEY
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
(`{nameof(TablePasswordVault.Id)}`,`{nameof(TablePasswordVault.Hash)}`, `{nameof(TablePasswordVault.Json)}`)
VALUES
(@{nameof(TablePasswordVault.Id)}, @{nameof(TablePasswordVault.Hash)}, @{nameof(TablePasswordVault.Json)});");

        /// <summary>
        /// 插入成功后会更新 credential.Id
        /// </summary>
        public override Result AddPassword(ref Credential credential)
        {
            string info = IoC.Translate("We can not insert into database:");
            lock (this)
            {
                var result = OpenConnection(info);
                if (!result.IsSuccess) return result;

                try
                {
                    // TODO: 检查 credential.Id 是否已经存在
                    var id = Ulid.NewUlid().ToString();
                    var tpv = new TablePasswordVault()
                    {
                        Id = credential.Id,
                        Hash = credential.GetHash(),
                        Json = JsonConvert.SerializeObject(credential),
                    };
                    int affCount = _dbConnection?.Execute(SqlInsertPasswordVault, tpv) ?? 0;
                    if (affCount > 0)
                        SetDataUpdateTimestamp();
                    credential.Id = id; // 更新Id
                    return Result.Success();
                }
                catch (Exception e)
                {
                    return Result.Fail(info, DatabaseName, e.Message);
                }
            }
        }

        /// <summary>
        /// 插入成功后会更新 protocolBase.Id
        /// </summary>
        public override Result AddPassword(IEnumerable<ProtocolBase> protocolBases)
        {
            string info = IoC.Translate("We can not insert into database:");
            lock (this)
            {
                var result = OpenConnection(info);
                if (!result.IsSuccess) return result;
                try
                {
                    var rng = new NUlid.Rng.MonotonicUlidRng();
                    foreach (var protocolBase in protocolBases)
                    {
                        if (protocolBase.IsTmpSession())
                            protocolBase.Id = Ulid.NewUlid(rng).ToString();
                    }
                    var PasswordVaults = protocolBases.Select(x => x.ToDbPassword()).ToList();
                    var affCount = _dbConnection?.Execute(SqlInsert, PasswordVaults) > 0 ? protocolBases.Count() : 0;
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
WHERE `{nameof(TablePasswordVault.Id)}`= @{nameof(TablePasswordVault.Id)};");
        public override Result UpdatePassword(ProtocolBase PasswordVault)
        {
            string info = IoC.Translate("We can not update on database:");
            lock (this)
            {
                var result = OpenConnection(info);
                if (!result.IsSuccess) return result;
                try
                {
                    var ret = _dbConnection?.Execute(SqlUpdatePasswordVault, PasswordVault.ToDbPassword()) > 0;
                    if (ret)
                    {
                        SetDataUpdateTimestamp();
                    }
                    else
                    {
                        // TODO 如果`{nameof(TablePasswordVault.Id)}`= @{nameof(TablePasswordVault.Id)}的项目不存在时怎么办？
                    }
                    return Result.Success();
                }
                catch (Exception e)
                {
                    return ResultString.Fail(info, DatabaseName, e.Message);
                }
            }
        }

        public override Result UpdatePassword(IEnumerable<ProtocolBase> PasswordVaults)
        {
            string info = IoC.Translate("We can not update on database:");
            lock (this)
            {
                var result = OpenConnection(info);
                if (!result.IsSuccess) return result;
                try
                {
                    var items = PasswordVaults.Select(x => x.ToDbPassword());
                    var ret = _dbConnection?.Execute(SqlUpdate, items) > 0;
                    if (ret)
                    {
                        SetDataUpdateTimestamp();
                    }
                    else
                    {
                        // TODO 如果`{nameof(TablePasswordVault.Id)}`= @{nameof(TablePasswordVault.Id)}的项目不存在时怎么办？
                    }
                    return Result.Success();
                }
                catch (Exception e)
                {
                    return ResultString.Fail(info, DatabaseName, e.Message);
                }
            }
        }

        public override Result DeletePassword(IEnumerable<string> ids)
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
                        ? $@"DELETE FROM `{TablePasswordVault.TABLE_NAME}` WHERE `{nameof(TablePasswordVault.Id)}` = ANY(@{nameof(TablePasswordVault.Id)});"
                        : $@"DELETE FROM `{TablePasswordVault.TABLE_NAME}` WHERE `{nameof(TablePasswordVault.Id)}` IN @{nameof(TablePasswordVault.Id)};";
                    var ret = _dbConnection?.Execute(NormalizedSql(sql), new { Id = ids }) > 0;
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