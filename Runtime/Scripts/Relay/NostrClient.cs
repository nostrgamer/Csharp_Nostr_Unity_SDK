using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using NostrUnity.Crypto;
using NostrUnity.Models;
using NostrUnity.Relay;
using NostrUnity.Utils;

namespace NostrUnity
{
    /// <summary>
    /// High-level client for interacting with the Nostr network
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
        
        private async void Awake()
        {
            try
            {
                // Create relay manager
                _relayManager = gameObject.AddComponent<NostrRelayManager>();
                
                // Set up handlers
                _relayManager.OnEventReceived += HandleEventReceived;
                _relayManager.OnRelayStateChanged += HandleRelayStateChanged;
                _relayManager.OnRelayError += HandleRelayError;
                
                // Add default relays
                foreach (string relay in _defaultRelays)
                {
                    try
                    {
                        await AddRelayAsync(relay);
                    }
                    catch (Exception ex)
                    {
                        NostrErrorHandler.HandleError(ex, $"NostrClient.Awake - Adding relay {relay}", NostrErrorHandler.NostrErrorSeverity.Warning);
                        // Continue with other relays even if one fails
                    }
                }
            }
            catch (Exception ex)
            {
                NostrErrorHandler.HandleError(ex, "NostrClient.Awake", NostrErrorHandler.NostrErrorSeverity.Critical);
            }
        }
        
        private async void OnDestroy()
        {
            try
            {
                if (_relayManager != null)
                {
                    await _relayManager.DisconnectAllAsync();
                    _relayManager.OnEventReceived -= HandleEventReceived;
                    _relayManager.OnRelayStateChanged -= HandleRelayStateChanged;
                    _relayManager.OnRelayError -= HandleRelayError;
                }
            }
            catch (Exception ex)
            {
                NostrErrorHandler.HandleError(ex, "NostrClient.OnDestroy", NostrErrorHandler.NostrErrorSeverity.Warning);
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
                if (string.IsNullOrEmpty(privateKey))
                    throw new ArgumentException("Private key cannot be null or empty", nameof(privateKey));
                
                _keyPair = new KeyPair(privateKey);
                NostrErrorHandler.HandleError($"Loaded key pair with public key: {_keyPair.Npub}", "NostrClient.LoadKeyPair", NostrErrorHandler.NostrErrorSeverity.Info);
            }
            catch (Exception ex)
            {
                NostrErrorHandler.HandleError(ex, "NostrClient.LoadKeyPair", NostrErrorHandler.NostrErrorSeverity.Error);
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
                NostrErrorHandler.HandleError($"Generated new key pair with public key: {_keyPair.Npub}", "NostrClient.GenerateKeyPair", NostrErrorHandler.NostrErrorSeverity.Info);
            }
            catch (Exception ex)
            {
                NostrErrorHandler.HandleError(ex, "NostrClient.GenerateKeyPair", NostrErrorHandler.NostrErrorSeverity.Error);
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
        public async Task<bool> AddRelayAsync(string relayUrl)
        {
            try
            {
                return await _relayManager.AddRelayAsync(relayUrl);
            }
            catch (Exception ex)
            {
                NostrErrorHandler.HandleError(ex, "NostrClient.AddRelay", NostrErrorHandler.NostrErrorSeverity.Error);
                return false;
            }
        }
        
        /// <summary>
        /// Removes a relay
        /// </summary>
        /// <param name="relayUrl">The URL of the relay to remove</param>
        /// <returns>True if the relay was removed successfully</returns>
        public async Task<bool> RemoveRelayAsync(string relayUrl)
        {
            try
            {
                return await _relayManager.RemoveRelayAsync(relayUrl);
            }
            catch (Exception ex)
            {
                NostrErrorHandler.HandleError(ex, "NostrClient.RemoveRelay", NostrErrorHandler.NostrErrorSeverity.Error);
                return false;
            }
        }
        
        /// <summary>
        /// Publishes a text note to connected relays
        /// </summary>
        /// <param name="content">The content of the note</param>
        /// <returns>The created and published event</returns>
        public async Task<NostrEvent> PublishTextNoteAsync(string content)
        {
            try
            {
                if (_keyPair == null)
                    throw new InvalidOperationException("No key pair loaded. Call LoadKeyPair or GenerateKeyPair first.");
                
                if (string.IsNullOrEmpty(content))
                    throw new ArgumentException("Content cannot be null or empty", nameof(content));
                
                // Create a text note event
                var nostrEvent = new NostrEvent(_keyPair.PublicKeyHex, (int)NostrEventKind.TextNote, content);
                
                // Sign the event
                nostrEvent.Sign(_keyPair.PrivateKeyHex);
                
                // Publish to all connected relays
                await _relayManager.PublishEventAsync(nostrEvent);
                
                return nostrEvent;
            }
            catch (Exception ex)
            {
                NostrErrorHandler.HandleError(ex, "NostrClient.PublishTextNote", NostrErrorHandler.NostrErrorSeverity.Error);
                throw;
            }
        }
        
        /// <summary>
        /// Subscribes to text notes from a specific author
        /// </summary>
        /// <param name="authorPubKey">The public key of the author (hex or npub)</param>
        /// <returns>The subscription ID</returns>
        public async Task<string> SubscribeToAuthorAsync(string authorPubKey)
        {
            try
            {
                if (string.IsNullOrEmpty(authorPubKey))
                    throw new ArgumentException("Author public key cannot be null or empty", nameof(authorPubKey));
                
                // Create a filter for text notes from this author
                var filter = new Filter
                {
                    Authors = new[] { authorPubKey },
                    Kinds = new[] { (int)NostrEventKind.TextNote }
                };
                
                // Generate a subscription ID
                string subscriptionId = $"author_{authorPubKey}_{DateTime.UtcNow.Ticks}";
                
                // Subscribe to the events
                await _relayManager.SubscribeAsync(subscriptionId, filter);
                
                return subscriptionId;
            }
            catch (Exception ex)
            {
                NostrErrorHandler.HandleError(ex, "NostrClient.SubscribeToAuthor", NostrErrorHandler.NostrErrorSeverity.Error);
                throw;
            }
        }
        
        /// <summary>
        /// Subscribes to text notes with specific tags
        /// </summary>
        /// <param name="tags">The tags to filter by (without the # symbol)</param>
        /// <returns>The subscription ID</returns>
        public async Task<string> SubscribeToTagsAsync(string[] tags)
        {
            try
            {
                if (tags == null || tags.Length == 0)
                    throw new ArgumentException("Tags cannot be null or empty", nameof(tags));
                
                // Create a filter for text notes with the specified tags
                var filter = new Filter
                {
                    Kinds = new[] { (int)NostrEventKind.TextNote },
                    Tags = new Dictionary<string, string[]>
                    {
                        { "t", tags }
                    }
                };
                
                // Generate a subscription ID
                string subscriptionId = $"tags_{string.Join("_", tags)}_{DateTime.UtcNow.Ticks}";
                
                // Subscribe to the events
                await _relayManager.SubscribeAsync(subscriptionId, filter);
                
                return subscriptionId;
            }
            catch (Exception ex)
            {
                NostrErrorHandler.HandleError(ex, "NostrClient.SubscribeToTags", NostrErrorHandler.NostrErrorSeverity.Error);
                throw;
            }
        }
        
        /// <summary>
        /// Unsubscribes from a previous subscription
        /// </summary>
        /// <param name="subscriptionId">The ID of the subscription to cancel</param>
        public async Task UnsubscribeAsync(string subscriptionId)
        {
            try
            {
                await _relayManager.UnsubscribeAsync(subscriptionId);
            }
            catch (Exception ex)
            {
                NostrErrorHandler.HandleError(ex, "NostrClient.Unsubscribe", NostrErrorHandler.NostrErrorSeverity.Warning);
                throw;
            }
        }
        
        /// <summary>
        /// Saves the current private key securely
        /// </summary>
        public void SavePrivateKey()
        {
            try
            {
                if (_keyPair == null)
                    throw new InvalidOperationException("No key pair loaded. Call LoadKeyPair or GenerateKeyPair first.");
                
                SecurePlayerPrefs.SaveKey(_keyPair.PrivateKeyHex);
                NostrErrorHandler.HandleError("Private key saved securely", "NostrClient.SavePrivateKey", NostrErrorHandler.NostrErrorSeverity.Info);
            }
            catch (Exception ex)
            {
                NostrErrorHandler.HandleError(ex, "NostrClient.SavePrivateKey", NostrErrorHandler.NostrErrorSeverity.Error);
                throw;
            }
        }
        
        /// <summary>
        /// Loads the private key from secure storage
        /// </summary>
        public void LoadPrivateKeyFromStorage()
        {
            try
            {
                string privateKey = SecurePlayerPrefs.LoadKey();
                if (string.IsNullOrEmpty(privateKey))
                    throw new InvalidOperationException("No private key found in secure storage");
                
                LoadKeyPair(privateKey);
                NostrErrorHandler.HandleError("Private key loaded from secure storage", "NostrClient.LoadPrivateKeyFromStorage", NostrErrorHandler.NostrErrorSeverity.Info);
            }
            catch (Exception ex)
            {
                NostrErrorHandler.HandleError(ex, "NostrClient.LoadPrivateKeyFromStorage", NostrErrorHandler.NostrErrorSeverity.Error);
                throw;
            }
        }
        
        /// <summary>
        /// Deletes the private key from secure storage
        /// </summary>
        public void DeleteStoredPrivateKey()
        {
            try
            {
                SecurePlayerPrefs.DeleteKey();
                NostrErrorHandler.HandleError("Private key deleted from secure storage", "NostrClient.DeleteStoredPrivateKey", NostrErrorHandler.NostrErrorSeverity.Info);
            }
            catch (Exception ex)
            {
                NostrErrorHandler.HandleError(ex, "NostrClient.DeleteStoredPrivateKey", NostrErrorHandler.NostrErrorSeverity.Warning);
                throw;
            }
        }
        
        /// <summary>
        /// Connects to all configured relays
        /// </summary>
        public async Task ConnectToRelaysAsync()
        {
            try
            {
                await _relayManager.ConnectAllAsync();
            }
            catch (Exception ex)
            {
                NostrErrorHandler.HandleError(ex, "NostrClient.ConnectToRelays", NostrErrorHandler.NostrErrorSeverity.Error);
                throw;
            }
        }
        
        /// <summary>
        /// Adds a relay to connect to (synchronous version)
        /// </summary>
        /// <param name="relayUrl">The WebSocket URL of the relay</param>
        /// <returns>True if the relay was added successfully</returns>
        public bool AddRelay(string relayUrl)
        {
            try
            {
                // Call the async method but don't wait for it
                _ = AddRelayAsync(relayUrl);
                return true;
            }
            catch (Exception ex)
            {
                NostrErrorHandler.HandleError(ex, "NostrClient.AddRelay", NostrErrorHandler.NostrErrorSeverity.Error);
                return false;
            }
        }
        
        /// <summary>
        /// Publishes a text note to connected relays (synchronous version)
        /// </summary>
        /// <param name="content">The content of the note</param>
        /// <returns>The created and published event</returns>
        public NostrEvent PublishTextNote(string content)
        {
            try
            {
                if (_keyPair == null)
                    throw new InvalidOperationException("No key pair loaded. Call LoadKeyPair or GenerateKeyPair first.");
                
                if (string.IsNullOrEmpty(content))
                    throw new ArgumentException("Content cannot be null or empty", nameof(content));
                
                // Create a text note event
                var nostrEvent = new NostrEvent(_keyPair.PublicKeyHex, (int)NostrEventKind.TextNote, content);
                
                // Sign the event
                nostrEvent.Sign(_keyPair.PrivateKeyHex);
                
                // Publish to all connected relays (don't await)
                _ = _relayManager.PublishEventAsync(nostrEvent);
                
                return nostrEvent;
            }
            catch (Exception ex)
            {
                NostrErrorHandler.HandleError(ex, "NostrClient.PublishTextNote", NostrErrorHandler.NostrErrorSeverity.Error);
                throw;
            }
        }
        
        /// <summary>
        /// Subscribes to text notes from a specific author (synchronous version)
        /// </summary>
        /// <param name="authorPubKey">The public key of the author (hex or npub)</param>
        /// <returns>The subscription ID</returns>
        public string SubscribeToAuthorNotes(string authorPubKey)
        {
            try
            {
                if (string.IsNullOrEmpty(authorPubKey))
                    throw new ArgumentException("Author public key cannot be null or empty", nameof(authorPubKey));
                
                // Create a filter for text notes from this author
                var filter = new Filter
                {
                    Authors = new[] { authorPubKey },
                    Kinds = new[] { (int)NostrEventKind.TextNote }
                };
                
                // Generate a subscription ID
                string subscriptionId = $"author_{authorPubKey}_{DateTime.UtcNow.Ticks}";
                
                // Subscribe to the events (don't await)
                _ = _relayManager.SubscribeAsync(subscriptionId, filter);
                
                return subscriptionId;
            }
            catch (Exception ex)
            {
                NostrErrorHandler.HandleError(ex, "NostrClient.SubscribeToAuthorNotes", NostrErrorHandler.NostrErrorSeverity.Error);
                throw;
            }
        }
        
        #region Event Handlers
        
        private void HandleEventReceived(NostrEvent ev, string relayUrl)
        {
            OnEventReceived?.Invoke(ev, relayUrl);
        }
        
        private void HandleRelayStateChanged(string relayUrl, RelayState state)
        {
            OnRelayStateChanged?.Invoke(relayUrl, state);
        }
        
        private void HandleRelayError(string relayUrl, string error)
        {
            NostrErrorHandler.HandleError($"Relay error from {relayUrl}: {error}", "NostrClient", NostrErrorHandler.NostrErrorSeverity.Warning);
        }
        
        #endregion
    }
} 