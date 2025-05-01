using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Nostr.Unity.Utils;

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
        /// <param name="useHex">Whether to return the key in hex format (true) or Bech32 format (false)</param>
        /// <returns>The hex-encoded or Bech32-encoded private key</returns>
        public string GeneratePrivateKey(bool useHex = true)
        {
            // TODO: Replace with proper Secp256k1 key generation
            // This is a placeholder - we need to implement proper key generation
            
            // For now, generate a 32-byte random array
            byte[] privateKeyBytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(privateKeyBytes);
            }
            
            string hexKey = BytesToHex(privateKeyBytes);
            
            if (useHex)
            {
                return hexKey;
            }
            else
            {
                return Bech32.EncodeHex(NostrConstants.NSEC_PREFIX, hexKey);
            }
        }
        
        /// <summary>
        /// Derives a public key from a private key
        /// </summary>
        /// <param name="privateKey">The private key (hex or Bech32 format)</param>
        /// <param name="useHex">Whether to return the key in hex format (true) or Bech32 format (false)</param>
        /// <returns>The hex-encoded or Bech32-encoded public key</returns>
        public string GetPublicKey(string privateKey, bool useHex = true)
        {
            string hexPrivateKey = privateKey;
            
            // Check if the key is in Bech32 format
            if (privateKey.StartsWith(NostrConstants.NSEC_PREFIX + "1"))
            {
                try
                {
                    (string prefix, string data) = Bech32.DecodeToHex(privateKey);
                    if (prefix != NostrConstants.NSEC_PREFIX)
                    {
                        throw new ArgumentException("Invalid nsec prefix");
                    }
                    hexPrivateKey = data;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error decoding Bech32 private key: {ex.Message}");
                    throw;
                }
            }
            
            // TODO: Replace with proper Secp256k1 public key derivation
            // This is a placeholder - we need to implement proper key derivation
            
            // For now, just hash the private key to simulate a public key
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] privateKeyBytes = HexToBytes(hexPrivateKey);
                byte[] publicKeyBytes = sha256.ComputeHash(privateKeyBytes);
                
                string hexPublicKey = BytesToHex(publicKeyBytes);
                
                if (useHex)
                {
                    return hexPublicKey;
                }
                else
                {
                    return Bech32.EncodeHex(NostrConstants.NPUB_PREFIX, hexPublicKey);
                }
            }
        }
        
        /// <summary>
        /// Stores the private key in PlayerPrefs
        /// </summary>
        /// <param name="privateKey">The private key to store (hex or Bech32 format)</param>
        /// <param name="encrypt">Whether to encrypt the key in storage</param>
        /// <returns>True if successful, otherwise false</returns>
        public bool StoreKeys(string privateKey, bool encrypt = true)
        {
            try
            {
                string hexPrivateKey = privateKey;
                
                // Check if the key is in Bech32 format
                if (privateKey.StartsWith(NostrConstants.NSEC_PREFIX + "1"))
                {
                    try
                    {
                        (string prefix, string data) = Bech32.DecodeToHex(privateKey);
                        if (prefix != NostrConstants.NSEC_PREFIX)
                        {
                            throw new ArgumentException("Invalid nsec prefix");
                        }
                        hexPrivateKey = data;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error decoding Bech32 private key: {ex.Message}");
                        throw;
                    }
                }
                
                string valueToStore = hexPrivateKey;
                
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
                    valueToStore = EncryptDecrypt(hexPrivateKey, savedEncryptionKey);
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
        /// <param name="useBech32">Whether to return the key in Bech32 format</param>
        /// <returns>The private key, or null if not found</returns>
        public string LoadPrivateKey(bool useBech32 = false)
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
                
                if (useBech32)
                {
                    return Bech32.EncodeHex(NostrConstants.NSEC_PREFIX, storedValue);
                }
                else
                {
                    return storedValue;
                }
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
            return Bech32.HexToBytes(hex);
        }
        
        /// <summary>
        /// Converts a byte array to hex string
        /// </summary>
        private string BytesToHex(byte[] bytes)
        {
            return Bech32.BytesToHex(bytes);
        }
    }
} 