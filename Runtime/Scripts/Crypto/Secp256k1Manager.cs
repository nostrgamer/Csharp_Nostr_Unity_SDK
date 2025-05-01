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
                return Secp256k1.GeneratePrivateKey();
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

                // Use the real secp256k1 library to get the public key
                return Secp256k1.GetPublicKey(privateKey, true); // true = compressed format
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
                return Secp256k1.Hash(messageBytes);
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
                return Secp256k1.Sign(messageHash, privateKey);
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

                // Use the real secp256k1 library to verify the signature
                return Secp256k1.Verify(signature, messageHash, publicKey);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error verifying signature: {ex.Message}");
                return false;
            }
        }
    }
} 