using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Nostr.Unity
{
    /// <summary>
    /// Represents a Nostr event according to NIP-01
    /// </summary>
    [Serializable]
    public class NostrEvent
    {
        /// <summary>
        /// The event ID (32-bytes lowercase hex-encoded sha256 of the serialized event data)
        /// </summary>
        public string Id;
        
        /// <summary>
        /// The public key of the event creator (32-bytes lowercase hex-encoded public key)
        /// </summary>
        public string PubKey;
        
        /// <summary>
        /// When the event was created (Unix timestamp in seconds)
        /// </summary>
        public long CreatedAt;
        
        /// <summary>
        /// The event kind (integer indicating the type of event)
        /// </summary>
        public int Kind;
        
        /// <summary>
        /// Tags for the event (each tag is an array of strings)
        /// </summary>
        public List<List<string>> Tags = new List<List<string>>();
        
        /// <summary>
        /// The content of the event
        /// </summary>
        public string Content;
        
        /// <summary>
        /// The signature of the event
        /// </summary>
        public string Sig;
        
        /// <summary>
        /// Creates a new Nostr event with the current timestamp
        /// </summary>
        public NostrEvent()
        {
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Tags = new List<List<string>>();
        }
        
        /// <summary>
        /// Signs the event with a private key
        /// </summary>
        /// <param name="privateKey">The private key to sign with</param>
        public void Sign(string privateKey)
        {
            // Calculate the event ID if not already set
            if (string.IsNullOrEmpty(Id))
            {
                Id = CalculateId();
            }
            
            // TODO: Implement actual signing using Secp256k1
            // This is a placeholder - we need to implement proper signing
            Sig = "placeholder_signature";
            
            Debug.Log($"Signed event with ID: {Id}");
        }
        
        /// <summary>
        /// Verifies the event signature
        /// </summary>
        /// <returns>True if the signature is valid, otherwise false</returns>
        public bool Verify()
        {
            // TODO: Implement actual verification using Secp256k1
            // This is a placeholder - we need to implement proper verification
            return !string.IsNullOrEmpty(Sig);
        }
        
        /// <summary>
        /// Calculates the event ID according to NIP-01
        /// </summary>
        /// <returns>The event ID as a lowercase hex string</returns>
        private string CalculateId()
        {
            // Serialize the event data for ID calculation according to NIP-01
            string serialized = $"[0,\"{PubKey}\",{CreatedAt},{Kind},{SerializeTags()},\"{Content}\"]";
            
            // Calculate SHA256 hash
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(serialized);
                byte[] hash = sha256.ComputeHash(bytes);
                
                // Convert to lowercase hex string
                StringBuilder sb = new StringBuilder();
                foreach (byte b in hash)
                {
                    sb.Append(b.ToString("x2"));
                }
                
                return sb.ToString();
            }
        }
        
        /// <summary>
        /// Serializes the tags for ID calculation
        /// </summary>
        /// <returns>The serialized tags as a JSON array</returns>
        private string SerializeTags()
        {
            if (Tags == null || Tags.Count == 0)
            {
                return "[]";
            }
            
            StringBuilder sb = new StringBuilder();
            sb.Append('[');
            
            for (int i = 0; i < Tags.Count; i++)
            {
                List<string> tag = Tags[i];
                sb.Append('[');
                
                for (int j = 0; j < tag.Count; j++)
                {
                    sb.Append('"');
                    sb.Append(tag[j]);
                    sb.Append('"');
                    
                    if (j < tag.Count - 1)
                    {
                        sb.Append(',');
                    }
                }
                
                sb.Append(']');
                
                if (i < Tags.Count - 1)
                {
                    sb.Append(',');
                }
            }
            
            sb.Append(']');
            return sb.ToString();
        }
    }
} 