using System;

namespace NostrUnity.Crypto
{
    /// <summary>
    /// Essential cryptographic operations for Nostr
    /// </summary>
    public static class NostrCrypto
    {
        /// <summary>
        /// Signs a Nostr event using the provided private key
        /// </summary>
        public static string SignEvent(string eventId, string privateKey)
        {
            // Use KeyPair for signing
            var keyPair = new KeyPair(privateKey);
            return keyPair.SignId(eventId);
        }
        
        /// <summary>
        /// Verifies a signature for a Nostr event
        /// </summary>
        public static bool VerifySignature(string eventId, string signature, string publicKey)
        {
            return KeyPair.VerifySignature(publicKey, eventId, signature);
        }
        
        /// <summary>
        /// Converts hex string to byte array
        /// </summary>
        public static byte[] HexToBytes(string hex)
        {
            return CryptoUtils.HexToBytes(hex);
        }
        
        /// <summary>
        /// Converts byte array to hex string
        /// </summary>
        public static string BytesToHex(byte[] bytes)
        {
            return CryptoUtils.BytesToHex(bytes);
        }
    }
} 