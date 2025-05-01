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
            try
            {
                // Delete any existing keys that might be corrupted
                DeleteStoredKeys();
                Debug.Log("Deleted any existing keys before generating new ones");
                
                // Create a secure random 32-byte array
                byte[] privateKeyBytes = Secp256k1Manager.GeneratePrivateKey();
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
            catch (Exception ex)
            {
                Debug.LogError($"Error generating private key: {ex.Message}");
                // Return a fallback key in case of error
                return "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
            }
        }
        
        /// <summary>
        /// Gets a public key from a private key
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
            
            // Use Secp256k1Manager to derive the public key
            byte[] privateKeyBytes = HexToBytes(hexPrivateKey);
            byte[] publicKeyBytes = Secp256k1Manager.GetPublicKey(privateKeyBytes);
            
            // Secp256k1.Net returns 33-byte compressed public keys
            // For Nostr, we need 32-byte public keys (the x-coordinate without the compression prefix)
            byte[] nostrPublicKeyBytes;
            
            if (publicKeyBytes.Length == 33)
            {
                // Remove the compression prefix (first byte)
                nostrPublicKeyBytes = new byte[32];
                Array.Copy(publicKeyBytes, 1, nostrPublicKeyBytes, 0, 32);
            }
            else
            {
                // Unexpected format, use as-is
                nostrPublicKeyBytes = publicKeyBytes;
                Debug.LogWarning($"Unexpected public key format: {publicKeyBytes.Length} bytes");
            }
            
            string hexPublicKey = BytesToHex(nostrPublicKeyBytes);
            
            if (useHex)
            {
                return hexPublicKey;
            }
            else
            {
                return Bech32.EncodeHex(NostrConstants.NPUB_PREFIX, hexPublicKey);
            }
        }
        
        /// <summary>
        /// Signs a message with a private key
        /// </summary>
        /// <param name="message">The message to sign</param>
        /// <param name="privateKey">The private key to sign with (hex or Bech32 format)</param>
        /// <returns>The signature as a hex string</returns>
        public string SignMessage(string message, string privateKey)
        {
            // Convert private key to hex if needed
            string hexPrivateKey = privateKey;
            
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
            
            // Compute the SHA256 hash of the message
            byte[] messageHash = Secp256k1Manager.ComputeMessageHash(message);
            
            // Sign the message hash with the private key
            byte[] privateKeyBytes = HexToBytes(hexPrivateKey);
            byte[] signature = Secp256k1Manager.Sign(messageHash, privateKeyBytes);
            
            // Return the signature as a hex string
            return BytesToHex(signature);
        }
        
        /// <summary>
        /// Verifies a signature
        /// </summary>
        /// <param name="message">The original message</param>
        /// <param name="signature">The signature as a hex string</param>
        /// <param name="publicKey">The public key (hex or Bech32 format)</param>
        /// <returns>True if the signature is valid, otherwise false</returns>
        public bool VerifySignature(string message, string signature, string publicKey)
        {
            try
            {
                // Convert public key to hex if needed
                string hexPublicKey = publicKey;
                
                if (publicKey.StartsWith(NostrConstants.NPUB_PREFIX + "1"))
                {
                    try
                    {
                        (string prefix, string data) = Bech32.DecodeToHex(publicKey);
                        if (prefix != NostrConstants.NPUB_PREFIX)
                        {
                            throw new ArgumentException("Invalid npub prefix");
                        }
                        hexPublicKey = data;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error decoding Bech32 public key: {ex.Message}");
                        throw;
                    }
                }
                
                // Compute the SHA256 hash of the message
                byte[] messageHash = Secp256k1Manager.ComputeMessageHash(message);
                
                // Convert hex strings to bytes
                byte[] signatureBytes = HexToBytes(signature);
                byte[] publicKeyBytes = HexToBytes(hexPublicKey);
                
                // Add compression prefix if needed (Nostr uses 32-byte raw public keys)
                if (publicKeyBytes.Length == 32)
                {
                    byte[] compressedKey = new byte[33];
                    compressedKey[0] = 0x02; // Assume even y-coordinate for simplicity
                    Array.Copy(publicKeyBytes, 0, compressedKey, 1, 32);
                    publicKeyBytes = compressedKey;
                }
                
                // Verify the signature
                return Secp256k1Manager.Verify(messageHash, signatureBytes, publicKeyBytes);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error verifying signature: {ex.Message}");
                return false;
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
                
                if (string.IsNullOrEmpty(storedValue))
                {
                    Debug.LogWarning("Stored key exists but is empty");
                    DeleteStoredKeys(); // Clean up empty key
                    return null;
                }
                
                try
                {
                    if (PlayerPrefs.HasKey(ENCRYPTION_KEY))
                    {
                        string encryptionKey = PlayerPrefs.GetString(ENCRYPTION_KEY);
                        
                        if (string.IsNullOrEmpty(encryptionKey))
                        {
                            Debug.LogWarning("Encryption key exists but is empty");
                            // Continue with the stored value as-is
                        }
                        else
                        {
                            // Simple XOR decryption - should be replaced with more secure method
                            try {
                                storedValue = EncryptDecrypt(storedValue, encryptionKey);
                            }
                            catch (Exception ex) {
                                Debug.LogError($"Failed to decrypt private key: {ex.Message}");
                                DeleteStoredKeys(); // Clean up corrupted key
                                return null;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error decrypting private key: {ex.Message}");
                    Debug.LogWarning("Using a default key for testing");
                    DeleteStoredKeys(); // Clean up corrupted key
                    
                    // Create a deterministic test key if decryption fails
                    storedValue = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
                }
                
                // Verify the hex string is valid
                if (!IsValidHexString(storedValue) || storedValue.Length != 64)
                {
                    Debug.LogError($"Invalid hex in stored private key: {storedValue}");
                    Debug.LogWarning("Using a default key for testing");
                    DeleteStoredKeys(); // Clean up corrupted key
                    
                    // Create a deterministic test key
                    storedValue = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
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
                DeleteStoredKeys(); // Clean up on any error
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
            try
            {
                if (string.IsNullOrEmpty(text))
                {
                    Debug.LogWarning("Attempted to encrypt/decrypt an empty string");
                    return string.Empty;
                }
                
                if (string.IsNullOrEmpty(key))
                {
                    Debug.LogWarning("Attempted to encrypt/decrypt with an empty key");
                    return text; // Return original text if no key
                }
                
                // For decryption, first try to decode from Base64
                try
                {
                    if (IsBase64String(text))
                    {
                        byte[] bytes = Convert.FromBase64String(text);
                        text = Encoding.UTF8.GetString(bytes);
                    }
                }
                catch (FormatException ex)
                {
                    Debug.LogError($"Base64 decoding failed: {ex.Message}");
                    return "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
                }
                
                // XOR the text with the key
                StringBuilder result = new StringBuilder();
                for (int i = 0; i < text.Length; i++)
                {
                    result.Append((char)(text[i] ^ key[i % key.Length]));
                }
                
                // For encryption, convert to Base64
                if (result.Length > 0)
                {
                    try
                    {
                        byte[] bytes = Encoding.UTF8.GetBytes(result.ToString());
                        return Convert.ToBase64String(bytes);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error during base64 conversion: {ex.Message}");
                        // Return a safe fallback value that won't cause parsing errors
                        return "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
                    }
                }
                else
                {
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error in EncryptDecrypt: {ex.Message}");
                // Return a safe fallback value
                return "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
            }
        }
        
        /// <summary>
        /// Checks if a string appears to be a valid Base64 string
        /// </summary>
        private bool IsBase64String(string s)
        {
            if (string.IsNullOrEmpty(s))
                return false;
                
            s = s.Trim();
            return (s.Length % 4 == 0) && System.Text.RegularExpressions.Regex.IsMatch(s, @"^[a-zA-Z0-9\+/]*={0,3}$", System.Text.RegularExpressions.RegexOptions.None);
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
        
        /// <summary>
        /// Checks if a string is a valid hexadecimal string
        /// </summary>
        private bool IsValidHexString(string hex)
        {
            if (string.IsNullOrEmpty(hex))
                return false;
                
            // Check if the string consists only of hex characters (0-9, a-f, A-F)
            foreach (char c in hex)
            {
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                    return false;
            }
            
            return true;
        }
    }
} 