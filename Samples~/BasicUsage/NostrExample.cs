using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NostrUnity;
using NostrUnity.Models;
using NostrUnity.Relay;

namespace NostrUnity.Examples
{
    public class NostrExample : MonoBehaviour
    {
        [Header("Relay Configuration")]
        [SerializeField] private List<string> _relayUrls = new List<string>
        {
            "wss://relay.damus.io",
            "wss://nos.lol",
            "wss://relay.nostr.band"
        };
        
        [Header("UI Elements")]
        [SerializeField] private TMP_InputField _privateKeyInput;
        [SerializeField] private TMP_InputField _messageInput;
        [SerializeField] private Button _generateKeyButton;
        [SerializeField] private Button _loadKeyButton;
        [SerializeField] private Button _sendMessageButton;
        [SerializeField] private Button _subscribeButton;
        [SerializeField] private Button _unsubscribeButton;
        [SerializeField] private TextMeshProUGUI _publicKeyText;
        [SerializeField] private TextMeshProUGUI _statusText;
        [SerializeField] private TextMeshProUGUI _eventsText;
        
        private NostrClient _nostrClient;
        private string _currentSubscriptionId;
        
        private void Start()
        {
            // Initialize NostrClient
            _nostrClient = gameObject.AddComponent<NostrClient>();
            
            // Set up event handlers
            _nostrClient.OnEventReceived += HandleEventReceived;
            _nostrClient.OnRelayStateChanged += HandleRelayStateChanged;
            
            // Add relays
            foreach (string relayUrl in _relayUrls)
            {
                _nostrClient.AddRelay(relayUrl);
            }
            
            // Set up UI button listeners
            _generateKeyButton.onClick.AddListener(GenerateNewKeyPair);
            _loadKeyButton.onClick.AddListener(LoadKeyPair);
            _sendMessageButton.onClick.AddListener(SendMessage);
            _subscribeButton.onClick.AddListener(Subscribe);
            _unsubscribeButton.onClick.AddListener(Unsubscribe);
            
            // Initial UI state
            UpdateUIState();
            _statusText.text = "Ready - Please load or generate a key pair";
        }
        
        private void OnDestroy()
        {
            // Clean up event handlers
            if (_nostrClient != null)
            {
                _nostrClient.OnEventReceived -= HandleEventReceived;
                _nostrClient.OnRelayStateChanged -= HandleRelayStateChanged;
            }
        }
        
        private void GenerateNewKeyPair()
        {
            _nostrClient.GenerateKeyPair();
            _privateKeyInput.text = "Generated new private key";
            _publicKeyText.text = $"Public Key (npub): {_nostrClient.GetPublicKeyBech32()}";
            _statusText.text = "Generated new key pair";
            UpdateUIState();
        }
        
        private void LoadKeyPair()
        {
            string privateKey = _privateKeyInput.text.Trim();
            if (string.IsNullOrEmpty(privateKey))
            {
                _statusText.text = "Error: Please enter a private key";
                return;
            }
            
            try
            {
                _nostrClient.LoadKeyPair(privateKey);
                _publicKeyText.text = $"Public Key (npub): {_nostrClient.GetPublicKeyBech32()}";
                _statusText.text = "Loaded key pair successfully";
                UpdateUIState();
            }
            catch (System.Exception ex)
            {
                _statusText.text = $"Error loading key pair: {ex.Message}";
            }
        }
        
        private void SendMessage()
        {
            string message = _messageInput.text.Trim();
            if (string.IsNullOrEmpty(message))
            {
                _statusText.text = "Error: Please enter a message";
                return;
            }
            
            try
            {
                NostrEvent event_ = _nostrClient.PublishTextNote(message);
                _statusText.text = $"Published message with ID: {event_.Id}";
                _messageInput.text = string.Empty;
            }
            catch (System.Exception ex)
            {
                _statusText.text = $"Error publishing message: {ex.Message}";
            }
        }
        
        private void Subscribe()
        {
            if (!_nostrClient.HasKeyPair)
            {
                _statusText.text = "Error: Please load or generate a key pair first";
                return;
            }
            
            try
            {
                // Subscribe to our own notes
                string pubKey = _nostrClient.GetPublicKeyHex();
                _currentSubscriptionId = _nostrClient.SubscribeToAuthorNotes(pubKey);
                _statusText.text = $"Subscribed to own notes with ID: {_currentSubscriptionId}";
                UpdateUIState();
            }
            catch (System.Exception ex)
            {
                _statusText.text = $"Error subscribing: {ex.Message}";
            }
        }
        
        private void Unsubscribe()
        {
            if (string.IsNullOrEmpty(_currentSubscriptionId))
            {
                _statusText.text = "Error: No active subscription";
                return;
            }
            
            try
            {
                _nostrClient.Unsubscribe(_currentSubscriptionId);
                _statusText.text = $"Unsubscribed from: {_currentSubscriptionId}";
                _currentSubscriptionId = null;
                UpdateUIState();
            }
            catch (System.Exception ex)
            {
                _statusText.text = $"Error unsubscribing: {ex.Message}";
            }
        }
        
        private void HandleEventReceived(NostrEvent nostrEvent, string relayUrl)
        {
            // Add the event to the UI
            string shortUrl = GetShortRelayUrl(relayUrl);
            string message = $"[{shortUrl}] {nostrEvent.Content}\n{_eventsText.text}";
            
            // Limit the number of displayed messages to prevent UI overflow
            if (message.Length > 2000)
            {
                message = message.Substring(0, 2000) + "...";
            }
            
            // Update UI on the main thread
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                _eventsText.text = message;
            });
        }
        
        private void HandleRelayStateChanged(string relayUrl, RelayState state)
        {
            string shortUrl = GetShortRelayUrl(relayUrl);
            
            // Update UI on the main thread
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                _statusText.text = $"Relay {shortUrl} is now {state}";
            });
        }
        
        private string GetShortRelayUrl(string relayUrl)
        {
            // Extract hostname from relay URL
            System.Uri uri = new System.Uri(relayUrl);
            return uri.Host;
        }
        
        private void UpdateUIState()
        {
            bool hasKeyPair = _nostrClient.HasKeyPair;
            bool hasSubscription = !string.IsNullOrEmpty(_currentSubscriptionId);
            
            _sendMessageButton.interactable = hasKeyPair;
            _subscribeButton.interactable = hasKeyPair && !hasSubscription;
            _unsubscribeButton.interactable = hasSubscription;
        }
    }

    /// <summary>
    /// Helper class to dispatch actions to the Unity main thread
    /// </summary>
    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static UnityMainThreadDispatcher _instance;
        private readonly Queue<System.Action> _actionQueue = new Queue<System.Action>();
        
        public static UnityMainThreadDispatcher Instance()
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("UnityMainThreadDispatcher");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<UnityMainThreadDispatcher>();
            }
            return _instance;
        }
        
        public void Enqueue(System.Action action)
        {
            lock (_actionQueue)
            {
                _actionQueue.Enqueue(action);
            }
        }
        
        private void Update()
        {
            lock (_actionQueue)
            {
                while (_actionQueue.Count > 0)
                {
                    System.Action action = _actionQueue.Dequeue();
                    action();
                }
            }
        }
    }
} 