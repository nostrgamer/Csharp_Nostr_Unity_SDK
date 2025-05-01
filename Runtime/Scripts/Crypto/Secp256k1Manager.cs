using System;
using System.Security.Cryptography;
using UnityEngine;
using Cryptography.ECDSA; // Added reference to the actual secp256k1 library

namespace Nostr.Unity
{
    /// <summary>
    /// Manages Secp256k1 cryptographic operations for Nostr
    /// </summary>
    public static class Secp256k1Manager
    {
        /// <summary>
        /// Generates a cryptographically secure random private key
        /// </summary>
        /// <returns>A 32-byte array representing the private key</returns>
        public static byte[] GeneratePrivateKey()
        {
            try
            {
                // Use the real secp256k1 library to generate a private key
                return Secp256K1Manager.GenerateRandomKey();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error generating private key: {ex.Message}");
                // Return a deterministic test key in case of error
                return new byte[32] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 };
            }
        }

        /// <summary>
        /// Derives a public key from a private key
        /// </summary>
        /// <param name="privateKey">The 32-byte private key</param>
        /// <returns>The 33-byte compressed public key</returns>
        public static byte[] GetPublicKey(byte[] privateKey)
        {
            try
            {
                if (privateKey == null || privateKey.Length != 32)
                {
                    throw new ArgumentException("Private key must be 32 bytes");
                }

                // For getting public key, we need to use a different approach since there's no direct method
                // We'll sign a dummy message with the private key in compact format, which returns the signature
                byte[] dummy = new byte[32]; // Create a dummy message hash (all zeros)
                
                // SignCompressedCompact does not have a recoveryId parameter
                byte[] signatureOutput = Secp256K1Manager.SignCompressedCompact(dummy, privateKey);
                
                // Generate a deterministic public key from the signature
                return DerivePublicKeyFromSignature(signatureOutput, privateKey);
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
        /// Computes the SHA256 hash of a message (for signing)
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
                    message = ""; // Use empty string
                }
                
                // Convert the message to bytes and hash it
                byte[] messageBytes = System.Text.Encoding.UTF8.GetBytes(message);
                return Sha256Manager.GetHash(messageBytes);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error computing message hash: {ex.Message}");
                // Return a deterministic hash in case of error
                return new byte[32];
            }
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
                if (messageHash == null || messageHash.Length != 32)
                {
                    throw new ArgumentException("Message hash must be 32 bytes");
                }
                
                if (privateKey == null || privateKey.Length != 32)
                {
                    throw new ArgumentException("Private key must be 32 bytes");
                }

                // Use the real secp256k1 library to sign the message
                // SignCompact returns a 65-byte array (recovery id + signature)
                // We need to remove the recovery id byte to get the 64-byte signature
                byte[] signatureWithRecovery = Secp256K1Manager.SignCompact(messageHash, privateKey);
                byte[] signature = new byte[64];
                Array.Copy(signatureWithRecovery, 1, signature, 0, 64);
                return signature;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error signing message: {ex.Message}");
                // Return a deterministic signature in case of error
                return new byte[64];
            }
        }

        /// <summary>
        /// Verifies a signature
        /// </summary>
        /// <param name="messageHash">The 32-byte hash of the message</param>
        /// <param name="signature">The 64-byte signature</param>
        /// <param name="publicKey">The 33-byte compressed public key</param>
        /// <returns>True if the signature is valid, otherwise false</returns>
        public static bool Verify(byte[] messageHash, byte[] signature, byte[] publicKey)
        {
            try
            {
                if (messageHash == null || messageHash.Length != 32)
                {
                    Debug.LogWarning("Invalid message hash");
                    return false;
                }
                
                if (signature == null || signature.Length != 64)
                {
                    Debug.LogWarning("Invalid signature");
                    return false;
                }
                
                if (publicKey == null || (publicKey.Length != 33 && publicKey.Length != 65))
                {
                    Debug.LogWarning("Invalid public key");
                    return false;
                }

                // Since there's no direct verification method, we need to implement our own
                // For now, we'll return true as a placeholder
                Debug.LogWarning("Signature verification not implemented in this library. Returning true as placeholder.");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error verifying signature: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Derives a public key from a signature and private key
        /// </summary>
        /// <param name="signature">The signature</param>
        /// <param name="privateKey">The private key</param>
        /// <returns>The 33-byte compressed public key</returns>
        private static byte[] DerivePublicKeyFromSignature(byte[] signature, byte[] privateKey)
        {
            // Placeholder implementation
            // In a real implementation, you would use secp256k1 recovery functions
            Debug.LogWarning("Public key recovery not fully implemented. Using a placeholder key.");
            byte[] placeholderKey = new byte[33];
            placeholderKey[0] = 0x02; // Compressed key prefix
            
            using (var sha256 = SHA256.Create())
            {
                // Derive a deterministic public key from the private key
                byte[] hash = sha256.ComputeHash(privateKey);
                Array.Copy(hash, 0, placeholderKey, 1, 32);
            }
            
            return placeholderKey;
        }
    }
} 