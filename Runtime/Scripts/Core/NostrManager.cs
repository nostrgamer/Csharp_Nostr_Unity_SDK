using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Nostr.Unity.Utils;
using System.Collections;

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
            
            // Disable automatic Bech32 tests to avoid initialization errors
            /*
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
            */
            
            Debug.Log("NostrManager initialized successfully");
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
            PrivateKey = _keyManager.LoadPrivateKey("testpassword", false); // Load in hex format
            
            if (string.IsNullOrEmpty(PrivateKey) && autoGenerateKeys)
            {
                Debug.Log("No keys found, generating new ones...");
                PrivateKey = _keyManager.GeneratePrivateKey(true); // Generate in hex format
                _keyManager.StoreKeys(PrivateKey, "testpassword");
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
            try
            {
                // Convert to hex format if needed
                string hexPrivateKey = privateKey;
                
                if (string.IsNullOrEmpty(privateKey))
                {
                    Debug.LogWarning("Empty private key provided. Generating a new one.");
                    hexPrivateKey = _keyManager.GeneratePrivateKey(true);
                }
                else if (privateKey.StartsWith(NostrConstants.NSEC_PREFIX + "1"))
                {
                    try
                    {
                        hexPrivateKey = privateKey.ToHex();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error converting Bech32 key to hex: {ex.Message}");
                        hexPrivateKey = _keyManager.GeneratePrivateKey(true);
                    }
                }
                
                // Validate hex format
                if (!IsValidHexString(hexPrivateKey) || hexPrivateKey.Length != 64)
                {
                    Debug.LogWarning($"Invalid hex private key format: '{hexPrivateKey}'. Generating a new one.");
                    hexPrivateKey = _keyManager.GeneratePrivateKey(true);
                }
                
                PrivateKey = hexPrivateKey;
                
                try
                {
                    PublicKey = _keyManager.GetPublicKey(hexPrivateKey, true);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error deriving public key: {ex.Message}");
                    // Generate new key pair as fallback
                    PrivateKey = _keyManager.GeneratePrivateKey(true);
                    PublicKey = _keyManager.GetPublicKey(PrivateKey, true);
                }
                
                if (save)
                {
                    bool success = _keyManager.StoreKeys(hexPrivateKey, "testpassword");
                    if (!success)
                    {
                        Debug.LogWarning("Failed to store keys in PlayerPrefs");
                    }
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
            catch (Exception ex)
            {
                Debug.LogError($"Error setting private key: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Checks if a string is a valid hexadecimal string
        /// </summary>
        private bool IsValidHexString(string hex)
        {
            if (string.IsNullOrEmpty(hex))
                return false;
                
            // Check if the string consists only of hex characters (0-9, a-f, A-F)
            foreach (char c in hex)
            {
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                    return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Connects to the default relays
        /// </summary>
        public void ConnectToRelays()
        {
            StartCoroutine(ConnectToRelaysCoroutine());
        }
        
        private IEnumerator ConnectToRelaysCoroutine()
        {
            foreach (var relayUrl in defaultRelays)
            {
                bool connected = false;
                yield return _nostrClient.ConnectToRelay(relayUrl, result => connected = result);
                
                if (connected)
                {
                    Debug.Log($"Connected to relay: {relayUrl}");
                }
                else
                {
                    Debug.LogError($"Failed to connect to relay: {relayUrl}");
                }
            }
        }
        
        /// <summary>
        /// Posts a text note to connected relays
        /// </summary>
        /// <param name="content">The content of the note</param>
        public void PostTextNote(string content)
        {
            if (string.IsNullOrEmpty(PrivateKey))
            {
                Debug.LogError("Private key not set. Cannot sign event.");
                return;
            }
            
            var nostrEvent = new NostrEvent(PublicKey, (int)NostrEventKind.TextNote, content, Array.Empty<string[]>());
            nostrEvent.Sign(PrivateKey);
            
            StartCoroutine(PostTextNoteCoroutine(nostrEvent));
        }
        
        private IEnumerator PostTextNoteCoroutine(NostrEvent nostrEvent)
        {
            bool published = false;
            yield return _nostrClient.PublishEvent(nostrEvent, result => published = result);
            
            if (published)
            {
                Debug.Log("Note published successfully");
            }
            else
            {
                Debug.LogError("Failed to publish note");
            }
        }
        
        private void OnDestroy()
        {
            if (_nostrClient != null)
            {
                _nostrClient.Disconnect();
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Test method for verifying key generation
        /// </summary>
        public void TestKeyGeneration()
        {
            Debug.Log("Testing Nostr key generation with NBitcoin...");
            
            // Generate a private key
            string privateKey = _keyManager.GeneratePrivateKey();
            Debug.Log($"Generated private key: {privateKey}");
            
            // Derive the public key
            string publicKey = _keyManager.GetPublicKey(privateKey);
            Debug.Log($"Derived public key: {publicKey}");
            
            // Test signing a message
            string message = "Hello, Nostr!";
            string signature = _keyManager.SignMessage(message, privateKey);
            Debug.Log($"Signed message '{message}' with signature: {signature}");
            
            // Test verifying the signature
            bool isValid = _keyManager.VerifySignature(message, signature, publicKey);
            Debug.Log($"Signature verification result: {isValid}");
        }
#endif
    }
} 