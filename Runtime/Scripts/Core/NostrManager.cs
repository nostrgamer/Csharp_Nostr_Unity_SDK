using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Nostr.Unity.Utils;

namespace Nostr.Unity
{
    /// <summary>
    /// MonoBehaviour wrapper for Nostr SDK functionality
    /// </summary>
    public class NostrManager : MonoBehaviour
    {
        [SerializeField]
        private List<string> defaultRelays = new List<string>();
        
        [SerializeField]
        private bool connectOnStart = true;
        
        [SerializeField]
        private bool autoGenerateKeys = false;
        
        [SerializeField]
        private bool useBech32Format = true;
        
        private NostrClient _nostrClient;
        private NostrKeyManager _keyManager;
        
        // Events that can be subscribed to in the Unity Inspector
        public event Action<string> OnConnected;
        public event Action<string> OnDisconnected;
        public event Action<string> OnError;
        public event Action<NostrEvent> OnEventReceived;
        
        /// <summary>
        /// Gets the Nostr client instance
        /// </summary>
        public NostrClient Client => _nostrClient;
        
        /// <summary>
        /// Gets the key manager instance
        /// </summary>
        public NostrKeyManager KeyManager => _keyManager;
        
        /// <summary>
        /// Gets or sets the current user's private key
        /// </summary>
        public string PrivateKey { get; private set; }
        
        /// <summary>
        /// Gets the current user's public key
        /// </summary>
        public string PublicKey { get; private set; }
        
        /// <summary>
        /// Gets the current user's private key in Bech32 format (nsec)
        /// </summary>
        public string PrivateKeyBech32 => PrivateKey?.ToNsec();
        
        /// <summary>
        /// Gets the current user's public key in Bech32 format (npub)
        /// </summary>
        public string PublicKeyBech32 => PublicKey?.ToNpub();
        
        /// <summary>
        /// Gets a shortened version of the public key for display purposes
        /// </summary>
        public string ShortPublicKey => useBech32Format ? PublicKeyBech32?.ToShortKey() : PublicKey?.ToShortKey();
        
        private void Awake()
        {
            _nostrClient = new NostrClient();
            _keyManager = new NostrKeyManager();
            
            // Subscribe to events
            _nostrClient.Connected += (sender, relay) => OnConnected?.Invoke(relay);
            _nostrClient.Disconnected += (sender, relay) => OnDisconnected?.Invoke(relay);
            _nostrClient.Error += (sender, error) => OnError?.Invoke(error);
            _nostrClient.EventReceived += (sender, args) => OnEventReceived?.Invoke(args.Event);
            
            // If no default relays are set, use the constants
            if (defaultRelays.Count == 0)
            {
                defaultRelays.AddRange(NostrConstants.DEFAULT_RELAYS);
            }
            
            // Run Bech32 tests
            try
            {
                if (Bech32Tests.RunTests())
                {
                    Debug.Log("Bech32 tests completed successfully");
                }
                else
                {
                    Debug.LogWarning("Bech32 tests failed");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error running Bech32 tests: {ex.Message}");
            }
        }
        
        private void Start()
        {
            if (connectOnStart)
            {
                LoadOrCreateKeys();
                ConnectToRelays();
            }
        }
        
        /// <summary>
        /// Loads or creates keys based on settings
        /// </summary>
        public void LoadOrCreateKeys()
        {
            PrivateKey = _keyManager.LoadPrivateKey(false); // Load in hex format
            
            if (string.IsNullOrEmpty(PrivateKey) && autoGenerateKeys)
            {
                Debug.Log("No keys found, generating new ones...");
                PrivateKey = _keyManager.GeneratePrivateKey(true); // Generate in hex format
                _keyManager.StoreKeys(PrivateKey);
            }
            
            if (!string.IsNullOrEmpty(PrivateKey))
            {
                PublicKey = _keyManager.GetPublicKey(PrivateKey, true); // Get in hex format
                
                if (useBech32Format)
                {
                    Debug.Log($"Loaded public key: {PublicKeyBech32} (short: {ShortPublicKey})");
                }
                else
                {
                    Debug.Log($"Loaded public key: {PublicKey} (short: {ShortPublicKey})");
                }
            }
            else
            {
                Debug.LogWarning("No private key available. Please set one or enable auto-generation.");
            }
        }
        
        /// <summary>
        /// Sets the user's private key
        /// </summary>
        /// <param name="privateKey">The private key (hex or Bech32 format)</param>
        /// <param name="save">Whether to save the key to PlayerPrefs</param>
        public void SetPrivateKey(string privateKey, bool save = true)
        {
            // Convert to hex format if needed
            string hexPrivateKey = privateKey;
            
            if (privateKey.StartsWith(NostrConstants.NSEC_PREFIX + "1"))
            {
                hexPrivateKey = privateKey.ToHex();
            }
            
            PrivateKey = hexPrivateKey;
            PublicKey = _keyManager.GetPublicKey(hexPrivateKey, true);
            
            if (save)
            {
                _keyManager.StoreKeys(hexPrivateKey);
            }
            
            if (useBech32Format)
            {
                Debug.Log($"Set private key and derived public key: {PublicKeyBech32} (short: {ShortPublicKey})");
            }
            else
            {
                Debug.Log($"Set private key and derived public key: {PublicKey} (short: {ShortPublicKey})");
            }
        }
        
        /// <summary>
        /// Connects to the default relays
        /// </summary>
        public async void ConnectToRelays()
        {
            try
            {
                Debug.Log($"Connecting to {defaultRelays.Count} relays...");
                await _nostrClient.ConnectToRelays(defaultRelays);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error connecting to relays: {ex.Message}");
                OnError?.Invoke($"Error connecting to relays: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Posts a text note to connected relays
        /// </summary>
        /// <param name="content">The content of the note</param>
        public async void PostTextNote(string content)
        {
            try
            {
                if (string.IsNullOrEmpty(PrivateKey))
                {
                    throw new InvalidOperationException("Private key not set. Cannot sign event.");
                }
                
                var nostrEvent = new NostrEvent
                {
                    Kind = (int)NostrEventKind.TextNote,
                    PubKey = PublicKey,
                    Content = content,
                    CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };
                
                nostrEvent.Sign(PrivateKey);
                
                Debug.Log($"Posting note: {content}");
                await _nostrClient.PublishEvent(nostrEvent);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error posting note: {ex.Message}");
                OnError?.Invoke($"Error posting note: {ex.Message}");
            }
        }
        
        private void OnDestroy()
        {
            _nostrClient.Disconnect();
        }
    }
} 