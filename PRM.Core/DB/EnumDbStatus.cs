using System;
using PRM.Core.Model;

namespace PRM.Core.DB
{
    public enum EnumDbStatus
    {
        OK,
        AccessDenied,
        RsaPrivateKeyNotFound,
        RsaPrivateKeyFormatError,
        RsaNotMatched,
        NotConnected,
    }

    public static class EnumConnectResultErrorInfo
    {
        public static string GetErrorInfo(this EnumDbStatus result, SystemConfigLanguage lang, string dbPath)
        {
            switch (result)
            {
                case EnumDbStatus.AccessDenied:
                    return SystemConfig.Instance.Language.GetText("string_database_error_permission_denied");

                case EnumDbStatus.RsaPrivateKeyNotFound:
                    return SystemConfig.Instance.Language.GetText("string_database_error_rsa_private_key_not_found");

                case EnumDbStatus.RsaPrivateKeyFormatError:
                    return SystemConfig.Instance.Language.GetText("string_database_error_rsa_private_key_format_error");

                case EnumDbStatus.RsaNotMatched:
                    return SystemConfig.Instance.Language.GetText("string_database_error_rsa_private_key_not_match");

                case EnumDbStatus.OK:
                    break;

                case EnumDbStatus.NotConnected:
                    return "database: NotConnected!";

                default:
                    throw new ArgumentOutOfRangeException(nameof(result), result, null);
            }

            return "";
        }
    }
}