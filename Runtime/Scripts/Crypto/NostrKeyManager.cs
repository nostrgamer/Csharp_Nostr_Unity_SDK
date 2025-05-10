using System;
using System.Security.Cryptography;
using NBitcoin.Secp256k1;
using UnityEngine;

namespace NostrUnity.Crypto
{
    /// <summary>
    /// Manages Nostr key pairs and provides key-related operations
    /// </summary>
    public class NostrKeyManager
    {
        private static readonly RandomNumberGenerator _rng = RandomNumberGenerator.Create();

        /// <summary>
        /// Gets the public key from a private key
        /// </summary>
        /// <param name="privateKeyHex">The private key in hex format</param>
        /// <param name="compressed">Whether to return the compressed public key</param>
        /// <returns>The public key in hex format</returns>
        public string GetPublicKey(string privateKeyHex, bool compressed = true)
        {
            try
            {
                if (string.IsNullOrEmpty(privateKeyHex))
                    throw new ArgumentException("Private key cannot be null or empty", nameof(privateKeyHex));

                byte[] privateKeyBytes = HexToBytes(privateKeyHex);
                var ctx = new Context();
                
                if (!ECPrivKey.TryCreate(privateKeyBytes, out ECPrivKey privateKey))
                    throw new ArgumentException("Invalid private key");
                
                if (compressed)
                {
                    // Get compressed public key (33 bytes with 02/03 prefix)
                    var pubKey = privateKey.CreatePubKey();
                    return BitConverter.ToString(pubKey.ToBytes()).Replace("-", "").ToLowerInvariant();
                }
                else
                {
                    // Get x-only public key (32 bytes) as used in NIP-01
                    var xOnlyPubKey = privateKey.CreateXOnlyPubKey();
                    return BitConverter.ToString(xOnlyPubKey.ToBytes()).Replace("-", "").ToLowerInvariant();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error getting public key: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Generates a new key pair
        /// </summary>
        /// <returns>Tuple of (privateKey, publicKey) in hex format</returns>
        public (string PrivateKey, string PublicKey) GenerateKeyPair()
        {
            try
            {
                byte[] privateKeyBytes = new byte[32];
                _rng.GetBytes(privateKeyBytes);
                
                var ctx = new Context();
                
                if (!ECPrivKey.TryCreate(privateKeyBytes, out ECPrivKey privateKey))
                    throw new ArgumentException("Invalid generated private key");
                
                // Use x-only public key for consistency with verification
                var xOnlyPubKey = privateKey.CreateXOnlyPubKey();
                
                return (
                    BitConverter.ToString(privateKeyBytes).Replace("-", "").ToLowerInvariant(),
                    BitConverter.ToString(xOnlyPubKey.ToBytes()).Replace("-", "").ToLowerInvariant()
                );
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error generating key pair: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Converts a hex string to a byte array
        /// </summary>
        private static byte[] HexToBytes(string hex)
        {
            if (string.IsNullOrEmpty(hex))
                throw new ArgumentException("Hex string cannot be null or empty", nameof(hex));

            if (hex.Length % 2 != 0)
                throw new ArgumentException("Hex string must have an even length", nameof(hex));

            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return bytes;
        }
    }
} 