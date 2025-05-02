using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Nostr.Unity.Utils;
using System.IO;

namespace Nostr.Unity
{
    /// <summary>
    /// Manages Nostr private and public keys with secure storage
    /// </summary>
    public class NostrKeyManager
    {
        private const string KEY_STORAGE_PATH = "nostr_keys";
        private const string PRIVATE_KEY_FILE = "private_key";
        private const string PUBLIC_KEY_FILE = "public_key";
        private const int KEY_SIZE = 32;
        private const int IV_SIZE = 16;
        
        /// <summary>
        /// Generates a new private key
        /// </summary>
        /// <param name="useHex">Whether to return the key in hex format (true) or Bech32 format (false)</param>
        /// <returns>The hex-encoded or Bech32-encoded private key</returns>
        public string GeneratePrivateKey(bool useHex = true)
        {
            try
            {
                byte[] privateKey = new byte[KEY_SIZE];
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(privateKey);
                }
                
                if (useHex)
                {
                    return Bech32.BytesToHex(privateKey);
                }
                else
                {
                    return Bech32.Encode(NostrConstants.NSEC_PREFIX, privateKey);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error generating private key: {ex.Message}");
                throw;
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
            try
            {
                if (string.IsNullOrEmpty(privateKey))
                    throw new ArgumentException("Private key cannot be null or empty", nameof(privateKey));
                
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
                byte[] privateKeyBytes = Bech32.HexToBytes(hexPrivateKey);
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
                
                string hexPublicKey = Bech32.BytesToHex(nostrPublicKeyBytes);
                
                if (useHex)
                {
                    return hexPublicKey;
                }
                else
                {
                    return Bech32.Encode(NostrConstants.NPUB_PREFIX, nostrPublicKeyBytes);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error getting public key: {ex.Message}");
                throw;
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
            try
            {
                if (string.IsNullOrEmpty(message))
                    throw new ArgumentException("Message cannot be null or empty", nameof(message));
                
                if (string.IsNullOrEmpty(privateKey))
                    throw new ArgumentException("Private key cannot be null or empty", nameof(privateKey));
                
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
                
                // Convert the message to bytes
                byte[] messageBytes = Encoding.UTF8.GetBytes(message);
                
                // Compute the message hash
                byte[] messageHash = Secp256k1Manager.ComputeMessageHash(message);
                
                // Convert the private key to bytes
                byte[] privateKeyBytes = Bech32.HexToBytes(hexPrivateKey);
                
                // Generate the signature
                byte[] signatureBytes = Secp256k1Manager.Sign(messageHash, privateKeyBytes);
                
                // Convert to hex
                return Bech32.BytesToHex(signatureBytes);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error signing message: {ex.Message}");
                throw;
            }
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
                if (string.IsNullOrEmpty(message))
                    throw new ArgumentException("Message cannot be null or empty", nameof(message));
                
                if (string.IsNullOrEmpty(signature))
                    throw new ArgumentException("Signature cannot be null or empty", nameof(signature));
                
                if (string.IsNullOrEmpty(publicKey))
                    throw new ArgumentException("Public key cannot be null or empty", nameof(publicKey));
                
                string hexPublicKey = publicKey;
                
                // Check if the key is in Bech32 format
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
                        return false;
                    }
                }
                
                // Compute the message hash
                byte[] messageHash = Secp256k1Manager.ComputeMessageHash(message);
                
                // Convert the signature to bytes
                byte[] signatureBytes = Bech32.HexToBytes(signature);
                
                // Convert the public key to bytes
                byte[] publicKeyBytes = Bech32.HexToBytes(hexPublicKey);
                
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
        /// Stores the keys with encryption
        /// </summary>
        /// <param name="privateKey">The private key to store</param>
        /// <param name="password">Password for encryption</param>
        /// <returns>True if the keys were stored successfully</returns>
        public bool StoreKeys(string privateKey, string password)
        {
            try
            {
                if (string.IsNullOrEmpty(privateKey))
                    throw new ArgumentException("Private key cannot be null or empty", nameof(privateKey));
                
                if (string.IsNullOrEmpty(password))
                    throw new ArgumentException("Password cannot be null or empty", nameof(password));
                
                // Get the public key
                string publicKey = GetPublicKey(privateKey);
                
                // Create the storage directory if it doesn't exist
                string storagePath = Path.Combine(Application.persistentDataPath, KEY_STORAGE_PATH);
                Directory.CreateDirectory(storagePath);
                
                // Generate a random IV
                byte[] iv = new byte[IV_SIZE];
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(iv);
                }
                
                // Encrypt the private key
                byte[] encryptedPrivateKey = EncryptKey(privateKey, password, iv);
                
                // Store the private key with IV
                string privateKeyPath = Path.Combine(storagePath, PRIVATE_KEY_FILE);
                using (var fs = new FileStream(privateKeyPath, FileMode.Create))
                {
                    fs.Write(iv, 0, iv.Length);
                    fs.Write(encryptedPrivateKey, 0, encryptedPrivateKey.Length);
                }
                
                // Store the public key
                string publicKeyPath = Path.Combine(storagePath, PUBLIC_KEY_FILE);
                File.WriteAllText(publicKeyPath, publicKey);
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error storing keys: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Loads the private key from storage
        /// </summary>
        /// <param name="password">Password for decryption</param>
        /// <param name="useBech32">Whether to return the key in Bech32 format</param>
        /// <returns>The private key, or null if not found or decryption failed</returns>
        public string LoadPrivateKey(string password, bool useBech32 = false)
        {
            try
            {
                if (string.IsNullOrEmpty(password))
                    throw new ArgumentException("Password cannot be null or empty", nameof(password));
                
                string privateKeyPath = Path.Combine(Application.persistentDataPath, KEY_STORAGE_PATH, PRIVATE_KEY_FILE);
                
                if (!File.Exists(privateKeyPath))
                {
                    Debug.Log("No stored private key found");
                    return null;
                }
                
                // Read the IV and encrypted key
                byte[] fileData = File.ReadAllBytes(privateKeyPath);
                if (fileData.Length < IV_SIZE)
                {
                    Debug.LogError("Invalid key file format");
                    return null;
                }
                
                byte[] iv = new byte[IV_SIZE];
                byte[] encryptedKey = new byte[fileData.Length - IV_SIZE];
                
                Array.Copy(fileData, 0, iv, 0, IV_SIZE);
                Array.Copy(fileData, IV_SIZE, encryptedKey, 0, encryptedKey.Length);
                
                // Decrypt the key
                string privateKey = DecryptKey(encryptedKey, password, iv);
                
                if (useBech32)
                {
                    return Bech32.EncodeHex(NostrConstants.NSEC_PREFIX, privateKey);
                }
                
                return privateKey;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error loading private key: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Loads the public key from storage
        /// </summary>
        /// <param name="useBech32">Whether to return the key in Bech32 format</param>
        /// <returns>The public key, or null if not found</returns>
        public string LoadPublicKey(bool useBech32 = false)
        {
            try
            {
                string publicKeyPath = Path.Combine(Application.persistentDataPath, KEY_STORAGE_PATH, PUBLIC_KEY_FILE);
                
                if (!File.Exists(publicKeyPath))
                {
                    Debug.Log("No stored public key found");
                    return null;
                }
                
                string publicKey = File.ReadAllText(publicKeyPath);
                
                if (useBech32)
                {
                    return Bech32.EncodeHex(NostrConstants.NPUB_PREFIX, publicKey);
                }
                
                return publicKey;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error loading public key: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Deletes the stored keys
        /// </summary>
        public void DeleteStoredKeys()
        {
            try
            {
                string storagePath = Path.Combine(Application.persistentDataPath, KEY_STORAGE_PATH);
                if (Directory.Exists(storagePath))
                {
                    Directory.Delete(storagePath, true);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error deleting stored keys: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Checks if keys are stored
        /// </summary>
        /// <returns>True if keys are stored, otherwise false</returns>
        public bool HasStoredKeys()
        {
            string privateKeyPath = Path.Combine(Application.persistentDataPath, KEY_STORAGE_PATH, PRIVATE_KEY_FILE);
            return File.Exists(privateKeyPath);
        }
        
        private byte[] EncryptKey(string key, string password, byte[] iv)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = SHA256.HashData(Encoding.UTF8.GetBytes(password));
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                
                using (var encryptor = aes.CreateEncryptor())
                using (var ms = new MemoryStream())
                {
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        byte[] keyBytes = Encoding.UTF8.GetBytes(key);
                        cs.Write(keyBytes, 0, keyBytes.Length);
                    }
                    return ms.ToArray();
                }
            }
        }
        
        private string DecryptKey(byte[] encryptedKey, string password, byte[] iv)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = SHA256.HashData(Encoding.UTF8.GetBytes(password));
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                
                using (var decryptor = aes.CreateDecryptor())
                using (var ms = new MemoryStream(encryptedKey))
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (var sr = new StreamReader(cs))
                {
                    return sr.ReadToEnd();
                }
            }
        }
    }
} 