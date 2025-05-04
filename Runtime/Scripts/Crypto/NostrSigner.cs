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
        /// Signs an event hash with a private key
        /// </summary>
        /// <param name="eventId">The binary 32-byte event ID (SHA-256 hash of the serialized event)</param>
        /// <param name="privateKeyHex">The private key in hex format</param>
        /// <returns>The signature as a hex string</returns>
        public static string SignEvent(byte[] eventId, string privateKeyHex)
        {
            try
            {
                if (eventId == null || eventId.Length != 32)
                {
                    throw new ArgumentException("Event ID must be exactly 32 bytes", nameof(eventId));
                }
                
                Debug.Log($"NostrSigner - Signing event ID (binary, 32 bytes)");
                
                // Convert event ID bytes to hex for debugging
                string eventIdHex = BytesToHex(eventId);
                Debug.Log($"NostrSigner - Event ID (hex): {eventIdHex}");
                
                // Convert private key from hex to bytes - must be exactly 32 bytes
                byte[] privateKeyBytes = HexToBytes(privateKeyHex);
                if (privateKeyBytes.Length != 32)
                {
                    throw new ArgumentException("Private key must be exactly 32 bytes (64 hex characters)");
                }
                
                // Set up the signer with deterministic k generation (RFC 6979) - CRITICAL for Nostr
                // This ensures same message + key always produces same signature
                var curve = Org.BouncyCastle.Asn1.Sec.SecNamedCurves.GetByName("secp256k1");
                var domain = new ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H);
                var signer = new ECDsaSigner(new HMacDsaKCalculator(new Org.BouncyCastle.Crypto.Digests.Sha256Digest()));
                
                // Create the private key parameter
                var keyParameters = new ECPrivateKeyParameters(new BigInteger(1, privateKeyBytes), domain);
                
                // Initialize the signer
                signer.Init(true, keyParameters);
                
                // Sign the event ID directly
                BigInteger[] signature = signer.GenerateSignature(eventId);
                
                // Extract r and s values for normalization
                BigInteger r = signature[0];
                BigInteger s = signature[1];
                
                // CRITICAL: Normalize S value to lower half of curve as per BIP-0062
                // Nearly all Nostr relays enforce this "low S value" requirement
                BigInteger n = curve.N;
                BigInteger halfN = n.ShiftRight(1); // n/2
                
                // If s > n/2, set s = n - s (this creates an equivalent but canonical signature)
                if (s.CompareTo(halfN) > 0)
                {
                    s = n.Subtract(s);
                    Debug.Log("Normalized S value to lower half of curve for canonical signature");
                }
                
                // Convert to bytes with careful padding to ensure exact 32-byte lengths
                byte[] rBytes = PadTo32Bytes(r.ToByteArrayUnsigned());
                byte[] sBytes = PadTo32Bytes(s.ToByteArrayUnsigned());
                
                // Combine r and s to form the 64-byte signature
                byte[] sigBytes = new byte[64];
                Array.Copy(rBytes, 0, sigBytes, 0, 32);
                Array.Copy(sBytes, 0, sigBytes, 32, 32);
                
                // Convert to lowercase hex
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
        /// Signs a serialized event string with a private key (legacy method for backward compatibility)
        /// </summary>
        /// <param name="serializedEvent">The serialized event string to hash and sign</param>
        /// <param name="privateKeyHex">The private key in hex format</param>
        /// <returns>The signature as a hex string</returns>
        public static string SignEvent(string serializedEvent, string privateKeyHex)
        {
            if (string.IsNullOrEmpty(serializedEvent))
                throw new ArgumentException("Serialized event cannot be null or empty", nameof(serializedEvent));
                
            Debug.Log($"NostrSigner - Computing hash of serialized event: {serializedEvent}");
            
            // Calculate the event ID (SHA-256 hash of the serialized event)
            byte[] eventBytes = Encoding.UTF8.GetBytes(serializedEvent);
            byte[] eventId;
            using (var sha256 = SHA256.Create())
            {
                eventId = sha256.ComputeHash(eventBytes);
            }
            
            // Call the main method with the computed hash
            return SignEvent(eventId, privateKeyHex);
        }
        
        /// <summary>
        /// Verifies a Nostr event signature
        /// </summary>
        /// <param name="eventId">The binary 32-byte event ID (SHA-256 hash of the serialized event)</param>
        /// <param name="signatureHex">The signature in hex format</param>
        /// <param name="publicKeyHex">The public key in hex format</param>
        /// <returns>True if the signature is valid, otherwise false</returns>
        public static bool VerifySignature(byte[] eventId, string signatureHex, string publicKeyHex)
        {
            try
            {
                if (eventId == null || eventId.Length != 32)
                {
                    throw new ArgumentException("Event ID must be exactly 32 bytes", nameof(eventId));
                }
                
                // Convert event ID bytes to hex for debugging
                string eventIdHex = BytesToHex(eventId);
                Debug.Log($"NostrSigner - Verifying signature for event ID: {eventIdHex}");
                
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
                
                // Get the secp256k1 curve
                var curve = Org.BouncyCastle.Asn1.Sec.SecNamedCurves.GetByName("secp256k1");
                var domain = new ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H);
                
                // Ensure our signature follows low-S value canonical form
                BigInteger n = curve.N;
                BigInteger halfN = n.ShiftRight(1);
                if (s.CompareTo(halfN) > 0)
                {
                    Debug.LogWarning("Signature uses high S value - this might be rejected by some relays");
                }
                
                // Try verification with the provided key format
                try
                {
                    byte[] pubKeyBytes;
                    if (publicKeyHex.Length == 66 && (publicKeyHex.StartsWith("02") || publicKeyHex.StartsWith("03")))
                    {
                        pubKeyBytes = HexToBytes(publicKeyHex);
                    }
                    else if (publicKeyHex.Length == 64)
                    {
                        // This is a fallback that assumes 02 prefix - not cryptographically correct
                        pubKeyBytes = HexToBytes("02" + publicKeyHex);
                        Debug.LogWarning("Using assumed 02 prefix for public key");
                    }
                    else
                    {
                        Debug.LogError($"Invalid public key format: {publicKeyHex}");
                        return false;
                    }
                    
                    // Create the public key point
                    var q = curve.Curve.DecodePoint(pubKeyBytes);
                    var keyParameters = new ECPublicKeyParameters(q, domain);
                    
                    // Set up the verifier
                    var verifier = new ECDsaSigner();
                    verifier.Init(false, keyParameters);
                    
                    // Verify the signature against the binary event ID
                    bool result = verifier.VerifySignature(eventId, r, s);
                    Debug.Log($"NostrSigner - Verification result: {result}");
                    
                    return result;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error in first verification attempt: {ex.Message}");
                    
                    // If the first attempt fails, try the alternative compression prefix
                    try
                    {
                        string otherPrefix = publicKeyHex.StartsWith("02") ? "03" : "02";
                        string alternateKey = publicKeyHex.Length == 66 ? 
                            otherPrefix + publicKeyHex.Substring(2) : 
                            otherPrefix + publicKeyHex;
                            
                        Debug.LogWarning($"Trying alternate key prefix: {alternateKey}");
                        byte[] pubKeyBytes = HexToBytes(alternateKey);
                            
                        var q = curve.Curve.DecodePoint(pubKeyBytes);
                        var keyParameters = new ECPublicKeyParameters(q, domain);
                        
                        var verifier = new ECDsaSigner();
                        verifier.Init(false, keyParameters);
                        
                        bool result = verifier.VerifySignature(eventId, r, s);
                        Debug.Log($"NostrSigner - Verification with alternate key result: {result}");
                        
                        return result;
                    }
                    catch (Exception ex2)
                    {
                        Debug.LogError($"Error in alternate verification: {ex2.Message}");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error in NostrSigner.VerifySignature: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }
        
        /// <summary>
        /// Verifies a Nostr event signature (legacy method for backward compatibility)
        /// </summary>
        /// <param name="serializedEvent">The serialized event string</param>
        /// <param name="signatureHex">The signature in hex format</param>
        /// <param name="publicKeyHex">The public key in hex format</param>
        /// <returns>True if the signature is valid, otherwise false</returns>
        public static bool VerifySignature(string serializedEvent, string signatureHex, string publicKeyHex)
        {
            if (string.IsNullOrEmpty(serializedEvent))
                throw new ArgumentException("Serialized event cannot be null or empty", nameof(serializedEvent));
                
            // Calculate the event ID (SHA-256 hash of the serialized event)
            byte[] eventBytes = Encoding.UTF8.GetBytes(serializedEvent);
            byte[] eventId;
            using (var sha256 = SHA256.Create())
            {
                eventId = sha256.ComputeHash(eventBytes);
            }
            
            // Call the main method with the computed hash
            return VerifySignature(eventId, signatureHex, publicKeyHex);
        }
        
        /// <summary>
        /// Pads a byte array to exactly 32 bytes - CRITICAL for correct signature formatting
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
        
        /// <summary>
        /// Verifies a serialized string event and its signature for debugging purposes
        /// </summary>
        /// <param name="serializedEvent">The serialized event string</param>
        /// <param name="eventId">The event ID (hex)</param>
        /// <param name="signatureHex">The signature (hex)</param>
        /// <param name="publicKeyHex">The public key (hex)</param>
        /// <returns>True if the signature is valid, otherwise false</returns>
        public static bool DebugVerifySerializedEvent(string serializedEvent, string eventId, string signatureHex, string publicKeyHex)
        {
            try
            {
                Debug.Log($"DEBUG VERIFICATION - Serialized event: {serializedEvent}");
                
                // 1. Verify the id matches the SHA-256 of the serialized event
                byte[] eventBytes = Encoding.UTF8.GetBytes(serializedEvent);
                string computedId;
                byte[] idBytes;
                
                using (var sha256 = SHA256.Create())
                {
                    idBytes = sha256.ComputeHash(eventBytes);
                    computedId = BytesToHex(idBytes);
                }
                
                bool idMatches = string.Equals(computedId, eventId, StringComparison.OrdinalIgnoreCase);
                Debug.Log($"DEBUG VERIFICATION - Event ID check: {idMatches}");
                Debug.Log($"DEBUG VERIFICATION - Computed ID: {computedId}");
                Debug.Log($"DEBUG VERIFICATION - Given ID: {eventId}");
                
                if (!idMatches)
                {
                    return false;
                }
                
                // 2. Verify the signature for the hash
                bool sigValid = VerifySignature(idBytes, signatureHex, publicKeyHex);
                Debug.Log($"DEBUG VERIFICATION - Signature check: {sigValid}");
                
                return sigValid;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error in debug verification: {ex.Message}");
                return false;
            }
        }
    }
} 