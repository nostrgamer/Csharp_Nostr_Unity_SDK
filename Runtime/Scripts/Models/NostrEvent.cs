using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using Nostr.Unity.Utils;
using Nostr.Unity.Crypto;

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
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
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
            
            // Store the binary hash for signing
            byte[] idBytes = hashBytes;
            
            // Convert the hash to hex string for the JSON id field
            Id = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            Debug.Log($"Computed event ID: {Id}");
            
            // IMPORTANT: Sign the actual 32-byte hash (idBytes), not the hex string
            Signature = NostrSigner.SignEvent(idBytes, privateKey);
            
            // Log event details for debugging
            string completeEvent = SerializeComplete();
            Debug.Log($"Complete event: {completeEvent}");
            Debug.Log($"Event ID: {Id}");
            Debug.Log($"Public Key: {PublicKey}");
            Debug.Log($"Signature: {Signature}");
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
        /// Format: ["0", "pubkey", created_at, kind, tags, content]
        /// </summary>
        /// <returns>The serialized event data</returns>
        private string GetSerializedEvent()
        {
            // The proper way to serialize for ID computation is to:
            // 1. Create an array of [0, pubkey, created_at, kind, tags, content]
            // 2. JSON encode the ENTIRE array with proper escaping

            // Convert each field to its proper JSON representation
            string serializedPubkey = JsonConvert.SerializeObject(PublicKey);
            string serializedContent = JsonConvert.SerializeObject(Content);
            string serializedTags = JsonConvert.SerializeObject(Tags);
            
            // Format the array manually to ensure proper structure
            // This follows the exact NIP-01 serialization requirement
            string serialized = $"[0,{serializedPubkey},{CreatedAt},{Kind},{serializedTags},{serializedContent}]";
            
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
    }
} 