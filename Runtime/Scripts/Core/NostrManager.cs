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
        /// Gets or sets the current user's private key in hex format
        /// </summary>
        public string PrivateKey { get; private set; }
        
        /// <summary>
        /// Gets the current user's public key in hex format (without compression prefix)
        /// </summary>
        public string PublicKey { get; private set; }
        
        /// <summary>
        /// Gets the current user's public key in compressed hex format (with compression prefix)
        /// This is needed for signature verification
        /// </summary>
        public string CompressedPublicKey { get; private set; }
        
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
        
        /// <summary>
        /// Returns the current key information as a formatted string
        /// </summary>
        /// <returns>A string containing nsec and npub information</returns>
        public string GetKeyInfo()
        {
            if (string.IsNullOrEmpty(PrivateKey) || string.IsNullOrEmpty(PublicKey))
            {
                return "No keys currently loaded.";
            }

            string nsec = PrivateKeyBech32;
            string npub = PublicKeyBech32;
            
            return $"Private Key (nsec): {nsec}\nPublic Key (npub): {npub}";
        }
        
        private void Awake()
        {
            // Properly handle NostrClient creation/discovery
            _nostrClient = FindObjectOfType<NostrClient>();
            if (_nostrClient == null)
            {
                // Auto-create a NostrClient GameObject
                GameObject clientObject = new GameObject("NostrClient");
                _nostrClient = clientObject.AddComponent<NostrClient>();
                Debug.Log("Created NostrClient GameObject automatically");
            }
            
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
            // Initialize the Secp256k1 library if not already initialized
            if (!Secp256k1Manager.Initialize())
            {
                Debug.LogError("Failed to initialize Secp256k1 library. Key operations will not work.");
            }
            
            // If no keys exist and we're trying to connect, force enable auto-generation
            if (!_keyManager.HasStoredKeys())
            {
                // Automatically enable key generation for better user experience
                autoGenerateKeys = true;
                Debug.Log("No keys found, auto-generation enabled automatically");
            }
            
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
            try
            {
                PrivateKey = _keyManager.LoadPrivateKey("testpassword", false); // Load in hex format
                
                if (string.IsNullOrEmpty(PrivateKey))
                {
                    Debug.Log("No stored private key found");
                    
                    if (autoGenerateKeys)
                    {
                        Debug.Log("Auto-generating new keys...");
                        // Generate private key in hex format
                        PrivateKey = _keyManager.GeneratePrivateKey(true);
                        
                        // Validate the generated key
                        if (!IsValidHexString(PrivateKey) || PrivateKey.Length != 64)
                        {
                            Debug.LogError($"Generated key is invalid: {PrivateKey}");
                            return;
                        }
                        
                        // Store the newly generated keys
                        bool saved = _keyManager.StoreKeys(PrivateKey, "testpassword");
                        if (!saved)
                        {
                            Debug.LogWarning("Failed to store newly generated keys");
                        }
                    }
                    else
                    {
                        Debug.LogWarning("No private key available. Please set one or enable auto-generation.");
                        return;
                    }
                }
                
                // Always derive the public key from the private key
                if (!string.IsNullOrEmpty(PrivateKey))
                {
                    // Get public key in hex format
                    string fullPublicKey = _keyManager.GetPublicKey(PrivateKey, true);
                    
                    // Ensure the public key is valid before trying to convert to Bech32
                    if (!IsValidHexString(fullPublicKey) || (fullPublicKey.Length != 64 && fullPublicKey.Length != 66))
                    {
                        Debug.LogError($"Invalid public key format: {fullPublicKey}");
                        return;
                    }
                    
                    // Store the compressed public key format for signature verification
                    CompressedPublicKey = fullPublicKey;
                    
                    // If we have a compressed key (66 chars with 02 or 03 prefix), convert for bech32
                    if (fullPublicKey.Length == 66 && (fullPublicKey.StartsWith("02") || fullPublicKey.StartsWith("03")))
                    {
                        // Remove compression prefix for bech32 encoding but keep original for verification
                        PublicKey = fullPublicKey.Substring(2);
                        Debug.Log($"Converted compressed public key to format for bech32 encoding: {PublicKey}");
                    }
                    else
                    {
                        PublicKey = fullPublicKey;
                    }
                    
                    // Log the key in appropriate format
                    if (useBech32Format)
                    {
                        try
                        {
                            string bech32PublicKey = Bech32Util.EncodeHex(NostrConstants.NPUB_PREFIX, PublicKey);
                            Debug.Log($"Loaded public key: {bech32PublicKey} (short: {bech32PublicKey.ToShortKey()})");
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"Error converting public key to Bech32: {ex.Message}");
                            Debug.Log($"Using hex format instead. Public key: {PublicKey}");
                        }
                    }
                    else
                    {
                        Debug.Log($"Loaded public key: {PublicKey} (short: {PublicKey.ToShortKey()})");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error in LoadOrCreateKeys: {ex.Message}");
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
                    string fullPublicKey = _keyManager.GetPublicKey(hexPrivateKey, true);
                    
                    // Store the compressed public key format for signature verification
                    CompressedPublicKey = fullPublicKey;
                    
                    // If we have a compressed key (66 chars with 02 or 03 prefix), convert for bech32
                    if (fullPublicKey.Length == 66 && (fullPublicKey.StartsWith("02") || fullPublicKey.StartsWith("03")))
                    {
                        // Remove compression prefix for bech32 encoding but keep original for verification
                        PublicKey = fullPublicKey.Substring(2);
                    }
                    else
                    {
                        PublicKey = fullPublicKey;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error deriving public key: {ex.Message}");
                    // Generate new key pair as fallback
                    PrivateKey = _keyManager.GeneratePrivateKey(true);
                    CompressedPublicKey = _keyManager.GetPublicKey(PrivateKey, true);
                    
                    // Process for PublicKey
                    if (CompressedPublicKey.Length == 66 && (CompressedPublicKey.StartsWith("02") || CompressedPublicKey.StartsWith("03")))
                    {
                        PublicKey = CompressedPublicKey.Substring(2);
                    }
                    else
                    {
                        PublicKey = CompressedPublicKey;
                    }
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
            Debug.Log("==== STARTING EVENT POST PROCESS ====");
            
            if (string.IsNullOrEmpty(PrivateKey))
            {
                Debug.LogError("Private key not set. Cannot sign event.");
                return;
            }
            
            // Use the uncompressed PublicKey (without the 02/03 prefix)
            // Nostr relays expect a 32-byte hex string (64 characters)
            if (string.IsNullOrEmpty(PublicKey) || PublicKey.Length != 64)
            {
                Debug.LogError($"Public key is not in the required format. Expected 64 hex chars, got: {PublicKey}");
                return;
            }
            
            // Ensure private key is exactly 64 hex chars (32 bytes)
            if (PrivateKey.Length != 64 || !IsValidHexString(PrivateKey))
            {
                Debug.LogError($"Private key is not in the correct format. Expected 64 hex chars.");
                return;
            }
            
            // Check if the current time seems valid (not in the future)
            long currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Debug.Log($"TIMESTAMP TEST - Current UTC timestamp: {currentTime} ({DateTimeOffset.FromUnixTimeSeconds(currentTime).ToString("yyyy-MM-dd HH:mm:ss")} UTC)");
            
            // Validate keys match - the public key should be derived from the private key
            try
            {
                string derivedPublicKey = _keyManager.GetPublicKey(PrivateKey, true);
                Debug.Log($"KEY VALIDATION - Derived public key from private key: {derivedPublicKey}");
                Debug.Log($"KEY VALIDATION - Stored compressed public key: {CompressedPublicKey}");
                
                if (!string.IsNullOrEmpty(CompressedPublicKey) && 
                    !string.Equals(derivedPublicKey, CompressedPublicKey, StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogWarning($"CRITICAL KEY MISMATCH - Derived public key ({derivedPublicKey}) doesn't match stored compressed key ({CompressedPublicKey})");
                    Debug.LogWarning("This will cause signature verification failures on relays!");
                    
                    // Update our stored compressed key to the correct one
                    CompressedPublicKey = derivedPublicKey;
                    
                    // Ensure we have the uncompressed version right too
                    if (derivedPublicKey.Length == 66 && (derivedPublicKey.StartsWith("02") || derivedPublicKey.StartsWith("03")))
                    {
                        string newUncompressed = derivedPublicKey.Substring(2).ToLowerInvariant();
                        Debug.Log($"KEY VALIDATION - Updating uncompressed public key from {PublicKey} to {newUncompressed}");
                        PublicKey = newUncompressed;
                    }
                }
                else
                {
                    Debug.Log("KEY VALIDATION - Public key matches the derived key from private key ✓");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error validating key pair: {ex.Message}");
                // Continue anyway - the keys might still work
            }
            
            // Create event tags - empty for a simple text note
            string[][] tags = Array.Empty<string[]>();
            
            Debug.Log($"Creating note with public key: {PublicKey} (compressed: {CompressedPublicKey})");
            
            // Pass both the standard public key (for the relay) and the compressed version (for verification)
            var nostrEvent = new NostrEvent(
                PublicKey, 
                (int)NostrEventKind.TextNote, 
                content, 
                tags,
                CompressedPublicKey  // Pass the compressed key for verification
            );
            
            Debug.Log($"Signing event with private key starting with {PrivateKey.Substring(0, 4)}...");
            nostrEvent.Sign(PrivateKey);
            
            // Verify locally again before sending
            bool localVerification = nostrEvent.VerifySignature();
            Debug.Log($"EVENT VERIFICATION - Local verification result: {localVerification}");

            // Perform an intensive debug verification with detailed logs
            bool deepVerification = nostrEvent.DeepDebugVerification();
            Debug.Log($"EVENT VERIFICATION - Deep verification result: {deepVerification}");
            
            Debug.Log("==== SENDING EVENT TO RELAYS ====");
            StartCoroutine(PostTextNoteCoroutine(nostrEvent));
        }
        
        private IEnumerator PostTextNoteCoroutine(NostrEvent nostrEvent)
        {
            Debug.Log($"Publishing event ID: {nostrEvent.Id}");
            bool published = false;
            yield return _nostrClient.PublishEvent(nostrEvent, result => published = result);
            
            if (published)
            {
                Debug.Log($"Note published successfully with ID: {nostrEvent.Id}");
            }
            else
            {
                Debug.LogError($"Failed to publish note with ID: {nostrEvent.Id}");
            }
        }
        
        /// <summary>
        /// Sends a test message to the connected relays
        /// </summary>
        /// <param name="customMessage">Optional custom message. If not provided, a default test message will be used.</param>
        /// <returns>True if the message was sent successfully</returns>
        public bool SendTestMessage(string customMessage = null)
        {
            if (string.IsNullOrEmpty(PrivateKey))
            {
                Debug.LogError("Private key not set. Cannot send test message.");
                return false;
            }

            // Use the provided message or a default one
            string content = customMessage ?? $"Hello from Unity Nostr SDK! Test message sent at: {DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC")}";
            
            // Send the message
            Debug.Log($"Sending test message: \"{content}\"");
            PostTextNote(content);
            
            // In a real application, you'd want to use a callback to know if the message was actually sent
            // This just returns true if we attempted to send
            return true;
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