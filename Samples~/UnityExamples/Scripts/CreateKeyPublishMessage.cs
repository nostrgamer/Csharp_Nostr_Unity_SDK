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
    /// Example demonstrating how to generate a new Nostr key pair and publish messages
    /// </summary>
    public class CreateKeyPublishMessage : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button generateKeyButton;
        [SerializeField] private TMP_InputField privateKeyOutput;
        [SerializeField] private TMP_InputField publicKeyOutput;
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
        private bool _isKeyGenerated = false;
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
            
            // Set key output fields to read-only
            privateKeyOutput.readOnly = true;
            publicKeyOutput.readOnly = true;
            
            // Add button listeners
            generateKeyButton.onClick.AddListener(GenerateKey);
            publishButton.onClick.AddListener(PublishMessage);
            
            // Set initial status
            UpdateStatus("Click 'Generate Key' to create a new Nostr key pair");
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
            generateKeyButton.onClick.RemoveListener(GenerateKey);
            publishButton.onClick.RemoveListener(PublishMessage);
        }

        /// <summary>
        /// Generates a new Nostr key pair and connects to relays
        /// </summary>
        public void GenerateKey()
        {
            try
            {
                // Generate a new key pair
                _keyPair = new KeyPair();
                
                // Display the keys in the UI
                privateKeyOutput.text = _keyPair.Nsec;
                publicKeyOutput.text = _keyPair.Npub;
                
                // Update state
                _isKeyGenerated = true;
                
                // Connect to relays
                ConnectToRelays();
                
                // Update UI
                messageInputField.interactable = true;
                
                // Show feedback
                UpdateStatus($"New key pair generated successfully!\nPublic key: {_keyPair.Npub}\nConnecting to relays...");
                
                // Optional: Log to console (be careful with private keys in logs)
                Debug.Log($"[CreateKeyPublishMessage] Generated new key pair with public key: {_keyPair.Npub}");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error generating key pair: {ex.Message}");
                Debug.LogError($"Key generation error: {ex.Message}");
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
            if (!_isKeyGenerated || _keyPair == null)
            {
                UpdateStatus("Error: Please generate a key pair first");
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
                    publishButton.interactable = _isKeyGenerated && _connectedRelays > 0;
                    break;
                    
                case RelayState.Disconnected:
                    _connectedRelays = Math.Max(0, _connectedRelays - 1);
                    UpdateStatus($"Disconnected from {relayUrl}. {_connectedRelays} relays connected.");
                    publishButton.interactable = _isKeyGenerated && _connectedRelays > 0;
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
            Debug.Log($"[CreateKeyPublishMessage] {message}");
        }
    }
} 