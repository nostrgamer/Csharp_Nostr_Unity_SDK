using System;
using System.Security.Cryptography;
using UnityEngine;
using Nostr.Unity.Crypto.Recovery;

namespace Nostr.Unity
{
    /// <summary>
    /// Manages Secp256k1 cryptographic operations for Nostr
    /// </summary>
    public static class Secp256k1Manager
    {
        private static bool _isInitialized = false;
        private static BouncyCryptography _cryptoProvider;
        
        /// <summary>
        /// Initializes the Secp256k1 library
        /// </summary>
        public static bool Initialize()
        {
            try
            {
                if (_isInitialized)
                    return true;
                
                // Initialize the BouncyCastle manager
                if (!BouncyCastleManager.Initialize())
                {
                    Debug.LogError("Failed to initialize BouncyCastle manager");
                    return false;
                }
                
                _cryptoProvider = new BouncyCryptography();
                _isInitialized = true;
                
                Debug.Log("Secp256k1 manager initialized successfully");
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
            if (!EnsureInitialized())
            {
                throw new InvalidOperationException("Secp256k1 library is not initialized");
            }
            
            return _cryptoProvider.GeneratePrivateKey();
        }

        /// <summary>
        /// Derives a public key from a private key
        /// </summary>
        /// <param name="privateKey">The 32-byte private key</param>
        /// <returns>The public key (33 bytes compressed)</returns>
        public static byte[] GetPublicKey(byte[] privateKey)
        {
            if (!EnsureInitialized())
            {
                throw new InvalidOperationException("Secp256k1 library is not initialized");
            }
            
            if (privateKey == null || privateKey.Length != 32)
            {
                throw new ArgumentException("Private key must be 32 bytes");
            }
            
            return _cryptoProvider.GetPublicKey(privateKey);
        }

        /// <summary>
        /// Computes the SHA256 hash of a message (for signing)
        /// </summary>
        /// <param name="message">The message to hash</param>
        /// <returns>The 32-byte hash of the message</returns>
        public static byte[] ComputeMessageHash(string message)
        {
            if (!EnsureInitialized())
            {
                throw new InvalidOperationException("Secp256k1 library is not initialized");
            }
            
            return _cryptoProvider.ComputeMessageHash(message);
        }

        /// <summary>
        /// Signs a message hash with a private key
        /// </summary>
        /// <param name="messageHash">The 32-byte hash of the message</param>
        /// <param name="privateKey">The 32-byte private key</param>
        /// <returns>The 64-byte signature</returns>
        public static byte[] Sign(byte[] messageHash, byte[] privateKey)
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
            
            return _cryptoProvider.Sign(messageHash, privateKey);
        }
        
        /// <summary>
        /// Signs a message hash with a private key and returns a recoverable signature
        /// </summary>
        /// <param name="messageHash">The 32-byte hash of the message</param>
        /// <param name="privateKey">The 32-byte private key</param>
        /// <returns>The 65-byte recoverable signature (recovery ID + signature)</returns>
        public static byte[] SignRecoverable(byte[] messageHash, byte[] privateKey)
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
            
            // Sign the message
            byte[] signature = _cryptoProvider.Sign(messageHash, privateKey);
            
            // Get the public key (needed for recovery ID computation)
            byte[] publicKey = GetPublicKey(privateKey);
            
            // Extract r and s values (first and second half of the signature)
            byte[] r = new byte[32];
            byte[] s = new byte[32];
            Buffer.BlockCopy(signature, 0, r, 0, 32);
            Buffer.BlockCopy(signature, 32, s, 0, 32);
            
            // Create a recoverable signature
            var recoverableSignature = new NostrRecoverableSignature(r, s, messageHash, publicKey);
            
            // Return the 65-byte recoverable signature
            return recoverableSignature.RecoverableSignature;
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
            if (!EnsureInitialized())
            {
                throw new InvalidOperationException("Secp256k1 library is not initialized");
            }
            
            if (messageHash == null || messageHash.Length != 32)
            {
                throw new ArgumentException("Message hash must be 32 bytes");
            }
            
            if (signature == null || (signature.Length != 64 && signature.Length != 65))
            {
                throw new ArgumentException("Signature must be 64 or 65 bytes");
            }
            
            if (publicKey == null || publicKey.Length != 33)
            {
                throw new ArgumentException("Public key must be 33 bytes (compressed format)");
            }
            
            // If this is a recoverable signature (65 bytes), extract the regular signature (64 bytes)
            byte[] regularSignature = signature;
            if (signature.Length == 65)
            {
                regularSignature = new byte[64];
                Buffer.BlockCopy(signature, 1, regularSignature, 0, 64);
            }
            
            return _cryptoProvider.Verify(regularSignature, messageHash, publicKey);
        }
        
        /// <summary>
        /// Recovers a public key from a signature and message hash
        /// </summary>
        /// <param name="recoverySignature">The 65-byte recoverable signature</param>
        /// <param name="messageHash">The 32-byte hash of the message</param>
        /// <returns>The 33-byte compressed public key</returns>
        public static byte[] RecoverPublicKey(byte[] recoverySignature, byte[] messageHash)
        {
            if (!EnsureInitialized())
            {
                throw new InvalidOperationException("Secp256k1 library is not initialized");
            }
            
            if (recoverySignature == null || recoverySignature.Length != 65)
            {
                throw new ArgumentException("Recoverable signature must be 65 bytes");
            }
            
            if (messageHash == null || messageHash.Length != 32)
            {
                throw new ArgumentException("Message hash must be 32 bytes");
            }
            
            // The proper implementation will be added when BouncyCastle is available
            // For now, we just return a placeholder
            Debug.LogWarning("Public key recovery is not yet implemented");
            return new byte[33] { 0x02 };
        }

        /// <summary>
        /// Unity callback to clean up resources when the application is stopped or restarted
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Cleanup()
        {
            _isInitialized = false;
            _cryptoProvider = null;
        }
    }
} 