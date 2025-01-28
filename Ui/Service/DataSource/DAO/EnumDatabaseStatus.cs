using System;
using System.Diagnostics;
using _1RM.Service;

namespace _1RM.Service.DataSource.DAO
{
    public enum EnumDatabaseStatus
    {
        NotConnectedYet,
        AccessDenied,
        LostConnection,
        OK,
        EncryptKeyError, // 数据加密密钥不匹配，唯一的原因是软件未使用官方发布版本.
        OtherError,
    }

    public readonly struct DatabaseStatus
    {
        public static DatabaseStatus New(EnumDatabaseStatus status, string extend = "")
        {
            if (status == EnumDatabaseStatus.OtherError)
            {
                Debug.Assert(!string.IsNullOrEmpty(extend));
            }
            var ret = new DatabaseStatus()
            {
                Status = status,
                ExtendInfo = extend
            };
            return ret;
        }
        public EnumDatabaseStatus Status { get; private init; }
        public string ExtendInfo { get; private init; }
        public string GetErrorMessage
        {
            get
            {
                if (Status == EnumDatabaseStatus.OtherError)
                {
                    return "database: " + ExtendInfo;
                }
                return Status.GetErrorInfo() + " (" + ExtendInfo + ")";
            }
        }
    }


    public static class EnumConnectResultErrorInfo
    {
        public static string GetErrorInfo(this EnumDatabaseStatus result)
        {
            var lang = IoC.Get<LanguageService>();
            switch (result)
            {
                case EnumDatabaseStatus.AccessDenied:
                    return "database: no write permission, or the database is currently in use by another program!";

                case EnumDatabaseStatus.OK:
                    break;

                case EnumDatabaseStatus.NotConnectedYet:
                    return "database: database is not connected!";

                case EnumDatabaseStatus.LostConnection:
                    return "database: database lost connection!";

                case EnumDatabaseStatus.EncryptKeyError:
                    return $"database: your database is encrypted by a third-part build {Assert.APP_NAME}, this exe can not read your data correctly!";

                case EnumDatabaseStatus.OtherError:
                    return $"database: other error!";

                default:
                    throw new ArgumentOutOfRangeException(nameof(result), result, null);
            }

            return "";
        }
    }
}