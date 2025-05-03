using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using Nostr.Unity.Utils;

namespace Nostr.Unity
{
    /// <summary>
    /// Represents a Nostr event with proper validation and serialization
    /// </summary>
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
        /// <param name="publicKey">The event creator's public key</param>
        /// <param name="kind">The event kind</param>
        /// <param name="content">The event content</param>
        /// <param name="tags">Optional event tags</param>
        public NostrEvent(string publicKey, int kind, string content, string[][] tags = null)
        {
            if (string.IsNullOrEmpty(publicKey))
                throw new ArgumentException("Public key cannot be null or empty", nameof(publicKey));
            
            if (string.IsNullOrEmpty(content))
                throw new ArgumentException("Content cannot be null or empty", nameof(content));
            
            PublicKey = publicKey;
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
            
            // Compute the event ID
            Id = ComputeId();
            
            // Sign the event
            var keyManager = new NostrKeyManager();
            Signature = keyManager.SignMessage(GetSerializedEvent(), privateKey);
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
                var keyManager = new NostrKeyManager();
                return keyManager.VerifySignature(GetSerializedEvent(), Signature, PublicKey);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error verifying signature: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Computes the event ID
        /// </summary>
        /// <returns>The computed event ID</returns>
        private string ComputeId()
        {
            if (string.IsNullOrEmpty(PublicKey))
                throw new InvalidOperationException("Public key must be set before computing ID");
            
            string serializedEvent = GetSerializedEvent();
            using (var sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(serializedEvent));
                return Bech32Util.BytesToHex(hash);
            }
        }
        
        /// <summary>
        /// Gets the serialized event data for signing
        /// </summary>
        /// <returns>The serialized event data</returns>
        private string GetSerializedEvent()
        {
            var eventData = new object[] { 0, PublicKey, CreatedAt, Kind, Tags, Content };
            return JsonConvert.SerializeObject(eventData);
        }
    }
} 