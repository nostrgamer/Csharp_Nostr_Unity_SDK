using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using Nostr.Unity.Utils;
using Nostr.Unity.Crypto;
using Newtonsoft.Json.Linq;

namespace Nostr.Unity
{
    /// <summary>
    /// Represents a Nostr event with proper validation and serialization
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class NostrEvent
    {
        /// <summary>
        /// The event ID (32-byte hex-encoded string)
        /// </summary>
        [JsonProperty("id")]
        public string Id { get; private set; }
        
        /// <summary>
        /// The event creator's public key (32-byte hex-encoded string)
        /// </summary>
        [JsonProperty("pubkey")]
        public string PublicKey { get; private set; }
        
        /// <summary>
        /// The public key in compressed format for internal verification
        /// Not serialized to JSON - used only for signature verification
        /// </summary>
        [JsonIgnore]
        internal string CompressedPublicKey { get; private set; }
        
        /// <summary>
        /// The Unix timestamp when the event was created
        /// </summary>
        [JsonProperty("created_at")]
        public long CreatedAt { get; private set; }
        
        /// <summary>
        /// The event kind/type
        /// </summary>
        [JsonProperty("kind")]
        public int Kind { get; private set; }
        
        /// <summary>
        /// The event tags (array of arrays)
        /// </summary>
        [JsonProperty("tags")]
        public string[][] Tags { get; private set; }
        
        /// <summary>
        /// The event content
        /// </summary>
        [JsonProperty("content")]
        public string Content { get; private set; }
        
        /// <summary>
        /// The event signature (64-byte hex-encoded string)
        /// </summary>
        [JsonProperty("sig")]
        public string Signature { get; private set; }
        
        /// <summary>
        /// Creates a new Nostr event
        /// </summary>
        /// <param name="publicKey">The event creator's public key (uncompressed, 64 chars or compressed, 66 chars)</param>
        /// <param name="kind">The event kind</param>
        /// <param name="content">The event content</param>
        /// <param name="tags">Optional event tags</param>
        /// <param name="compressedPublicKey">Optional compressed public key for verification. If not provided, will attempt to derive it.</param>
        public NostrEvent(string publicKey, int kind, string content, string[][] tags = null, string compressedPublicKey = null)
        {
            if (string.IsNullOrEmpty(publicKey))
                throw new ArgumentException("Public key cannot be null or empty", nameof(publicKey));
            
            if (string.IsNullOrEmpty(content))
                throw new ArgumentException("Content cannot be null or empty", nameof(content));
            
            // Important: Nostr protocol expects exactly 64 characters (32 bytes) for public key
            // If public key has a compression prefix, strip it for JSON serialization
            if (publicKey.Length == 66 && (publicKey.StartsWith("02") || publicKey.StartsWith("03")))
            {
                // Store the compressed key for verification
                CompressedPublicKey = publicKey.ToLowerInvariant();
                // Strip the prefix for the Nostr standard public key
                PublicKey = publicKey.Substring(2).ToLowerInvariant();
                Debug.Log($"[DEBUG] Converted compressed key (with prefix) to standard Nostr format");
            }
            else if (publicKey.Length == 64)
            {
                // Standard key without prefix
                PublicKey = publicKey.ToLowerInvariant();
                
                // If we were provided with a compressed key, use that for verification
                if (!string.IsNullOrEmpty(compressedPublicKey))
                {
                    CompressedPublicKey = compressedPublicKey.ToLowerInvariant();
                    Debug.Log($"[DEBUG] Using explicitly provided compressed key for verification");
                }
                else
                {
                    // We'll compute the compressed key as needed in verification
                    Debug.Log($"[DEBUG] No compressed key provided, will derive during verification if needed");
                }
            }
            else
            {
                throw new ArgumentException($"Invalid public key format. Must be 64 chars (uncompressed) or 66 chars (compressed with 02/03 prefix). Got {publicKey.Length} chars.", nameof(publicKey));
            }
            
            Kind = kind;
            Content = content;
            Tags = tags ?? new string[0][];
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            
            // Generate the event ID
            ComputeId();
        }
        
        /// <summary>
        /// Computes the event ID based on the event data
        /// </summary>
        private void ComputeId()
        {
            // Get the serialized event for signing
            string serializedEvent = GetSerializedEvent();
            Debug.Log($"[DEBUG] Signing serialized event (raw): {serializedEvent}");
            
            // Compute the event ID as a hash of the serialized event
            byte[] eventBytes = Encoding.UTF8.GetBytes(serializedEvent);
            Debug.Log($"[DEBUG] Serialized event bytes length: {eventBytes.Length}");
            
            // Log the first 50 bytes for debugging (if too long)
            if (eventBytes.Length > 50) {
                string bytesPreview = BitConverter.ToString(eventBytes, 0, 50).Replace("-", "");
                Debug.Log($"[DEBUG] First 50 bytes of serialized event: {bytesPreview}...");
            }
            
            byte[] hashBytes;
            using (var sha256 = SHA256.Create())
            {
                hashBytes = sha256.ComputeHash(eventBytes);
            }
            
            // Log the raw hash bytes
            string hashHex = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            Debug.Log($"[DEBUG] Event ID hash (hex): {hashHex}");
            
            // Convert the hash to hex for the event ID
            Id = hashHex;
            
            Debug.Log($"TIMESTAMP DEBUG - Created event at unix timestamp: {CreatedAt} ({DateTimeOffset.FromUnixTimeSeconds(CreatedAt).ToString("yyyy-MM-dd HH:mm:ss")} UTC)");
        }
        
        /// <summary>
        /// Signs the event with the given private key
        /// </summary>
        /// <param name="privateKey">The private key to sign with</param>
        public void Sign(string privateKey)
        {
            try
            {
                if (string.IsNullOrEmpty(privateKey))
                    throw new ArgumentException("Private key cannot be null or empty", nameof(privateKey));
                
                if (string.IsNullOrEmpty(Id))
                    throw new InvalidOperationException("Event ID must be computed before signing");
                
                // Convert ID from hex to bytes for signing
                byte[] hashBytes = Hex.HexToBytes(Id);
                if (hashBytes.Length != 32)
                {
                    throw new InvalidOperationException($"Invalid event ID length: {hashBytes.Length} bytes. Expected 32 bytes.");
                }
                
                Debug.Log($"[DEBUG] Signing event with ID: {Id}");
                
                // Get the NostrSigner to sign the event
                var keyManager = new NostrKeyManager();
                string publicKey = keyManager.GetPublicKey(privateKey, true);
                Debug.Log($"[DEBUG] Public key used for signing (hex): {publicKey}");
                
                // Log whether we have a compressed public key
                Debug.Log($"[DEBUG] CompressedPublicKey available: {!string.IsNullOrEmpty(CompressedPublicKey)}");
                Debug.Log($"[DEBUG] PublicKey value: {PublicKey}");
                
                // Sign the event ID with the private key
                Signature = Crypto.NostrSigner.SignEvent(hashBytes, privateKey);
                Debug.Log($"[DEBUG] Generated signature (hex): {Signature}");
                
                // Verify signature immediately after signing
                bool verificationResult = VerifySignature();
                Debug.Log($"[DEBUG] Local signature verification result: {verificationResult}");
                
                if (!verificationResult)
                {
                    // Detailed signature verification failure diagnostics
                    Debug.LogError("[DEBUG] ⚠️ LOCAL SIGNATURE VERIFICATION FAILED - WHY?");
                    Debug.LogError($"[DEBUG] Event ID: {Id}");
                    Debug.LogError($"[DEBUG] Public Key: {PublicKey}");
                    Debug.LogError($"[DEBUG] Compressed Public Key: {CompressedPublicKey}");
                    Debug.LogError($"[DEBUG] Signature: {Signature}");
                    // Try to verify with alternate key formats
                    string derivedPublicKey = keyManager.GetPublicKey(privateKey, false);
                    string derivedCompressedKey = keyManager.GetPublicKey(privateKey, true);
                    Debug.LogError($"[DEBUG] Derived Public Key: {derivedPublicKey}");
                    Debug.LogError($"[DEBUG] Derived Compressed Key: {derivedCompressedKey}");
                }
            }
            catch (Exception ex)
            {
                // If something goes wrong with the signing, log it in detail
                Debug.LogError($"[DEBUG] Error during signing: {ex.Message}");
                Debug.LogError($"[DEBUG] Stack trace: {ex.StackTrace}");
                throw;
            }
        }
        
        /// <summary>
        /// Verifies the event signature
        /// </summary>
        /// <returns>True if the signature is valid, otherwise false</returns>
        public bool VerifySignature()
        {
            try
            {
                // Check if we have the necessary values
                if (string.IsNullOrEmpty(Id))
                {
                    Debug.LogError("[DEBUG] Cannot verify signature: Event ID is missing");
                    return false;
                }
                
                if (string.IsNullOrEmpty(PublicKey))
                {
                    Debug.LogError("[DEBUG] Cannot verify signature: Public key is missing");
                    return false;
                }
                
                if (string.IsNullOrEmpty(Signature))
                {
                    Debug.LogError("[DEBUG] Cannot verify signature: Signature is missing");
                    return false;
                }
                
                Debug.Log($"[DEBUG] Verifying signature for event: {Id}");
                Debug.Log($"[DEBUG] Using public key: {PublicKey}");
                Debug.Log($"[DEBUG] Using compressed public key: {CompressedPublicKey ?? "Not available"}");
                Debug.Log($"[DEBUG] Signature to verify: {Signature}");
                
                // Use compressed key if available, otherwise try standard key format for verification
                if (!string.IsNullOrEmpty(CompressedPublicKey))
                {
                    Debug.Log($"[DEBUG] Using pre-stored compressed public key for verification");
                    bool result = Crypto.NostrSigner.VerifySignatureHex(Id, Signature, CompressedPublicKey);
                    Debug.Log($"[DEBUG] Standard signature verification result: {result}");
                    return result;
                }
                
                // If we don't have a compressed key stored, try both standard compression prefixes
                string pubKey02 = "02" + PublicKey;
                string pubKey03 = "03" + PublicKey;
                
                Debug.Log($"[DEBUG] No compressed key available, trying with 02 prefix");
                bool result02 = Crypto.NostrSigner.VerifySignatureHex(Id, Signature, pubKey02);
                
                if (result02)
                {
                    // If verification passed with 02 prefix, store it for future use
                    CompressedPublicKey = pubKey02;
                    Debug.Log($"[DEBUG] Verification successful with 02 prefix, stored for future use");
                    return true;
                }
                
                Debug.Log($"[DEBUG] Trying with 03 prefix");
                bool result03 = Crypto.NostrSigner.VerifySignatureHex(Id, Signature, pubKey03);
                
                if (result03)
                {
                    // If verification passed with 03 prefix, store it for future use
                    CompressedPublicKey = pubKey03;
                    Debug.Log($"[DEBUG] Verification successful with 03 prefix, stored for future use");
                    return true;
                }
                
                // Both prefixes failed
                Debug.LogError("[DEBUG] Signature verification failed with both key prefixes");
                Debug.LogError("[DEBUG] This will likely cause relays to reject this event");
                
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DEBUG] Error during signature verification: {ex.Message}");
                Debug.LogError($"[DEBUG] Stack trace: {ex.StackTrace}");
                return false;
            }
        }
        
        /// <summary>
        /// Gets the serialized event for signing
        /// </summary>
        /// <returns>JSON string of the event for signing</returns>
        public string GetSerializedEvent()
        {
            // CRITICAL: For Nostr event serialization, we need to construct exactly:
            // [0, <pubkey string>, <unix timestamp>, <kind number>, <tags array>, <content string>]
            
            // Log each field for debugging to ensure correct values
            Debug.Log($"[DEBUG] SERIALIZATION - Event pubkey: {PublicKey}");
            Debug.Log($"[DEBUG] SERIALIZATION - Event created_at: {CreatedAt}");
            Debug.Log($"[DEBUG] SERIALIZATION - Event kind: {Kind}");
            Debug.Log($"[DEBUG] SERIALIZATION - Event tags length: {Tags?.Length ?? 0}");
            Debug.Log($"[DEBUG] SERIALIZATION - Event content: {Content}");
            
            // Use a proper fixed array to ensure consistent ordering
            object[] eventArray = new object[]
            {
                0,              // Version marker, always 0
                PublicKey,      // Hex public key without any prefix (32 bytes, 64 chars)
                CreatedAt,      // Unix timestamp in seconds
                Kind,           // Event kind as integer
                Tags,           // Array of tag arrays
                Content         // Content as string
            };
            
            // Use the strict serialization settings required by Nostr relays
            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.None,
                NullValueHandling = NullValueHandling.Include,
                // Ensure we don't add any extra whitespace that would affect the hash
                StringEscapeHandling = StringEscapeHandling.Default
            };
            
            // Serialize exactly as specified in NIP-01
            string serialized = JsonConvert.SerializeObject(eventArray, settings);
            
            // Log for debugging
            Debug.Log($"[DEBUG] Full serialized event for ID computation: {serialized}");
            
            // Verify this is valid JSON by attempting to parse it
            try
            {
                var test = JArray.Parse(serialized);
                Debug.Log($"[DEBUG] Valid JSON array with {test.Count} elements");
                
                // Log individual elements for detailed inspection
                for (int i = 0; i < test.Count; i++)
                {
                    var element = test[i];
                    string elementType = element.Type.ToString();
                    string elementValue = element.ToString(Formatting.None);
                    
                    // Truncate very long values
                    if (elementValue.Length > 100)
                        elementValue = elementValue.Substring(0, 97) + "...";
                        
                    Debug.Log($"[DEBUG] Element {i}: Type={elementType}, Value={elementValue}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DEBUG] SERIALIZATION ERROR - Invalid JSON: {ex.Message}");
            }
            
            return serialized;
        }
        
        /// <summary>
        /// Serializes the complete event including ID and signature
        /// </summary>
        /// <returns>JSON string of the complete event</returns>
        public string SerializeComplete()
        {
            // Create a new anonymous object to ensure proper order
            var completeEvent = new
            {
                id = Id?.ToLowerInvariant(),
                pubkey = PublicKey?.ToLowerInvariant(),
                created_at = CreatedAt,
                kind = Kind,
                tags = Tags,
                content = Content,
                sig = Signature?.ToLowerInvariant()
            };
            
            // Use settings matching Nostr relay expectations
            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.None,
                NullValueHandling = NullValueHandling.Include
            };
            
            return JsonConvert.SerializeObject(completeEvent, settings);
        }
        
        /// <summary>
        /// Deep debug method to verify the exact serialization and signature against a reference implementation
        /// </summary>
        /// <returns>True if verification passes all steps</returns>
        public bool DeepDebugVerification()
        {
            try
            {
                // Get the serialized event string that should be used for ID computation
                string serializedEvent = GetSerializedEvent();
                
                // Check if the ID matches what we compute from the serialized event
                byte[] eventBytes = Encoding.UTF8.GetBytes(serializedEvent);
                string computedId;
                
                using (var sha256 = SHA256.Create())
                {
                    computedId = BitConverter.ToString(sha256.ComputeHash(eventBytes)).Replace("-", "").ToLowerInvariant();
                }
                
                bool idMatches = string.Equals(computedId, Id, StringComparison.OrdinalIgnoreCase);
                Debug.Log($"DEEP DEBUG - ID check: {idMatches} (Computed: {computedId}, Expected: {Id})");
                
                if (!idMatches)
                {
                    Debug.LogError("CRITICAL: Event ID does not match the hash of serialized event");
                    return false;
                }
                
                // Verify the signature using the hex string method
                string keyForVerification = CompressedPublicKey ?? ("02" + PublicKey);
                bool sigValid = NostrSigner.VerifySignatureHex(Id, Signature, keyForVerification);
                
                // Log the complete process
                Debug.Log($"DEEP DEBUG - Complete verification result: {sigValid}");
                Debug.Log($"DEEP DEBUG - Serialized: {serializedEvent}");
                Debug.Log($"DEEP DEBUG - ID: {Id}");
                Debug.Log($"DEEP DEBUG - PubKey: {PublicKey}");
                Debug.Log($"DEEP DEBUG - CompressedPubKey: {keyForVerification}");
                Debug.Log($"DEEP DEBUG - Sig: {Signature}");
                
                return sigValid;
            }
            catch (Exception ex)
            {
                Debug.LogError($"DEEP DEBUG - Verification error: {ex.Message}");
                return false;
            }
        }
    }
} 