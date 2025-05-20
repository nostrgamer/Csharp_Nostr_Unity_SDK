using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NostrUnity.Crypto;
using NostrUnity.Models;
using NostrUnity.Relay;
using System;
using System.Collections.Generic;

namespace NostrUnity.Samples
{
    /// <summary>
    /// Example demonstrating how to input a secret key and publish messages to Nostr
    /// </summary>
    public class InputKeyPublishMessage : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_InputField keyInputField;
        [SerializeField] private Button validateButton;
        [SerializeField] private TMP_InputField messageInputField;
        [SerializeField] private Button publishButton;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("Relay Settings")]
        [Tooltip("List of relay URLs to connect to")]
        [SerializeField] private List<string> relayUrls = new List<string>
        {
            "wss://relay.damus.io",
            "wss://relay.nostr.band",
            "wss://nos.lol"
        };

        // Private variables
        private KeyPair _keyPair;
        private NostrRelayManager _relayManager;
        private bool _isKeyValid = false;
        private int _connectedRelays = 0;
        private string _lastPublishedEventId = null;

        private void Awake()
        {
            // Create relay manager
            _relayManager = new GameObject("NostrRelayManager").AddComponent<NostrRelayManager>();
            _relayManager.transform.SetParent(transform);
            
            // Set up event handlers
            _relayManager.OnRelayStateChanged += HandleRelayStateChanged;
            _relayManager.OnRelayError += HandleRelayError;
            _relayManager.OnEventReceived += HandleEventReceived;
        }

        private void Start()
        {
            // Setup initial UI state
            publishButton.interactable = false;
            messageInputField.interactable = false;
            
            // Add button listeners
            validateButton.onClick.AddListener(ValidateKey);
            publishButton.onClick.AddListener(PublishMessage);
            
            // Set initial status
            UpdateStatus("Please enter your Nostr secret key (nsec)");
        }

        private void OnDestroy()
        {
            // Remove event handlers
            if (_relayManager != null)
            {
                _relayManager.OnRelayStateChanged -= HandleRelayStateChanged;
                _relayManager.OnRelayError -= HandleRelayError;
                _relayManager.OnEventReceived -= HandleEventReceived;
            }
            
            // Remove listeners
            validateButton.onClick.RemoveListener(ValidateKey);
            publishButton.onClick.RemoveListener(PublishMessage);
        }

        /// <summary>
        /// Validates the entered secret key and connects to relays if valid
        /// </summary>
        public void ValidateKey()
        {
            string keyInput = keyInputField.text.Trim();
            
            if (string.IsNullOrEmpty(keyInput))
            {
                UpdateStatus("Error: Please enter a secret key");
                return;
            }
            
            try
            {
                // Create key pair from the input
                _keyPair = new KeyPair(keyInput);
                
                // Key is valid if we reach here (otherwise an exception would be thrown)
                _isKeyValid = true;
                
                // Connect to relays
                ConnectToRelays();
                
                // Update UI
                messageInputField.interactable = true;
                
                // Show feedback
                UpdateStatus($"Key valid! Public key: {_keyPair.Npub}\nConnecting to relays...");
                
                // Success feedback - you could also add visual feedback here
                keyInputField.textComponent.color = Color.green;
            }
            catch (Exception ex)
            {
                _isKeyValid = false;
                UpdateStatus($"Invalid key: {ex.Message}");
                Debug.LogError($"Key validation error: {ex.Message}");
                
                // Error feedback
                keyInputField.textComponent.color = Color.red;
            }
        }

        /// <summary>
        /// Connects to all configured relays
        /// </summary>
        private void ConnectToRelays()
        {
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
        /// Publishes the entered message to all connected relays
        /// </summary>
        public void PublishMessage()
        {
            if (!_isKeyValid || _keyPair == null)
            {
                UpdateStatus("Error: Please validate your key first");
                return;
            }
            
            if (_connectedRelays == 0)
            {
                UpdateStatus("Error: No connected relays. Please wait for relay connections.");
                return;
            }
            
            string message = messageInputField.text.Trim();
            if (string.IsNullOrEmpty(message))
            {
                UpdateStatus("Error: Please enter a message to publish");
                return;
            }
            
            try
            {
                // Create a text note event (kind 1)
                NostrEvent textNote = new NostrEvent(
                    _keyPair.PublicKeyHex, 
                    (int)NostrEventKind.TextNote, 
                    message
                );
                
                // Sign the event with our private key
                textNote.Sign(_keyPair.PrivateKeyHex);
                
                // Verify the signature locally
                bool isValid = textNote.VerifySignature();
                if (!isValid)
                {
                    UpdateStatus("Error: Failed to create a valid signature for the message");
                    return;
                }
                
                // Store the event ID so we can identify it when the relay sends it back
                _lastPublishedEventId = textNote.Id;
                
                // Publish to all connected relays
                UpdateStatus("Publishing message...");
                _relayManager.PublishEvent(textNote);
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error publishing message: {ex.Message}");
                Debug.LogError($"Publish error: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles relay state change events
        /// </summary>
        private void HandleRelayStateChanged(string relayUrl, RelayState state)
        {
            switch (state)
            {
                case RelayState.Connected:
                    _connectedRelays++;
                    UpdateStatus($"Connected to {relayUrl}. {_connectedRelays} relays connected.");
                    publishButton.interactable = _isKeyValid && _connectedRelays > 0;
                    break;
                    
                case RelayState.Disconnected:
                    _connectedRelays = Math.Max(0, _connectedRelays - 1);
                    UpdateStatus($"Disconnected from {relayUrl}. {_connectedRelays} relays connected.");
                    publishButton.interactable = _isKeyValid && _connectedRelays > 0;
                    break;
                    
                case RelayState.Connecting:
                    UpdateStatus($"Connecting to {relayUrl}...");
                    break;
            }
        }

        /// <summary>
        /// Handles relay error events
        /// </summary>
        private void HandleRelayError(string relayUrl, string error)
        {
            UpdateStatus($"Error from relay {relayUrl}: {error}");
        }

        /// <summary>
        /// Handles received events from relays
        /// </summary>
        private void HandleEventReceived(NostrEvent ev, string relayUrl)
        {
            // Check if this is our own published event coming back to us
            if (ev.Id == _lastPublishedEventId && ev.Pubkey == _keyPair?.PublicKeyHex)
            {
                UpdateStatus($"Message published successfully! Event ID: {ev.Id}");
                
                // Clear the message input field after successful publish
                messageInputField.text = "";
                
                // Reset the last published event ID
                _lastPublishedEventId = null;
            }
        }

        /// <summary>
        /// Updates the status text
        /// </summary>
        private void UpdateStatus(string message)
        {
            statusText.text = "Status: " + message;
            Debug.Log($"[InputKeyPublishMessage] {message}");
        }
    }
} 