using System;
using System.Diagnostics;
using _1RM.Service;

namespace _1RM.Model.DAO
{
    public enum EnumDbStatus
    {
        NotConnectedYet,
        AccessDenied,
        LostConnection,

        RsaPrivateKeyNotFound,
        RsaPrivateKeyFormatError,
        RsaNotMatched,
        DataIsDamaged,

        OK,
    }


    public enum EnumConnectionStatus
    {
        NotConnectedYet, // Die
        AccessDenied, // Die
        CanConnect, // To LostConnection OR DisConnected
        LostConnection, // Die
    }
    public enum EnumEncryptionStatus
    {
        NonEncryption,
        RsaPrivateKeyNotFound, // Die
        RsaPrivateKeyMismatch, // Die
        DataIsDamaged // Die
    }

    public static class EnumConnectResultErrorInfo
    {
        public static string GetErrorInfo(this EnumDbStatus result)
        {
            var lang = IoC.Get<LanguageService>();
            Debug.Assert(lang != null);
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

                case EnumDbStatus.NotConnectedYet:
                    return "database: NotConnected!";

                case EnumDbStatus.DataIsDamaged:
                    return "database: Data is damaged!"; // todo translate

                case EnumDbStatus.LostConnection:
                    return "database: Lost Connection!";
                default:
                    throw new ArgumentOutOfRangeException(nameof(result), result, null);
            }

            return "";
        }
    }
}