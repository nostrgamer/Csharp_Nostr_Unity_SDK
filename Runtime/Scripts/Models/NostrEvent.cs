using System;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using UnityEngine;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nostr.Unity
{
    /// <summary>
    /// Represents a Nostr event according to NIP-01
    /// </summary>
    [Serializable]
    public class NostrEvent
    {
        /// <summary>
        /// The event ID (32-byte hex-encoded string)
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; private set; }
        
        /// <summary>
        /// The event creator's public key (32-byte hex-encoded string)
        /// </summary>
        [JsonPropertyName("pubkey")]
        public string PublicKey { get; private set; }
        
        /// <summary>
        /// The Unix timestamp when the event was created
        /// </summary>
        [JsonPropertyName("created_at")]
        public long CreatedAt { get; private set; }
        
        /// <summary>
        /// The event kind/type
        /// </summary>
        [JsonPropertyName("kind")]
        public int Kind { get; private set; }
        
        /// <summary>
        /// The event tags (array of arrays)
        /// </summary>
        [JsonPropertyName("tags")]
        public string[][] Tags { get; private set; } = new string[0][];
        
        /// <summary>
        /// The event content
        /// </summary>
        [JsonPropertyName("content")]
        public string Content { get; private set; }
        
        /// <summary>
        /// The event signature (64-byte hex-encoded string)
        /// </summary>
        [JsonPropertyName("sig")]
        public string Signature { get; private set; }
        
        /// <summary>
        /// Creates a new, empty Nostr event
        /// </summary>
        public NostrEvent()
        {
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Tags = new string[0][];
        }

        /// <summary>
        /// Creates a new Nostr event with the specified parameters
        /// </summary>
        public NostrEvent(string publicKey, int kind, string content, string[][] tags = null)
        {
            if (string.IsNullOrEmpty(publicKey))
                throw new ArgumentException("Public key cannot be null or empty", nameof(publicKey));
            
            if (string.IsNullOrEmpty(content))
                throw new ArgumentException("Content cannot be null or empty", nameof(content));
            
            PublicKey = publicKey;
            Kind = kind;
            Content = content;
            Tags = tags ?? new string[0][];
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
        
        /// <summary>
        /// Signs the event with a private key
        /// </summary>
        /// <param name="privateKeyHex">The hex-encoded private key</param>
        public void Sign(string privateKeyHex)
        {
            if (string.IsNullOrEmpty(privateKeyHex))
                throw new ArgumentException("Private key cannot be null or empty", nameof(privateKeyHex));
            
            // Generate the event ID (if not already set)
            if (string.IsNullOrEmpty(Id))
            {
                Id = ComputeId();
            }
            
            // Sign the event
            NostrKeyManager keyManager = new NostrKeyManager();
            Signature = keyManager.SignMessage(GetSignatureHash(), privateKeyHex);
        }
        
        /// <summary>
        /// Computes the event ID according to NIP-01
        /// </summary>
        /// <returns>The hex-encoded event ID</returns>
        public string ComputeId()
        {
            if (string.IsNullOrEmpty(PublicKey))
                throw new InvalidOperationException("Public key must be set before computing ID");
            
            byte[] serializedEvent = GetSerializedEvent();
            using (var sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(serializedEvent);
                return BytesToHex(hash);
            }
        }
        
        /// <summary>
        /// Gets the hash of the event for signature purposes (equivalent to the event ID)
        /// </summary>
        /// <returns>The hex-encoded event ID/hash</returns>
        public string GetSignatureHash()
        {
            if (string.IsNullOrEmpty(Id))
            {
                Id = ComputeId();
            }
            return Id;
        }
        
        /// <summary>
        /// Serializes the event according to NIP-01 specification
        /// </summary>
        /// <returns>UTF-8 bytes of the serialized event</returns>
        private byte[] GetSerializedEvent()
        {
            // Format according to NIP-01: [0, pubkey, created_at, kind, tags, content]
            string serialized = $"[0,\"{PublicKey}\",{CreatedAt},{Kind},{SerializeTags()},\"{EscapeJsonString(Content)}\"]";
            return Encoding.UTF8.GetBytes(serialized);
        }
        
        /// <summary>
        /// Serializes the tags array to a JSON string
        /// </summary>
        private string SerializeTags()
        {
            if (Tags == null || Tags.Length == 0)
            {
                return "[]";
            }
            
            var tagStrings = Tags.Select(tag => 
            {
                var escapedElements = tag.Select(el => $"\"{EscapeJsonString(el)}\"");
                return $"[{string.Join(",", escapedElements)}]";
            });
            
            return $"[{string.Join(",", tagStrings)}]";
        }
        
        /// <summary>
        /// Escapes special characters in JSON strings
        /// </summary>
        private string EscapeJsonString(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return string.Empty;
            }
            
            // Use JsonSerializer to properly escape the string
            return JsonSerializer.Serialize(str).Trim('"');
        }
        
        /// <summary>
        /// Converts a hex string to a byte array
        /// </summary>
        private byte[] HexToBytes(string hex)
        {
            if (string.IsNullOrEmpty(hex))
            {
                return new byte[0];
            }
            
            // Remove 0x prefix if present
            if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                hex = hex.Substring(2);
            }
            
            // Ensure even length
            if (hex.Length % 2 != 0)
            {
                hex = "0" + hex;
            }
            
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            
            return bytes;
        }
        
        /// <summary>
        /// Converts a byte array to a hex string
        /// </summary>
        private string BytesToHex(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return string.Empty;
            }
            
            StringBuilder hex = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
            {
                hex.AppendFormat("{0:x2}", b);
            }
            return hex.ToString();
        }
        
        /// <summary>
        /// Verifies the event signature
        /// </summary>
        /// <returns>True if the signature is valid, otherwise false</returns>
        public bool VerifySignature()
        {
            try
            {
                if (string.IsNullOrEmpty(Signature) || string.IsNullOrEmpty(PublicKey))
                {
                    return false;
                }
                
                // Compute the hash for verification
                string hash = GetSignatureHash();
                
                // Verify with NostrKeyManager
                NostrKeyManager keyManager = new NostrKeyManager();
                return keyManager.VerifySignature(hash, Signature, PublicKey);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error verifying event signature: {ex.Message}");
                return false;
            }
        }
    }
} 