using System;
using System.Linq;
using System.Text;

namespace Nostr.Unity.Utils
{
    /// <summary>
    /// Utility methods for hex string conversions
    /// </summary>
    public static class Hex
    {
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