using System;
using PRM.Service;

namespace PRM.Model.DAO
{
    public enum EnumDbStatus
    {
        OK,
        AccessDenied,
        RsaPrivateKeyNotFound,
        RsaPrivateKeyFormatError,
        RsaNotMatched,
        NotConnected,
        DataIsDamaged
    }

    public static class EnumConnectResultErrorInfo
    {
        public static string GetErrorInfo(this EnumDbStatus result, LanguageService lang)
        {
            switch (result)
            {
                case EnumDbStatus.AccessDenied:
                    return lang.Translate("string_database_error_permission_denied");

                case EnumDbStatus.RsaPrivateKeyNotFound:
                    return lang.Translate("string_database_error_rsa_private_key_not_found");

                case EnumDbStatus.RsaPrivateKeyFormatError:
                    return lang.Translate("string_database_error_rsa_private_key_format_error");

                case EnumDbStatus.RsaNotMatched:
                    return lang.Translate("string_database_error_rsa_private_key_not_match");

                case EnumDbStatus.OK:
                    break;

                case EnumDbStatus.NotConnected:
                    return "database: NotConnected!";

                case EnumDbStatus.DataIsDamaged:
                    return "database: Data is damaged!"; // todo translate

                default:
                    throw new ArgumentOutOfRangeException(nameof(result), result, null);
            }

            return "";
        }
    }
}