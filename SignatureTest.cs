using System;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Math;
using Newtonsoft.Json;

namespace Nostr.Unity.Tests
{
    /// <summary>
    /// Provides test methods for Nostr signatures using BouncyCastle
    /// </summary>
    public static class NostrSignatureTest
    {
        /// <summary>
        /// Runs a complete signature test and returns the results
        /// </summary>
        public static string RunTest(string privateKeyHex = null)
        {
            StringBuilder output = new StringBuilder();
            output.AppendLine("=== Nostr Signature Test ===");
            
            // Test private key (you can replace this with your actual test key)
            if (string.IsNullOrEmpty(privateKeyHex))
            {
                privateKeyHex = "5ae29bc19d45304ce86ade58c04693bc7e93382365d2f0a7cb4b1e1ec76299f1";
            }
            output.AppendLine($"Test private key: {privateKeyHex}");
            
            // Get public key
            string publicKeyHex = GetPublicKey(privateKeyHex);
            output.AppendLine($"Derived public key: {publicKeyHex}");
            string uncompressedPublicKey = publicKeyHex.Substring(2);
            output.AppendLine($"Uncompressed public key: {uncompressedPublicKey}");
            
            // Test 1: Sign a simple known hash
            output.AppendLine("\n--- Test 1: Basic Signing ---");
            string testHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
            output.AppendLine($"Test hash: {testHash}");
            
            string signature = SignEventId(testHash, privateKeyHex);
            output.AppendLine($"Generated signature: {signature}");
            
            // Test signature verification
            bool verified = VerifySignatureHex(testHash, signature, publicKeyHex);
            output.AppendLine($"Verification result: {verified}");
            
            // Test 2: Create a full Nostr event
            output.AppendLine("\n--- Test 2: Nostr Event ---");
            string content = "Test message";
            long createdAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string[][] tags = new string[0][];
            
            // Create serialized event for signing
            string serializedEvent = SerializeForSigning(uncompressedPublicKey, createdAt, 1, content, tags);
            output.AppendLine($"Serialized event: {serializedEvent}");
            
            // Compute event ID
            byte[] eventBytes = Encoding.UTF8.GetBytes(serializedEvent);
            byte[] idBytes;
            using (var sha256 = SHA256.Create())
            {
                idBytes = sha256.ComputeHash(eventBytes);
            }
            string eventId = BytesToHex(idBytes);
            output.AppendLine($"Event ID: {eventId}");
            
            // Sign the event ID
            string eventSignature = SignEventId(eventId, privateKeyHex);
            output.AppendLine($"Event signature: {eventSignature}");
            
            // Test event signature verification
            bool eventVerified = VerifySignatureHex(eventId, eventSignature, publicKeyHex);
            output.AppendLine($"Event verification result: {eventVerified}");
            
            output.AppendLine("\nTests complete.");
            return output.ToString();
        }
        
        /// <summary>
        /// Get a compressed public key from private key
        /// </summary>
        public static string GetPublicKey(string privateKeyHex)
        {
            try
            {
                byte[] privateKeyBytes = HexToBytes(privateKeyHex);
                
                // Get the secp256k1 curve
                var curve = Org.BouncyCastle.Asn1.Sec.SecNamedCurves.GetByName("secp256k1");
                var domain = new Org.BouncyCastle.Crypto.Parameters.ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H);
                
                // Create the private key
                var d = new BigInteger(1, privateKeyBytes);
                
                // Derive the public key
                var q = domain.G.Multiply(d);
                
                // Get the compressed format
                byte[] compressedPubKey = q.GetEncoded(true);
                
                return BytesToHex(compressedPubKey);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting public key: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Sign an event ID with a private key
        /// </summary>
        public static string SignEventId(string eventIdHex, string privateKeyHex)
        {
            try
            {
                // Convert hex inputs to bytes
                byte[] eventIdBytes = HexToBytes(eventIdHex);
                byte[] privateKeyBytes = HexToBytes(privateKeyHex);
                
                // Get the secp256k1 curve
                var curve = Org.BouncyCastle.Asn1.Sec.SecNamedCurves.GetByName("secp256k1");
                var domain = new ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H);
                
                // Create the private key parameter
                var privKeyParams = new ECPrivateKeyParameters(new BigInteger(1, privateKeyBytes), domain);
                
                // Use deterministic ECDSA (RFC 6979)
                var signer = new ECDsaSigner(new HMacDsaKCalculator(new Org.BouncyCastle.Crypto.Digests.Sha256Digest()));
                signer.Init(true, privKeyParams);
                
                // Sign the message
                BigInteger[] signature = signer.GenerateSignature(eventIdBytes);
                BigInteger r = signature[0];
                BigInteger s = signature[1];
                
                // Ensure low-S value (BIP 0062)
                BigInteger n = curve.N;
                BigInteger halfN = n.ShiftRight(1);
                if (s.CompareTo(halfN) > 0)
                {
                    s = n.Subtract(s);
                    Console.WriteLine("Converted signature to low-S form");
                }
                
                // Combine r and s into a 64-byte signature
                byte[] rBytes = r.ToByteArrayUnsigned();
                byte[] sBytes = s.ToByteArrayUnsigned();
                
                byte[] sigBytes = new byte[64];
                
                // Ensure r and s are 32 bytes each with padding if needed
                if (rBytes.Length < 32)
                {
                    Array.Copy(rBytes, 0, sigBytes, 32 - rBytes.Length, rBytes.Length);
                }
                else
                {
                    Array.Copy(rBytes, Math.Max(0, rBytes.Length - 32), sigBytes, 0, Math.Min(32, rBytes.Length));
                }
                
                if (sBytes.Length < 32)
                {
                    Array.Copy(sBytes, 0, sigBytes, 64 - sBytes.Length, sBytes.Length);
                }
                else
                {
                    Array.Copy(sBytes, Math.Max(0, sBytes.Length - 32), sigBytes, 32, Math.Min(32, sBytes.Length));
                }
                
                return BytesToHex(sigBytes);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error signing message: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Verify a signature using hex strings
        /// </summary>
        public static bool VerifySignatureHex(string msgHex, string sigHex, string pubKeyHex)
        {
            try
            {
                byte[] msgBytes = HexToBytes(msgHex);
                byte[] sigBytes = HexToBytes(sigHex);
                byte[] pubKeyBytes = HexToBytes(pubKeyHex);
                
                if (sigBytes.Length != 64)
                {
                    Console.WriteLine($"Invalid signature length: {sigBytes.Length}");
                    return false;
                }
                
                // Extract r and s values
                byte[] rBytes = new byte[32];
                byte[] sBytes = new byte[32];
                Array.Copy(sigBytes, 0, rBytes, 0, 32);
                Array.Copy(sigBytes, 32, sBytes, 0, 32);
                
                BigInteger r = new BigInteger(1, rBytes);
                BigInteger s = new BigInteger(1, sBytes);
                
                // Get the secp256k1 curve
                var curve = Org.BouncyCastle.Asn1.Sec.SecNamedCurves.GetByName("secp256k1");
                var domain = new ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H);
                
                var point = curve.Curve.DecodePoint(pubKeyBytes);
                var pubKeyParams = new ECPublicKeyParameters(point, domain);
                
                // Initialize verifier
                var verifier = new ECDsaSigner();
                verifier.Init(false, pubKeyParams);
                
                // Verify the signature
                return verifier.VerifySignature(msgBytes, r, s);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error verifying signature: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Serialize an event for signing (Nostr format)
        /// </summary>
        public static string SerializeForSigning(string pubkey, long createdAt, int kind, string content, string[][] tags)
        {
            // Build the event array for serialization
            var eventArray = new object[]
            {
                0,              // Version marker, always 0
                pubkey,         // Public key (hex string, no prefix)
                createdAt,      // Unix timestamp
                kind,           // Event kind
                tags,           // Event tags
                content         // Event content
            };
            
            return JsonConvert.SerializeObject(eventArray);
        }
        
        /// <summary>
        /// Convert bytes to hex string
        /// </summary>
        public static string BytesToHex(byte[] bytes)
        {
            StringBuilder hex = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
            {
                hex.AppendFormat("{0:x2}", b);
            }
            return hex.ToString();
        }
        
        /// <summary>
        /// Convert hex string to bytes
        /// </summary>
        public static byte[] HexToBytes(string hex)
        {
            if (string.IsNullOrEmpty(hex))
                throw new ArgumentException("Hex string cannot be null or empty");
                
            if (hex.Length % 2 != 0)
                throw new ArgumentException("Hex string must have an even number of characters");
                
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < hex.Length; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            }
                
            return bytes;
        }
    }
} 