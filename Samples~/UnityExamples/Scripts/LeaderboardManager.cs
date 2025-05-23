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
    /// Example demonstrating how to implement a high score leaderboard using Nostr
    /// </summary>
    public class LeaderboardManager : MonoBehaviour
    {
        [Header("Main UI")]
        [SerializeField] private TextMeshProUGUI highScoreText;
        [SerializeField] private Button playGameButton;
        [SerializeField] private Button enterLeaderboardKeyButton;
        [SerializeField] private Button generateLeaderboardKeyButton;
        
        [Header("Game Over Panel")]
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private TMP_InputField initialsInputField;
        [SerializeField] private Button postScoreButton;
        [SerializeField] private TextMeshProUGUI gameOverStatusText;
        
        [Header("Enter Key Panel")]
        [SerializeField] private GameObject contentPanelNostrKeyInput;
        [SerializeField] private TMP_InputField keyInputField;
        [SerializeField] private Button validateKeyButton;
        [SerializeField] private TextMeshProUGUI keyInputStatusText;
        
        [Header("Generate Key Panel")]
        [SerializeField] private GameObject contentPanelNostrKeyGeneration;
        [SerializeField] private Button generateKeyButton;
        [SerializeField] private TMP_InputField privateKeyOutput;
        [SerializeField] private TMP_InputField publicKeyOutput;
        [SerializeField] private TextMeshProUGUI keyGenerationStatusText;
        
        [Header("Game Settings")]
        [Tooltip("Minimum possible score when playing")]
        [SerializeField] private int minScore = 100;
        [Tooltip("Maximum possible score when playing")]
        [SerializeField] private int maxScore = 1000;
        [Tooltip("Character limit for player initials")]
        [SerializeField] private int initialsCharLimit = 3;
        
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
        private int _currentScore = 0;

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
            // Set up initial UI state
            UpdateHighScoreDisplay(0);
            
            // Hide panels initially
            contentPanelNostrKeyInput.SetActive(false);
            contentPanelNostrKeyGeneration.SetActive(false);
            gameOverPanel.SetActive(false);
            
            // Set read-only fields
            privateKeyOutput.readOnly = true;
            publicKeyOutput.readOnly = true;
            
            // Set initials input limit
            initialsInputField.characterLimit = initialsCharLimit;
            
            // Disable post score button until game is played
            postScoreButton.interactable = false;
            
            // Add button listeners
            playGameButton.onClick.AddListener(PlayGame);
            enterLeaderboardKeyButton.onClick.AddListener(ShowKeyInputPanel);
            generateLeaderboardKeyButton.onClick.AddListener(ShowKeyGenerationPanel);
            validateKeyButton.onClick.AddListener(ValidateKey);
            generateKeyButton.onClick.AddListener(GenerateKey);
            postScoreButton.onClick.AddListener(PostScore);
            
            // Set initial status messages
            UpdateKeyInputStatus("Enter your Nostr secret key (nsec)");
            UpdateKeyGenerationStatus("Click 'Generate Key' to create a new Nostr key pair");
            UpdateGameOverStatus("Game over! Enter your initials to post your score.");
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
            playGameButton.onClick.RemoveListener(PlayGame);
            enterLeaderboardKeyButton.onClick.RemoveListener(ShowKeyInputPanel);
            generateLeaderboardKeyButton.onClick.RemoveListener(ShowKeyGenerationPanel);
            validateKeyButton.onClick.RemoveListener(ValidateKey);
            generateKeyButton.onClick.RemoveListener(GenerateKey);
            postScoreButton.onClick.RemoveListener(PostScore);
        }

        #region UI Management

        /// <summary>
        /// Updates the high score display
        /// </summary>
        private void UpdateHighScoreDisplay(int score)
        {
            _currentHighScore = score;
            highScoreText.text = $"High Score: {_currentHighScore}";
        }

        /// <summary>
        /// Shows the key input panel
        /// </summary>
        private void ShowKeyInputPanel()
        {
            contentPanelNostrKeyInput.SetActive(true);
            contentPanelNostrKeyGeneration.SetActive(false);
            gameOverPanel.SetActive(false);
            UpdateKeyInputStatus("Enter your Nostr secret key (nsec)");
        }

        /// <summary>
        /// Shows the key generation panel
        /// </summary>
        private void ShowKeyGenerationPanel()
        {
            contentPanelNostrKeyInput.SetActive(false);
            contentPanelNostrKeyGeneration.SetActive(true);
            gameOverPanel.SetActive(false);
            UpdateKeyGenerationStatus("Click 'Generate Key' to create a new Nostr key pair");
        }

        /// <summary>
        /// Shows the game over panel
        /// </summary>
        private void ShowGameOverPanel()
        {
            contentPanelNostrKeyInput.SetActive(false);
            contentPanelNostrKeyGeneration.SetActive(false);
            gameOverPanel.SetActive(true);
            
            // Clear previous input and focus the field
            initialsInputField.text = "";
            initialsInputField.Select();
            
            UpdateGameOverStatus($"Game over! You scored {_currentScore} points! Enter your initials.");
        }

        /// <summary>
        /// Updates the key input panel status text
        /// </summary>
        private void UpdateKeyInputStatus(string message)
        {
            keyInputStatusText.text = "Status: " + message;
            Debug.Log($"[LeaderboardManager] {message}");
        }

        /// <summary>
        /// Updates the key generation panel status text
        /// </summary>
        private void UpdateKeyGenerationStatus(string message)
        {
            keyGenerationStatusText.text = "Status: " + message;
            Debug.Log($"[LeaderboardManager] {message}");
        }

        /// <summary>
        /// Updates the game over panel status text
        /// </summary>
        private void UpdateGameOverStatus(string message)
        {
            gameOverStatusText.text = "Status: " + message;
            Debug.Log($"[LeaderboardManager] {message}");
        }

        #endregion

        #region Key Management

        /// <summary>
        /// Validates the entered secret key
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
                
                // Return to main screen
                contentPanelNostrKeyInput.SetActive(false);
                
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
        /// Generates a new Nostr key pair for the leaderboard
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
                
                // Show feedback
                UpdateKeyGenerationStatus($"New key pair generated successfully!\nPublic key: {_keyPair.Npub}\nConnecting to relays...");
                
                // Optional: Log to console (be careful with private keys in logs)
                Debug.Log($"[LeaderboardManager] Generated new leaderboard key with public key: {_keyPair.Npub}");
            }
            catch (Exception ex)
            {
                UpdateKeyGenerationStatus($"Error generating key pair: {ex.Message}");
                Debug.LogError($"Key generation error: {ex.Message}");
            }
        }

        #endregion

        #region Game Functions

        /// <summary>
        /// Starts a new game session
        /// </summary>
        public void PlayGame()
        {
            // For this demo, we'll just generate a random score
            _currentScore = UnityEngine.Random.Range(minScore, maxScore + 1);
            
            // Check if this is a new high score
            if (_currentScore > _currentHighScore)
            {
                UpdateHighScoreDisplay(_currentScore);
            }
            
            // Show game over screen to enter initials
            ShowGameOverPanel();
            
            // Enable post score button if we have a valid key and relays
            postScoreButton.interactable = _isKeyValid && _connectedRelays > 0;
            
            Debug.Log($"[LeaderboardManager] Game played with score: {_currentScore}");
        }

        /// <summary>
        /// Posts the current score to the leaderboard
        /// </summary>
        public void PostScore()
        {
            if (!_isKeyValid || _keyPair == null)
            {
                UpdateGameOverStatus("Error: No leaderboard key configured. Please set up a key first.");
                return;
            }
            
            if (_connectedRelays == 0)
            {
                UpdateGameOverStatus("Error: No connected relays. Please wait for relay connections.");
                return;
            }
            
            string initials = initialsInputField.text.Trim().ToUpper();
            if (string.IsNullOrEmpty(initials))
            {
                UpdateGameOverStatus("Error: Please enter your initials");
                return;
            }
            
            // Publish the score
            PublishScore(initials, _currentScore);
        }

        /// <summary>
        /// Publishes a score to Nostr
        /// </summary>
        private void PublishScore(string initials, int score)
        {
            try
            {
                // Create the content with the score in a nice format
                string content = $"SCORE: {score} - {initials}";
                
                // Add tags for better discoverability and filtering
                string[][] tags = new string[][]
                {
                    new string[] { "t", "gamescore" },  // 't' tag for topic/category
                    new string[] { "score", score.ToString() },  // Custom tag for score value
                    new string[] { "player", initials }  // Custom tag for player initials
                };
                
                // Create a text note event (kind 1)
                NostrEvent scoreEvent = new NostrEvent(
                    _keyPair.PublicKeyHex, 
                    (int)NostrEventKind.TextNote, 
                    content,
                    tags
                );
                
                // Sign the event with our private key
                scoreEvent.Sign(_keyPair.PrivateKeyHex);
                
                // Verify the signature locally
                bool isValid = scoreEvent.VerifySignature();
                if (!isValid)
                {
                    UpdateGameOverStatus("Error: Failed to create a valid signature for the score");
                    return;
                }
                
                // Store the event ID so we can identify it when the relay sends it back
                _lastPublishedEventId = scoreEvent.Id;
                
                // Publish to all connected relays
                UpdateGameOverStatus("Publishing score...");
                _relayManager.PublishEvent(scoreEvent);
            }
            catch (Exception ex)
            {
                UpdateGameOverStatus($"Error publishing score: {ex.Message}");
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
                    UpdateGameOverStatus(connectedMsg);
                    
                    // Enable post button if we're on the game over screen and have a valid key
                    if (gameOverPanel.activeSelf && _isKeyValid)
                    {
                        postScoreButton.interactable = true;
                    }
                    break;
                    
                case RelayState.Disconnected:
                    _connectedRelays = Math.Max(0, _connectedRelays - 1);
                    string disconnectedMsg = $"Disconnected from {relayUrl}. {_connectedRelays} relays connected.";
                    UpdateKeyInputStatus(disconnectedMsg);
                    UpdateKeyGenerationStatus(disconnectedMsg);
                    UpdateGameOverStatus(disconnectedMsg);
                    
                    // Disable post button if no relays connected
                    if (_connectedRelays <= 0)
                    {
                        postScoreButton.interactable = false;
                    }
                    break;
                    
                case RelayState.Connecting:
                    string connectingMsg = $"Connecting to {relayUrl}...";
                    UpdateKeyInputStatus(connectingMsg);
                    UpdateKeyGenerationStatus(connectingMsg);
                    UpdateGameOverStatus(connectingMsg);
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
            UpdateGameOverStatus(errorMsg);
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
                UpdateGameOverStatus(successMsg);
                
                // Hide game over panel and go back to main screen after successful publishing
                gameOverPanel.SetActive(false);
                
                // Reset the last published event ID
                _lastPublishedEventId = null;
            }
            
            // For a more complete implementation, you could scan for other scores
            // posted to this same leaderboard pubkey and display a complete leaderboard
        }

        #endregion
    }
} 