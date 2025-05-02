using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Threading.Tasks;
using System.Collections;

namespace Nostr.Unity.Examples
{
    /// <summary>
    /// Example component showing how to use the Nostr SDK
    /// </summary>
    public class NostrClientExample : MonoBehaviour
    {
        [Header("Relay Configuration")]
        [SerializeField] 
        private string[] relayUrls = new string[] 
        { 
            "wss://relay.damus.io",
            "wss://relay.nostr.info", 
            "wss://nostr-pub.wellorder.net" 
        };
        
        [Header("UI Elements (Optional)")]
        [SerializeField] 
        private Text statusText;
        
        [SerializeField] 
        private Text publicKeyText;
        
        [SerializeField] 
        private InputField messageInput;
        
        [SerializeField] 
        private Button sendButton;
        
        [Header("Key Management")]
        [SerializeField]
        private bool useEncryption = true;
        
        [SerializeField]
        private string encryptionPassword = "change-this-in-production"; // Only for demo!
        
        private NostrClient _client;
        private NostrKeyManager _keyManager;
        private string _privateKey;
        private string _publicKey;
        
        private void Start()
        {
            // Initialize the Secp256k1 library
            if (!Secp256k1Manager.Initialize())
            {
                Debug.LogError("Failed to initialize Secp256k1Manager");
                UpdateStatus("Failed to initialize cryptography");
                return;
            }
            
            // Create a new key manager
            _keyManager = new NostrKeyManager();
            
            // Load or generate keys
            InitializeKeys();
            
            // Initialize the Nostr client
            _client = new NostrClient();
            
            // Set up event handlers
            _client.Connected += OnConnected;
            _client.Disconnected += OnDisconnected;
            _client.EventReceived += OnEventReceived;
            _client.Error += OnError;
            
            // Connect to relays
            StartCoroutine(ConnectToRelays());
            
            // Set up UI button
            if (sendButton != null && messageInput != null)
            {
                sendButton.onClick.AddListener(() => StartCoroutine(SendMessage()));
            }
            
            // Run a basic cryptography test
            RunCryptoTest();
        }
        
        private void InitializeKeys()
        {
            try
            {
                // Check if we have stored keys
                if (_keyManager.HasStoredKeys())
                {
                    if (useEncryption)
                    {
                        // In production, you'd prompt the user for a password
                        // This is just for demonstration
                        _privateKey = DecryptPrivateKey(_keyManager.LoadPrivateKey(useBech32: false), encryptionPassword);
                    }
                    else
                    {
                        _privateKey = _keyManager.LoadPrivateKey(useBech32: false);
                    }
                    
                    UpdateStatus("Loaded existing keys");
                }
                else
                {
                    // Generate a new key pair
                    _privateKey = _keyManager.GeneratePrivateKey(useHex: true);
                    
                    // Store the keys (with optional encryption)
                    if (useEncryption)
                    {
                        string encryptedKey = EncryptPrivateKey(_privateKey, encryptionPassword);
                        bool stored = _keyManager.StoreKeys(encryptedKey, encrypt: false); // Already encrypted
                        
                        if (stored)
                        {
                            UpdateStatus("Generated and stored encrypted keys");
                        }
                        else
                        {
                            UpdateStatus("Generated new keys (encryption failed)");
                        }
                    }
                    else
                    {
                        bool stored = _keyManager.StoreKeys(_privateKey, encrypt: false);
                        
                        if (stored)
                        {
                            UpdateStatus("Generated and stored new keys");
                        }
                        else
                        {
                            UpdateStatus("Generated new keys (not stored)");
                        }
                    }
                }
                
                // Get the public key
                _publicKey = _keyManager.GetPublicKey(_privateKey, useHex: true);
                
                // Display the public key
                if (publicKeyText != null)
                {
                    string displayKey = _publicKey.Substring(0, 8) + "..." + _publicKey.Substring(_publicKey.Length - 8);
                    publicKeyText.text = "Public Key: " + displayKey;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error initializing keys: {ex.Message}");
                UpdateStatus("Failed to initialize keys");
            }
        }
        
        // Simple encryption for demo purposes only
        // In production, use a more secure method
        private string EncryptPrivateKey(string privateKey, string password)
        {
            if (string.IsNullOrEmpty(privateKey) || string.IsNullOrEmpty(password))
                return privateKey;
                
            // Very basic XOR encryption - NOT SECURE, just for demo
            byte[] keyBytes = System.Text.Encoding.UTF8.GetBytes(privateKey);
            byte[] passwordBytes = System.Text.Encoding.UTF8.GetBytes(password);
            byte[] result = new byte[keyBytes.Length];
            
            for (int i = 0; i < keyBytes.Length; i++)
            {
                result[i] = (byte)(keyBytes[i] ^ passwordBytes[i % passwordBytes.Length]);
            }
            
            return Convert.ToBase64String(result);
        }
        
        // Simple decryption for demo purposes only
        private string DecryptPrivateKey(string encryptedKey, string password)
        {
            if (string.IsNullOrEmpty(encryptedKey) || string.IsNullOrEmpty(password))
                return encryptedKey;
                
            try
            {
                // Very basic XOR decryption - NOT SECURE, just for demo
                byte[] encryptedBytes = Convert.FromBase64String(encryptedKey);
                byte[] passwordBytes = System.Text.Encoding.UTF8.GetBytes(password);
                byte[] result = new byte[encryptedBytes.Length];
                
                for (int i = 0; i < encryptedBytes.Length; i++)
                {
                    result[i] = (byte)(encryptedBytes[i] ^ passwordBytes[i % passwordBytes.Length]);
                }
                
                return System.Text.Encoding.UTF8.GetString(result);
            }
            catch
            {
                // If decryption fails, return as-is (might not be encrypted)
                return encryptedKey;
            }
        }
        
        private IEnumerator ConnectToRelays()
        {
            UpdateStatus("Connecting to relays...");
            foreach (var relayUrl in relayUrls)
            {
                bool connected = false;
                yield return StartCoroutine(_client.ConnectToRelay(relayUrl, result => connected = result));
                if (connected)
                {
                    Debug.Log($"Connected to relay: {relayUrl}");
                }
                else
                {
                    Debug.LogWarning($"Failed to connect to relay: {relayUrl}");
                }
            }
            // Subscribe to text notes
            Filter filter = new Filter();
            filter.Kinds = new int[] { (int)NostrEventKind.TextNote };
            filter.Limit = 10;
            long oneHourAgo = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 3600;
            filter.Since = oneHourAgo;
            string subscriptionId = _client.Subscribe(filter, null); // Provide a callback if needed
            Debug.Log($"Subscribed with ID: {subscriptionId}");
        }
        
        private IEnumerator SendMessage()
        {
            if (string.IsNullOrEmpty(messageInput.text))
                yield break;
            string message = messageInput.text;
            UpdateStatus("Sending message...");
            // Create a new text note event
            NostrEvent nostrEvent = new NostrEvent(_publicKey, (int)NostrEventKind.TextNote, message);
            // Sign the event
            nostrEvent.Sign(_privateKey);
            // Verify the signature locally before sending
            if (!nostrEvent.VerifySignature())
            {
                Debug.LogError("Event signature verification failed locally");
                UpdateStatus("Signing error: Invalid signature");
                yield break;
            }
            bool published = false;
            yield return StartCoroutine(_client.PublishEvent(nostrEvent, result => published = result));
            if (published)
            {
                messageInput.text = "";
                UpdateStatus("Message sent");
            }
            else
            {
                UpdateStatus("Failed to send message");
            }
        }
        
        private void OnConnected(object sender, string relayUrl)
        {
            Debug.Log($"Connected to relay: {relayUrl}");
            UpdateStatus($"Connected to {relayUrl}");
        }
        
        private void OnDisconnected(object sender, string relayUrl)
        {
            Debug.Log($"Disconnected from relay: {relayUrl}");
            UpdateStatus($"Disconnected from {relayUrl}");
        }
        
        private void OnEventReceived(object sender, NostrEventArgs args)
        {
            try
            {
                Debug.Log($"Received event: {args.Event.Id} from {args.RelayUrl}");
                
                // Verify the signature
                bool valid = args.Event.VerifySignature();
                
                if (valid)
                {
                    Debug.Log($"Verified message: {args.Event.Content}");
                    UpdateStatus($"Received: {args.Event.Content.Substring(0, Math.Min(args.Event.Content.Length, 20))}...");
                }
                else
                {
                    Debug.LogWarning($"Invalid signature for event: {args.Event.Id}");
                    UpdateStatus("Received message with invalid signature");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error processing received event: {ex.Message}");
            }
        }
        
        private void OnError(object sender, string error)
        {
            Debug.LogError($"Nostr client error: {error}");
            UpdateStatus($"Error: {error}");
        }
        
        private void UpdateStatus(string status)
        {
            Debug.Log(status);
            
            if (statusText != null)
            {
                // Update UI on the main thread
                statusText.text = status;
            }
        }
        
        private void RunCryptoTest()
        {
            try
            {
                // Test message
                string testMessage = "Hello, Nostr!";
                
                // Generate a new key pair for testing
                byte[] privateKeyBytes = Secp256k1Manager.GeneratePrivateKey();
                byte[] publicKeyBytes = Secp256k1Manager.GetPublicKey(privateKeyBytes);
                
                // Compute message hash
                byte[] messageHash = Secp256k1Manager.ComputeMessageHash(testMessage);
                
                // Generate a recoverable signature
                byte[] recoverableSignature = Secp256k1Manager.SignRecoverable(messageHash, privateKeyBytes);
                
                // Verify the signature
                bool verified = Secp256k1Manager.Verify(messageHash, recoverableSignature, publicKeyBytes);
                
                if (!verified)
                {
                    Debug.LogError("Signature verification failed");
                    return;
                }
                
                // Recover public key from signature
                byte[] recoveredKey = Secp256k1Manager.RecoverPublicKey(recoverableSignature, messageHash);
                
                // Compare the original and recovered public keys
                bool keysMatch = CompareKeys(publicKeyBytes, recoveredKey);
                
                if (keysMatch)
                {
                    Debug.Log("Cryptography test passed: Key recovery successful");
                }
                else
                {
                    Debug.LogError("Key recovery failed: keys don't match");
                }
                
                // Test event serialization and verification
                TestEventSerialization();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Crypto test error: {ex.Message}");
            }
        }
        
        private void TestEventSerialization()
        {
            // Create a test event
            NostrEvent testEvent = new NostrEvent();
            testEvent.Kind = 1; // Text note
            testEvent.CreatedAt = 1683036160; // Fixed timestamp for consistent ID
            testEvent.Content = "Test message";
            testEvent.PublicKey = _publicKey;
            testEvent.Tags = new string[][] 
            {
                new string[] { "e", "1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef" },
                new string[] { "p", "abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890" }
            };
            
            // Compute ID
            string id = testEvent.ComputeId();
            Debug.Log($"Test Event ID: {id}");
            
            // Sign the event
            testEvent.Sign(_privateKey);
            
            // Verify signature
            bool valid = testEvent.VerifySignature();
            Debug.Log($"Test Event Signature Valid: {valid}");
            
            if (!valid)
            {
                Debug.LogError("Test event signature verification failed");
            }
        }
        
        private bool CompareKeys(byte[] key1, byte[] key2)
        {
            if (key1.Length != key2.Length)
                return false;
                
            for (int i = 0; i < key1.Length; i++)
            {
                if (key1[i] != key2[i])
                    return false;
            }
            
            return true;
        }
        
        private void OnDestroy()
        {
            // Clean up event handlers
            if (_client != null)
            {
                _client.Connected -= OnConnected;
                _client.Disconnected -= OnDisconnected;
                _client.EventReceived -= OnEventReceived;
                _client.Error -= OnError;
                
                // Disconnect from relays
                _client.Disconnect();
            }
        }
    }
} 