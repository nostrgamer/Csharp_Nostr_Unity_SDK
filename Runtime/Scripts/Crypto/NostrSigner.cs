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

        /// <summary>
        /// Verifies the signature of the event
        /// </summary>
        /// <param name="eventId">The event ID (hex)</param>
        /// <param name="signatureHex">The signature (hex)</param>
        /// <param name="publicKeyHex">The public key (hex)</param>
        /// <returns>True if the signature is valid, otherwise false</returns>
        public static bool VerifySignature(string eventId, string signatureHex, string publicKeyHex)
        {
            try
            {
                // Ensure all inputs are in lowercase
                eventId = eventId.ToLowerInvariant();
                signatureHex = signatureHex.ToLowerInvariant();
                publicKeyHex = publicKeyHex.ToLowerInvariant();
                
                // Check if pubkey starts with 02 or 03 (compressed format)
                if (publicKeyHex.Length == 66 && (publicKeyHex.StartsWith("02") || publicKeyHex.StartsWith("03")))
                {
                    // It's already in compressed format
                }
                else if (publicKeyHex.Length == 64)
                {
                    // Assume it's an uncompressed pubkey without the 02/03 prefix
                    // For verification we need the 02/03 prefix
                    publicKeyHex = "02" + publicKeyHex;
                }
                else
                {
                    Debug.LogError($"Invalid public key format: {publicKeyHex}");
                    return false;
                }
                
                // Convert hex strings to byte arrays
                byte[] eventIdBytes = HexToBytes(eventId);
                byte[] signatureBytes = HexToBytes(signatureHex);
                byte[] publicKeyBytes = HexToBytes(publicKeyHex);
                
                // Ensure the signature is in canonical form (low-S value)
                signatureBytes = EnsureCanonicalSignature(signatureBytes);
                
                return BouncyCryptography.VerifySignature(eventIdBytes, signatureBytes, publicKeyBytes);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error verifying signature: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Ensures the signature is in canonical form (low-S value) which is required by Nostr relays
        /// </summary>
        /// <param name="signature">The signature bytes (64 bytes: r || s)</param>
        /// <returns>The canonical signature</returns>
        private static byte[] EnsureCanonicalSignature(byte[] signature)
        {
            if (signature.Length != 64)
            {
                Debug.LogWarning($"Signature length is not 64 bytes: {signature.Length}");
                return signature;
            }
            
            // Extract the r and s values (32 bytes each)
            byte[] r = new byte[32];
            byte[] s = new byte[32];
            Buffer.BlockCopy(signature, 0, r, 0, 32);
            Buffer.BlockCopy(signature, 32, s, 0, 32);
            
            // Secp256k1 curve order (n) / 2
            // Hex: 7FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF5D576E7357A4501DDFE92F46681B20A0
            byte[] nHalfBytes = HexToBytes("7FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF5D576E7357A4501DDFE92F46681B20A0");
            
            // Check if s is greater than n/2 (high-S value)
            bool highS = false;
            for (int i = 0; i < 32; i++)
            {
                if (s[i] > nHalfBytes[i])
                {
                    highS = true;
                    break;
                }
                else if (s[i] < nHalfBytes[i])
                {
                    break;
                }
            }
            
            // If it's a high-S signature, convert to low-S
            if (highS)
            {
                Debug.Log("Converting high-S signature to low-S canonical form");
                
                // Create a new signature array with the same r but low-S value
                byte[] canonicalSignature = new byte[64];
                Buffer.BlockCopy(r, 0, canonicalSignature, 0, 32);
                
                // Calculate n - s (curve order - s)
                BigInteger curveOrder = new BigInteger(HexToBytes("FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFEBAAEDCE6AF48A03BBFD25E8CD0364141").Reverse().ToArray());
                BigInteger sValue = new BigInteger(s.Reverse().ToArray());
                BigInteger lowS = curveOrder - sValue;
                
                // Convert back to byte array
                byte[] lowSBytes = lowS.ToByteArray().Reverse().ToArray();
                
                // Pad with zeros if needed
                if (lowSBytes.Length < 32)
                {
                    byte[] padded = new byte[32];
                    Buffer.BlockCopy(lowSBytes, 0, padded, 32 - lowSBytes.Length, lowSBytes.Length);
                    lowSBytes = padded;
                }
                else if (lowSBytes.Length > 32)
                {
                    // Trim if too long
                    byte[] trimmed = new byte[32];
                    Buffer.BlockCopy(lowSBytes, lowSBytes.Length - 32, trimmed, 0, 32);
                    lowSBytes = trimmed;
                }
                
                Buffer.BlockCopy(lowSBytes, 0, canonicalSignature, 32, 32);
                return canonicalSignature;
            }
            
            return signature;
        }

        /// <summary>
        /// Ensures the signature is in canonical form (low-S value) which is required by Nostr relays
        /// </summary>
        /// <param name="signature">The signature bytes (64 bytes: r || s)</param>
        /// <returns>The canonical signature</returns>
        public static byte[] GetCanonicalSignature(byte[] signature)
        {
            return EnsureCanonicalSignature(signature);
        }

        /// <summary>
        /// Signs the event ID with a private key
        /// </summary>
        /// <param name="eventId">The event ID (hex string)</param>
        /// <param name="privateKey">The private key (hex string)</param>
        /// <returns>The signature (hex string)</returns>
        public static string SignEventId(string eventId, string privateKey)
        {
            try
            {
                // Convert hex strings to byte arrays
                byte[] eventIdBytes = HexToBytes(eventId);
                byte[] privateKeyBytes = HexToBytes(privateKey);
                
                // Sign the event ID
                byte[] signatureBytes = BouncyCryptography.SignMessage(eventIdBytes, privateKeyBytes);
                
                // Ensure canonical signature (low-S value)
                signatureBytes = EnsureCanonicalSignature(signatureBytes);
                
                // Convert signature bytes to hex string
                return BitConverter.ToString(signatureBytes).Replace("-", "").ToLowerInvariant();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error signing event ID: {ex.Message}");
                throw;
            }
        }
    }
} 