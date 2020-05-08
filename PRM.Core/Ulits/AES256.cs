using System;
using System.IO;
using System.Security.Cryptography;

// ref: https://github.com/jonjomckay/dotnet-simpleaes 

namespace SimpleAES
{
    /// <summary>
    /// A simple wrapper to the AesManaged class and the AES algorithm.
    /// Uses 256 bit key, 128 bit psuedo-random salt and a 16 bit
    /// psuedo-randomly generated Initialization Vector 
    /// </summary>
    public class AES256
    {
        // Preconfigured Encryption Parameters
        private static readonly int BlockBitSize = 128;
        // To be sure we get the correct IV size, set the block size
        private static readonly int KeyBitSize = 256;
        // AES 256 bit key encryption
        // Preconfigured Password Key Derivation Parameters
        private static readonly int SaltBitSize = 128;

        private static readonly int Iterations = 10000;
        /// <summary>
        /// Encrypts the plainText input using the given Key.
        /// A 128 bit random salt will be generated and prepended to the ciphertext before it is base64 encoded.
        /// A 16 bit random Initialization Vector will also be generated prepended to the ciphertext before it is base64 encoded.
        /// </summary>
        /// <param name="plainText">The plain text to encrypt.</param>
        /// <param name="key">The plain text encryption key.</param>
        /// <returns>The salt, IV and the ciphertext, Base64 encoded.</returns>
        public static string Encrypt(string plainText, string key)
        {
            //User Error Checks
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentNullException("key");
            }
            if (string.IsNullOrEmpty(plainText))
            {
                throw new ArgumentNullException("plainText");
            }

            // Derive a new Salt and IV from the Key, using a 128 bit salt and 10,000 iterations
            using (var keyDerivationFunction = new Rfc2898DeriveBytes(key, SaltBitSize / 8, Iterations))
            {
                using (var aesManaged = Aes.Create())
                {
                    aesManaged.KeySize = KeyBitSize;
                    aesManaged.BlockSize = BlockBitSize;

                    // Generate random IV
                    aesManaged.GenerateIV();

                    // Retrieve the Salt, Key and IV
                    byte[] saltBytes = keyDerivationFunction.Salt;
                    byte[] keyBytes = keyDerivationFunction.GetBytes(KeyBitSize / 8);
                    byte[] ivBytes = aesManaged.IV;

                    // Create an encryptor to perform the stream transform.
                    // Create the streams used for encryption.
                    using (var encryptor = aesManaged.CreateEncryptor(keyBytes, ivBytes))
                    {
                        using (var memoryStream = new MemoryStream())
                        {
                            using (var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                            {
                                using (var streamWriter = new StreamWriter(cryptoStream))
                                {
                                    // Send the data through the StreamWriter, through the CryptoStream, to the underlying MemoryStream
                                    streamWriter.Write(plainText);
                                }
                            }

                            // Return the encrypted bytes from the memory stream in Base64 form.
                            byte[] cipherTextBytes = memoryStream.ToArray();

                            // Resize saltBytes and append IV
                            Array.Resize(ref saltBytes, saltBytes.Length + ivBytes.Length);
                            Array.Copy(ivBytes, 0, saltBytes, SaltBitSize / 8, ivBytes.Length);

                            // Resize saltBytes with IV and append cipherText
                            Array.Resize(ref saltBytes, saltBytes.Length + cipherTextBytes.Length);
                            Array.Copy(cipherTextBytes, 0, saltBytes, (SaltBitSize / 8) + ivBytes.Length, cipherTextBytes.Length);

                            return Convert.ToBase64String(saltBytes);
                        }
                    }
                }
            }
        }


        /// <summary>
        /// Decrypts the ciphertext using the Key.
        /// </summary>
        /// <param name="ciphertext">The ciphertext to decrypt.</param>
        /// <param name="key">The plain text encryption key.</param>
        /// <returns>The decrypted text.</returns>
        public static string Decrypt(string ciphertext, string key)
        {
            if (string.IsNullOrEmpty(ciphertext))
            {
                throw new ArgumentNullException("cipherText");
            }
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentNullException("key");
            }

            // Prepare the Salt and IV arrays
            byte[] saltBytes = new byte[SaltBitSize / 8];
            byte[] ivBytes = new byte[BlockBitSize / 8];

            // Read all the bytes from the cipher text
            byte[] allTheBytes = Convert.FromBase64String(ciphertext);

            // Extract the Salt, IV from our ciphertext
            Array.Copy(allTheBytes, 0, saltBytes, 0, saltBytes.Length);
            Array.Copy(allTheBytes, saltBytes.Length, ivBytes, 0, ivBytes.Length);

            // Extract the Ciphered bytes
            byte[] ciphertextBytes = new byte[allTheBytes.Length - saltBytes.Length - ivBytes.Length];
            Array.Copy(allTheBytes, saltBytes.Length + ivBytes.Length, ciphertextBytes, 0, ciphertextBytes.Length);

            using (var keyDerivationFunction = new Rfc2898DeriveBytes(key, saltBytes, Iterations))
            {
                // Get the Key bytes
                byte[] keyBytes = keyDerivationFunction.GetBytes(KeyBitSize / 8);

                // Create a decrytor to perform the stream transform.
                // Create the streams used for decryption.
                // The default Cipher Mode is CBC and the Padding is PKCS7 which are both good
                using (var aesManaged = Aes.Create())
                {
                    aesManaged.KeySize = KeyBitSize;
                    aesManaged.BlockSize = BlockBitSize;

                    using (var decryptor = aesManaged.CreateDecryptor(keyBytes, ivBytes))
                    {
                        using (var memoryStream = new MemoryStream(ciphertextBytes))
                        {
                            using (var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
                            {
                                using (var streamReader = new StreamReader(cryptoStream))
                                {
                                    // Return the decrypted bytes from the decrypting stream.
                                    return streamReader.ReadToEnd();
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}