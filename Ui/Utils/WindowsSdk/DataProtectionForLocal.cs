using System;
using System.Threading.Tasks;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.DataProtection;
using Windows.Storage.Streams;

namespace _1RM.Utils.WindowsSdk
{
    /// <summary>
    /// encrypt string to base64, it can be decrypt only by the same local user, it can't be decrypt by other user or other machine
    /// REF: https://learn.microsoft.com/en-us/uwp/api/windows.security.cryptography.dataprotection.dataprotectionprovider?view=winrt-22621
    /// </summary>
    public static class DataProtectionForLocal
    {
        /// <summary>
        /// encrypt string to base64, it can be decrypt only by the same local user
        /// return null means failed
        /// </summary>
        public static async Task<string?> Protect(string strMsg, BinaryStringEncoding encoding = BinaryStringEncoding.Utf8)
        {
            try
            {
                var strDescriptor = "LOCAL=user";
                //String strDescriptor = "LOCAL=machine";
                // Create a DataProtectionProvider object for the specified descriptor.
                var provider = new DataProtectionProvider(strDescriptor);

                // Encode the plaintext input message to a buffer.
                var buffMsg = CryptographicBuffer.ConvertStringToBinary(strMsg, encoding);

                // Encrypt the message.
                var buffProtected = await provider.ProtectAsync(buffMsg);

                // Execution of the Protect function resumes here
                // after the awaited task (Provider.Protect) completes.
                var base64 = CryptographicBuffer.EncodeToBase64String(buffProtected);
                return base64;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// return null means failed
        /// </summary>
        public static async Task<string?> Unprotect(IBuffer buffProtected, BinaryStringEncoding encoding = BinaryStringEncoding.Utf8)
        {
            try
            {
                // Create a DataProtectionProvider object.
                var provider = new DataProtectionProvider();

                // Decrypt the protected message specified on input.
                var buffUnprotected = await provider.UnprotectAsync(buffProtected);

                // Execution of the Unprotect method resumes here
                // after the awaited task (Provider.UnprotectAsync) completes
                // Convert the unprotected message from an IBuffer object to a string.
                var strClearText = CryptographicBuffer.ConvertBinaryToString(encoding, buffUnprotected);

                // Return the plaintext string.
                return strClearText;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// return null means failed
        /// </summary>
        public static async Task<string?> Unprotect(string base64, BinaryStringEncoding encoding = BinaryStringEncoding.Utf8)
        {
            var buffProtected = CryptographicBuffer.DecodeFromBase64String(base64);
            if (buffProtected == null)
                return null;
            return await Unprotect(buffProtected, encoding);
        }
    }
}
