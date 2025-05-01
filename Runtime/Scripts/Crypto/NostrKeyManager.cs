using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Nostr.Unity
{
    /// <summary>
    /// Manages Nostr private and public keys
    /// </summary>
    public class NostrKeyManager
    {
        private const string NSEC_KEY = "NOSTR_NSEC";
        private const string ENCRYPTION_KEY = "NOSTR_ENCRYPTION_KEY";
        
        /// <summary>
        /// Generates a new private key
        /// </summary>
        /// <returns>The hex-encoded private key</returns>
        public string GeneratePrivateKey()
        {
            // TODO: Replace with proper Secp256k1 key generation
            // This is a placeholder - we need to implement proper key generation
            
            // For now, generate a 32-byte random array
            byte[] privateKeyBytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(privateKeyBytes);
            }
            
            // Convert to hex string
            StringBuilder sb = new StringBuilder();
            foreach (byte b in privateKeyBytes)
            {
                sb.Append(b.ToString("x2"));
            }
            
            return sb.ToString();
        }
        
        /// <summary>
        /// Derives a public key from a private key
        /// </summary>
        /// <param name="privateKey">The hex-encoded private key</param>
        /// <returns>The hex-encoded public key</returns>
        public string GetPublicKey(string privateKey)
        {
            // TODO: Replace with proper Secp256k1 public key derivation
            // This is a placeholder - we need to implement proper key derivation
            
            // For now, just hash the private key to simulate a public key
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] privateKeyBytes = HexToBytes(privateKey);
                byte[] publicKeyBytes = sha256.ComputeHash(privateKeyBytes);
                
                // Convert to hex string
                StringBuilder sb = new StringBuilder();
                foreach (byte b in publicKeyBytes)
                {
                    sb.Append(b.ToString("x2"));
                }
                
                return sb.ToString();
            }
        }
        
        /// <summary>
        /// Stores the private key in PlayerPrefs
        /// </summary>
        /// <param name="privateKey">The hex-encoded private key to store</param>
        /// <param name="encrypt">Whether to encrypt the key in storage</param>
        /// <returns>True if successful, otherwise false</returns>
        public bool StoreKeys(string privateKey, bool encrypt = true)
        {
            try
            {
                string valueToStore = privateKey;
                
                if (encrypt)
                {
                    // Generate encryption key if not exists
                    if (!PlayerPrefs.HasKey(ENCRYPTION_KEY))
                    {
                        string newEncryptionKey = Guid.NewGuid().ToString();
                        PlayerPrefs.SetString(ENCRYPTION_KEY, newEncryptionKey);
                    }
                    
                    // Simple XOR encryption - should be replaced with more secure method
                    string savedEncryptionKey = PlayerPrefs.GetString(ENCRYPTION_KEY);
                    valueToStore = EncryptDecrypt(privateKey, savedEncryptionKey);
                }
                
                PlayerPrefs.SetString(NSEC_KEY, valueToStore);
                PlayerPrefs.Save();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to store keys: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Loads the private key from PlayerPrefs
        /// </summary>
        /// <returns>The hex-encoded private key, or null if not found</returns>
        public string LoadPrivateKey()
        {
            try
            {
                if (!PlayerPrefs.HasKey(NSEC_KEY))
                {
                    Debug.Log("No stored private key found");
                    return null;
                }
                
                string storedValue = PlayerPrefs.GetString(NSEC_KEY);
                
                if (PlayerPrefs.HasKey(ENCRYPTION_KEY))
                {
                    // Simple XOR decryption - should be replaced with more secure method
                    string encryptionKey = PlayerPrefs.GetString(ENCRYPTION_KEY);
                    storedValue = EncryptDecrypt(storedValue, encryptionKey);
                }
                
                return storedValue;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load private key: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Deletes the stored private key
        /// </summary>
        public void DeleteStoredKeys()
        {
            PlayerPrefs.DeleteKey(NSEC_KEY);
            PlayerPrefs.DeleteKey(ENCRYPTION_KEY);
            PlayerPrefs.Save();
        }
        
        /// <summary>
        /// Checks if a private key is stored
        /// </summary>
        /// <returns>True if a private key is stored, otherwise false</returns>
        public bool HasStoredKeys()
        {
            return PlayerPrefs.HasKey(NSEC_KEY);
        }
        
        /// <summary>
        /// Simple XOR encryption/decryption - should be replaced with more secure method
        /// </summary>
        private string EncryptDecrypt(string text, string key)
        {
            StringBuilder result = new StringBuilder();
            for (int i = 0; i < text.Length; i++)
            {
                result.Append((char)(text[i] ^ key[i % key.Length]));
            }
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(result.ToString()));
        }
        
        /// <summary>
        /// Converts a hex string to byte array
        /// </summary>
        private byte[] HexToBytes(string hex)
        {
            if (hex.Length % 2 == 1)
            {
                throw new ArgumentException("Hex string must have an even length");
            }
            
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < hex.Length; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            }
            
            return bytes;
        }
    }
} 