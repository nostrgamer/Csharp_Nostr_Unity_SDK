using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Nostr.Unity.Utils
{
    /// <summary>
    /// Utility class for Bech32 encoding and decoding.
    /// Based on BIP-173 and NIP-19 specifications.
    /// </summary>
    public static class Bech32
    {
        // Bech32 character set for encoding
        private const string Charset = "qpzry9x8gf2tvdw0s3jn54khce6mua7l";
        
        // Bech32 character set index lookup
        private static readonly Dictionary<char, int> CharsetRev = Charset
            .Select((c, i) => new { Character = c, Index = i })
            .ToDictionary(x => x.Character, x => x.Index);
        
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
            
            // Convert data bytes to 5-bit values
            byte[] converted = ConvertBits(data, 8, 5, true);
            
            // Create checksum
            byte[] checksum = CreateChecksum(hrp, converted);
            
            // Build the final string
            StringBuilder sb = new StringBuilder(hrp.Length + 1 + converted.Length + checksum.Length);
            sb.Append(hrp);
            sb.Append('1');
            
            foreach (byte b in converted)
            {
                sb.Append(Charset[b]);
            }
            
            foreach (byte b in checksum)
            {
                sb.Append(Charset[b]);
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
            
            // Ensure proper case (Bech32 can be all uppercase or all lowercase)
            if (bech32Str.Any(char.IsUpper) && bech32Str.Any(char.IsLower))
                throw new FormatException("Bech32 string must be all uppercase or all lowercase");
            
            // Normalize to lowercase
            bech32Str = bech32Str.ToLowerInvariant();
            
            // Find separator
            int separatorPos = bech32Str.LastIndexOf('1');
            if (separatorPos < 1)
                throw new FormatException("Bech32 string missing separator");
            
            // Extract HRP
            string hrp = bech32Str.Substring(0, separatorPos);
            
            // Extract data part
            string dataPart = bech32Str.Substring(separatorPos + 1);
            if (dataPart.Length < 6)
                throw new FormatException("Bech32 data section too short");
            
            // Validate characters
            if (dataPart.Any(c => !CharsetRev.ContainsKey(c)))
                throw new FormatException("Bech32 data contains invalid characters");
            
            // Convert to 5-bit values
            byte[] values = new byte[dataPart.Length];
            for (int i = 0; i < dataPart.Length; i++)
            {
                values[i] = (byte)CharsetRev[dataPart[i]];
            }
            
            // Verify checksum
            if (!VerifyChecksum(hrp, values))
                throw new FormatException("Bech32 checksum verification failed");
            
            // Extract data (excluding checksum)
            byte[] dataWithoutChecksum = new byte[values.Length - 6];
            Array.Copy(values, dataWithoutChecksum, values.Length - 6);
            
            // Convert from 5-bit values back to original byte array
            byte[] decodedData = ConvertBits(dataWithoutChecksum, 5, 8, false);
            
            return (hrp, decodedData);
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
            
            if (hex.Length % 2 != 0)
                throw new ArgumentException("Hex string must have an even length", nameof(hex));
            
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < hex.Length; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
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
            if (bytes == null || bytes.Length == 0)
                throw new ArgumentException("Byte array cannot be null or empty", nameof(bytes));
            
            StringBuilder sb = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
            {
                sb.Append(b.ToString("x2"));
            }
            
            return sb.ToString();
        }
        
        /// <summary>
        /// Converts data from one bit depth to another
        /// </summary>
        /// <param name="data">The data to convert</param>
        /// <param name="fromBits">The bit depth of the input</param>
        /// <param name="toBits">The bit depth of the output</param>
        /// <param name="pad">Whether to pad the output</param>
        /// <returns>The converted data</returns>
        private static byte[] ConvertBits(byte[] data, int fromBits, int toBits, bool pad)
        {
            int acc = 0;
            int bits = 0;
            List<byte> result = new List<byte>();
            int maxv = (1 << toBits) - 1;
            
            foreach (byte value in data)
            {
                if (value >> fromBits != 0)
                {
                    throw new ArgumentException($"Invalid value: {value} (exceeds {fromBits} bits)");
                }
                
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
                throw new ArgumentException("Could not convert bits with non-zero padding");
            }
            
            return result.ToArray();
        }
        
        /// <summary>
        /// Creates a Bech32 checksum
        /// </summary>
        /// <param name="hrp">Human-readable prefix</param>
        /// <param name="data">The data</param>
        /// <returns>The checksum</returns>
        private static byte[] CreateChecksum(string hrp, byte[] data)
        {
            byte[] hrpExpanded = HrpExpand(hrp);
            byte[] values = new byte[hrpExpanded.Length + data.Length + 6];
            
            Array.Copy(hrpExpanded, values, hrpExpanded.Length);
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
        /// Verifies a Bech32 checksum
        /// </summary>
        /// <param name="hrp">Human-readable prefix</param>
        /// <param name="data">The data (including checksum)</param>
        /// <returns>True if the checksum is valid, otherwise false</returns>
        private static bool VerifyChecksum(string hrp, byte[] data)
        {
            byte[] hrpExpanded = HrpExpand(hrp);
            byte[] values = new byte[hrpExpanded.Length + data.Length];
            
            Array.Copy(hrpExpanded, values, hrpExpanded.Length);
            Array.Copy(data, 0, values, hrpExpanded.Length, data.Length);
            
            return Polymod(values) == 1;
        }
        
        /// <summary>
        /// Expands the human-readable prefix for checksum computation
        /// </summary>
        /// <param name="hrp">Human-readable prefix</param>
        /// <returns>Expanded human-readable prefix</returns>
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
        /// Computes the Bech32 checksum
        /// </summary>
        /// <param name="values">The values to compute the checksum for</param>
        /// <returns>The checksum value</returns>
        private static uint Polymod(byte[] values)
        {
            // Generator polynomial for Bech32 checksum
            uint[] generator = { 0x3b6a57b2, 0x26508e6d, 0x1ea119fa, 0x3d4233dd, 0x2a1462b3 };
            uint chk = 1;
            
            foreach (byte value in values)
            {
                uint b = (chk >> 25);
                chk = ((chk & 0x1ffffff) << 5) ^ value;
                
                for (int i = 0; i < 5; i++)
                {
                    if (((b >> i) & 1) != 0)
                    {
                        chk ^= generator[i];
                    }
                }
            }
            
            return chk;
        }
    }
} 