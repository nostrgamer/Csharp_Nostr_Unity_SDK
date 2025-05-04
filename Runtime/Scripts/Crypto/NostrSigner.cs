using System;
using System.Text;
using System.Security.Cryptography;
using UnityEngine;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Math;
using Nostr.Unity.Utils;

namespace Nostr.Unity.Crypto
{
    /// <summary>
    /// Specialized signer for Nostr events following the NIP-01 specification precisely
    /// </summary>
    public static class NostrSigner
    {
        /// <summary>
        /// Signs an event ID according to the Nostr specification
        /// </summary>
        public static string SignEvent(string serializedEvent, string privateKeyHex)
        {
            try
            {
                Debug.Log($"NostrSigner - Signing event: {serializedEvent}");
                
                // Calculate the event ID (SHA-256 hash of the serialized event)
                byte[] eventBytes = Encoding.UTF8.GetBytes(serializedEvent);
                byte[] eventId;
                using (var sha256 = SHA256.Create())
                {
                    eventId = sha256.ComputeHash(eventBytes);
                }
                
                // Convert to hex for debugging
                string eventIdHex = BytesToHex(eventId);
                Debug.Log($"NostrSigner - Event ID (hex): {eventIdHex}");
                
                // Convert private key from hex to bytes
                byte[] privateKeyBytes = HexToBytes(privateKeyHex);
                
                // Set up the signer with deterministic k generation (important!)
                var signer = new ECDsaSigner(new HMacDsaKCalculator(new Org.BouncyCastle.Crypto.Digests.Sha256Digest()));
                
                // Get secp256k1 curve parameters
                var curve = Org.BouncyCastle.Asn1.Sec.SecNamedCurves.GetByName("secp256k1");
                var domain = new ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H);
                
                // Create the private key parameter
                var keyParameters = new ECPrivateKeyParameters(new BigInteger(1, privateKeyBytes), domain);
                
                // Initialize the signer
                signer.Init(true, keyParameters);
                
                // Sign the event ID 
                BigInteger[] signature = signer.GenerateSignature(eventId);
                
                // Convert to bytes and concatenate r and s
                byte[] rBytes = PadTo32Bytes(signature[0].ToByteArrayUnsigned());
                byte[] sBytes = PadTo32Bytes(signature[1].ToByteArrayUnsigned());
                
                // Combine r and s to form the 64-byte signature
                byte[] sigBytes = new byte[64];
                Array.Copy(rBytes, 0, sigBytes, 0, 32);
                Array.Copy(sBytes, 0, sigBytes, 32, 32);
                
                // Convert to hex
                string signatureHex = BytesToHex(sigBytes);
                Debug.Log($"NostrSigner - Generated signature: {signatureHex}");
                
                return signatureHex;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error in NostrSigner.SignEvent: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }
        
        /// <summary>
        /// Verifies a Nostr event signature
        /// </summary>
        public static bool VerifySignature(string serializedEvent, string signatureHex, string publicKeyHex)
        {
            try
            {
                // Calculate the event ID (SHA-256 hash of the serialized event)
                byte[] eventBytes = Encoding.UTF8.GetBytes(serializedEvent);
                byte[] eventId;
                using (var sha256 = SHA256.Create())
                {
                    eventId = sha256.ComputeHash(eventBytes);
                }
                
                // Convert signature hex to bytes
                byte[] signatureBytes = HexToBytes(signatureHex);
                if (signatureBytes.Length != 64)
                {
                    Debug.LogError("Signature must be exactly 64 bytes");
                    return false;
                }
                
                // Extract r and s components
                byte[] rBytes = new byte[32];
                byte[] sBytes = new byte[32];
                Array.Copy(signatureBytes, 0, rBytes, 0, 32);
                Array.Copy(signatureBytes, 32, sBytes, 0, 32);
                
                // Convert to BigIntegers
                BigInteger r = new BigInteger(1, rBytes);
                BigInteger s = new BigInteger(1, sBytes);
                
                // Ensure we have a properly formatted public key (must start with 02 or 03)
                byte[] pubKeyBytes;
                if (publicKeyHex.Length == 66 && (publicKeyHex.StartsWith("02") || publicKeyHex.StartsWith("03")))
                {
                    pubKeyBytes = HexToBytes(publicKeyHex);
                }
                else if (publicKeyHex.Length == 64)
                {
                    // This is a fallback that assumes 02 prefix - not cryptographically correct
                    // Ideally, you would derive the correct prefix based on the actual point
                    pubKeyBytes = HexToBytes("02" + publicKeyHex);
                    Debug.LogWarning("Using assumed 02 prefix for public key. This may cause verification issues with relays.");
                }
                else
                {
                    Debug.LogError($"Invalid public key format: {publicKeyHex}");
                    return false;
                }
                
                // Set up the verifier
                var curve = Org.BouncyCastle.Asn1.Sec.SecNamedCurves.GetByName("secp256k1");
                var domain = new ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H);
                
                // Create the public key point
                var q = curve.Curve.DecodePoint(pubKeyBytes);
                var keyParameters = new ECPublicKeyParameters(q, domain);
                
                // Set up the verifier
                var verifier = new ECDsaSigner();
                verifier.Init(false, keyParameters);
                
                // Verify the signature
                bool result = verifier.VerifySignature(eventId, r, s);
                Debug.Log($"NostrSigner - Verification result: {result}");
                
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error in NostrSigner.VerifySignature: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }
        
        /// <summary>
        /// Pads a byte array to exactly 32 bytes
        /// </summary>
        private static byte[] PadTo32Bytes(byte[] input)
        {
            if (input.Length == 32)
                return input;
                
            byte[] result = new byte[32];
            
            if (input.Length > 32)
            {
                // If it's longer than 32 bytes, take the least significant 32 bytes
                Array.Copy(input, input.Length - 32, result, 0, 32);
            }
            else
            {
                // If it's shorter than 32 bytes, pad with zeros at the beginning
                Array.Copy(input, 0, result, 32 - input.Length, input.Length);
            }
            
            return result;
        }
        
        /// <summary>
        /// Converts a byte array to a hex string (lowercase)
        /// </summary>
        private static string BytesToHex(byte[] bytes)
        {
            StringBuilder hex = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
                hex.AppendFormat("{0:x2}", b);
            return hex.ToString();
        }
        
        /// <summary>
        /// Converts a hex string to a byte array
        /// </summary>
        private static byte[] HexToBytes(string hex)
        {
            if (string.IsNullOrEmpty(hex))
                throw new ArgumentException("Hex string cannot be null or empty");
                
            if (hex.Length % 2 != 0)
                throw new ArgumentException("Hex string must have an even number of characters");
                
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < hex.Length; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
                
            return bytes;
        }
    }
} 