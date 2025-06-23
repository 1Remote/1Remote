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
            lock (this)
            {
                _dbConnection?.Execute(NormalizedSql(@$"
CREATE TABLE IF NOT EXISTS `{TableCredential.TABLE_NAME}` (
    `{nameof(TableCredential.Id)}`       VARCHAR (64) PRIMARY KEY
                                              NOT NULL
                                              UNIQUE,
    `{nameof(TableCredential.Name)}`     VARCHAR (128)
                                              NOT NULL
                                              UNIQUE,
    `{nameof(TableCredential.Hash)}`     VARCHAR (32) NOT NULL,
    `{nameof(TableCredential.Json)}`     TEXT         NOT NULL
);
"));
            }
        }

        public override ResultSelects<Credential> GetCredentials(bool closeInEnd = false)
        {
            string info = IoC.Translate("We can not select from database:");

            lock (this)
            {
                var result = OpenConnection(info);
                if (!result.IsSuccess) return ResultSelects<Credential>.Fail(result.ErrorInfo);
                try
                {
                    var ps = _dbConnection.Query<TableCredential>(NormalizedSql($"SELECT * FROM `{TableCredential.TABLE_NAME}`"))
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

        private string SqlInsertCredentialVault => NormalizedSql($@"INSERT INTO `{TableCredential.TABLE_NAME}`
(`{nameof(TableCredential.Id)}`,`{nameof(TableCredential.Name)}`,`{nameof(TableCredential.Hash)}`, `{nameof(TableCredential.Json)}`)
VALUES
(@{nameof(TableCredential.Id)}, @{nameof(TableCredential.Name)}, @{nameof(TableCredential.Hash)}, @{nameof(TableCredential.Json)});");

        public override Result AddCredential(ref Credential credential)
        {
            string info = IoC.Translate("We can not insert into database:");
            lock (this)
            {
                var result = OpenConnection(info);
                if (!result.IsSuccess) return result;

                try
                {
                    var tableCredential = credential.ToTableCredential();
                    int affCount = _dbConnection?.Execute(SqlInsertCredentialVault, tableCredential) ?? 0;
                    if (affCount > 0)
                    {
                        credential.DatabaseId = tableCredential.Id; // update the DatabaseId to match the new Id
                        SetTableUpdateTimestamp(TableCredential.TABLE_NAME);
                        return Result.Success();
                    }
                    return Result.Fail(info, DatabaseName, "No rows affected during insert operation.");
                }
                catch (Exception e)
                {
                    return Result.Fail(info, DatabaseName, e.Message);
                }
            }
        }

        private string SqlUpdatePasswordVault => NormalizedSql($@"UPDATE `{TableCredential.TABLE_NAME}` SET
`{nameof(TableCredential.Name)}` = @{nameof(TableCredential.Name)},
`{nameof(TableCredential.Hash)}` = @{nameof(TableCredential.Hash)},
`{nameof(TableCredential.Json)}` = @{nameof(TableCredential.Json)}
WHERE `{nameof(TableCredential.Id)}`= @{nameof(TableCredential.Id)};");
        public override Result UpdateCredential(Credential credential, List<ProtocolBaseWithAddressPortUserPwd>? relatedProtocols = null)
        {
            string info = IoC.Translate("We can not update on database:");
            lock (this)
            {
                var result = OpenConnection(info);
                if (!result.IsSuccess || _dbConnection == null) return result;
                try
                {
                    bool ret = false;
                    using (var transaction = _dbConnection.BeginTransaction())
                    {
                        // make sure Address and Port are empty, because they are not used in credential vault
                        credential.Address = "";
                        credential.Port = "";
                        var tpv = new TableCredential()
                        {
                            Id = credential.DatabaseId,
                            Name = credential.Name,
                            Hash = credential.GetHash(),
                            Json = JsonConvert.SerializeObject(credential),
                        };
                        ret = _dbConnection?.Execute(SqlUpdatePasswordVault, tpv) > 0;



                        // after credential is updated, the related protocols will also be updated with the new credential information.
                        if (ret && relatedProtocols?.Count > 0)
                        {
                            foreach (var protocol in relatedProtocols)
                            {
                                protocol.InheritedCredentialName = tpv.Name;
                                protocol.SetCredential(credential);
                            }
                            ret = _dbConnection?.Execute(SqlUpdate, relatedProtocols.Select(x => x.ToTableServer())) > 0;
                        }
                        if (!ret)
                        {
                            transaction.Rollback();
                        }
                        else
                        {
                            transaction.Commit();
                        }
                    }


                    if (ret)
                    {
                        SetTableUpdateTimestamp(TableCredential.TABLE_NAME);
                        return Result.Success();
                    }
                    return Result.Fail(info, DatabaseName, "No rows affected during update operation.");
                }
                catch (Exception e)
                {
                    return ResultString.Fail(info, DatabaseName, e.Message);
                }
            }
        }

        //public override Result UpdateCredential(IEnumerable<Credential> credentials)
        //{
        //    string info = IoC.Translate("We can not update on database:");
        //    lock (this)
        //    {
        //        var result = OpenConnection(info);
        //        if (!result.IsSuccess) return result;
        //        try
        //        {
        //            var items = new List<TableCredential>();
        //            foreach (var credential in credentials)
        //            {
        //                var tpv = new TableCredential()
        //                {
        //                    Name = credential.Name,
        //                    Hash = credential.GetHash(),
        //                    Json = JsonConvert.SerializeObject(credential),
        //                };
        //                items.Add(tpv);
        //            }
        //            var ret = _dbConnection?.Execute(SqlUpdate, items) > 0;
        //            if (ret)
        //            {
        //                return Result.Success();
        //            }
        //            return Result.Fail(info, DatabaseName, "No rows affected during update operation.");
        //        }
        //        catch (Exception e)
        //        {
        //            return ResultString.Fail(info, DatabaseName, e.Message);
        //        }
        //    }
        //}

        public override Result DeleteCredential(IEnumerable<string> names, List<ProtocolBaseWithAddressPortUserPwd>? relatedProtocols = null)
        {
            var info = IoC.Translate("We can not delete from database:");
            if (!names.Any())
                return Result.Success();

            lock (this)
            {
                var result = OpenConnection(info);
                if (!result.IsSuccess || _dbConnection == null) return result;
                try
                {
                    bool ret = false;
                    using (var transaction = _dbConnection.BeginTransaction())
                    {
                        // special case: dapper does not support IN operator for postgresql
                        var sql = DatabaseType == DatabaseType.PostgreSQL
                            ? $@"DELETE FROM `{TableCredential.TABLE_NAME}` WHERE `{nameof(TableCredential.Name)}` = ANY(@{nameof(TableCredential.Name)});"
                            : $@"DELETE FROM `{TableCredential.TABLE_NAME}` WHERE `{nameof(TableCredential.Name)}` IN @{nameof(TableCredential.Name)};";
                        ret = _dbConnection?.Execute(NormalizedSql(sql), new { Name = names }) > 0;

                        // after credential is deleted, the related protocols will also be updated to clear the credential information.
                        if (ret && relatedProtocols?.Count > 0)
                        {
                            foreach (var protocol in relatedProtocols)
                            {
                                protocol.InheritedCredentialName = ""; // clear the credential name
                            }
                            ret = _dbConnection?.Execute(SqlUpdate, relatedProtocols.Select(x => x.ToTableServer())) > 0;
                        }
                        if (!ret)
                        {
                            transaction.Rollback();
                        }
                        else
                        {
                            transaction.Commit();
                        }
                    }

                    if (ret)
                    {
                        SetTableUpdateTimestamp(TableCredential.TABLE_NAME);
                        return Result.Success();
                    }
                    return Result.Fail(info, DatabaseName, "No rows affected during delete operation.");
                }
                catch (Exception e)
                {
                    return Result.Fail(info, DatabaseName, e.Message);
                }
            }
        }
    }
}