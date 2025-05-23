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
    /// Example demonstrating how to send game invitations via Nostr
    /// </summary>
    public class GameInvitationManager : MonoBehaviour
    {
        [Header("Main UI")]
        [SerializeField] private Button enterNostrKeyButton;
        [SerializeField] private Button generateNostrKeyButton;
        
        [Header("Key Input Panel")]
        [SerializeField] private GameObject contentPanelNostrKeyInput;
        [SerializeField] private TMP_InputField keyInputField;
        [SerializeField] private Button validateKeyButton;
        [SerializeField] private TextMeshProUGUI keyInputStatusText;
        
        [Header("Key Generation Panel")]
        [SerializeField] private GameObject contentPanelNostrKeyGeneration;
        [SerializeField] private Button generateKeyButton;
        [SerializeField] private TMP_InputField privateKeyOutput;
        [SerializeField] private TMP_InputField publicKeyOutput;
        [SerializeField] private TextMeshProUGUI keyGenerationStatusText;
        
        [Header("Game Invitation Panel")]
        [SerializeField] private GameObject gameInvitePanel;
        [SerializeField] private TMP_InputField gameInviteMessage;
        [SerializeField] private TMP_InputField gameInviteLink;
        [SerializeField] private Button sendGameMessageButton;
        [SerializeField] private TextMeshProUGUI inviteStatusText;
        
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
            // Hide panels initially
            contentPanelNostrKeyInput.SetActive(false);
            contentPanelNostrKeyGeneration.SetActive(false);
            gameInvitePanel.SetActive(false);
            
            // Set read-only fields
            privateKeyOutput.readOnly = true;
            publicKeyOutput.readOnly = true;
            
            // Disable send button until a key is validated/generated
            sendGameMessageButton.interactable = false;
            
            // Add button listeners
            enterNostrKeyButton.onClick.AddListener(ShowKeyInputPanel);
            generateNostrKeyButton.onClick.AddListener(ShowKeyGenerationPanel);
            validateKeyButton.onClick.AddListener(ValidateKey);
            generateKeyButton.onClick.AddListener(GenerateKey);
            sendGameMessageButton.onClick.AddListener(SendGameInvitation);
            
            // Set initial status messages
            UpdateKeyInputStatus("Enter your Nostr secret key (nsec)");
            UpdateKeyGenerationStatus("Click 'Generate Key' to create a new Nostr key pair");
            UpdateInviteStatus("Waiting for key input...");
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
            
            // Remove button listeners
            enterNostrKeyButton.onClick.RemoveListener(ShowKeyInputPanel);
            generateNostrKeyButton.onClick.RemoveListener(ShowKeyGenerationPanel);
            validateKeyButton.onClick.RemoveListener(ValidateKey);
            generateKeyButton.onClick.RemoveListener(GenerateKey);
            sendGameMessageButton.onClick.RemoveListener(SendGameInvitation);
        }

        #region UI Management

        /// <summary>
        /// Shows the key input panel and hides others
        /// </summary>
        private void ShowKeyInputPanel()
        {
            contentPanelNostrKeyInput.SetActive(true);
            contentPanelNostrKeyGeneration.SetActive(false);
            gameInvitePanel.SetActive(false);
            UpdateKeyInputStatus("Enter your Nostr secret key (nsec)");
        }

        /// <summary>
        /// Shows the key generation panel and hides others
        /// </summary>
        private void ShowKeyGenerationPanel()
        {
            contentPanelNostrKeyInput.SetActive(false);
            contentPanelNostrKeyGeneration.SetActive(true);
            gameInvitePanel.SetActive(false);
            UpdateKeyGenerationStatus("Click 'Generate Key' to create a new Nostr key pair");
        }

        /// <summary>
        /// Shows the game invite panel
        /// </summary>
        private void ShowGameInvitePanel()
        {
            gameInvitePanel.SetActive(true);
            UpdateInviteStatus("Enter your invitation message and game link");
        }

        /// <summary>
        /// Updates the key input panel status text
        /// </summary>
        private void UpdateKeyInputStatus(string message)
        {
            keyInputStatusText.text = "Status: " + message;
            Debug.Log($"[GameInvitationManager] {message}");
        }

        /// <summary>
        /// Updates the key generation panel status text
        /// </summary>
        private void UpdateKeyGenerationStatus(string message)
        {
            keyGenerationStatusText.text = "Status: " + message;
            Debug.Log($"[GameInvitationManager] {message}");
        }

        /// <summary>
        /// Updates the game invitation panel status text
        /// </summary>
        private void UpdateInviteStatus(string message)
        {
            inviteStatusText.text = "Status: " + message;
            Debug.Log($"[GameInvitationManager] {message}");
        }

        #endregion

        #region Key Management

        /// <summary>
        /// Validates the entered secret key and connects to relays if valid
        /// </summary>
        public void ValidateKey()
        {
            string keyInput = keyInputField.text.Trim();
            
            if (string.IsNullOrEmpty(keyInput))
            {
                UpdateKeyInputStatus("Error: Please enter a secret key");
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
                ShowGameInvitePanel();
                sendGameMessageButton.interactable = true;
                
                // Show feedback
                UpdateKeyInputStatus($"Key valid! Public key: {_keyPair.Npub}\nConnecting to relays...");
                
                // Success feedback
                keyInputField.textComponent.color = Color.green;
            }
            catch (Exception ex)
            {
                _isKeyValid = false;
                UpdateKeyInputStatus($"Invalid key: {ex.Message}");
                Debug.LogError($"Key validation error: {ex.Message}");
                
                // Error feedback
                keyInputField.textComponent.color = Color.red;
            }
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
                _isKeyValid = true;
                
                // Connect to relays
                ConnectToRelays();
                
                // Update UI
                ShowGameInvitePanel();
                sendGameMessageButton.interactable = true;
                
                // Show feedback
                UpdateKeyGenerationStatus($"New key pair generated successfully!\nPublic key: {_keyPair.Npub}\nConnecting to relays...");
                
                // Optional: Log to console (be careful with private keys in logs)
                Debug.Log($"[GameInvitationManager] Generated new key pair with public key: {_keyPair.Npub}");
            }
            catch (Exception ex)
            {
                UpdateKeyGenerationStatus($"Error generating key pair: {ex.Message}");
                Debug.LogError($"Key generation error: {ex.Message}");
            }
        }

        #endregion

        #region Game Invitation Functions

        /// <summary>
        /// Sends a game invitation with the provided message and link
        /// </summary>
        public void SendGameInvitation()
        {
            if (!_isKeyValid || _keyPair == null)
            {
                Debug.LogError("Attempted to send invitation without a valid key");
                return;
            }
            
            if (_connectedRelays == 0)
            {
                string message = "No connected relays. Please wait for relay connections.";
                UpdateInviteStatus(message);
                return;
            }
            
            string inviteMessage = gameInviteMessage.text.Trim();
            string inviteLink = gameInviteLink.text.Trim();
            
            if (string.IsNullOrEmpty(inviteMessage))
            {
                UpdateInviteStatus("Error: Please enter an invitation message");
                return;
            }
            
            if (string.IsNullOrEmpty(inviteLink))
            {
                UpdateInviteStatus("Error: Please enter a game link");
                return;
            }
            
            // Publish the invitation
            PublishInvitation(inviteMessage, inviteLink);
        }

        /// <summary>
        /// Publishes a game invitation to Nostr
        /// </summary>
        private void PublishInvitation(string message, string link)
        {
            try
            {
                // Create the content with the message and link in a nice format
                string content = $"{message}\n\nJoin my game here: {link}";
                
                // Create tags as a 2D string array
                string[][] tags = new string[][]
                {
                    new string[] { "r", link },        // 'r' tag for URLs
                    new string[] { "t", "gameinvite" } // 't' tag for topic/category
                };
                
                // Create a text note event (kind 1)
                NostrEvent inviteEvent = new NostrEvent(
                    _keyPair.PublicKeyHex, 
                    (int)NostrEventKind.TextNote, 
                    content,
                    tags
                );
                
                // Sign the event with our private key
                inviteEvent.Sign(_keyPair.PrivateKeyHex);
                
                // Verify the signature locally
                bool isValid = inviteEvent.VerifySignature();
                if (!isValid)
                {
                    UpdateInviteStatus("Error: Failed to create a valid signature for the invitation");
                    return;
                }
                
                // Store the event ID so we can identify it when the relay sends it back
                _lastPublishedEventId = inviteEvent.Id;
                
                // Publish to all connected relays
                UpdateInviteStatus("Publishing game invitation...");
                _relayManager.PublishEvent(inviteEvent);
            }
            catch (Exception ex)
            {
                UpdateInviteStatus($"Error publishing invitation: {ex.Message}");
                Debug.LogError($"Publish error: {ex.Message}");
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

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handles relay state change events
        /// </summary>
        private void HandleRelayStateChanged(string relayUrl, RelayState state)
        {
            switch (state)
            {
                case RelayState.Connected:
                    _connectedRelays++;
                    string connectedMsg = $"Connected to {relayUrl}. {_connectedRelays} relays connected.";
                    UpdateKeyInputStatus(connectedMsg);
                    UpdateKeyGenerationStatus(connectedMsg);
                    UpdateInviteStatus(connectedMsg);
                    
                    // Enable send button if we have a valid key and are connected
                    if (_isKeyValid)
                    {
                        sendGameMessageButton.interactable = true;
                    }
                    break;
                    
                case RelayState.Disconnected:
                    _connectedRelays = Math.Max(0, _connectedRelays - 1);
                    string disconnectedMsg = $"Disconnected from {relayUrl}. {_connectedRelays} relays connected.";
                    UpdateKeyInputStatus(disconnectedMsg);
                    UpdateKeyGenerationStatus(disconnectedMsg);
                    UpdateInviteStatus(disconnectedMsg);
                    
                    // Disable send button if no relays are connected
                    if (_connectedRelays <= 0)
                    {
                        sendGameMessageButton.interactable = false;
                    }
                    break;
                    
                case RelayState.Connecting:
                    string connectingMsg = $"Connecting to {relayUrl}...";
                    UpdateKeyInputStatus(connectingMsg);
                    UpdateKeyGenerationStatus(connectingMsg);
                    UpdateInviteStatus(connectingMsg);
                    break;
            }
        }

        /// <summary>
        /// Handles relay error events
        /// </summary>
        private void HandleRelayError(string relayUrl, string error)
        {
            string errorMsg = $"Error from relay {relayUrl}: {error}";
            UpdateKeyInputStatus(errorMsg);
            UpdateKeyGenerationStatus(errorMsg);
            UpdateInviteStatus(errorMsg);
        }

        /// <summary>
        /// Handles received events from relays
        /// </summary>
        private void HandleEventReceived(NostrEvent ev, string relayUrl)
        {
            // Check if this is our own published event coming back to us
            if (ev.Id == _lastPublishedEventId && ev.Pubkey == _keyPair?.PublicKeyHex)
            {
                string successMsg = $"Game invitation published successfully! Event ID: {ev.Id}";
                UpdateInviteStatus(successMsg);
                
                // Clear the input fields after successful publishing
                gameInviteMessage.text = "";
                gameInviteLink.text = "";
                
                // Reset the last published event ID
                _lastPublishedEventId = null;
            }
            
            // In a more advanced implementation, you could also check for responses
            // to your invitations here by looking for events that tag your invitation
        }

        #endregion
    }
} 