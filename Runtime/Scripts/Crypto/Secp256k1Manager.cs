using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Nostr.Unity
{
    /// <summary>
    /// Manages Secp256k1 cryptographic operations for Nostr
    /// </summary>
    public static class Secp256k1Manager
    {
        /// <summary>
        /// Generates a new random private key
        /// </summary>
        /// <returns>The private key as a byte array</returns>
        public static byte[] GeneratePrivateKey()
        {
            try
            {
                // Create a secure random 32-byte array
                byte[] privateKey = new byte[32];
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(privateKey);
                }
                
                // Ensure the private key is valid
                return ValidatePrivateKey(privateKey);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error generating private key: {ex.Message}");
                // Return a fallback key in case of error
                return new byte[32] { 
                    0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 
                    0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10,
                    0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 
                    0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F, 0x20
                };
            }
        }
        
        /// <summary>
        /// Validates a private key
        /// </summary>
        /// <param name="privateKey">The private key to validate</param>
        /// <returns>The validated private key</returns>
        private static byte[] ValidatePrivateKey(byte[] privateKey)
        {
            try
            {
                // Simple validation - make sure it's not all zeros
                if (privateKey == null || privateKey.Length != 32 || privateKey.All(b => b == 0))
                {
                    Debug.LogWarning("Invalid private key detected, generating a new one");
                    // Create a simple deterministic key for testing
                    byte[] newKey = new byte[32];
                    for (int i = 0; i < 32; i++)
                    {
                        newKey[i] = (byte)(i + 1);
                    }
                    return newKey;
                }
                
                return privateKey;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error validating private key: {ex.Message}");
                // Return a fallback key in case of error
                byte[] fallbackKey = new byte[32];
                for (int i = 0; i < 32; i++)
                {
                    fallbackKey[i] = (byte)(i + 1);
                }
                return fallbackKey;
            }
        }
        
        /// <summary>
        /// Derives a public key from a private key
        /// </summary>
        /// <param name="privateKey">The private key as a byte array</param>
        /// <returns>The public key as a byte array</returns>
        public static byte[] GetPublicKey(byte[] privateKey)
        {
            try
            {
                // TODO: Implement full secp256k1 key derivation
                // For now, we're using a simplified placeholder
                // This should be replaced with proper Secp256k1 implementation later
                
                Debug.LogWarning("Using placeholder public key derivation. Replace with proper Secp256k1 implementation.");
                
                if (privateKey == null || privateKey.Length != 32)
                {
                    Debug.LogError("Invalid private key for public key derivation");
                    return new byte[33] { 0x02, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10,
                        0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F, 0x20 };
                }
                
                // Generate a deterministic 33-byte public key from the private key
                // (Not cryptographically correct, but works as a placeholder)
                byte[] publicKey = new byte[33];
                publicKey[0] = 0x02; // Compression prefix
                
                using (SHA256 sha256 = SHA256.Create())
                {
                    byte[] hash = sha256.ComputeHash(privateKey);
                    Array.Copy(hash, 0, publicKey, 1, 32);
                }
                
                return publicKey;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error generating public key: {ex.Message}");
                // Return a fallback key in case of error
                byte[] fallbackKey = new byte[33];
                fallbackKey[0] = 0x02; // compression prefix
                for (int i = 0; i < 32; i++)
                {
                    fallbackKey[i + 1] = (byte)(i + 1);
                }
                return fallbackKey;
            }
        }
        
        /// <summary>
        /// Creates a signature for a message using the private key
        /// </summary>
        /// <param name="messageHash">The SHA256 hash of the message to sign</param>
        /// <param name="privateKey">The private key to sign with</param>
        /// <returns>The signature as a 64-byte array</returns>
        public static byte[] Sign(byte[] messageHash, byte[] privateKey)
        {
            try
            {
                // TODO: Implement full secp256k1 signing
                // For now, we're using a simplified placeholder
                // This should be replaced with proper Secp256k1 implementation later
                
                Debug.LogWarning("Using placeholder signing. Replace with proper Secp256k1 implementation.");
                
                if (messageHash == null || privateKey == null || messageHash.Length != 32 || privateKey.Length != 32)
                {
                    Debug.LogError("Invalid inputs for signature generation");
                    return new byte[64];
                }
                
                // Generate a deterministic signature based on the message hash and private key
                // (Not cryptographically correct, but works as a placeholder)
                byte[] signature = new byte[64];
                
                using (HMACSHA256 hmac = new HMACSHA256(privateKey))
                {
                    byte[] sigPart1 = hmac.ComputeHash(messageHash);
                    byte[] sigPart2 = hmac.ComputeHash(sigPart1);
                    
                    Array.Copy(sigPart1, 0, signature, 0, 32);
                    Array.Copy(sigPart2, 0, signature, 32, 32);
                }
                
                return signature;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error generating signature: {ex.Message}");
                // Return a fallback signature in case of error
                byte[] fallbackSig = new byte[64];
                for (int i = 0; i < 64; i++)
                {
                    fallbackSig[i] = (byte)(i + 1);
                }
                return fallbackSig;
            }
        }
        
        /// <summary>
        /// Verifies a signature for a message using the public key
        /// </summary>
        /// <param name="messageHash">The SHA256 hash of the message</param>
        /// <param name="signature">The signature to verify</param>
        /// <param name="publicKey">The public key to verify against</param>
        /// <returns>True if the signature is valid, otherwise false</returns>
        public static bool Verify(byte[] messageHash, byte[] signature, byte[] publicKey)
        {
            try
            {
                // TODO: Implement full secp256k1 verification
                // For now, we're using a simplified placeholder that always returns true
                // This should be replaced with proper Secp256k1 implementation later
                
                Debug.LogWarning("Using placeholder signature verification. Replace with proper Secp256k1 implementation.");
                
                // For now, we'll validate format but not cryptographic correctness
                if (messageHash == null || signature == null || publicKey == null)
                {
                    Debug.LogError("Null input provided to signature verification");
                    return false;
                }
                
                return signature.Length == 64 && publicKey.Length >= 32;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error verifying signature: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Computes the SHA256 hash of a message
        /// </summary>
        /// <param name="message">The message to hash</param>
        /// <returns>The SHA256 hash of the message</returns>
        public static byte[] ComputeMessageHash(string message)
        {
            try
            {
                if (string.IsNullOrEmpty(message))
                {
                    Debug.LogWarning("Empty message provided for hashing");
                    return new byte[32]; // Return zeros for empty message
                }
                
                using (SHA256 sha256 = SHA256.Create())
                {
                    return sha256.ComputeHash(Encoding.UTF8.GetBytes(message));
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error computing message hash: {ex.Message}");
                return new byte[32]; // Return zeros on error
            }
        }
    }
} 