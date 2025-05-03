using System;
using System.Linq;
using NBitcoin;
using NBitcoin.DataEncoders;
using System.Text;

namespace Nostr.Unity.Utils
{
    /// <summary>
    /// Utility class for Bech32 encoding and decoding using NBitcoin's implementation.
    /// </summary>
    public static class Bech32
    {
        /// <summary>
        /// Encodes data with a human-readable prefix using Bech32 encoding
        /// </summary>
        /// <param name="hrp">Human-readable prefix (e.g., "npub", "nsec")</param>
        /// <param name="data">Data to encode</param>
        /// <returns>Bech32 encoded string</returns>
        public static string Encode(string hrp, byte[] data)
        {
            if (string.IsNullOrEmpty(hrp))
                throw new ArgumentException("Human-readable prefix cannot be null or empty", nameof(hrp));
            
            if (data == null || data.Length == 0)
                throw new ArgumentException("Data cannot be null or empty", nameof(data));
            
            var encoder = new Bech32Encoder(hrp);
            return encoder.Encode(data);
        }
        
        /// <summary>
        /// Decodes a Bech32 encoded string
        /// </summary>
        /// <param name="bech32Str">The Bech32 encoded string</param>
        /// <returns>Tuple containing the human-readable prefix and the decoded data</returns>
        public static (string hrp, byte[] data) Decode(string bech32Str)
        {
            if (string.IsNullOrEmpty(bech32Str))
                throw new ArgumentException("Bech32 string cannot be null or empty", nameof(bech32Str));
            
            // NBitcoin requires 1 separator
            int separatorPos = bech32Str.LastIndexOf('1');
            if (separatorPos < 1)
                throw new FormatException("Bech32 string missing separator");
            
            string hrp = bech32Str.Substring(0, separatorPos);
            var encoder = new Bech32Encoder(hrp);
            
            try
            {
                var decoded = encoder.Decode(bech32Str, out _);
                return (hrp, decoded);
            }
            catch (Exception ex)
            {
                throw new FormatException($"Invalid Bech32 string: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Encodes a hex string using Bech32 with the given prefix
        /// </summary>
        /// <param name="prefix">Human-readable prefix (e.g., "npub", "nsec")</param>
        /// <param name="hexData">Hex string to encode</param>
        /// <returns>Bech32 encoded string</returns>
        public static string EncodeHex(string prefix, string hexData)
        {
            if (string.IsNullOrEmpty(hexData))
                throw new ArgumentException("Hex data cannot be null or empty", nameof(hexData));
            
            byte[] data = HexToBytes(hexData);
            return Encode(prefix, data);
        }
        
        /// <summary>
        /// Decodes a Bech32 string and returns the decoded data as a hex string
        /// </summary>
        /// <param name="bech32Str">The Bech32 encoded string</param>
        /// <returns>Tuple containing the prefix and the hex-encoded data</returns>
        public static (string prefix, string hexData) DecodeToHex(string bech32Str)
        {
            (string prefix, byte[] data) = Decode(bech32Str);
            string hexData = BytesToHex(data);
            return (prefix, hexData);
        }
        
        /// <summary>
        /// Converts a hex string to a byte array
        /// </summary>
        /// <param name="hex">The hex string to convert</param>
        /// <returns>The byte array</returns>
        public static byte[] HexToBytes(string hex)
        {
            if (string.IsNullOrEmpty(hex))
                throw new ArgumentException("Hex string cannot be null or empty", nameof(hex));
            
            // Remove 0x prefix if present
            if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                hex = hex.Substring(2);
                
            // Ensure even length
            if (hex.Length % 2 != 0)
                hex = "0" + hex; // Pad with leading zero
            
            try
            {
                return Enumerable.Range(0, hex.Length)
                    .Where(x => x % 2 == 0)
                    .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                    .ToArray();
            }
            catch (FormatException ex)
            {
                throw new FormatException($"Invalid hex character in string '{hex}': {ex.Message}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error converting hex to bytes: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// Converts a byte array to a hex string
        /// </summary>
        /// <param name="bytes">The byte array to convert</param>
        /// <returns>The hex string</returns>
        public static string BytesToHex(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes), "Byte array cannot be null");
                
            if (bytes.Length == 0)
                return string.Empty; // Return empty string for empty array
            
            try
            {
                return string.Concat(bytes.Select(b => b.ToString("x2")));
            }
            catch (Exception ex)
            {
                throw new Exception($"Error converting bytes to hex: {ex.Message}", ex);
            }
        }
    }
} 