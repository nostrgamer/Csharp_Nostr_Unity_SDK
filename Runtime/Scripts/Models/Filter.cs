using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Nostr.Unity
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
        public string[] Ids { get; set; }
        
        /// <summary>
        /// List of pubkeys to filter (the pubkey of an event)
        /// </summary>
        public string[] Authors { get; set; }
        
        /// <summary>
        /// List of event kinds to filter
        /// </summary>
        public int[] Kinds { get; set; }
        
        /// <summary>
        /// Filter by events referencing these event ids
        /// </summary>
        public string[] EventRefs { get; set; }
        
        /// <summary>
        /// Filter by events referencing these pubkeys
        /// </summary>
        public string[] PubkeyRefs { get; set; }
        
        /// <summary>
        /// Filter by events newer than this unix timestamp
        /// </summary>
        public long? Since { get; set; }
        
        /// <summary>
        /// Filter by events older than this unix timestamp
        /// </summary>
        public long? Until { get; set; }
        
        /// <summary>
        /// Maximum number of events to return
        /// </summary>
        public int Limit { get; set; } = 25;
        
        /// <summary>
        /// Creates a new empty filter
        /// </summary>
        public Filter()
        {
        }
        
        /// <summary>
        /// Converts the filter to a JSON string for transmission
        /// </summary>
        /// <returns>A JSON representation of the filter</returns>
        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
} 