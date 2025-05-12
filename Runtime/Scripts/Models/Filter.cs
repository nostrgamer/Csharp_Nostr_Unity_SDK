using System;
using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace NostrUnity.Models
{
    /// <summary>
    /// Represents a subscription filter for Nostr events according to NIP-01
    /// </summary>
    [Serializable]
    public class Filter
    {
        /// <summary>
        /// List of event IDs to query for
        /// </summary>
        [JsonPropertyName("ids")]
        public string[] Ids { get; set; }
        
        /// <summary>
        /// List of pubkeys (authors) to query for
        /// </summary>
        [JsonPropertyName("authors")]
        public string[] Authors { get; set; }
        
        /// <summary>
        /// List of event kinds to query for
        /// </summary>
        [JsonPropertyName("kinds")]
        public int[] Kinds { get; set; }
        
        /// <summary>
        /// Event tags to query for
        /// </summary>
        [JsonPropertyName("tags")]
        public Dictionary<string, string[]> Tags { get; set; }
        
        /// <summary>
        /// Query events since this timestamp
        /// </summary>
        [JsonPropertyName("since")]
        public long? Since { get; set; }
        
        /// <summary>
        /// Query events until this timestamp
        /// </summary>
        [JsonPropertyName("until")]
        public long? Until { get; set; }
        
        /// <summary>
        /// Limit the number of events returned
        /// </summary>
        [JsonPropertyName("limit")]
        public int? Limit { get; set; }

        /// <summary>
        /// Gets or sets the event IDs to filter by (e tags)
        /// </summary>
        [JsonIgnore]
        public string[] EventTags
        {
            get
            {
                if (Tags != null && Tags.TryGetValue("e", out var eTags))
                {
                    return eTags;
                }
                return Array.Empty<string>();
            }
            set
            {
                if (Tags == null)
                {
                    Tags = new Dictionary<string, string[]>();
                }
                Tags["e"] = value ?? Array.Empty<string>();
            }
        }

        /// <summary>
        /// Gets or sets the pubkeys to filter by (p tags)
        /// </summary>
        [JsonIgnore]
        public string[] PubkeyTags
        {
            get
            {
                if (Tags != null && Tags.TryGetValue("p", out var pTags))
                {
                    return pTags;
                }
                return Array.Empty<string>();
            }
            set
            {
                if (Tags == null)
                {
                    Tags = new Dictionary<string, string[]>();
                }
                Tags["p"] = value ?? Array.Empty<string>();
            }
        }
        
        /// <summary>
        /// Creates a new empty filter
        /// </summary>
        public Filter()
        {
            Ids = Array.Empty<string>();
            Authors = Array.Empty<string>();
            Kinds = Array.Empty<int>();
            Tags = new Dictionary<string, string[]>();
        }
    }
} 