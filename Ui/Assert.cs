namespace _1RM
{
    internal static class Assert
    {
        private const string APP_NAME_RAW = "1Remote";
#if DEBUG
        public const string APP_NAME = $"{APP_NAME_RAW}_Debug";
#if FOR_MICROSOFT_STORE_ONLY
        public const string APP_DISPLAY_NAME = $"{APP_NAME}(Store)_Debug";
#else
        public const string APP_DISPLAY_NAME = APP_NAME;
#endif
#else
        public const string APP_NAME = $"{APP_NAME_RAW}";
#if FOR_MICROSOFT_STORE_ONLY
        public const string APP_DISPLAY_NAME = $"{APP_NAME}(Store)";
#else
        public const string APP_DISPLAY_NAME = APP_NAME;
#endif
#endif


        public const string MS_APP_CENTER_SECRET = "===REPLACE_ME_WITH_APP_CENTER_SECRET===";
        public const string STRING_SALT = "===REPLACE_ME_WITH_SALT===";
        public const string MS_STORE_PATH = "ms-windows-store://review/?productid=9PNMNF92JNFP";
    }
}
