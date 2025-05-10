using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace NostrUnity.Crypto
{
    /// <summary>
    /// Provides Bech32 encoding and decoding functionality for Nostr identifiers
    /// </summary>
    public static class Bech32
    {
        private const string Charset = "qpzry9x8gf2tvdw0s3jn54khce6mua7l";

        /// <summary>
        /// Encodes data with a human-readable prefix using Bech32
        /// </summary>
        /// <param name="hrp">Human readable prefix (e.g., "npub", "nsec", "note")</param>
        /// <param name="data">The data to encode</param>
        /// <returns>The Bech32 encoded string</returns>
        public static string Encode(string hrp, byte[] data)
        {
            if (string.IsNullOrEmpty(hrp))
                throw new ArgumentException("Human-readable prefix cannot be null or empty", nameof(hrp));
            
            if (data == null || data.Length == 0)
                throw new ArgumentException("Data cannot be null or empty", nameof(data));

            // Convert to 5-bit values
            byte[] converted = ConvertBits(data, 8, 5, true);
            
            // Create checksum
            byte[] checksum = CreateChecksum(hrp, converted);
            
            // Build result string
            StringBuilder result = new StringBuilder(hrp + "1");
            
            // Add data values
            foreach (byte b in converted)
            {
                result.Append(Charset[b]);
            }
            
            // Add checksum values
            foreach (byte b in checksum)
            {
                result.Append(Charset[b]);
            }
            
            return result.ToString();
        }

        /// <summary>
        /// Decodes a Bech32 string into its human-readable prefix and data
        /// </summary>
        /// <param name="bech32">The Bech32 encoded string</param>
        /// <returns>The decoded data as byte array</returns>
        public static byte[] Decode(string bech32)
        {
            if (string.IsNullOrEmpty(bech32))
                throw new ArgumentException("Bech32 string cannot be null or empty", nameof(bech32));

            // Normalize case
            bech32 = bech32.ToLowerInvariant();
            
            // Check and extract HRP and data
            int pos = bech32.LastIndexOf('1');
            if (pos < 1)
                throw new FormatException("Invalid Bech32 format (no separator '1' found)");
            
            string hrp = bech32.Substring(0, pos);
            string encodedData = bech32.Substring(pos + 1);
            
            // Check for invalid chars
            foreach (char c in encodedData)
            {
                if (Charset.IndexOf(c) == -1)
                    throw new FormatException($"Invalid character in Bech32 string: {c}");
            }
            
            // Convert chars to 5-bit values
            byte[] dataBytes = new byte[encodedData.Length];
            for (int i = 0; i < encodedData.Length; i++)
            {
                dataBytes[i] = (byte)Charset.IndexOf(encodedData[i]);
            }
            
            // Separate checksum and data
            if (dataBytes.Length < 6)
                throw new FormatException("Bech32 string too short (missing checksum)");
                
            byte[] data = new byte[dataBytes.Length - 6];
            Array.Copy(dataBytes, data, data.Length);
            
            byte[] checksum = new byte[6];
            Array.Copy(dataBytes, dataBytes.Length - 6, checksum, 0, 6);
            
            // Verify checksum
            byte[] calculatedChecksum = CreateChecksum(hrp, data);
            for (int i = 0; i < 6; i++)
            {
                if (calculatedChecksum[i] != checksum[i])
                    throw new FormatException("Invalid Bech32 checksum");
            }
            
            // Convert from 5-bit to 8-bit
            return ConvertBits(data, 5, 8, false);
        }

        /// <summary>
        /// Creates a checksum for Bech32 encoding
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
        /// Expands the human-readable part for checksum calculation
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
        /// Calculates the Bech32 checksum
        /// </summary>
        private static uint Polymod(byte[] values)
        {
            uint[] generator = { 0x3b6a57b2, 0x26508e6d, 0x1ea119fa, 0x3d4233dd, 0x2a1462b3 };
            uint checksum = 1;
            
            foreach (byte value in values)
            {
                uint c0 = checksum >> 25;
                checksum = ((checksum & 0x1ffffff) << 5) ^ value;
                
                for (int i = 0; i < 5; i++)
                {
                    if (((c0 >> i) & 1) != 0)
                    {
                        checksum ^= generator[i];
                    }
                }
            }
            
            return checksum;
        }

        /// <summary>
        /// Converts between bit sizes
        /// </summary>
        /// <param name="data">The data to convert</param>
        /// <param name="fromBits">Starting bit size (e.g., 8 for bytes)</param>
        /// <param name="toBits">Target bit size (e.g., 5 for Bech32)</param>
        /// <param name="pad">Whether to pad the result</param>
        /// <returns>The converted data</returns>
        private static byte[] ConvertBits(byte[] data, int fromBits, int toBits, bool pad)
        {
            int acc = 0;
            int bits = 0;
            List<byte> result = new List<byte>();
            int maxv = (1 << toBits) - 1;
            
            foreach (byte value in data)
            {
                if ((value >> fromBits) > 0)
                    throw new ArgumentOutOfRangeException(nameof(data), "Invalid value for conversion");
                
                acc = (acc << fromBits) | value;
                bits += fromBits;
                
                while (bits >= toBits)
                {
                    bits -= toBits;
                    result.Add((byte)((acc >> bits) & maxv));
                }
            }
            
            if (pad && bits > 0)
            {
                result.Add((byte)((acc << (toBits - bits)) & maxv));
            }
            else if (bits >= fromBits || ((acc << (toBits - bits)) & maxv) != 0)
            {
                throw new InvalidOperationException("Cannot convert bits with padding disabled");
            }
            
            return result.ToArray();
        }
    }
} 