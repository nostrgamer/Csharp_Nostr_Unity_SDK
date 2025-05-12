using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace NostrUnity.Utils
{
    /// <summary>
    /// Utility for securely storing sensitive data in PlayerPrefs using AES encryption
    /// </summary>
    public static class SecurePlayerPrefs
    {
        // WARNING: For production, use a user-supplied passphrase or secure key management
        // Hardcoded 32-byte key (256 bits for AES-256)
        private static readonly byte[] StaticKey = new byte[32] 
        { 
            0x4E, 0x6F, 0x73, 0x74, 0x72, 0x55, 0x6E, 0x69, 
            0x74, 0x79, 0x53, 0x44, 0x4B, 0x5F, 0x41, 0x45, 
            0x53, 0x5F, 0x4B, 0x45, 0x59, 0x5F, 0x32, 0x35, 
            0x36, 0x5F, 0x42, 0x49, 0x54, 0x53, 0x21, 0x21 
        };
        
        // Hardcoded 16-byte IV (128 bits for AES)
        private static readonly byte[] StaticIV = new byte[16] 
        { 
            0x4E, 0x6F, 0x73, 0x74, 0x72, 0x55, 0x6E, 0x69, 
            0x74, 0x79, 0x53, 0x44, 0x4B, 0x5F, 0x49, 0x56 
        };

        private const string KeyPref = "nostr_private_key";

        public static void SaveKey(string privateKey)
        {
            if (string.IsNullOrEmpty(privateKey))
                throw new ArgumentException("Private key cannot be null or empty", nameof(privateKey));

            string encrypted = Encrypt(privateKey);
            PlayerPrefs.SetString(KeyPref, encrypted);
            PlayerPrefs.Save();
        }

        public static string LoadKey()
        {
            if (!PlayerPrefs.HasKey(KeyPref))
                return null;
            string encrypted = PlayerPrefs.GetString(KeyPref);
            return Decrypt(encrypted);
        }

        public static void DeleteKey()
        {
            PlayerPrefs.DeleteKey(KeyPref);
        }

        private static string Encrypt(string plainText)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = StaticKey;
                aes.IV = StaticIV;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                byte[] encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
                return Convert.ToBase64String(encryptedBytes);
            }
        }

        private static string Decrypt(string cipherText)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = StaticKey;
                aes.IV = StaticIV;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                byte[] cipherBytes = Convert.FromBase64String(cipherText);
                byte[] plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
                return Encoding.UTF8.GetString(plainBytes);
            }
        }
    }
} 