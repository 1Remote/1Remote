namespace PRM.Core.DB
{
    public enum EnumConnectResult
    {
        Success,
        AccessDenied,
        RsaPrivateKeyNotFound,
        RsaPrivateKeyFormatError,
        RsaNotMatched,
    }
}