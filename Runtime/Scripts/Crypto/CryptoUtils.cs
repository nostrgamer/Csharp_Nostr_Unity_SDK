using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace NostrUnity.Crypto
{
    /// <summary>
    /// Provides general cryptographic utilities for Nostr
    /// </summary>
    public static class CryptoUtils
    {
        /// <summary>
        /// Converts a hexadecimal string to a byte array
        /// </summary>
        /// <param name="hex">The hexadecimal string to convert</param>
        /// <returns>The resulting byte array</returns>
        public static byte[] HexToBytes(string hex)
        {
            if (string.IsNullOrEmpty(hex))
                throw new ArgumentException("Hex string cannot be null or empty", nameof(hex));
            
            // Remove 0x prefix if present
            if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                hex = hex.Substring(2);
            
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
        /// Converts a byte array to a hexadecimal string
        /// </summary>
        /// <param name="bytes">The byte array to convert</param>
        /// <param name="prefix">Whether to include the "0x" prefix</param>
        /// <returns>The resulting hexadecimal string</returns>
        public static string BytesToHex(byte[] bytes, bool prefix = false)
        {
            if (bytes == null || bytes.Length == 0)
                throw new ArgumentException("Byte array cannot be null or empty", nameof(bytes));
            
            string hex = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
            return prefix ? "0x" + hex : hex;
        }
        
        /// <summary>
        /// Computes the SHA-256 hash of the given data
        /// </summary>
        /// <param name="data">The data to hash</param>
        /// <returns>The SHA-256 hash as a byte array</returns>
        public static byte[] Sha256(byte[] data)
        {
            if (data == null || data.Length == 0)
                throw new ArgumentException("Data cannot be null or empty", nameof(data));
            
            using (SHA256 sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(data);
            }
        }
        
        /// <summary>
        /// Computes the SHA-256 hash of a string after UTF-8 encoding
        /// </summary>
        /// <param name="data">The string to hash</param>
        /// <returns>The SHA-256 hash as a byte array</returns>
        public static byte[] Sha256(string data)
        {
            if (string.IsNullOrEmpty(data))
                throw new ArgumentException("Data cannot be null or empty", nameof(data));
            
            return Sha256(Encoding.UTF8.GetBytes(data));
        }

        /// <summary>
        /// Converts between different Nostr ID formats
        /// </summary>
        /// <param name="id">The ID to convert (hex or Bech32)</param>
        /// <param name="targetFormat">The target format ("hex", "npub", "nsec", "note")</param>
        /// <returns>The converted ID</returns>
        public static string ConvertIdFormat(string id, string targetFormat)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("ID cannot be null or empty", nameof(id));
            
            // First determine the current format and convert to bytes
            byte[] idBytes;
            
            if (id.StartsWith("npub1") || id.StartsWith("nsec1") || id.StartsWith("note1"))
            {
                // This is a Bech32 formatted ID
                idBytes = Bech32.Decode(id);
            }
            else
            {
                // Assume hex format
                idBytes = HexToBytes(id);
            }
            
            // Convert to the target format
            switch (targetFormat.ToLowerInvariant())
            {
                case "hex":
                    return BytesToHex(idBytes);
                
                case "npub":
                    return Bech32.Encode("npub", idBytes);
                
                case "nsec":
                    return Bech32.Encode("nsec", idBytes);
                
                case "note":
                    return Bech32.Encode("note", idBytes);
                
                default:
                    throw new ArgumentException($"Unknown target format: {targetFormat}", nameof(targetFormat));
            }
        }
        
        /// <summary>
        /// Generates a random 32-byte array suitable for private keys
        /// </summary>
        /// <returns>A random 32-byte array</returns>
        public static byte[] GenerateRandomBytes32()
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                byte[] bytes = new byte[32];
                rng.GetBytes(bytes);
                return bytes;
            }
        }
    }
} 