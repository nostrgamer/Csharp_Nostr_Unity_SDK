using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Nostr.Unity.Utils
{
    /// <summary>
    /// Utility class for Bech32 encoding and decoding
    /// </summary>
    public static class Bech32Util
    {
        // Bech32 character set for encoding
        private const string CHARSET = "qpzry9x8gf2tvdw0s3jn54khce6mua7l";
        
        // Bech32 separator
        private const char SEPARATOR = '1';
        
        // Bech32 checksum generator polynomial values
        private static readonly uint[] GENERATOR = { 0x3b6a57b2, 0x26508e6d, 0x1ea119fa, 0x3d4233dd, 0x2a1462b3 };
        
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
            
            // Convert data to 5-bit values
            byte[] converted = ConvertBits(data, 8, 5, true);
            
            // Create checksum
            byte[] checksum = CreateChecksum(hrp, converted);
            
            // Build the encoded string
            StringBuilder sb = new StringBuilder(hrp.Length + 1 + converted.Length + checksum.Length);
            sb.Append(hrp);
            sb.Append(SEPARATOR);
            
            foreach (var b in converted)
            {
                sb.Append(CHARSET[b]);
            }
            
            foreach (var b in checksum)
            {
                sb.Append(CHARSET[b]);
            }
            
            return sb.ToString();
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
            
            // Verify case is not mixed
            if (bech32Str.ToLower() != bech32Str && bech32Str.ToUpper() != bech32Str)
                throw new FormatException("Mixed case strings are not valid Bech32");
            
            // Normalize to lowercase
            bech32Str = bech32Str.ToLower();
            
            // Find the separator
            int separatorPos = bech32Str.LastIndexOf(SEPARATOR);
            if (separatorPos < 1)
                throw new FormatException("Bech32 string missing separator");
            
            // Extract HRP and data
            string hrp = bech32Str.Substring(0, separatorPos);
            string encodedData = bech32Str.Substring(separatorPos + 1);
            
            // Validate characters
            foreach (char c in encodedData)
            {
                if (CHARSET.IndexOf(c) == -1)
                    throw new FormatException($"Invalid character in Bech32 string: {c}");
            }
            
            // Convert the data part from the charset to bytes
            byte[] data = new byte[encodedData.Length];
            for (int i = 0; i < encodedData.Length; i++)
            {
                data[i] = (byte)CHARSET.IndexOf(encodedData[i]);
            }
            
            // Verify the checksum
            if (!VerifyChecksum(hrp, data))
                throw new FormatException("Invalid Bech32 checksum");
            
            // Return the decoded data (minus the 6 checksum bytes)
            int dataLength = data.Length - 6;
            byte[] decodedData = new byte[dataLength];
            Array.Copy(data, decodedData, dataLength);
            
            // Convert back from 5-bit to 8-bit
            byte[] result = ConvertBits(decodedData, 5, 8, false);
            
            return (hrp, result);
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
            
            // Validate that this is a proper hex string
            if (!System.Text.RegularExpressions.Regex.IsMatch(hexData, "^[0-9a-fA-F]+$"))
                throw new ArgumentException($"Invalid hex string: '{hexData}'. Must contain only hexadecimal characters (0-9, a-f, A-F).", nameof(hexData));
            
            // Ensure expected length for public/private keys
            if (prefix == "npub" || prefix == "nsec")
            {
                // Handle compressed key format (02/03 prefix) for public keys
                if (prefix == "npub" && hexData.Length == 66 && (hexData.StartsWith("02") || hexData.StartsWith("03")))
                {
                    // Remove the compression prefix for bech32 encoding
                    hexData = hexData.Substring(2);
                }
                // For all other cases, enforce 64 character length
                else if (hexData.Length != 64)
                {
                    throw new ArgumentException($"Invalid key length: {hexData.Length}. Expected 64 characters for {prefix} keys.", nameof(hexData));
                }
            }
            
            try
            {
                byte[] data = HexToBytes(hexData);
                return Encode(prefix, data);
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Error encoding hex data: {ex.Message}", nameof(hexData), ex);
            }
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
        
        /// <summary>
        /// Converts from one bit size to another, maintaining data size
        /// </summary>
        private static byte[] ConvertBits(byte[] data, int fromBits, int toBits, bool pad)
        {
            int acc = 0;
            int bits = 0;
            List<byte> result = new List<byte>();
            int maxv = (1 << toBits) - 1;
            
            foreach (var value in data)
            {
                if ((value >> fromBits) > 0)
                    throw new FormatException($"Invalid value: {value}");
                
                acc = (acc << fromBits) | value;
                bits += fromBits;
                
                while (bits >= toBits)
                {
                    bits -= toBits;
                    result.Add((byte)((acc >> bits) & maxv));
                }
            }
            
            if (pad)
            {
                if (bits > 0)
                {
                    result.Add((byte)((acc << (toBits - bits)) & maxv));
                }
            }
            else if (bits >= fromBits || ((acc << (toBits - bits)) & maxv) != 0)
            {
                throw new FormatException("Invalid padding in data");
            }
            
            return result.ToArray();
        }
        
        /// <summary>
        /// Calculate the Bech32 checksum
        /// </summary>
        private static byte[] CreateChecksum(string hrp, byte[] data)
        {
            byte[] hrpExpanded = HrpExpand(hrp);
            byte[] values = new byte[hrpExpanded.Length + data.Length + 6];
            
            Array.Copy(hrpExpanded, 0, values, 0, hrpExpanded.Length);
            Array.Copy(data, 0, values, hrpExpanded.Length, data.Length);
            
            uint polymod = Polymod(values) ^ 1;
            byte[] checksum = new byte[6];
            
            for (int i = 0; i < 6; i++)
            {
                checksum[i] = (byte)((polymod >> (5 * (5 - i))) & 31);
            }
            
            return checksum;
        }
        
        /// <summary>
        /// Verify the Bech32 checksum
        /// </summary>
        private static bool VerifyChecksum(string hrp, byte[] data)
        {
            byte[] hrpExpanded = HrpExpand(hrp);
            byte[] values = new byte[hrpExpanded.Length + data.Length];
            
            Array.Copy(hrpExpanded, 0, values, 0, hrpExpanded.Length);
            Array.Copy(data, 0, values, hrpExpanded.Length, data.Length);
            
            return Polymod(values) == 1;
        }
        
        /// <summary>
        /// Expand the human-readable prefix for checksum computation
        /// </summary>
        private static byte[] HrpExpand(string hrp)
        {
            byte[] result = new byte[hrp.Length * 2 + 1];
            
            for (int i = 0; i < hrp.Length; i++)
            {
                result[i] = (byte)(hrp[i] >> 5);
                result[i + hrp.Length + 1] = (byte)(hrp[i] & 31);
            }
            
            result[hrp.Length] = 0;
            return result;
        }
        
        /// <summary>
        /// Calculate Bech32 checksum
        /// </summary>
        private static uint Polymod(byte[] values)
        {
            uint chk = 1;
            
            foreach (var value in values)
            {
                uint b = (chk >> 25);
                chk = ((chk & 0x1ffffff) << 5) ^ value;
                
                for (int i = 0; i < 5; i++)
                {
                    if (((b >> i) & 1) == 1)
                    {
                        chk ^= GENERATOR[i];
                    }
                }
            }
            
            return chk;
        }
    }
} 