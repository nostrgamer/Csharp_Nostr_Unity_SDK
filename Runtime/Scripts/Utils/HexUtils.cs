using System;
using System.Text;

namespace NostrUnity.Utils
{
    /// <summary>
    /// Utility methods for hex string conversions
    /// </summary>
    public static class HexUtils
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

            if (hex.Length % 2 != 0)
                throw new ArgumentException("Hex string must have an even length", nameof(hex));

            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                string byteString = hex.Substring(i * 2, 2);
                bytes[i] = Convert.ToByte(byteString, 16);
            }

            return bytes;
        }
        
        /// <summary>
        /// Converts a byte array to a hex string
        /// </summary>
        /// <param name="bytes">The byte array to convert</param>
        /// <returns>The hex string</returns>
        public static string BytesToHex(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }
    }
} 