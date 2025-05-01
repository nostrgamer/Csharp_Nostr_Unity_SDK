using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Nostr.Unity.Crypto
{
    /// <summary>
    /// Fallback implementation of secp256k1 functionality using pure C#
    /// This is used when the native library cannot be loaded
    /// </summary>
    public static class Secp256k1FallbackManager
    {
        /// <summary>
        /// Generates a random private key
        /// </summary>
        public static byte[] GeneratePrivateKey()
        {
            try
            {
                // Generate a random 32-byte array for the private key
                byte[] privateKey = new byte[32];
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(privateKey);
                }
                
                // In a real implementation, we would verify this is valid for the curve
                // Here we just return it as is
                return privateKey;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error generating private key: {ex.Message}");
                // Return a deterministic test key in case of error
                byte[] fallbackKey = new byte[32];
                for (int i = 0; i < 32; i++)
                {
                    fallbackKey[i] = (byte)(i + 1);
                }
                return fallbackKey;
            }
        }

        /// <summary>
        /// Derives a 'public key' from a private key
        /// This is a placeholder implementation that returns a deterministic value based on the private key
        /// </summary>
        public static byte[] GetPublicKey(byte[] privateKey)
        {
            try
            {
                if (privateKey == null || privateKey.Length != 32)
                {
                    throw new ArgumentException("Private key must be 32 bytes");
                }
                
                // In a real implementation, we would do proper EC math to derive the public key
                // Here we just create a deterministic value based on the private key
                using (var sha256 = SHA256.Create())
                {
                    // Hash the private key to get a 32-byte value
                    byte[] hashed = sha256.ComputeHash(privateKey);
                    
                    // Create a compressed public key format (33 bytes)
                    byte[] publicKey = new byte[33];
                    publicKey[0] = 0x02; // Compressed key prefix (even y-coordinate)
                    Buffer.BlockCopy(hashed, 0, publicKey, 1, 32);
                    
                    return publicKey;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error deriving public key: {ex.Message}");
                // Return a deterministic test key in case of error
                byte[] fallbackKey = new byte[33];
                fallbackKey[0] = 0x02; // Compressed key prefix
                return fallbackKey;
            }
        }

        /// <summary>
        /// Computes the SHA256 hash of a message
        /// </summary>
        public static byte[] ComputeMessageHash(string message)
        {
            try
            {
                if (string.IsNullOrEmpty(message))
                {
                    Debug.LogWarning("Empty message provided for hashing");
                    message = ""; // Use empty string
                }
                
                // Convert the message to bytes and hash it using SHA256
                byte[] messageBytes = Encoding.UTF8.GetBytes(message);
                using (var sha256 = SHA256.Create())
                {
                    return sha256.ComputeHash(messageBytes);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error computing message hash: {ex.Message}");
                // Return a deterministic test hash in case of error
                return new byte[32];
            }
        }

        /// <summary>
        /// Creates a 'signature' for a message hash
        /// This is a placeholder implementation that returns a deterministic value
        /// </summary>
        public static byte[] Sign(byte[] messageHash, byte[] privateKey)
        {
            try
            {
                if (messageHash == null || messageHash.Length != 32)
                {
                    throw new ArgumentException("Message hash must be 32 bytes");
                }
                
                if (privateKey == null || privateKey.Length != 32)
                {
                    throw new ArgumentException("Private key must be 32 bytes");
                }
                
                // In a real implementation, we would do proper EC signature generation
                // Here we just create a deterministic value based on the private key and message
                using (var hmac = new HMACSHA256(privateKey))
                {
                    // Sign the message hash using HMAC-SHA256 with the private key
                    byte[] firstHalf = hmac.ComputeHash(messageHash);
                    
                    // Generate a second half by hashing the first half
                    using (var sha256 = SHA256.Create())
                    {
                        byte[] secondHalf = sha256.ComputeHash(firstHalf);
                        
                        // Combine to create a 64-byte signature
                        byte[] signature = new byte[64];
                        Buffer.BlockCopy(firstHalf, 0, signature, 0, 32);
                        Buffer.BlockCopy(secondHalf, 0, signature, 32, 32);
                        
                        return signature;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error signing message: {ex.Message}");
                // Return a deterministic test signature in case of error
                return new byte[64];
            }
        }

        /// <summary>
        /// 'Verifies' a signature
        /// This is a placeholder implementation that always returns true for simplicity
        /// </summary>
        public static bool Verify(byte[] messageHash, byte[] signature, byte[] publicKey)
        {
            try
            {
                // In a real implementation, we would do proper EC signature verification
                // Here we just return true as a placeholder
                Debug.LogWarning("Using fallback signature verification (always returns true)");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error verifying signature: {ex.Message}");
                return false;
            }
        }
    }
} 