using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using NostrUnity.Utils;
using NostrUnity.Crypto;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Threading.Tasks;

namespace NostrUnity.Models
{
    /// <summary>
    /// Represents a Nostr event with proper validation and serialization
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    [Serializable]
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
        public string Pubkey { get; private set; }
        
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
        public string Sig { get; private set; }
        
        [JsonProperty]
        public byte[] PrivateKey { get; set; }
        
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
                Pubkey = publicKey.Substring(2).ToLowerInvariant();
                Debug.Log($"[DEBUG] Converted compressed key (with prefix) to standard Nostr format");
            }
            else if (publicKey.Length == 64)
            {
                // Standard key without prefix
                Pubkey = publicKey.ToLowerInvariant();
                
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
        /// Computes the event ID according to NIP-01
        /// </summary>
        private void ComputeId()
        {
            string serialized = SerializeForId();
            Debug.Log($"Serialized for ID: {serialized}");
            
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(serialized));
            Id = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
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
                byte[] hashBytes = new byte[Id.Length / 2];
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    hashBytes[i] = Convert.ToByte(Id.Substring(i * 2, 2), 16);
                }
                
                Debug.Log($"[DEBUG] Signing event with ID: {Id}");
                
                // Get the public keys to verify consistency
                var keyManager = new NostrKeyManager();
                
                // Get both formats of the public key
                string xOnlyPublicKey = keyManager.GetPublicKey(privateKey, false);  // x-only (32 bytes)
                string compressedPublicKey = keyManager.GetPublicKey(privateKey, true);  // compressed (33 bytes with prefix)
                
                Debug.Log($"[DEBUG] Derived x-only public key: {xOnlyPublicKey}");
                Debug.Log($"[DEBUG] Derived compressed public key: {compressedPublicKey}");
                
                // Verify the event's public key matches the one derived from the private key
                if (!string.Equals(Pubkey, xOnlyPublicKey, StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogError($"[DEBUG] WARNING: The public key in the event ({Pubkey}) doesn't match the one derived from the private key ({xOnlyPublicKey})");
                    Debug.LogError("[DEBUG] This event may be rejected by relays if signed with mismatched key");
                    // We continue anyway as this might be intentional in some cases
                }
                
                // Store the compressed public key for verification
                CompressedPublicKey = compressedPublicKey;
                Debug.Log($"[DEBUG] Stored compressed public key: {CompressedPublicKey}");
                
                // Sign the event ID with the private key
                Sig = NostrCrypto.SignEvent(Id, privateKey);
                Debug.Log($"[DEBUG] Generated signature (hex): {Sig}");
                
                // Verify signature immediately after signing
                bool verificationResult = VerifySignature();
                Debug.Log($"[DEBUG] Local signature verification result: {verificationResult}");
                
                if (!verificationResult)
                {
                    // Detailed signature verification failure diagnostics
                    Debug.LogError("[DEBUG] ⚠️ LOCAL SIGNATURE VERIFICATION FAILED - WHY?");
                    Debug.LogError($"[DEBUG] Event ID: {Id}");
                    Debug.LogError($"[DEBUG] Public Key: {Pubkey}");
                    Debug.LogError($"[DEBUG] Compressed Public Key: {CompressedPublicKey}");
                    Debug.LogError($"[DEBUG] Signature: {Sig}");
                    // Try to verify with alternate key formats
                    Debug.LogError($"[DEBUG] Derived x-only Public Key: {xOnlyPublicKey}");
                    Debug.LogError($"[DEBUG] Derived Compressed Key: {compressedPublicKey}");
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
                
                if (string.IsNullOrEmpty(Pubkey))
                {
                    Debug.LogError("[DEBUG] Cannot verify signature: Public key is missing");
                    return false;
                }
                
                if (string.IsNullOrEmpty(Sig))
                {
                    Debug.LogError("[DEBUG] Cannot verify signature: Signature is missing");
                    return false;
                }
                
                Debug.Log($"[DEBUG] Verifying signature for event: {Id}");
                Debug.Log($"[DEBUG] Using public key: {Pubkey}");
                Debug.Log($"[DEBUG] Using compressed public key: {CompressedPublicKey ?? "Not available"}");
                Debug.Log($"[DEBUG] Signature to verify: {Sig}");
                
                // First check if we have a valid public key length
                if (Pubkey.Length != 64)
                {
                    Debug.LogError($"[DEBUG] Invalid public key length: {Pubkey.Length} (expected 64 chars for x-only key)");
                    return false;
                }
                
                // Try direct verification first with x-only public key format
                try
                {
                    bool resultDirect = NostrCrypto.VerifySignature(Id, Sig, Pubkey);
                    if (resultDirect)
                    {
                        Debug.Log($"[DEBUG] Direct x-only key verification succeeded");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[DEBUG] Direct x-only verification failed: {ex.Message}");
                }
                
                // Try with compressed key formats if available
                if (!string.IsNullOrEmpty(CompressedPublicKey))
                {
                    try
                    {
                        Debug.Log($"[DEBUG] Using pre-stored compressed public key for verification");
                        bool result = NostrCrypto.VerifySignature(Id, Sig, CompressedPublicKey);
                        Debug.Log($"[DEBUG] Compressed key verification result: {result}");
                        if (result) return true;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[DEBUG] Compressed key verification failed: {ex.Message}");
                    }
                }
                
                // Try both standard compression prefixes as a fallback
                try
                {
                    // If we don't have a compressed key stored, try both standard compression prefixes
                    string pubKey02 = "02" + Pubkey;
                    
                    Debug.Log($"[DEBUG] Trying with 02 prefix");
                    bool result02 = NostrCrypto.VerifySignature(Id, Sig, pubKey02);
                    
                    if (result02)
                    {
                        // If verification passed with 02 prefix, store it for future use
                        CompressedPublicKey = pubKey02;
                        Debug.Log($"[DEBUG] Verification successful with 02 prefix, stored for future use");
                        return true;
                    }
                    
                    Debug.Log($"[DEBUG] Trying with 03 prefix");
                    string pubKey03 = "03" + Pubkey;
                    bool result03 = NostrCrypto.VerifySignature(Id, Sig, pubKey03);
                    
                    if (result03)
                    {
                        // If verification passed with 03 prefix, store it for future use
                        CompressedPublicKey = pubKey03;
                        Debug.Log($"[DEBUG] Verification successful with 03 prefix, stored for future use");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[DEBUG] Prefix-based verification failed: {ex.Message}");
                }
                
                // All verification attempts failed
                Debug.LogError("[DEBUG] Signature verification failed with all key format attempts");
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
        /// Gets the serialized event data for signing/ID computation
        /// </summary>
        /// <returns>The serialized event as JSON</returns>
        public string GetSerializedEvent()
        {
            // Create a new anonymous object with only the required fields in the specific order
            var serializableEvent = new
            {
                pubkey = Pubkey.ToLowerInvariant(),
                created_at = CreatedAt,
                kind = Kind,
                tags = Tags ?? new string[0][],
                content = Content
            };
            
            // Use JsonConvert for serialization
            string serialized = JsonConvert.SerializeObject(serializableEvent);
            
            // Ensure consistent formatting by creating an array with fixed order
            var jsonParse = JObject.Parse(serialized);
            var arr = new JArray();
            
            arr.Add(0);
            arr.Add(jsonParse["pubkey"]);
            arr.Add(jsonParse["created_at"]);
            arr.Add(jsonParse["kind"]);
            arr.Add(jsonParse["tags"]);
            arr.Add(jsonParse["content"]);
            
            // Convert to JSON string
            string json = arr.ToString(Formatting.None);
            
            Debug.Log($"[DEBUG] Serialized event (raw): {json}");
            
            return json;
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
                pubkey = Pubkey?.ToLowerInvariant(),
                created_at = CreatedAt,
                kind = Kind,
                tags = Tags,
                content = Content,
                sig = Sig?.ToLowerInvariant()
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
                string keyForVerification = CompressedPublicKey ?? ("02" + Pubkey);
                bool sigValid = NostrCrypto.VerifySignature(Id, Sig, keyForVerification);
                
                // Log the complete process
                Debug.Log($"DEEP DEBUG - Complete verification result: {sigValid}");
                Debug.Log($"DEEP DEBUG - Serialized: {serializedEvent}");
                Debug.Log($"DEEP DEBUG - ID: {Id}");
                Debug.Log($"DEEP DEBUG - PubKey: {Pubkey}");
                Debug.Log($"DEEP DEBUG - CompressedPubKey: {keyForVerification}");
                Debug.Log($"DEEP DEBUG - Sig: {Sig}");
                
                return sigValid;
            }
            catch (Exception ex)
            {
                Debug.LogError($"DEEP DEBUG - Verification error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Serializes the event for ID computation according to NIP-01
        /// </summary>
        public string SerializeForId()
        {
            var array = new object[] { 0, Pubkey, CreatedAt, Kind, Tags ?? Array.Empty<string[]>(), Content };
            return JsonConvert.SerializeObject(array);
        }

        /// <summary>
        /// Serializes the event for sending to relays
        /// </summary>
        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }

        /// <summary>
        /// Creates a new text note event
        /// </summary>
        public static NostrEvent CreateTextNote(string content, string publicKey, long createdAt)
        {
            return new NostrEvent(publicKey, (int)NostrEventKind.TextNote, content, Array.Empty<string[]>());
        }
    }

    /// <summary>
    /// Represents the different kinds of Nostr events
    /// </summary>
    public enum NostrEventKind
    {
        Metadata = 0,
        TextNote = 1,
        RecommendRelay = 2,
        Contacts = 3,
        EncryptedDirectMessage = 4,
        EventDeletion = 5,
        Reaction = 7,
        ChannelCreation = 40,
        ChannelMetadata = 41,
        ChannelMessage = 42,
        ChannelHideMessage = 43,
        ChannelMuteUser = 44
    }
} 