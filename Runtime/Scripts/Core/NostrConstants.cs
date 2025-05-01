using System;

namespace Nostr.Unity
{
    /// <summary>
    /// Constants for the Nostr protocol
    /// </summary>
    public static class NostrConstants
    {
        /// <summary>
        /// Bech32 prefix for public keys (npub)
        /// </summary>
        public const string NPUB_PREFIX = "npub";
        
        /// <summary>
        /// Bech32 prefix for private keys (nsec)
        /// </summary>
        public const string NSEC_PREFIX = "nsec";
        
        /// <summary>
        /// Default relay URLs
        /// </summary>
        public static readonly string[] DEFAULT_RELAYS = new string[]
        {
            "wss://relay.damus.io",
            "wss://nos.lol",
            "wss://relay.nostr.band"
        };
        
        /// <summary>
        /// The length of a private key in bytes
        /// </summary>
        public const int PRIVATE_KEY_LENGTH = 32;
        
        /// <summary>
        /// The length of a public key in bytes
        /// </summary>
        public const int PUBLIC_KEY_LENGTH = 32;
        
        /// <summary>
        /// Nostr protocol message types
        /// </summary>
        public static class MessageType
        {
            /// <summary>
            /// Event message type (client -> relay)
            /// </summary>
            public const string EVENT = "EVENT";
            
            /// <summary>
            /// Request message type (client -> relay)
            /// </summary>
            public const string REQ = "REQ";
            
            /// <summary>
            /// Close message type (client -> relay)
            /// </summary>
            public const string CLOSE = "CLOSE";
        }
    }
} 