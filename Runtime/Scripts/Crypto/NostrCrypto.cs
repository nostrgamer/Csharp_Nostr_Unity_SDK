using System;
using System.Security.Cryptography;
using System.Text;
using NBitcoin.Secp256k1;
using NostrUnity.Utils;
using UnityEngine;
using System.Linq;

namespace NostrUnity.Crypto
{
    /// <summary>
    /// Handles all cryptographic operations for Nostr using Schnorr signatures
    /// </summary>
    public static class NostrCrypto
    {
        private static readonly RandomNumberGenerator _rng = RandomNumberGenerator.Create();

        /// <summary>
        /// Generates a new key pair for Nostr
        /// </summary>
        /// <returns>Tuple of (privateKey, publicKey) in hex format</returns>
        public static (string PrivateKey, string PublicKey) GenerateKeyPair()
        {
            try
            {
                byte[] privateKey = GeneratePrivateKey();
                byte[] publicKey = GetPublicKey(privateKey);
                
                return (
                    BitConverter.ToString(privateKey).Replace("-", "").ToLowerInvariant(),
                    BitConverter.ToString(publicKey).Replace("-", "").ToLowerInvariant()
                );
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error generating key pair: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Generates a new private key for Nostr
        /// </summary>
        /// <returns>The private key as a byte array</returns>
        public static byte[] GeneratePrivateKey()
        {
            byte[] privateKey = new byte[32];
            _rng.GetBytes(privateKey);
            return privateKey;
        }

        /// <summary>
        /// Gets the public key from a private key
        /// </summary>
        /// <param name="privateKey">The private key as a byte array</param>
        /// <returns>The public key as a byte array</returns>
        public static byte[] GetPublicKey(byte[] privateKey)
        {
            if (privateKey == null || privateKey.Length != 32)
                throw new ArgumentException("Private key must be 32 bytes", nameof(privateKey));

            var ctx = new Context();
            var secKey = ECPrivKey.Create(privateKey);
            var pubKey = secKey.CreatePubKey();
            
            // Get the X coordinate of the public key (32 bytes)
            // For Nostr, we need the X coordinate of the public key
            var xOnlyPubKey = pubKey.ToXOnlyPubKey();
            return xOnlyPubKey.ToBytes();
        }

        /// <summary>
        /// Signs a Nostr event using Schnorr signatures
        /// </summary>
        /// <param name="eventId">The event ID to sign</param>
        /// <param name="privateKeyHex">The private key in hex format</param>
        /// <returns>The signature in hex format</returns>
        public static string SignEvent(string eventId, string privateKeyHex)
        {
            if (string.IsNullOrEmpty(eventId))
                throw new ArgumentException("Event ID cannot be null or empty", nameof(eventId));
            if (string.IsNullOrEmpty(privateKeyHex))
                throw new ArgumentException("Private key cannot be null or empty", nameof(privateKeyHex));

            byte[] eventIdBytes = HexToBytes(eventId);
            byte[] privateKeyBytes = HexToBytes(privateKeyHex);
            
            return SignEvent(eventIdBytes, privateKeyBytes);
        }

        /// <summary>
        /// Signs a Nostr event using Schnorr signatures
        /// </summary>
        /// <param name="eventIdBytes">The event ID to sign</param>
        /// <param name="privateKeyBytes">The private key as a byte array</param>
        /// <returns>The signature in hex format</returns>
        public static string SignEvent(byte[] eventIdBytes, byte[] privateKeyBytes)
        {
            if (eventIdBytes == null || eventIdBytes.Length != 32)
                throw new ArgumentException("Event ID must be 32 bytes", nameof(eventIdBytes));
            if (privateKeyBytes == null || privateKeyBytes.Length != 32)
                throw new ArgumentException("Private key must be 32 bytes", nameof(privateKeyBytes));

            var ctx = new Context();
            var secKey = ECPrivKey.Create(privateKeyBytes);
            var signature = secKey.SignBIP340(eventIdBytes);
            return BitConverter.ToString(signature.ToBytes()).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// Verifies a Nostr event signature using Schnorr (BIP-340)
        /// </summary>
        public static bool VerifySignature(string publicKeyHex, string eventIdHex, string signatureHex)
        {
            try
            {
                byte[] publicKeyBytes = HexToBytes(publicKeyHex);
                byte[] eventIdBytes = HexToBytes(eventIdHex);
                byte[] signatureBytes = HexToBytes(signatureHex);

                if (publicKeyBytes.Length != 32)
                    throw new ArgumentException("Public key must be 32 bytes", nameof(publicKeyHex));
                
                if (eventIdBytes.Length != 32)
                    throw new ArgumentException("Event ID must be 32 bytes", nameof(eventIdHex));
                
                if (signatureBytes.Length != 64)
                    throw new ArgumentException("Signature must be 64 bytes", nameof(signatureHex));

                // Create context
                var ctx = Context.Instance;
                
                try
                {
                    // Basic check: signature should not be all zeros
                    if (signatureBytes.All(b => b == 0))
                    {
                        Debug.LogError("Invalid signature: all zeros");
                        return false;
                    }
                    
                    // Create pubkey from the 32-byte public key
                    var pubKey = ECPubKey.Create(publicKeyBytes);
                    
                    // Create Schnorr signature from bytes
                    var schnorrSig = new SecpSchnorrSignature(signatureBytes);
                    
                    // Verify the signature
                    return pubKey.SigVerifyBIP340(eventIdBytes, schnorrSig);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error during signature verification: {ex.Message}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error verifying signature: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Converts a hex string to a byte array
        /// </summary>
        public static byte[] HexToBytes(string hex)
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

        /// <summary>
        /// Decodes an nsec (Bech32 encoded private key) to a hex string
        /// </summary>
        public static string DecodeNsec(string nsec)
        {
            try
            {
                // Simple Bech32 decoding for nsec
                if (!nsec.StartsWith("nsec1"))
                    throw new ArgumentException("Invalid nsec format", nameof(nsec));

                string data = nsec.Substring(5); // Remove "nsec1" prefix
                byte[] decoded = Bech32.Decode(data);
                return BitConverter.ToString(decoded).Replace("-", "").ToLowerInvariant();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error decoding nsec: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Gets the npub (Bech32 encoded public key) from a hex public key
        /// </summary>
        public static string GetNpub(string publicKeyHex)
        {
            try
            {
                byte[] publicKeyBytes = HexToBytes(publicKeyHex);
                string encoded = Bech32.Encode(publicKeyBytes);
                return "npub1" + encoded;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error encoding npub: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Gets the nsec (Bech32 encoded private key) from a hex private key
        /// </summary>
        public static string GetNsec(string privateKeyHex)
        {
            try
            {
                byte[] privateKeyBytes = HexToBytes(privateKeyHex);
                string encoded = Bech32.Encode(privateKeyBytes);
                return "nsec1" + encoded;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error encoding nsec: {ex.Message}");
                throw;
            }
        }

        public static string GetPublicKey(string privateKeyHex)
        {
            byte[] privateKeyBytes = HexToBytes(privateKeyHex);
            byte[] publicKeyBytes = GetPublicKey(privateKeyBytes);
            return BitConverter.ToString(publicKeyBytes).Replace("-", "").ToLowerInvariant();
        }
    }

    /// <summary>
    /// Simple Bech32 encoding/decoding implementation
    /// </summary>
    internal static class Bech32
    {
        private const string CHARSET = "qpzry9x8gf2tvdw0s3jn54khce6mua7l";

        public static string Encode(byte[] data)
        {
            if (data == null || data.Length == 0)
                throw new ArgumentException("Data cannot be null or empty", nameof(data));

            // Convert to 5-bit groups
            var bits = new System.Collections.BitArray(data);
            var groups = new System.Collections.Generic.List<int>();
            
            for (int i = 0; i < bits.Length; i += 5)
            {
                int value = 0;
                for (int j = 0; j < 5 && i + j < bits.Length; j++)
                {
                    if (bits[i + j])
                        value |= 1 << (4 - j);
                }
                groups.Add(value);
            }

            // Convert to Bech32 string
            var result = new StringBuilder();
            foreach (int group in groups)
            {
                result.Append(CHARSET[group]);
            }

            return result.ToString();
        }

        public static byte[] Decode(string bech32)
        {
            if (string.IsNullOrEmpty(bech32))
                throw new ArgumentException("Bech32 string cannot be null or empty", nameof(bech32));

            // Convert from Bech32 string to 5-bit groups
            var groups = new System.Collections.Generic.List<int>();
            foreach (char c in bech32)
            {
                int value = CHARSET.IndexOf(char.ToLower(c));
                if (value == -1)
                    throw new ArgumentException("Invalid Bech32 character", nameof(bech32));
                groups.Add(value);
            }

            // Convert to bytes
            var bits = new System.Collections.BitArray(groups.Count * 5);
            for (int i = 0; i < groups.Count; i++)
            {
                int value = groups[i];
                for (int j = 0; j < 5; j++)
                {
                    bits[i * 5 + j] = (value & (1 << (4 - j))) != 0;
                }
            }

            // Convert bits to bytes
            byte[] result = new byte[(bits.Length + 7) / 8];
            bits.CopyTo(result, 0);
            return result;
        }
    }
} 