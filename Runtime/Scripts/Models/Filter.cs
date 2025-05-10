using System;
using System.Text.Json.Serialization;

namespace NostrUnity.Models
{
    /// <summary>
    /// Represents a subscription filter for Nostr events according to NIP-01
    /// </summary>
    [Serializable]
    public class Filter
    {
        /// <summary>
        /// List of event ids to filter
        /// </summary>
        [JsonPropertyName("ids")]
        public string[] Ids { get; set; }
        
        /// <summary>
        /// List of pubkeys to filter (the pubkey of an event)
        /// </summary>
        [JsonPropertyName("authors")]
        public string[] Authors { get; set; }
        
        /// <summary>
        /// List of event kinds to filter
        /// </summary>
        [JsonPropertyName("kinds")]
        public int[] Kinds { get; set; }
        
        /// <summary>
        /// Filter by events referencing these event ids
        /// </summary>
        [JsonPropertyName("#e")]
        public string[] EventTags { get; set; }
        
        /// <summary>
        /// Filter by events referencing these pubkeys
        /// </summary>
        [JsonPropertyName("#p")]
        public string[] PubkeyTags { get; set; }
        
        /// <summary>
        /// Filter by events newer than this unix timestamp
        /// </summary>
        [JsonPropertyName("since")]
        public long? Since { get; set; }
        
        /// <summary>
        /// Filter by events older than this unix timestamp
        /// </summary>
        [JsonPropertyName("until")]
        public long? Until { get; set; }
        
        /// <summary>
        /// Maximum number of events to return
        /// </summary>
        [JsonPropertyName("limit")]
        public int? Limit { get; set; }
        
        /// <summary>
        /// Creates a new empty filter
        /// </summary>
        public Filter()
        {
            Ids = Array.Empty<string>();
            Authors = Array.Empty<string>();
            Kinds = Array.Empty<int>();
            EventTags = Array.Empty<string>();
            PubkeyTags = Array.Empty<string>();
        }
    }
} 