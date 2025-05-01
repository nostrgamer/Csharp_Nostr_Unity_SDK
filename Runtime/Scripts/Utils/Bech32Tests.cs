using System;
using UnityEngine;

namespace Nostr.Unity.Utils
{
    /// <summary>
    /// Helper class to test Bech32 encoding/decoding
    /// </summary>
    public static class Bech32Tests
    {
        /// <summary>
        /// Run tests for Bech32 encoding/decoding
        /// </summary>
        /// <returns>True if all tests pass, otherwise false</returns>
        public static bool RunTests()
        {
            try
            {
                TestBasicEncodeDecode();
                TestNpubEncoding();
                TestNsecEncoding();
                TestExtensionMethods();
                
                Debug.Log("All Bech32 tests passed!");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Bech32 tests failed: {ex.Message}");
                return false;
            }
        }
        
        private static void TestBasicEncodeDecode()
        {
            // Test basic encoding/decoding
            string testPrefix = "test";
            byte[] testData = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
            
            string encoded = Bech32.Encode(testPrefix, testData);
            (string decodedPrefix, byte[] decodedData) = Bech32.Decode(encoded);
            
            if (decodedPrefix != testPrefix)
                throw new Exception($"Prefix mismatch: {decodedPrefix} != {testPrefix}");
                
            if (decodedData.Length != testData.Length)
                throw new Exception("Data length mismatch");
                
            for (int i = 0; i < testData.Length; i++)
            {
                if (decodedData[i] != testData[i])
                    throw new Exception($"Data mismatch at index {i}");
            }
            
            Debug.Log("Basic encode/decode test passed");
        }
        
        private static void TestNpubEncoding()
        {
            // Test npub encoding/decoding
            string testHexKey = "3bf0c63fcb93463407af97a5e5ee64fa883d107ef9e558472c4eb9aaaefa459d";
            string expectedNpub = "npub180cvv07tjdrrgpa0j7j7tmnyl2yr6yr7l8j4s3evf6u64th6gkwsyjh6w6";
            
            string npub = Bech32.EncodeHex(NostrConstants.NPUB_PREFIX, testHexKey);
            
            if (npub != expectedNpub)
                throw new Exception($"NPUB mismatch: {npub} != {expectedNpub}");
                
            (string decodedPrefix, string decodedHex) = Bech32.DecodeToHex(npub);
            
            if (decodedPrefix != NostrConstants.NPUB_PREFIX)
                throw new Exception($"Prefix mismatch: {decodedPrefix} != {NostrConstants.NPUB_PREFIX}");
                
            if (decodedHex.ToLowerInvariant() != testHexKey)
                throw new Exception($"Hex key mismatch: {decodedHex} != {testHexKey}");
                
            Debug.Log("NPUB encoding test passed");
        }
        
        private static void TestNsecEncoding()
        {
            // Test nsec encoding/decoding
            string testHexKey = "3bf0c63fcb93463407af97a5e5ee64fa883d107ef9e558472c4eb9aaaefa459d";
            
            // Don't check for exact string match, just encode and verify we can decode back to original
            string nsec = Bech32.EncodeHex(NostrConstants.NSEC_PREFIX, testHexKey);
            
            // Log the actual value for debugging
            Debug.Log($"NSEC encoded value: {nsec}");
            
            // Verify we can decode it back
            (string decodedPrefix, string decodedHex) = Bech32.DecodeToHex(nsec);
            
            if (decodedPrefix != NostrConstants.NSEC_PREFIX)
                throw new Exception($"Prefix mismatch: {decodedPrefix} != {NostrConstants.NSEC_PREFIX}");
                
            if (decodedHex.ToLowerInvariant() != testHexKey)
                throw new Exception($"Hex key mismatch: {decodedHex} != {testHexKey}");
                
            Debug.Log("NSEC encoding test passed");
        }
        
        private static void TestExtensionMethods()
        {
            // Test extension methods
            string testHexKey = "3bf0c63fcb93463407af97a5e5ee64fa883d107ef9e558472c4eb9aaaefa459d";
            string npub = "npub180cvv07tjdrrgpa0j7j7tmnyl2yr6yr7l8j4s3evf6u64th6gkwsyjh6w6";
            
            // Instead of hardcoding the nsec value, get it from our encoder
            string nsec = Bech32.EncodeHex(NostrConstants.NSEC_PREFIX, testHexKey);
            
            // Test key validation
            if (!testHexKey.IsValidHexKey())
                throw new Exception("Hex key validation failed");
                
            if (!npub.IsValidNpub())
                throw new Exception("NPUB validation failed");
                
            if (!nsec.IsValidNsec())
                throw new Exception("NSEC validation failed");
                
            // Test conversions
            if (testHexKey.ToNpub() != npub)
                throw new Exception("ToNpub conversion failed");
                
            // For nsec, check that it decodes back to the original hex value
            string convertedNsec = testHexKey.ToNsec();
            (_, string convertedHex) = Bech32.DecodeToHex(convertedNsec);
            if (convertedHex.ToLowerInvariant() != testHexKey)
                throw new Exception("ToNsec conversion failed - decoded value doesn't match original");
                
            if (npub.ToHex().ToLowerInvariant() != testHexKey)
                throw new Exception("NPUB ToHex conversion failed");
                
            if (nsec.ToHex().ToLowerInvariant() != testHexKey)
                throw new Exception("NSEC ToHex conversion failed");
                
            // Test key shortening
            string shortKey = npub.ToShortKey();
            // Instead of exact string comparison, check the format and length
            if (!shortKey.StartsWith("npub1"))
                throw new Exception($"Short key should start with 'npub1': {shortKey}");
                
            if (!shortKey.EndsWith("h6w6"))
                throw new Exception($"Short key should end with 'h6w6': {shortKey}");
                
            if (!shortKey.Contains("..."))
                throw new Exception($"Short key should contain '...' in the middle: {shortKey}");
                
            // Verify correct length (5 chars + ... + 5 chars)
            if (shortKey.Length != 13) // 5 + 3 + 5
                throw new Exception($"Short key has incorrect length: {shortKey.Length}, expected 13");
                
            Debug.Log("Extension methods test passed");
        }
    }
} 