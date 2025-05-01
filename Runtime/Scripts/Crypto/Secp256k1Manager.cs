using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Nostr.Unity.Crypto;

namespace Nostr.Unity
{
    /// <summary>
    /// Flags for secp256k1 operations
    /// </summary>
    public enum Secp256k1Flags
    {
        SECP256K1_EC_COMPRESSED = (1 << 1),
        SECP256K1_EC_UNCOMPRESSED = (0 << 1)
    }
    
    /// <summary>
    /// Manages Secp256k1 cryptographic operations for Nostr
    /// </summary>
    public static class Secp256k1Manager
    {
        // Static instance of our secp256k1 wrapper
        private static Secp256k1Wrapper _secp256k1Instance;
        private static bool _isInitialized = false;
        private static bool _usingFallback = false;
        
        /// <summary>
        /// Initializes the Secp256k1 library
        /// </summary>
        public static bool Initialize()
        {
            try
            {
                if (_isInitialized)
                    return true;
                
                try
                {
                    // Try to create a new wrapper instance (native implementation)
                    _secp256k1Instance = new Secp256k1Wrapper();
                    _isInitialized = true;
                    _usingFallback = false;
                    Debug.Log("Secp256k1 native library initialized successfully");
                    return true;
                }
                catch (Exception ex)
                {
                    // If native implementation fails, use the fallback
                    Debug.LogWarning($"Native secp256k1 initialization failed: {ex.Message}. Using fallback implementation.");
                    _secp256k1Instance = null;
                    _isInitialized = true;
                    _usingFallback = true;
                    Debug.Log("Secp256k1 fallback implementation initialized");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to initialize Secp256k1 library: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Ensures the library is initialized before calling any cryptographic functions
        /// </summary>
        private static bool EnsureInitialized()
        {
            if (!_isInitialized)
                return Initialize();
            return true;
        }
        
        /// <summary>
        /// Generates a cryptographically secure random private key
        /// </summary>
        /// <returns>A 32-byte array representing the private key</returns>
        public static byte[] GeneratePrivateKey()
        {
            try
            {
                if (!EnsureInitialized())
                {
                    throw new InvalidOperationException("Secp256k1 library is not initialized");
                }
                
                if (_usingFallback)
                {
                    // Use fallback implementation
                    return Secp256k1FallbackManager.GeneratePrivateKey();
                }
                else
                {
                    // Use native implementation
                    // Generate a random 32-byte array for the private key
                    byte[] privateKey = new byte[Secp256k1Wrapper.PRIVKEY_LENGTH];
                    using (var rng = RandomNumberGenerator.Create())
                    {
                        rng.GetBytes(privateKey);
                    }
                    
                    // Ensure it's a valid private key for the curve
                    if (!_secp256k1Instance.SecretKeyVerify(privateKey))
                    {
                        Debug.LogWarning("Generated key was invalid for secp256k1, retrying...");
                        return GeneratePrivateKey(); // Try again
                    }
                    
                    return privateKey;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error generating private key: {ex.Message}");
                // If everything fails, use the fallback implementation
                return Secp256k1FallbackManager.GeneratePrivateKey();
            }
        }

        /// <summary>
        /// Derives a public key from a private key
        /// </summary>
        /// <param name="privateKey">The 32-byte private key</param>
        /// <returns>The public key (33 bytes compressed)</returns>
        public static byte[] GetPublicKey(byte[] privateKey)
        {
            try
            {
                if (!EnsureInitialized())
                {
                    throw new InvalidOperationException("Secp256k1 library is not initialized");
                }
                
                if (privateKey == null || privateKey.Length != Secp256k1Wrapper.PRIVKEY_LENGTH)
                {
                    throw new ArgumentException("Private key must be 32 bytes");
                }
                
                if (_usingFallback)
                {
                    // Use fallback implementation
                    return Secp256k1FallbackManager.GetPublicKey(privateKey);
                }
                else
                {
                    // Use native implementation
                    // Create a public key in the internal format
                    byte[] internalPubKey = new byte[Secp256k1Wrapper.PUBKEY_LENGTH];
                    if (!_secp256k1Instance.PublicKeyCreate(internalPubKey, privateKey))
                    {
                        throw new Exception("Failed to create public key");
                    }
                    
                    // Serialize to compressed format (33 bytes)
                    byte[] compressedPubKey = new byte[Secp256k1Wrapper.SERIALIZED_COMPRESSED_PUBKEY_LENGTH];
                    if (!_secp256k1Instance.PublicKeySerialize(compressedPubKey, internalPubKey, true))
                    {
                        throw new Exception("Failed to serialize public key");
                    }
                    
                    return compressedPubKey;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error deriving public key: {ex.Message}");
                // If everything fails, use the fallback implementation
                return Secp256k1FallbackManager.GetPublicKey(privateKey);
            }
        }

        /// <summary>
        /// Computes the SHA256 hash of a message (for signing)
        /// </summary>
        /// <param name="message">The message to hash</param>
        /// <returns>The 32-byte hash of the message</returns>
        public static byte[] ComputeMessageHash(string message)
        {
            // Both implementations use the same SHA256 method
            return Secp256k1FallbackManager.ComputeMessageHash(message);
        }

        /// <summary>
        /// Signs a message hash with a private key
        /// </summary>
        /// <param name="messageHash">The 32-byte hash of the message</param>
        /// <param name="privateKey">The 32-byte private key</param>
        /// <returns>The 64-byte signature</returns>
        public static byte[] Sign(byte[] messageHash, byte[] privateKey)
        {
            try
            {
                if (!EnsureInitialized())
                {
                    throw new InvalidOperationException("Secp256k1 library is not initialized");
                }
                
                if (messageHash == null || messageHash.Length != 32)
                {
                    throw new ArgumentException("Message hash must be 32 bytes");
                }
                
                if (privateKey == null || privateKey.Length != Secp256k1Wrapper.PRIVKEY_LENGTH)
                {
                    throw new ArgumentException("Private key must be 32 bytes");
                }
                
                if (_usingFallback)
                {
                    // Use fallback implementation
                    return Secp256k1FallbackManager.Sign(messageHash, privateKey);
                }
                else
                {
                    // Use native implementation
                    // Create a signature
                    byte[] signature = new byte[Secp256k1Wrapper.SIGNATURE_LENGTH];
                    if (!_secp256k1Instance.Sign(signature, messageHash, privateKey))
                    {
                        throw new Exception("Failed to sign message");
                    }
                    
                    return signature;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error signing message: {ex.Message}");
                // If everything fails, use the fallback implementation
                return Secp256k1FallbackManager.Sign(messageHash, privateKey);
            }
        }

        /// <summary>
        /// Verifies a signature
        /// </summary>
        /// <param name="messageHash">The 32-byte hash of the message</param>
        /// <param name="signature">The 64-byte signature</param>
        /// <param name="publicKey">The public key (33-byte compressed format)</param>
        /// <returns>True if the signature is valid, otherwise false</returns>
        public static bool Verify(byte[] messageHash, byte[] signature, byte[] publicKey)
        {
            try
            {
                if (!EnsureInitialized())
                {
                    throw new InvalidOperationException("Secp256k1 library is not initialized");
                }
                
                if (messageHash == null || messageHash.Length != 32)
                {
                    Debug.LogWarning("Invalid message hash");
                    return false;
                }
                
                if (signature == null || signature.Length != Secp256k1Wrapper.SIGNATURE_LENGTH)
                {
                    Debug.LogWarning("Invalid signature");
                    return false;
                }
                
                if (publicKey == null || publicKey.Length != Secp256k1Wrapper.SERIALIZED_COMPRESSED_PUBKEY_LENGTH)
                {
                    Debug.LogWarning("Invalid public key");
                    return false;
                }
                
                if (_usingFallback)
                {
                    // Use fallback implementation
                    return Secp256k1FallbackManager.Verify(messageHash, signature, publicKey);
                }
                else
                {
                    // Use native implementation
                    // Parse the compressed public key to get the internal format
                    byte[] internalPubKey = new byte[Secp256k1Wrapper.PUBKEY_LENGTH];
                    if (!_secp256k1Instance.PublicKeyParse(internalPubKey, publicKey))
                    {
                        Debug.LogWarning("Failed to parse public key");
                        return false;
                    }
                    
                    // Verify the signature
                    return _secp256k1Instance.Verify(signature, messageHash, internalPubKey);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error verifying signature: {ex.Message}");
                // If everything fails, use the fallback implementation
                return Secp256k1FallbackManager.Verify(messageHash, signature, publicKey);
            }
        }
        
        /// <summary>
        /// Returns true if using the fallback implementation
        /// </summary>
        public static bool IsUsingFallback()
        {
            EnsureInitialized();
            return _usingFallback;
        }
        
        /// <summary>
        /// Cleanup resources when application quits
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Cleanup()
        {
            if (_secp256k1Instance != null)
            {
                _secp256k1Instance.Dispose();
                _secp256k1Instance = null;
            }
            _isInitialized = false;
            _usingFallback = false;
        }
    }
} 