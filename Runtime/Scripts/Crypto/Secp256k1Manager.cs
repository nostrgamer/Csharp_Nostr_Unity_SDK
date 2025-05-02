using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Nostr.Unity.Crypto;

namespace Nostr.Unity
{
    /// <summary>
    /// Manages Secp256k1 cryptographic operations for Nostr
    /// </summary>
    public static class Secp256k1Manager
    {
        private static bool _isInitialized = false;
        
        /// <summary>
        /// Initializes the Secp256k1 library
        /// </summary>
        public static bool Initialize()
        {
            try
            {
                if (_isInitialized)
                    return true;
                
                _isInitialized = true;
                Debug.Log("Secp256k1 BouncyCastle implementation initialized");
                return true;
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
                
                return Secp256k1BouncyCastleManager.GeneratePrivateKey();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error generating private key: {ex.Message}");
                // If an error occurs, try to generate a key anyway
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
                
                if (privateKey == null || privateKey.Length != 32)
                {
                    throw new ArgumentException("Private key must be 32 bytes");
                }
                
                return Secp256k1BouncyCastleManager.GetPublicKey(privateKey);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error deriving public key: {ex.Message}");
                // If an error occurs, use the fallback implementation
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
            return Secp256k1BouncyCastleManager.ComputeMessageHash(message);
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
                
                if (privateKey == null || privateKey.Length != 32)
                {
                    throw new ArgumentException("Private key must be 32 bytes");
                }
                
                return Secp256k1BouncyCastleManager.Sign(messageHash, privateKey);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error signing message: {ex.Message}");
                // If an error occurs, use the fallback implementation
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
                    throw new ArgumentException("Message hash must be 32 bytes");
                }
                
                if (signature == null || signature.Length != 64)
                {
                    throw new ArgumentException("Signature must be 64 bytes");
                }
                
                if (publicKey == null || publicKey.Length != 33)
                {
                    throw new ArgumentException("Public key must be 33 bytes (compressed format)");
                }
                
                return Secp256k1BouncyCastleManager.Verify(messageHash, signature, publicKey);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error verifying signature: {ex.Message}");
                // If an error occurs, use the fallback implementation
                return Secp256k1FallbackManager.Verify(messageHash, signature, publicKey);
            }
        }

        /// <summary>
        /// Unity callback to clean up resources when the application is stopped or restarted
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Cleanup()
        {
            _isInitialized = false;
        }
    }
} 