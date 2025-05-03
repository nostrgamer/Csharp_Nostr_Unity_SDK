using System;
using System.Text.RegularExpressions;

namespace Nostr.Unity.Utils
{
    /// <summary>
    /// Extension methods for Nostr related types
    /// </summary>
    public static class NostrExtensions
    {
        private static readonly Regex HexRegex = new Regex("^[0-9a-fA-F]{64}$", RegexOptions.Compiled);
        
        /// <summary>
        /// Checks if a string is a valid hex key (64 hex characters)
        /// </summary>
        /// <param name="key">The key to check</param>
        /// <returns>True if the key is a valid hex key, otherwise false</returns>
        public static bool IsValidHexKey(this string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;
                
            return HexRegex.IsMatch(key);
        }
        
        /// <summary>
        /// Checks if a string is a valid npub key
        /// </summary>
        /// <param name="key">The key to check</param>
        /// <returns>True if the key is a valid npub key, otherwise false</returns>
        public static bool IsValidNpub(this string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;
                
            if (!key.StartsWith(NostrConstants.NPUB_PREFIX + "1"))
                return false;
                
            try
            {
                (string prefix, _) = Bech32Util.DecodeToHex(key);
                return prefix == NostrConstants.NPUB_PREFIX;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Checks if a string is a valid nsec key
        /// </summary>
        /// <param name="key">The key to check</param>
        /// <returns>True if the key is a valid nsec key, otherwise false</returns>
        public static bool IsValidNsec(this string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;
                
            if (!key.StartsWith(NostrConstants.NSEC_PREFIX + "1"))
                return false;
                
            try
            {
                (string prefix, _) = Bech32Util.DecodeToHex(key);
                return prefix == NostrConstants.NSEC_PREFIX;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Converts a key to hex format (if it's in Bech32 format)
        /// </summary>
        /// <param name="key">The key to convert (hex or Bech32)</param>
        /// <returns>The key in hex format</returns>
        public static string ToHex(this string key)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));
                
            // Already in hex format
            if (IsValidHexKey(key))
                return key;
                
            // Try to decode from npub or nsec
            if (key.StartsWith(NostrConstants.NPUB_PREFIX + "1") || key.StartsWith(NostrConstants.NSEC_PREFIX + "1"))
            {
                try
                {
                    (_, string hexData) = Bech32Util.DecodeToHex(key);
                    return hexData;
                }
                catch (Exception ex)
                {
                    throw new ArgumentException($"Invalid Bech32 key: {ex.Message}", nameof(key));
                }
            }
            
            throw new ArgumentException("Invalid key format", nameof(key));
        }
        
        /// <summary>
        /// Converts a hex key to npub format
        /// </summary>
        /// <param name="hexKey">The hex key to convert</param>
        /// <returns>The key in npub format</returns>
        public static string ToNpub(this string hexKey)
        {
            if (string.IsNullOrEmpty(hexKey))
                throw new ArgumentException("Key cannot be null or empty", nameof(hexKey));
                
            // Already in npub format
            if (IsValidNpub(hexKey))
                return hexKey;
                
            // Convert from hex to npub
            if (IsValidHexKey(hexKey))
                return Bech32Util.EncodeHex(NostrConstants.NPUB_PREFIX, hexKey);
                
            // Try to convert from nsec
            if (IsValidNsec(hexKey))
            {
                string hex = ToHex(hexKey);
                return Bech32Util.EncodeHex(NostrConstants.NPUB_PREFIX, hex);
            }
            
            throw new ArgumentException("Invalid key format", nameof(hexKey));
        }
        
        /// <summary>
        /// Converts a hex key to nsec format
        /// </summary>
        /// <param name="hexKey">The hex key to convert</param>
        /// <returns>The key in nsec format</returns>
        public static string ToNsec(this string hexKey)
        {
            if (string.IsNullOrEmpty(hexKey))
                throw new ArgumentException("Key cannot be null or empty", nameof(hexKey));
                
            // Already in nsec format
            if (IsValidNsec(hexKey))
                return hexKey;
                
            // Convert from hex to nsec
            if (IsValidHexKey(hexKey))
                return Bech32Util.EncodeHex(NostrConstants.NSEC_PREFIX, hexKey);
                
            throw new ArgumentException("Invalid key format", nameof(hexKey));
        }
        
        /// <summary>
        /// Shortens a key for display (first and last 4 characters)
        /// </summary>
        /// <param name="key">The key to shorten</param>
        /// <returns>The shortened key</returns>
        public static string ToShortKey(this string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;
                
            if (key.Length <= 12)
                return key;
                
            return $"{key.Substring(0, 5)}...{key.Substring(key.Length - 5)}";
        }
    }
} 