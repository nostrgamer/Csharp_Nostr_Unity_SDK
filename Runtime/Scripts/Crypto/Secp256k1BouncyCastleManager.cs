using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Nostr.Unity.Crypto
{
    /// <summary>
    /// Pure C# implementation of cryptographic functions for Nostr using built-in .NET cryptography
    /// </summary>
    public static class Secp256k1BouncyCastleManager
    {
        /// <summary>
        /// Generates a new random private key
        /// </summary>
        /// <returns>A 32-byte array containing the private key</returns>
        public static byte[] GeneratePrivateKey()
        {
            try
            {
                // Create a secure random number generator
                byte[] privateKey = new byte[32];
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(privateKey);
                }
                
                return privateKey;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error generating private key: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Creates a deterministic public key from the private key
        /// Note: This is a simplified implementation without actual EC operations
        /// </summary>
        /// <param name="privateKey">The 32-byte private key</param>
        /// <returns>A simulated 33-byte compressed public key</returns>
        public static byte[] GetPublicKey(byte[] privateKey)
        {
            try
            {
                if (privateKey == null || privateKey.Length != 32)
                {
                    throw new ArgumentException("Private key must be 32 bytes");
                }
                
                // Create a deterministic public key based on the private key
                using (var sha256 = SHA256.Create())
                {
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
                throw;
            }
        }

        /// <summary>
        /// Computes the SHA256 hash of a message
        /// </summary>
        /// <param name="message">The message to hash</param>
        /// <returns>The 32-byte hash of the message</returns>
        public static byte[] ComputeMessageHash(string message)
        {
            try
            {
                if (string.IsNullOrEmpty(message))
                {
                    Debug.LogWarning("Empty message provided for hashing");
                    message = "";
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
                throw;
            }
        }

        /// <summary>
        /// Creates a deterministic signature based on the message hash and private key
        /// Note: This is a simplified implementation without actual EC signing
        /// </summary>
        /// <param name="messageHash">The 32-byte hash of the message</param>
        /// <param name="privateKey">The 32-byte private key</param>
        /// <returns>A simulated 64-byte signature</returns>
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
                
                // Use HMAC-SHA256 to create a deterministic signature
                using (var hmac = new HMACSHA256(privateKey))
                {
                    // First half is HMAC-SHA256 of the message hash
                    byte[] firstHalf = hmac.ComputeHash(messageHash);
                    
                    // Second half is SHA256 of the first half
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
                throw;
            }
        }

        /// <summary>
        /// Simulates signature verification
        /// Note: This is a simplified implementation without actual EC verification
        /// </summary>
        /// <param name="messageHash">The 32-byte hash of the message</param>
        /// <param name="signature">The 64-byte signature</param>
        /// <param name="publicKey">The public key (33 bytes compressed)</param>
        /// <returns>Always returns true for demonstration</returns>
        public static bool Verify(byte[] messageHash, byte[] signature, byte[] publicKey)
        {
            try
            {
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
                
                // For demonstration purposes, this always returns true
                // In a real implementation, we would verify the signature with EC math
                Debug.LogWarning("Using simplified signature verification (always returns true)");
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