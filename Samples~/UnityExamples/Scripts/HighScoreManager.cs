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
    /// Example demonstrating Nostr integration with a game's high score system
    /// </summary>
    public class HighScoreManager : MonoBehaviour
    {
        [Header("Main UI")]
        [SerializeField] private Button enterKeyButton;
        [SerializeField] private Button generateKeyButton;
        [SerializeField] private TextMeshProUGUI highScoreText;
        [SerializeField] private Button playGameButton;
        
        [Header("Enter Key UI")]
        [SerializeField] private GameObject contentPanelEnterNostrKey;
        [SerializeField] private TMP_InputField keyInputField;
        [SerializeField] private Button validateNsecButton;
        [SerializeField] private TextMeshProUGUI enterKeyStatusText;
        
        [Header("Generate Key UI")]
        [SerializeField] private GameObject contentPanelGenerateNostrKey;
        [SerializeField] private Button generateKeyButton2;
        [SerializeField] private TMP_InputField privateKeyOutput;
        [SerializeField] private TMP_InputField publicKeyOutput;
        [SerializeField] private TextMeshProUGUI generateKeyStatusText;
        
        [Header("Game Settings")]
        [Tooltip("Minimum possible score when playing")]
        [SerializeField] private int minScore = 0;
        [Tooltip("Maximum possible score when playing")]
        [SerializeField] private int maxScore = 1000;
        
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
        private int _currentHighScore = 0;
        private string _playerName = "Player";

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
            // Hide both content panels initially
            contentPanelEnterNostrKey.SetActive(false);
            contentPanelGenerateNostrKey.SetActive(false);
            
            // Set read-only fields
            privateKeyOutput.readOnly = true;
            publicKeyOutput.readOnly = true;
            
            // Disable play button until a key is validated/generated
            playGameButton.interactable = false;
            
            // Display initial high score
            UpdateHighScore(0);
            
            // Add button listeners
            enterKeyButton.onClick.AddListener(ShowEnterKeyPanel);
            generateKeyButton.onClick.AddListener(ShowGenerateKeyPanel);
            validateNsecButton.onClick.AddListener(ValidateKey);
            generateKeyButton2.onClick.AddListener(GenerateKey);
            playGameButton.onClick.AddListener(PlayGame);
            
            // Set initial status messages
            UpdateEnterKeyStatus("Enter your Nostr secret key (nsec)");
            UpdateGenerateKeyStatus("Click 'Generate Key' to create a new Nostr key pair");
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
            enterKeyButton.onClick.RemoveListener(ShowEnterKeyPanel);
            generateKeyButton.onClick.RemoveListener(ShowGenerateKeyPanel);
            validateNsecButton.onClick.RemoveListener(ValidateKey);
            generateKeyButton2.onClick.RemoveListener(GenerateKey);
            playGameButton.onClick.RemoveListener(PlayGame);
        }

        #region UI Management

        /// <summary>
        /// Shows the Enter Key panel and hides others
        /// </summary>
        private void ShowEnterKeyPanel()
        {
            contentPanelEnterNostrKey.SetActive(true);
            contentPanelGenerateNostrKey.SetActive(false);
            UpdateEnterKeyStatus("Enter your Nostr secret key (nsec)");
        }

        /// <summary>
        /// Shows the Generate Key panel and hides others
        /// </summary>
        private void ShowGenerateKeyPanel()
        {
            contentPanelEnterNostrKey.SetActive(false);
            contentPanelGenerateNostrKey.SetActive(true);
            UpdateGenerateKeyStatus("Click 'Generate Key' to create a new Nostr key pair");
        }

        /// <summary>
        /// Updates the high score display
        /// </summary>
        private void UpdateHighScore(int score)
        {
            _currentHighScore = score;
            highScoreText.text = $"High Score: {_currentHighScore}";
        }

        /// <summary>
        /// Updates the enter key panel status text
        /// </summary>
        private void UpdateEnterKeyStatus(string message)
        {
            enterKeyStatusText.text = "Status: " + message;
            Debug.Log($"[HighScoreManager] {message}");
        }

        /// <summary>
        /// Updates the generate key panel status text
        /// </summary>
        private void UpdateGenerateKeyStatus(string message)
        {
            generateKeyStatusText.text = "Status: " + message;
            Debug.Log($"[HighScoreManager] {message}");
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
                UpdateEnterKeyStatus("Error: Please enter a secret key");
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
                playGameButton.interactable = true;
                
                // Show feedback
                UpdateEnterKeyStatus($"Key valid! Public key: {_keyPair.Npub}\nConnecting to relays...");
                
                // Success feedback - you could also add visual feedback here
                keyInputField.textComponent.color = Color.green;
            }
            catch (Exception ex)
            {
                _isKeyValid = false;
                UpdateEnterKeyStatus($"Invalid key: {ex.Message}");
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
                playGameButton.interactable = true;
                
                // Show feedback
                UpdateGenerateKeyStatus($"New key pair generated successfully!\nPublic key: {_keyPair.Npub}\nConnecting to relays...");
                
                // Optional: Log to console (be careful with private keys in logs)
                Debug.Log($"[HighScoreManager] Generated new key pair with public key: {_keyPair.Npub}");
            }
            catch (Exception ex)
            {
                UpdateGenerateKeyStatus($"Error generating key pair: {ex.Message}");
                Debug.LogError($"Key generation error: {ex.Message}");
            }
        }

        #endregion

        #region Game and Nostr Functions

        /// <summary>
        /// Simulates playing a game and publishes the score
        /// </summary>
        public void PlayGame()
        {
            if (!_isKeyValid || _keyPair == null)
            {
                Debug.LogError("Attempted to play game without a valid key");
                return;
            }
            
            if (_connectedRelays == 0)
            {
                string message = "No connected relays. Please wait for relay connections.";
                UpdateEnterKeyStatus(message);
                UpdateGenerateKeyStatus(message);
                return;
            }
            
            // Generate a random score
            int score = UnityEngine.Random.Range(minScore, maxScore + 1);
            
            // Update high score if this score is higher
            if (score > _currentHighScore)
            {
                UpdateHighScore(score);
            }
            
            // Publish the score
            PublishScore(score);
        }

        /// <summary>
        /// Publishes a score to Nostr
        /// </summary>
        private void PublishScore(int score)
        {
            try
            {
                // Create the score message with game info
                string message = $"{_playerName} scored {score} points in Nostr Unity SDK Demo Game!";
                
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
                    string errorMsg = "Error: Failed to create a valid signature for the score";
                    UpdateEnterKeyStatus(errorMsg);
                    UpdateGenerateKeyStatus(errorMsg);
                    return;
                }
                
                // Store the event ID so we can identify it when the relay sends it back
                _lastPublishedEventId = textNote.Id;
                
                // Publish to all connected relays
                string pubMsg = $"Publishing score of {score}...";
                UpdateEnterKeyStatus(pubMsg);
                UpdateGenerateKeyStatus(pubMsg);
                _relayManager.PublishEvent(textNote);
            }
            catch (Exception ex)
            {
                string errorMsg = $"Error publishing score: {ex.Message}";
                UpdateEnterKeyStatus(errorMsg);
                UpdateGenerateKeyStatus(errorMsg);
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
                    UpdateEnterKeyStatus(connectedMsg);
                    UpdateGenerateKeyStatus(connectedMsg);
                    break;
                    
                case RelayState.Disconnected:
                    _connectedRelays = Math.Max(0, _connectedRelays - 1);
                    string disconnectedMsg = $"Disconnected from {relayUrl}. {_connectedRelays} relays connected.";
                    UpdateEnterKeyStatus(disconnectedMsg);
                    UpdateGenerateKeyStatus(disconnectedMsg);
                    break;
                    
                case RelayState.Connecting:
                    string connectingMsg = $"Connecting to {relayUrl}...";
                    UpdateEnterKeyStatus(connectingMsg);
                    UpdateGenerateKeyStatus(connectingMsg);
                    break;
            }
        }

        /// <summary>
        /// Handles relay error events
        /// </summary>
        private void HandleRelayError(string relayUrl, string error)
        {
            string errorMsg = $"Error from relay {relayUrl}: {error}";
            UpdateEnterKeyStatus(errorMsg);
            UpdateGenerateKeyStatus(errorMsg);
        }

        /// <summary>
        /// Handles received events from relays
        /// </summary>
        private void HandleEventReceived(NostrEvent ev, string relayUrl)
        {
            // Check if this is our own published event coming back to us
            if (ev.Id == _lastPublishedEventId && ev.Pubkey == _keyPair?.PublicKeyHex)
            {
                string successMsg = $"Score published successfully! Event ID: {ev.Id}";
                UpdateEnterKeyStatus(successMsg);
                UpdateGenerateKeyStatus(successMsg);
                
                // Reset the last published event ID
                _lastPublishedEventId = null;
            }
        }

        #endregion
    }
} 