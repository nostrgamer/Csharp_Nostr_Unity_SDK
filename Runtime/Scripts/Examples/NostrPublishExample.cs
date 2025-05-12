using UnityEngine;
using NostrUnity.Crypto;
using NostrUnity.Models;
using NostrUnity.Relay;
using NostrUnity.Utils;
using System.Collections.Generic;
using System;

namespace NostrUnity.Examples
{
    /// <summary>
    /// Example demonstrating how to publish Nostr events to relays
    /// </summary>
    public class NostrPublishExample : MonoBehaviour
    {
        [Header("Key Settings")]
        [Tooltip("Private key in hex or nsec format. Leave empty to generate a new key.")]
        [SerializeField] private string privateKeyInput = "";
        
        [Tooltip("Check to generate a new key pair")]
        [SerializeField] private bool generateNewKey = false;
        
        [Tooltip("Generated nsec (read-only)")]
        [SerializeField] private string generatedNsec = "";
        
        [Header("Relay Settings")]
        [Tooltip("List of relay URLs to connect to")]
        [SerializeField] private List<string> relayUrls = new List<string>
        {
            "wss://relay.damus.io",
            "wss://relay.nostr.band",
            "wss://nos.lol"
        };
        
        [Header("Message Settings")]
        [Tooltip("Content for the message to publish")]
        [SerializeField] private string messageContent = "Hello from Unity Nostr SDK!";
        
        [Header("Actions")]
        [SerializeField] private bool connectToRelays = false;
        [SerializeField] private bool publishEvent = false;
        
        [Header("Status (Read Only)")]
        [Multiline(5)]
        [SerializeField] private string statusText = "Ready";
        
        [SerializeField] private string connectionStatus = "Disconnected";
        
        // Private members
        private KeyPair _keyPair;
        private NostrRelayManager _relayManager;
        private bool _connected = false;
        private Queue<NostrEvent> _pendingEvents = new Queue<NostrEvent>();
        private int _connectedRelayCount = 0;
        
        private void Awake()
        {
            // Create the relay manager
            _relayManager = new GameObject("NostrRelayManager").AddComponent<NostrRelayManager>();
            _relayManager.transform.SetParent(transform);
            
            // Set up event handlers
            _relayManager.OnRelayStateChanged += HandleRelayStateChanged;
            _relayManager.OnRelayError += HandleRelayError;
            _relayManager.OnEventReceived += HandleEventReceived;
        }
        
        private void Start()
        {
            // If generate new key is checked, generate immediately
            if (generateNewKey)
            {
                GenerateNewKeyPair();
                generateNewKey = false;
                return;
            }

            // Try to load from secure storage if no key is input
            if (string.IsNullOrEmpty(privateKeyInput))
            {
                string loadedKey = SecurePlayerPrefs.LoadKey();
                if (!string.IsNullOrEmpty(loadedKey))
                {
                    privateKeyInput = loadedKey;
                    UpdateStatus("Loaded private key from secure storage.");
                }
            }

            // Initialize key pair
            if (!string.IsNullOrEmpty(privateKeyInput))
            {
                try
                {
                    _keyPair = new KeyPair(privateKeyInput);
                    UpdateStatus($"Key loaded. Public key: {_keyPair.Npub}");
                    // Save securely
                    SecurePlayerPrefs.SaveKey(privateKeyInput);
                }
                catch (Exception ex)
                {
                    UpdateStatus($"Error loading key: {ex.Message}");
                    Debug.LogError($"Error loading key: {ex.Message}");
                }
            }
            else
            {
                // Generate a new key pair automatically if no key is available
                GenerateNewKeyPair();
            }
        }
        
        private void Update()
        {
            // Handle relay connection toggle
            if (connectToRelays && !_connected)
            {
                ConnectToRelays();
                connectToRelays = false;
            }
            
            // Handle publish event toggle
            if (publishEvent)
            {
                PublishTextNote();
                publishEvent = false;
            }
            
            // Update connection status UI
            UpdateConnectionStatus();
        }
        
        private void OnDestroy()
        {
            if (_relayManager != null)
            {
                _relayManager.OnRelayStateChanged -= HandleRelayStateChanged;
                _relayManager.OnRelayError -= HandleRelayError;
                _relayManager.OnEventReceived -= HandleEventReceived;
            }
        }
        
        /// <summary>
        /// Connects to all configured relays
        /// </summary>
        [ContextMenu("Connect to Relays")]
        public void ConnectToRelays()
        {
            UpdateStatus("Connecting to relays...");
            
            // Add all relays from the list
            foreach (string url in relayUrls)
            {
                if (!string.IsNullOrEmpty(url))
                {
                    _relayManager.AddRelay(url);
                }
            }
            
            // Connect to all relays
            _relayManager.ConnectAll();
        }
        
        /// <summary>
        /// Publishes a text note to all connected relays, or queues it for later if not connected
        /// </summary>
        [ContextMenu("Publish Text Note")]
        public void PublishTextNote()
        {
            if (_keyPair == null)
            {
                UpdateStatus("Error: No key pair available. Please generate or load a key first.");
                return;
            }
            
            try
            {
                // Create a text note event (kind 1)
                NostrEvent textNote = new NostrEvent(
                    _keyPair.PublicKeyHex, 
                    (int)NostrEventKind.TextNote, 
                    messageContent
                );
                
                // Sign the event with our private key
                textNote.Sign(_keyPair.PrivateKeyHex);
                
                // Verify the signature locally
                bool isValid = textNote.VerifySignature();
                
                if (!isValid)
                {
                    UpdateStatus("Error: Event signature is invalid.");
                    return;
                }
                
                // Check if we have any connected relays
                if (_connectedRelayCount == 0)
                {
                    UpdateStatus("No connected relays available. Event will be queued for delivery when a relay connects.");
                    _pendingEvents.Enqueue(textNote);
                    
                    // If we're not connected, make sure we try to connect
                    if (!_connected)
                    {
                        ConnectToRelays();
                    }
                    
                    return;
                }
                
                // Publish to all connected relays
                _relayManager.PublishEvent(textNote);
                
                // Format event URLs for common clients
                string noteId = NIP19Formatter.EventToNote(textNote.Id);
                string snortUrl = NIP19Formatter.GetEventUrl(textNote.Id, NostrWebClient.Snort);
                string irisUrl = NIP19Formatter.GetEventUrl(textNote.Id, NostrWebClient.Iris);
                
                UpdateStatus($"Event published to {_connectedRelayCount} relay(s)!\n" +
                             $"Event ID: {textNote.Id}\n" +
                             $"NIP-19 Note ID: {noteId}\n" +
                             $"Content: {textNote.Content}");
                
                Debug.Log($"Published note with ID: {textNote.Id}");
                Debug.Log($"NIP-19 format: {noteId}");
                Debug.Log($"View on Snort: {snortUrl}");
                Debug.Log($"View on Iris: {irisUrl}");
                Debug.Log($"View on other clients: https://nostr.band/note/{noteId}");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error publishing event: {ex.Message}");
                Debug.LogError($"Error publishing event: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Processes any queued events when a relay connects
        /// </summary>
        private void ProcessQueuedEvents()
        {
            if (_pendingEvents.Count == 0)
                return;
                
            UpdateStatus($"Processing {_pendingEvents.Count} queued events...");
            
            while (_pendingEvents.Count > 0)
            {
                NostrEvent queuedEvent = _pendingEvents.Dequeue();
                _relayManager.PublishEvent(queuedEvent);
                
                // Format event URLs for common clients
                string noteId = NIP19Formatter.EventToNote(queuedEvent.Id);
                string snortUrl = NIP19Formatter.GetEventUrl(queuedEvent.Id, NostrWebClient.Snort);
                
                UpdateStatus($"Queued event published! Event ID: {queuedEvent.Id}\nNIP-19 Note ID: {noteId}");
                Debug.Log($"Published queued note with ID: {queuedEvent.Id}");
                Debug.Log($"NIP-19 format: {noteId}");
                Debug.Log($"View on Snort: {snortUrl}");
            }
        }
        
        /// <summary>
        /// Updates the connection status display
        /// </summary>
        private void UpdateConnectionStatus()
        {
            string status = _connectedRelayCount > 0 
                ? $"Connected to {_connectedRelayCount} relay(s)" 
                : "Not connected";
                
            connectionStatus = status;
        }
        
        #region Event Handlers
        
        private void HandleRelayStateChanged(string relayUrl, RelayState state)
        {
            Debug.Log($"Relay {relayUrl} changed state to: {state}");
            
            if (state == RelayState.Connected)
            {
                _connected = true;
                _connectedRelayCount++;
                UpdateStatus($"Connected to relay: {relayUrl}. Total connected relays: {_connectedRelayCount}");
                
                // If we have queued events, publish them now
                ProcessQueuedEvents();
            }
            else if (state == RelayState.Disconnected || state == RelayState.Failed)
            {
                if (_connectedRelayCount > 0)
                    _connectedRelayCount--;
                
                if (_connectedRelayCount == 0)
                    _connected = false;
                    
                UpdateStatus($"Relay {relayUrl} disconnected. Total connected relays: {_connectedRelayCount}");
            }
        }
        
        private void HandleRelayError(string relayUrl, string error)
        {
            Debug.LogError($"Error from relay {relayUrl}: {error}");
            UpdateStatus($"Relay error from {relayUrl}: {error}");
        }
        
        private void HandleEventReceived(NostrEvent ev, string relayUrl)
        {
            Debug.Log($"Received event from {relayUrl}: {ev.Id}");
            
            // Here you could update UI or process the event further
        }
        
        #endregion
        
        /// <summary>
        /// Updates the status text
        /// </summary>
        private void UpdateStatus(string message)
        {
            statusText = $"[{DateTime.Now.ToString("HH:mm:ss")}] {message}";
            Debug.Log($"[NostrPublishExample] {message}");
        }

        /// <summary>
        /// Deletes the private key from secure storage
        /// </summary>
        public void DeleteStoredPrivateKey()
        {
            SecurePlayerPrefs.DeleteKey();
            UpdateStatus("Deleted private key from secure storage.");
        }

        private void GenerateNewKeyPair()
        {
            try
            {
                // Generate a new key pair
                _keyPair = new KeyPair();
                privateKeyInput = _keyPair.Nsec;
                generatedNsec = _keyPair.Nsec;
                UpdateStatus($"Generated new key pair! Public key: {_keyPair.Npub}");
                
                // Save securely
                SecurePlayerPrefs.SaveKey(_keyPair.Nsec);
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error generating key pair: {ex.Message}");
                Debug.LogError($"Error generating key pair: {ex.Message}");
            }
        }
    }
} 