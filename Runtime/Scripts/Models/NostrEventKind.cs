using System;

namespace Nostr.Unity
{
    /// <summary>
    /// Standard Nostr event kinds as specified in NIPs
    /// </summary>
    public enum NostrEventKind
    {
        /// <summary>
        /// Set Metadata: NIP-01
        /// The content is a stringified JSON object containing profile metadata
        /// </summary>
        Metadata = 0,
        
        /// <summary>
        /// Text Note: NIP-01
        /// The content is a plain text message
        /// </summary>
        TextNote = 1,
        
        /// <summary>
        /// Recommend Relay: NIP-01
        /// The content is a set of relay URLs recommended to followers
        /// </summary>
        RecommendRelay = 2
    }
} 