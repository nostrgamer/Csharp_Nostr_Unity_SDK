using System;
using System.Collections.Generic;
using UnityEngine;
using NostrUnity.Crypto;
using NostrUnity.Models;
using NostrUnity.Relay;

namespace NostrUnity
{
    /// <summary>
    /// Provides a high-level client for interacting with the Nostr network
    /// </summary>
    public class NostrClient : MonoBehaviour
    {
        [SerializeField] private List<string> _defaultRelays = new List<string>();
        
        private NostrRelayManager _relayManager;
        private KeyPair _keyPair;
        
        /// <summary>
        /// Event triggered when a Nostr event is received
        /// </summary>
        public event Action<NostrEvent, string> OnEventReceived;
        
        /// <summary>
        /// Event triggered when a relay connection state changes
        /// </summary>
        public event Action<string, RelayState> OnRelayStateChanged;
        
        /// <summary>
        /// Gets whether the client has a loaded key pair
        /// </summary>
        public bool HasKeyPair => _keyPair != null;
        
        private void Awake()
        {
            // Create relay manager
            _relayManager = gameObject.AddComponent<NostrRelayManager>();
            
            // Set up handlers
            _relayManager.OnEventReceived += HandleEventReceived;
            _relayManager.OnRelayStateChanged += HandleRelayStateChanged;
            
            // Add default relays
            foreach (string relay in _defaultRelays)
            {
                _relayManager.AddRelay(relay);
            }
        }
        
        /// <summary>
        /// Loads a key pair from a private key
        /// </summary>
        /// <param name="privateKey">The private key (hex or nsec format)</param>
        public void LoadKeyPair(string privateKey)
        {
            try
            {
                _keyPair = new KeyPair(privateKey);
                Debug.Log($"Loaded key pair with public key: {_keyPair.PublicKeyHex}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load key pair: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Generates a new random key pair
        /// </summary>
        public void GenerateKeyPair()
        {
            try
            {
                _keyPair = new KeyPair();
                Debug.Log($"Generated new key pair with public key: {_keyPair.PublicKeyHex}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to generate key pair: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Gets the current user's public key in hex format
        /// </summary>
        /// <returns>The public key as a hex string</returns>
        public string GetPublicKeyHex()
        {
            if (_keyPair == null)
                throw new InvalidOperationException("No key pair loaded. Call LoadKeyPair or GenerateKeyPair first.");
            
            return _keyPair.PublicKeyHex;
        }
        
        /// <summary>
        /// Gets the current user's public key in bech32 format (npub)
        /// </summary>
        /// <returns>The public key as an npub string</returns>
        public string GetPublicKeyBech32()
        {
            if (_keyPair == null)
                throw new InvalidOperationException("No key pair loaded. Call LoadKeyPair or GenerateKeyPair first.");
            
            return _keyPair.Npub;
        }
        
        /// <summary>
        /// Adds a relay to connect to
        /// </summary>
        /// <param name="relayUrl">The WebSocket URL of the relay</param>
        /// <returns>True if the relay was added successfully</returns>
        public bool AddRelay(string relayUrl)
        {
            return _relayManager.AddRelay(relayUrl);
        }
        
        /// <summary>
        /// Removes a relay
        /// </summary>
        /// <param name="relayUrl">The URL of the relay to remove</param>
        /// <returns>True if the relay was removed successfully</returns>
        public bool RemoveRelay(string relayUrl)
        {
            return _relayManager.RemoveRelay(relayUrl);
        }
        
        /// <summary>
        /// Publishes a text note to connected relays
        /// </summary>
        /// <param name="content">The content of the note</param>
        /// <returns>The created and published event</returns>
        public NostrEvent PublishTextNote(string content)
        {
            if (_keyPair == null)
                throw new InvalidOperationException("No key pair loaded. Call LoadKeyPair or GenerateKeyPair first.");
            
            if (string.IsNullOrEmpty(content))
                throw new ArgumentException("Content cannot be null or empty", nameof(content));
            
            try
            {
                // Create a text note event
                var nostrEvent = new NostrEvent(_keyPair.PublicKeyHex, 1, content);
                
                // Sign the event
                nostrEvent.Sign(_keyPair.PrivateKeyHex);
                
                // Publish to all connected relays
                _relayManager.PublishEvent(nostrEvent);
                
                return nostrEvent;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to publish text note: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Subscribes to text notes from a specific author
        /// </summary>
        /// <param name="authorPubKey">The public key of the author (hex or npub)</param>
        /// <returns>The subscription ID</returns>
        public string SubscribeToAuthorNotes(string authorPubKey)
        {
            if (string.IsNullOrEmpty(authorPubKey))
                throw new ArgumentException("Author public key cannot be null or empty", nameof(authorPubKey));
            
            // Convert npub to hex if needed
            if (authorPubKey.StartsWith("npub1"))
            {
                authorPubKey = CryptoUtils.ConvertIdFormat(authorPubKey, "hex");
            }
            
            // Create a filter for the author's text notes
            var filter = new Filter
            {
                Authors = new[] { authorPubKey },
                Kinds = new[] { 1 } // Text notes
            };
            
            // Generate a subscription ID
            string subscriptionId = $"author_{authorPubKey.Substring(0, 10)}_{DateTime.UtcNow.Ticks}";
            
            // Subscribe to the events
            _relayManager.Subscribe(subscriptionId, filter);
            
            return subscriptionId;
        }
        
        /// <summary>
        /// Subscribes to text notes containing specific tags
        /// </summary>
        /// <param name="tags">The tags to filter by (without the # symbol)</param>
        /// <returns>The subscription ID</returns>
        public string SubscribeToTags(string[] tags)
        {
            if (tags == null || tags.Length == 0)
                throw new ArgumentException("Tags cannot be null or empty", nameof(tags));
            
            // Create a filter for text notes with the specified tags
            var filter = new Filter
            {
                Kinds = new[] { 1 } // Text notes
                // In a more complete implementation, we would need to add tag filtering
            };
            
            // Generate a subscription ID
            string subscriptionId = $"tags_{string.Join("_", tags)}_{DateTime.UtcNow.Ticks}";
            
            // Subscribe to the events
            _relayManager.Subscribe(subscriptionId, filter);
            
            return subscriptionId;
        }
        
        /// <summary>
        /// Unsubscribes from a previous subscription
        /// </summary>
        /// <param name="subscriptionId">The ID of the subscription to cancel</param>
        public void Unsubscribe(string subscriptionId)
        {
            if (string.IsNullOrEmpty(subscriptionId))
                throw new ArgumentException("Subscription ID cannot be null or empty", nameof(subscriptionId));
            
            _relayManager.Unsubscribe(subscriptionId);
        }
        
        private void HandleEventReceived(NostrEvent nostrEvent, string relayUrl)
        {
            // Forward the event to listeners
            OnEventReceived?.Invoke(nostrEvent, relayUrl);
        }
        
        private void HandleRelayStateChanged(string relayUrl, RelayState state)
        {
            // Forward the state change to listeners
            OnRelayStateChanged?.Invoke(relayUrl, state);
        }
    }
} 