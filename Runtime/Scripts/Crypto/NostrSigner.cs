using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Crypto.Digests;
using Nostr.Unity.Utils;

namespace Nostr.Unity.Crypto
{
    /// <summary>
    /// Specialized signer for Nostr events following the NIP-01 specification precisely
    /// </summary>
    public static class NostrSigner
    {
        // Static BouncyCryptography instance for operations
        private static readonly Nostr.Unity.BouncyCryptography _cryptography = new Nostr.Unity.BouncyCryptography();
        
        /// <summary>
        /// Signs a Nostr event using the provided private key
        /// </summary>
        /// <param name="eventId">The event ID (32-byte SHA-256 hash)</param>
        /// <param name="privateKeyHex">The private key in hex format</param>
        /// <returns>The signature in hex format</returns>
        public static string SignEvent(byte[] eventId, string privateKeyHex)
        {
            try
            {
                if (eventId == null || eventId.Length != 32)
                    throw new ArgumentException("Event ID must be 32 bytes", nameof(eventId));
                
                if (string.IsNullOrEmpty(privateKeyHex))
                    throw new ArgumentException("Private key cannot be null or empty", nameof(privateKeyHex));
                
                // Normalize to lowercase and strip any "nsec" prefix
                privateKeyHex = privateKeyHex.ToLowerInvariant();
                if (privateKeyHex.StartsWith("nsec"))
                {
                    // Try to decode from bech32
                    try
                    {
                        string decodedKey = Bech32Util.DecodeKey(privateKeyHex);
                        if (!string.IsNullOrEmpty(decodedKey))
                        {
                            privateKeyHex = decodedKey;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error decoding nsec: {ex.Message}");
                    }
                }
                
                // Ensure key is exactly 64 characters (32 bytes) hex
                if (privateKeyHex.Length != 64 || !System.Text.RegularExpressions.Regex.IsMatch(privateKeyHex, "^[0-9a-f]{64}$"))
                {
                    throw new ArgumentException("Private key must be 64 hex characters", nameof(privateKeyHex));
                }
                
                // Convert the private key from hex to bytes
                byte[] privateKeyBytes = HexToBytes(privateKeyHex);
                
                // Sign the message hash with the private key
                byte[] signatureBytes = SignMessage(eventId, privateKeyBytes);
                
                // Debug logging
                Debug.Log($"SignEvent - Event ID length: {eventId.Length} bytes");
                Debug.Log($"SignEvent - Private key length: {privateKeyBytes.Length} bytes");
                Debug.Log($"SignEvent - Signature length: {signatureBytes.Length} bytes");
                
                // Convert the signature to a hex string
                string signature = BytesToHex(signatureBytes).ToLowerInvariant();
                
                return signature;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error signing event: {ex.Message}");
                Debug.LogError($"Stack trace: {ex.StackTrace}");
                throw; // Re-throw to let the caller handle it
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
                
            Debug.Log($"[DEBUG] NostrSigner - Computing hash of serialized event: {serializedEvent}");
            
            // Calculate the event ID (SHA-256 hash of the serialized event)
            byte[] eventBytes = Encoding.UTF8.GetBytes(serializedEvent);
            Debug.Log($"[DEBUG] NostrSigner - Serialized event bytes length: {eventBytes.Length}");
            
            byte[] eventId;
            using (var sha256 = SHA256.Create())
            {
                eventId = sha256.ComputeHash(eventBytes);
            }
            
            string eventIdHex = BytesToHex(eventId);
            Debug.Log($"[DEBUG] NostrSigner - Computed event ID hash: {eventIdHex}");
            
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
                
                // Validate input formats
                if (string.IsNullOrEmpty(signatureHex))
                {
                    Debug.LogError("[DEBUG] Signature is null or empty");
                    return false;
                }
                
                if (string.IsNullOrEmpty(publicKeyHex))
                {
                    Debug.LogError("[DEBUG] Public key is null or empty");
                    return false;
                }
                
                // Normalize inputs to lowercase
                signatureHex = signatureHex.ToLowerInvariant();
                publicKeyHex = publicKeyHex.ToLowerInvariant();
                
                // Handle different public key formats
                byte[] pubKeyBytes;
                if (publicKeyHex.Length == 64) // Raw 32-byte key without prefix
                {
                    // Compressed format is actually required for verification
                    // We'll try both possible prefixes (02 and 03)
                    Debug.Log("[DEBUG] Uncompressed public key detected, will try both compression prefixes");
                    pubKeyBytes = new byte[33];
                    pubKeyBytes[0] = 0x02; // First try with 02 prefix
                    Array.Copy(HexToBytes(publicKeyHex), 0, pubKeyBytes, 1, 32);
                }
                else if (publicKeyHex.Length == 66 && (publicKeyHex.StartsWith("02") || publicKeyHex.StartsWith("03")))
                {
                    // Already compressed format
                    Debug.Log($"[DEBUG] Using compressed public key with prefix: {publicKeyHex.Substring(0, 2)}");
                    pubKeyBytes = HexToBytes(publicKeyHex);
                }
                else
                {
                    Debug.LogError($"Invalid public key format: {publicKeyHex}");
                    return false;
                }
                
                // Convert signature to bytes
                if (signatureHex.Length != 128)
                {
                    Debug.LogError($"[DEBUG] Invalid signature length: {signatureHex.Length} chars, expected 128");
                    return false;
                }
                
                byte[] signatureBytes = HexToBytes(signatureHex);
                if (signatureBytes.Length != 64)
                {
                    Debug.LogError($"[DEBUG] Invalid signature byte length: {signatureBytes.Length}, expected 64");
                    return false;
                }
                
                // Split signature into r and s components
                byte[] rBytes = new byte[32];
                byte[] sBytes = new byte[32];
                Array.Copy(signatureBytes, 0, rBytes, 0, 32);
                Array.Copy(signatureBytes, 32, sBytes, 0, 32);
                
                var r = new BigInteger(1, rBytes);
                var s = new BigInteger(1, sBytes);
                
                // Get the secp256k1 curve
                var curve = Org.BouncyCastle.Asn1.Sec.SecNamedCurves.GetByName("secp256k1");
                var domain = new ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H);
                
                // Create the public key point
                var q = curve.Curve.DecodePoint(pubKeyBytes);
                var keyParameters = new ECPublicKeyParameters(q, domain);
                
                // Set up the verifier
                var verifier = new ECDsaSigner();
                verifier.Init(false, keyParameters);
                
                // Verify the signature against the binary event ID
                bool result = verifier.VerifySignature(eventId, r, s);
                
                // If verification fails with 02 prefix, try with 03 prefix
                if (!result && publicKeyHex.Length == 64)
                {
                    Debug.Log("[DEBUG] First verification attempt failed, trying with 03 prefix");
                    pubKeyBytes[0] = 0x03;
                    q = curve.Curve.DecodePoint(pubKeyBytes);
                    keyParameters = new ECPublicKeyParameters(q, domain);
                    verifier.Init(false, keyParameters);
                    result = verifier.VerifySignature(eventId, r, s);
                }
                
                Debug.Log($"[DEBUG] Signature verification result: {result}");
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DEBUG] Error in VerifySignature: {ex.Message}");
                Debug.LogError($"[DEBUG] Stack trace: {ex.StackTrace}");
                return false;
            }
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
        /// Verifies a signature using hex string inputs
        /// </summary>
        /// <param name="messageHex">The message hex string</param>
        /// <param name="signatureHex">The signature hex string</param>
        /// <param name="publicKeyHex">The public key hex string</param>
        /// <returns>True if verified, otherwise false</returns>
        public static bool VerifySignatureHex(string messageHex, string signatureHex, string publicKeyHex)
        {
            try
            {
                // Validate inputs
                if (string.IsNullOrEmpty(messageHex))
                {
                    Debug.LogError("[DEBUG] VerifySignatureHex: Message hex is null or empty");
                    return false;
                }
                
                if (string.IsNullOrEmpty(signatureHex))
                {
                    Debug.LogError("[DEBUG] VerifySignatureHex: Signature hex is null or empty");
                    return false;
                }
                
                if (string.IsNullOrEmpty(publicKeyHex))
                {
                    Debug.LogError("[DEBUG] VerifySignatureHex: Public key hex is null or empty");
                    return false;
                }
                
                Debug.Log($"[DEBUG] VerifySignatureHex - Message: {messageHex}");
                Debug.Log($"[DEBUG] VerifySignatureHex - Signature: {signatureHex}");
                Debug.Log($"[DEBUG] VerifySignatureHex - Public Key: {publicKeyHex}");
                
                // Convert hex strings to bytes
                byte[] messageBytes = HexToBytes(messageHex);
                byte[] signatureBytes = HexToBytes(signatureHex);
                byte[] publicKeyBytes = HexToBytes(publicKeyHex);
                
                Debug.Log($"[DEBUG] VerifySignatureHex - Message bytes length: {messageBytes.Length}");
                Debug.Log($"[DEBUG] VerifySignatureHex - Signature bytes length: {signatureBytes.Length}");
                Debug.Log($"[DEBUG] VerifySignatureHex - Public key bytes length: {publicKeyBytes.Length}");
                
                // Verify the signature
                bool result = VerifySignature(messageBytes, signatureBytes, publicKeyBytes);
                Debug.Log($"[DEBUG] VerifySignatureHex - Verification result: {result}");
                
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DEBUG] Error in VerifySignatureHex: {ex.Message}");
                Debug.LogError($"[DEBUG] Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Verifies a signature using byte arrays
        /// </summary>
        /// <param name="messageBytes">The message bytes (typically event ID hash)</param>
        /// <param name="signatureBytes">The signature bytes (r and s concatenated)</param>
        /// <param name="publicKeyBytes">The public key bytes (compressed form with prefix)</param>
        /// <returns>True if verified, otherwise false</returns>
        public static bool VerifySignature(byte[] messageBytes, byte[] signatureBytes, byte[] publicKeyBytes)
        {
            try
            {
                // Validate inputs
                if (messageBytes == null || messageBytes.Length == 0)
                {
                    Debug.LogError("[DEBUG] VerifySignature: Message bytes are null or empty");
                    return false;
                }
                
                if (signatureBytes == null || signatureBytes.Length != 64)
                {
                    Debug.LogError($"[DEBUG] VerifySignature: Signature has invalid length: {signatureBytes?.Length ?? 0} bytes, expected 64");
                    return false;
                }
                
                if (publicKeyBytes == null || publicKeyBytes.Length == 0)
                {
                    Debug.LogError("[DEBUG] VerifySignature: Public key bytes are null or empty");
                    return false;
                }
                
                // Ensure the public key is properly formatted
                if (publicKeyBytes.Length != 33 && publicKeyBytes.Length != 65)
                {
                    Debug.LogError($"[DEBUG] VerifySignature: Public key has unexpected length: {publicKeyBytes.Length}");
                    Debug.LogError("[DEBUG] Expected 33 bytes (compressed) or 65 bytes (uncompressed)");
                    
                    // If we have 32 bytes, it might be missing the compression prefix
                    if (publicKeyBytes.Length == 32)
                    {
                        Debug.LogWarning("[DEBUG] Public key appears to be missing compression prefix, attempting to add 02 prefix");
                        
                        // Create a new array with the 02 prefix
                        byte[] fixedKeyBytes = new byte[33];
                        fixedKeyBytes[0] = 0x02; // Add the compression prefix
                        Array.Copy(publicKeyBytes, 0, fixedKeyBytes, 1, 32);
                        publicKeyBytes = fixedKeyBytes;
                        
                        Debug.LogWarning($"[DEBUG] Modified public key length: {publicKeyBytes.Length} bytes");
                    }
                }
                
                // Extract r and s components from signature
                byte[] rBytes = new byte[32];
                byte[] sBytes = new byte[32];
                Buffer.BlockCopy(signatureBytes, 0, rBytes, 0, 32);
                Buffer.BlockCopy(signatureBytes, 32, sBytes, 0, 32);
                
                // Convert to BigIntegers
                BigInteger r = new BigInteger(1, rBytes);
                BigInteger s = new BigInteger(1, sBytes);
                
                // Debug the r and s values
                Debug.Log($"[DEBUG] VerifySignature - r: {r.ToString(16)}");
                Debug.Log($"[DEBUG] VerifySignature - s: {s.ToString(16)}");
                
                // Get the secp256k1 curve
                var curve = Org.BouncyCastle.Asn1.Sec.SecNamedCurves.GetByName("secp256k1");
                var domain = new ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H);
                
                // Validate s is in canonical form (low-S value)
                var n = curve.N;
                var halfN = n.ShiftRight(1);
                if (s.CompareTo(halfN) > 0)
                {
                    Debug.LogWarning("[DEBUG] VerifySignature - Signature uses high-S value, normalizing for verification");
                    s = n.Subtract(s); // Convert to low-S equivalent
                }
                
                // Try primary verification
                try
                {
                    // Create the public key point
                    var point = curve.Curve.DecodePoint(publicKeyBytes);
                    var pubKeyParams = new ECPublicKeyParameters(point, domain);
                    
                    // Set up the verifier
                    var verifier = new ECDsaSigner();
                    verifier.Init(false, pubKeyParams);
                    
                    // Verify the signature
                    bool result = verifier.VerifySignature(messageBytes, r, s);
                    Debug.Log($"[DEBUG] VerifySignature - Primary verification result: {result}");
                    
                    if (result)
                        return true;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[DEBUG] Error in primary verification: {ex.Message}");
                    // Continue to try alternate verification
                }
                
                // If primary verification failed, try alternative public key format
                if (publicKeyBytes.Length == 33)
                {
                    try
                    {
                        // If the key starts with 0x02, try with 0x03 and vice versa
                        byte[] altPublicKeyBytes = new byte[33];
                        Array.Copy(publicKeyBytes, altPublicKeyBytes, 33);
                        altPublicKeyBytes[0] = (byte)(altPublicKeyBytes[0] == 0x02 ? 0x03 : 0x02);
                        
                        Debug.LogWarning($"[DEBUG] Trying alternate public key prefix: 0x{altPublicKeyBytes[0]:X2}");
                        
                        var point = curve.Curve.DecodePoint(altPublicKeyBytes);
                        var pubKeyParams = new ECPublicKeyParameters(point, domain);
                        
                        var verifier = new ECDsaSigner();
                        verifier.Init(false, pubKeyParams);
                        
                        bool result = verifier.VerifySignature(messageBytes, r, s);
                        Debug.Log($"[DEBUG] VerifySignature - Alternate verification result: {result}");
                        
                        return result;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[DEBUG] Error in alternate verification: {ex.Message}");
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DEBUG] Error in VerifySignature: {ex.Message}");
                Debug.LogError($"[DEBUG] Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Signs a message with a private key
        /// </summary>
        /// <param name="messageHash">The message hash to sign (32 bytes)</param>
        /// <param name="privateKey">The private key (32 bytes)</param>
        /// <returns>The signature (64 bytes)</returns>
        public static byte[] SignMessage(byte[] messageHash, byte[] privateKey)
        {
            try
            {
                // Use the BouncyCryptography instance to sign
                return _cryptography.Sign(messageHash, privateKey);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error in SignMessage: {ex.Message}");
                throw;
            }
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
        public static string BytesToHex(byte[] bytes)
        {
            StringBuilder hex = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
                hex.AppendFormat("{0:x2}", b);
            return hex.ToString();
        }
        
        /// <summary>
        /// Converts a hex string to a byte array
        /// </summary>
        public static byte[] HexToBytes(string hex)
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
        /// Ensures the signature is in canonical form (low-S value) which is required by Nostr relays
        /// </summary>
        /// <param name="signature">The signature bytes (64 bytes: r || s)</param>
        /// <returns>The canonical signature</returns>
        private static byte[] EnsureCanonicalSignature(byte[] signature)
        {
            if (signature.Length != 64)
            {
                Debug.LogError($"CRITICAL ERROR: Signature length is not 64 bytes: {signature.Length}");
                return signature;
            }
            
            try
            {
                // Extract the r and s values (32 bytes each)
                byte[] r = new byte[32];
                byte[] s = new byte[32];
                Buffer.BlockCopy(signature, 0, r, 0, 32);
                Buffer.BlockCopy(signature, 32, s, 0, 32);
                
                // DEBUG: Log the components
                Debug.Log($"Signature components - R: {BytesToHex(r)}, S: {BytesToHex(s)}");
                
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
                    
                    // Curve order for secp256k1
                    // Hex: FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFEBAAEDCE6AF48A03BBFD25E8CD0364141
                    byte[] curveOrderBytes = HexToBytes("FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFEBAAEDCE6AF48A03BBFD25E8CD0364141");
                    
                    // Create BigInteger representations with BouncyCastle format
                    BigInteger curveOrder = new BigInteger(1, curveOrderBytes);
                    BigInteger sValue = new BigInteger(1, s);
                    
                    // Calculate n - s using Subtract method
                    BigInteger lowS = curveOrder.Subtract(sValue);
                    
                    // Convert back to byte array with proper padding
                    byte[] lowSBytes = lowS.ToByteArrayUnsigned();
                    
                    // Ensure it's exactly 32 bytes
                    if (lowSBytes.Length < 32)
                    {
                        byte[] padded = new byte[32];
                        Buffer.BlockCopy(lowSBytes, 0, padded, 32 - lowSBytes.Length, lowSBytes.Length);
                        lowSBytes = padded;
                    }
                    else if (lowSBytes.Length > 32)
                    {
                        // Trim if too long (should not happen with properly formatted signatures)
                        byte[] trimmed = new byte[32];
                        Buffer.BlockCopy(lowSBytes, lowSBytes.Length - 32, trimmed, 0, 32);
                        lowSBytes = trimmed;
                    }
                    
                    // Copy the new S value to the canonical signature
                    Buffer.BlockCopy(lowSBytes, 0, canonicalSignature, 32, 32);
                    
                    Debug.Log($"Original S: {BytesToHex(s)}");
                    Debug.Log($"Canonical S: {BytesToHex(lowSBytes)}");
                    
                    return canonicalSignature;
                }
                else
                {
                    Debug.Log("Signature already has low-S value, no canonicalization needed");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error in EnsureCanonicalSignature: {ex.Message}");
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
                // Ensure inputs are valid
                if (string.IsNullOrEmpty(eventId) || eventId.Length != 64)
                    throw new ArgumentException("Event ID must be exactly 64 characters", nameof(eventId));
                    
                if (string.IsNullOrEmpty(privateKey) || privateKey.Length != 64)
                    throw new ArgumentException("Private key must be exactly 64 characters", nameof(privateKey));
                
                // Convert hex strings to byte arrays
                byte[] eventIdBytes = HexToBytes(eventId);
                byte[] privateKeyBytes = HexToBytes(privateKey);
                
                // Get the secp256k1 curve parameters
                var curve = Org.BouncyCastle.Asn1.Sec.SecNamedCurves.GetByName("secp256k1");
                var domain = new Org.BouncyCastle.Crypto.Parameters.ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H);
                
                // Create the private key parameter
                var privKeyParams = new Org.BouncyCastle.Crypto.Parameters.ECPrivateKeyParameters(
                    new Org.BouncyCastle.Math.BigInteger(1, privateKeyBytes), domain);
                
                // Use deterministic ECDSA (RFC 6979) - this is critical for Nostr
                var signer = new Org.BouncyCastle.Crypto.Signers.ECDsaSigner(
                    new Org.BouncyCastle.Crypto.Signers.HMacDsaKCalculator(
                        new Org.BouncyCastle.Crypto.Digests.Sha256Digest()));
                        
                signer.Init(true, privKeyParams);
                
                // Sign the event ID
                Org.BouncyCastle.Math.BigInteger[] signature = signer.GenerateSignature(eventIdBytes);
                Org.BouncyCastle.Math.BigInteger r = signature[0];
                Org.BouncyCastle.Math.BigInteger s = signature[1];
                
                // CRITICAL: Normalize S value to lower half of curve as per BIP-0062
                Org.BouncyCastle.Math.BigInteger n = curve.N;
                Org.BouncyCastle.Math.BigInteger halfN = n.ShiftRight(1);
                
                // If s > n/2, set s = n - s (this creates an equivalent but canonical signature)
                if (s.CompareTo(halfN) > 0)
                {
                    s = n.Subtract(s);
                    Debug.Log("Normalized S value to low-S form for canonical signature");
                }
                
                // Combine r and s into a 64-byte signature
                byte[] rBytes = r.ToByteArrayUnsigned();
                byte[] sBytes = s.ToByteArrayUnsigned();
                
                // Create a properly padded 64-byte signature
                byte[] sigBytes = new byte[64];
                
                // Ensure r and s are exactly 32 bytes with proper padding
                if (rBytes.Length < 32)
                {
                    // Pad with zeros at the beginning
                    Buffer.BlockCopy(rBytes, 0, sigBytes, 32 - rBytes.Length, rBytes.Length);
                }
                else
                {
                    // Copy the least significant 32 bytes if larger
                    Buffer.BlockCopy(rBytes, Math.Max(0, rBytes.Length - 32), sigBytes, 0, Math.Min(32, rBytes.Length));
                }
                
                if (sBytes.Length < 32)
                {
                    // Pad with zeros at the beginning
                    Buffer.BlockCopy(sBytes, 0, sigBytes, 64 - sBytes.Length, sBytes.Length);
                }
                else
                {
                    // Copy the least significant 32 bytes if larger
                    Buffer.BlockCopy(sBytes, Math.Max(0, sBytes.Length - 32), sigBytes, 32, Math.Min(32, sBytes.Length));
                }
                
                string signatureHex = BytesToHex(sigBytes).ToLowerInvariant();
                
                // Double check signature length
                if (signatureHex.Length != 128)
                {
                    throw new InvalidOperationException($"Generated signature has incorrect length: {signatureHex.Length}, expected 128");
                }
                
                return signatureHex;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error signing event ID: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Pads a byte array to the specified length if needed
        /// </summary>
        /// <param name="input">The input byte array</param>
        /// <param name="targetLength">The target length</param>
        /// <returns>A padded byte array of the target length</returns>
        private static byte[] PadIfNeeded(byte[] input, int targetLength)
        {
            if (input.Length == targetLength)
                return input;
                
            byte[] result = new byte[targetLength];
            
            if (input.Length > targetLength)
            {
                // If it's longer than targetLength bytes, take the least significant bytes
                Array.Copy(input, input.Length - targetLength, result, 0, targetLength);
            }
            else
            {
                // If it's shorter than targetLength bytes, pad with zeros at the beginning
                Array.Copy(input, 0, result, targetLength - input.Length, input.Length);
            }
            
            return result;
        }

        /// <summary>
        /// Diagnostics tool to check if the public key is in the correct format for Nostr relays
        /// </summary>
        /// <param name="publicKeyHex">The public key to check</param>
        /// <returns>A status report on key format</returns>
        public static string DiagnosePublicKeyFormat(string publicKeyHex)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"Diagnosing public key: {publicKeyHex}");
                
                // Check length
                sb.AppendLine($"Key length: {publicKeyHex.Length} characters");
                
                if (publicKeyHex.Length == 66)
                {
                    if (publicKeyHex.StartsWith("02") || publicKeyHex.StartsWith("03"))
                    {
                        sb.AppendLine("✓ Key appears to be in compressed format with prefix");
                        sb.AppendLine($"Compression prefix: {publicKeyHex.Substring(0, 2)}");
                        
                        // Check if it can be decoded as a valid point
                        try
                        {
                            byte[] pubKeyBytes = HexToBytes(publicKeyHex);
                            var curve = Org.BouncyCastle.Asn1.Sec.SecNamedCurves.GetByName("secp256k1");
                            var q = curve.Curve.DecodePoint(pubKeyBytes);
                            sb.AppendLine("✓ Key is valid on the secp256k1 curve");
                            
                            // Strip prefix for Nostr standard
                            string nostrStandardKey = publicKeyHex.Substring(2);
                            sb.AppendLine($"Recommended Nostr event pubkey value: {nostrStandardKey}");
                        }
                        catch (Exception ex)
                        {
                            sb.AppendLine($"✗ Key could not be decoded as a valid curve point: {ex.Message}");
                        }
                    }
                    else
                    {
                        sb.AppendLine("✗ Key is 66 characters but doesn't start with 02 or 03 compression prefix");
                    }
                }
                else if (publicKeyHex.Length == 64)
                {
                    sb.AppendLine("✓ Key appears to be in standard Nostr format (uncompressed, 64 chars)");
                    
                    // Check if it can be used with 02 or 03 prefix
                    bool valid02 = false;
                    bool valid03 = false;
                    
                    try
                    {
                        byte[] pubKeyBytes02 = HexToBytes("02" + publicKeyHex);
                        var curve = Org.BouncyCastle.Asn1.Sec.SecNamedCurves.GetByName("secp256k1");
                        var q = curve.Curve.DecodePoint(pubKeyBytes02);
                        valid02 = true;
                    }
                    catch { }
                    
                    try
                    {
                        byte[] pubKeyBytes03 = HexToBytes("03" + publicKeyHex);
                        var curve = Org.BouncyCastle.Asn1.Sec.SecNamedCurves.GetByName("secp256k1");
                        var q = curve.Curve.DecodePoint(pubKeyBytes03);
                        valid03 = true;
                    }
                    catch { }
                    
                    if (valid02 && valid03)
                    {
                        sb.AppendLine("⚠ Key is ambiguous - both 02 and 03 prefixes are valid");
                    }
                    else if (valid02)
                    {
                        sb.AppendLine("✓ Key is valid with 02 prefix");
                        sb.AppendLine($"Recommended verification key: 02{publicKeyHex}");
                    }
                    else if (valid03)
                    {
                        sb.AppendLine("✓ Key is valid with 03 prefix");
                        sb.AppendLine($"Recommended verification key: 03{publicKeyHex}");
                    }
                    else
                    {
                        sb.AppendLine("✗ Key is not valid with either 02 or 03 prefix");
                    }
                }
                else
                {
                    sb.AppendLine("✗ Key has invalid length. Should be 64 chars (Nostr standard) or 66 chars (with prefix)");
                }
                
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Error in key diagnosis: {ex.Message}";
            }
        }
    }
} 