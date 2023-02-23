using System;
using System.Diagnostics;
using _1RM.Service;

namespace _1RM.Model.DAO
{
    public enum EnumDatabaseStatus
    {
        NotConnectedYet,
        AccessDenied,
        LostConnection,
        OK,
        EncryptKeyError, // 数据加密密钥不匹配，唯一的原因是软件未使用官方发布版本.
    }


    public static class EnumConnectResultErrorInfo
    {
        public static string GetErrorInfo(this EnumDatabaseStatus result)
        {
            var lang = IoC.Get<LanguageService>();
            Debug.Assert(lang != null);
            switch (result)
            {
                case EnumDatabaseStatus.AccessDenied:
                    return lang.Translate("string_database_error_permission_denied");

                case EnumDatabaseStatus.OK:
                    break;

                case EnumDatabaseStatus.NotConnectedYet:
                    return "database: Primary database is notConnected!";

                case EnumDatabaseStatus.LostConnection:
                    return "database: Primary database lost connection!";

                case EnumDatabaseStatus.EncryptKeyError:
                    return $"database: your primary database is encrypted by a third-part build {Assert.APP_NAME}, this app can not read your data correctly!";

                default:
                    throw new ArgumentOutOfRangeException(nameof(result), result, null);
            }

            return "";
        }
    }
}