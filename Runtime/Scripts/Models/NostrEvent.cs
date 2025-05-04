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
        private string CompressedPublicKey { get; set; }
        
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
        /// <param name="publicKey">The event creator's public key (uncompressed, 64 chars)</param>
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
            
            // Important: Nostr expects exactly 64 characters (32 bytes) for public key
            // If public key has a prefix, strip it for JSON serialization
            if (publicKey.Length == 66 && (publicKey.StartsWith("02") || publicKey.StartsWith("03")))
            {
                // Store the compressed key for verification
                CompressedPublicKey = publicKey.ToLowerInvariant();
                // Strip the prefix for the Nostr standard public key
                PublicKey = publicKey.Substring(2).ToLowerInvariant();
            }
            else if (publicKey.Length == 64)
            {
                // Standard format for Nostr JSON
                PublicKey = publicKey.ToLowerInvariant();
                
                // For verification, use the compressed key if provided
                if (!string.IsNullOrEmpty(compressedPublicKey) && 
                    compressedPublicKey.Length == 66 && 
                    (compressedPublicKey.StartsWith("02") || compressedPublicKey.StartsWith("03")))
                {
                    CompressedPublicKey = compressedPublicKey.ToLowerInvariant();
                }
            }
            else
            {
                Debug.LogError($"Invalid public key format: {publicKey}");
                throw new ArgumentException("Public key must be either 64 characters or 66 characters with 02/03 prefix", nameof(publicKey));
            }
            
            Kind = kind;
            Content = content;
            Tags = tags ?? Array.Empty<string[]>();
            
            // CRITICAL: Use the current time (UTC) for the timestamp
            // Many relays reject events with future timestamps
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Debug.Log($"TIMESTAMP DEBUG - Created event at unix timestamp: {CreatedAt} ({DateTimeOffset.FromUnixTimeSeconds(CreatedAt).ToString("yyyy-MM-dd HH:mm:ss")} UTC)");
        }
        
        /// <summary>
        /// Signs the event with a private key
        /// </summary>
        /// <param name="privateKey">The private key to sign with</param>
        public void Sign(string privateKey)
        {
            if (string.IsNullOrEmpty(privateKey))
                throw new ArgumentException("Private key cannot be null or empty", nameof(privateKey));
            
            // Ensure private key is lowercase
            privateKey = privateKey.ToLowerInvariant();
            
            // Get the serialized event array for signing and creating ID
            string serializedEvent = GetSerializedEvent();
            Debug.Log($"Signing serialized event: {serializedEvent}");
            
            // Compute the event ID as a hash of the serialized event
            byte[] eventBytes = Encoding.UTF8.GetBytes(serializedEvent);
            byte[] hashBytes;
            using (var sha256 = SHA256.Create())
            {
                hashBytes = sha256.ComputeHash(eventBytes);
            }
            
            // Convert the hash to hex for the event ID
            Id = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            
            // Generate the signature using the private key
            string signatureHex = NostrSigner.SignEventId(Id, privateKey);
            
            // Ensure the signature is in canonical form (low-S value required by relays)
            byte[] signatureBytes = NostrSigner.HexToBytes(signatureHex);
            signatureBytes = NostrSigner.GetCanonicalSignature(signatureBytes);
            Signature = BitConverter.ToString(signatureBytes).Replace("-", "").ToLowerInvariant();
            
            Debug.Log($"Generated event ID: {Id}");
            Debug.Log($"Generated signature: {Signature}");
        }
        
        /// <summary>
        /// Manually computes an ID using a reference implementation approach 
        /// </summary>
        private string ComputeManualId(string serializedEvent)
        {
            try
            {
                // Convert the serialized event to UTF-8 bytes
                byte[] eventBytes = Encoding.UTF8.GetBytes(serializedEvent);
                
                // Compute the SHA-256 hash
                byte[] hash;
                using (var sha256 = SHA256.Create())
                {
                    hash = sha256.ComputeHash(eventBytes);
                }
                
                // Convert to lowercase hex string
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error in manual ID computation: {ex.Message}");
                return "ERROR";
            }
        }
        
        /// <summary>
        /// Verifies the event signature
        /// </summary>
        /// <returns>True if the signature is valid, otherwise false</returns>
        public bool VerifySignature()
        {
            if (string.IsNullOrEmpty(Id) || string.IsNullOrEmpty(Signature))
                return false;
            
            try
            {
                // Get the serialized event array
                string serializedEvent = GetSerializedEvent();
                Debug.Log($"Verifying serialized event: {serializedEvent}");
                
                // Compute the SHA256 hash to get the binary event ID
                byte[] eventBytes = Encoding.UTF8.GetBytes(serializedEvent);
                byte[] idBytes;
                string computedId;
                
                using (var sha256 = SHA256.Create())
                {
                    idBytes = sha256.ComputeHash(eventBytes);
                    computedId = BitConverter.ToString(idBytes).Replace("-", "").ToLowerInvariant();
                }
                
                // Verify the ID matches what we'd compute
                if (!string.Equals(computedId, Id, StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogError($"ID mismatch. Event has ID {Id} but computed ID is {computedId}");
                    return false;
                }
                
                // Use the compressed key if available, otherwise properly construct one
                string keyForVerification;
                if (!string.IsNullOrEmpty(CompressedPublicKey) && CompressedPublicKey.Length == 66)
                {
                    keyForVerification = CompressedPublicKey;
                    Debug.Log($"Verifying with compressed key: {keyForVerification}");
                }
                else
                {
                    // For proper verification, we need the public key in compressed format
                    // In a production system, you should derive this properly instead of assuming
                    keyForVerification = "02" + PublicKey;
                    Debug.Log($"Verifying with assumed compressed key: {keyForVerification}");
                }
                
                // IMPORTANT: Verify using the binary hash (idBytes), not the hex string
                return NostrSigner.VerifySignature(idBytes, Signature, keyForVerification);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error verifying signature: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Gets the serialized event data for signing, following the Nostr specification exactly.
        /// Format: [0, pubkey, created_at, kind, tags, content]
        /// </summary>
        /// <returns>The serialized event data</returns>
        private string GetSerializedEvent()
        {
            // CRITICAL: For Nostr event serialization, we need to construct exactly:
            // [0, <pubkey string>, <unix timestamp>, <kind number>, <tags array>, <content string>]
            
            // Log each field for debugging to ensure correct values
            Debug.Log($"SERIALIZATION DEBUG - Event pubkey: {PublicKey}");
            Debug.Log($"SERIALIZATION DEBUG - Event created_at: {CreatedAt}");
            Debug.Log($"SERIALIZATION DEBUG - Event kind: {Kind}");
            Debug.Log($"SERIALIZATION DEBUG - Event tags length: {Tags?.Length ?? 0}");
            Debug.Log($"SERIALIZATION DEBUG - Event content: {Content}");
            
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
            Debug.Log($"CRITICAL - Event serialized for ID computation: {serialized}");
            
            // Verify this is valid JSON by attempting to parse it
            try
            {
                var test = JArray.Parse(serialized);
                Debug.Log("SERIALIZATION DEBUG - Valid JSON array with " + test.Count + " elements");
            }
            catch (Exception ex)
            {
                Debug.LogError($"SERIALIZATION ERROR - Invalid JSON: {ex.Message}");
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
                
                // Call the debug verification to check all steps
                bool result = NostrSigner.DebugVerifySerializedEvent(
                    serializedEvent,
                    Id,
                    Signature,
                    CompressedPublicKey ?? ("02" + PublicKey) // Use compressed key or assume it
                );
                
                // Log the complete process
                Debug.Log($"DEEP DEBUG - Complete verification result: {result}");
                Debug.Log($"DEEP DEBUG - Serialized: {serializedEvent}");
                Debug.Log($"DEEP DEBUG - ID: {Id}");
                Debug.Log($"DEEP DEBUG - PubKey: {PublicKey}");
                Debug.Log($"DEEP DEBUG - CompressedPubKey: {CompressedPublicKey ?? ("02" + PublicKey)}");
                Debug.Log($"DEEP DEBUG - Sig: {Signature}");
                
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"DEEP DEBUG - Verification error: {ex.Message}");
                return false;
            }
        }
    }
} 