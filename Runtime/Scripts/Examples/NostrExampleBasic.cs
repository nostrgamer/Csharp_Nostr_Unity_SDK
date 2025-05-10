using System;
using UnityEngine;
using UnityEngine.UI;
using NostrUnity.Models;
using NostrUnity.Crypto;
using NostrUnity.Relay;

namespace NostrUnity.Examples
{
    /// <summary>
    /// Basic example demonstrating Nostr client functionality
    /// </summary>
    public class NostrExampleBasic : MonoBehaviour
    {
        [Header("Relay Configuration")]
        [SerializeField] private string[] _relayUrls = new string[]
        {
            "wss://relay.damus.io",
            "wss://nos.lol",
            "wss://relay.nostr.band"
        };
        
        [Header("UI Elements")]
        [SerializeField] private InputField _privateKeyInput;
        [SerializeField] private InputField _messageInput;
        [SerializeField] private Button _loadKeyButton;
        [SerializeField] private Button _sendMessageButton;
        [SerializeField] private Text _statusText;
        [SerializeField] private Text _eventsText;
        
        private NostrClient _nostrClient;
        private KeyPair _keyPair;
        private string _activeSubscriptionId;
        
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
            _loadKeyButton.onClick.AddListener(LoadKeyPair);
            _sendMessageButton.onClick.AddListener(SendMessage);
            
            // Initial UI state
            UpdateUI(false);
            _statusText.text = "Please load a key pair";
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
                // Load the key pair
                _nostrClient.LoadKeyPair(privateKey);
                _statusText.text = $"Loaded key pair with public key: {_nostrClient.GetPublicKeyBech32()}";
                UpdateUI(true);
                
                // Subscribe to our own events
                SubscribeToOwnEvents();
            }
            catch (Exception ex)
            {
                _statusText.text = $"Error loading key pair: {ex.Message}";
            }
        }
        
        private void SendMessage()
        {
            string content = _messageInput.text.Trim();
            if (string.IsNullOrEmpty(content))
            {
                _statusText.text = "Error: Please enter a message";
                return;
            }
            
            try
            {
                NostrEvent evt = _nostrClient.PublishTextNote(content);
                _statusText.text = $"Published message with ID: {evt.Id}";
                _messageInput.text = "";
            }
            catch (Exception ex)
            {
                _statusText.text = $"Error sending message: {ex.Message}";
            }
        }
        
        private void SubscribeToOwnEvents()
        {
            try
            {
                string pubKey = _nostrClient.GetPublicKeyHex();
                _activeSubscriptionId = _nostrClient.SubscribeToAuthorNotes(pubKey);
                _statusText.text += $"\nSubscribed to own events with ID: {_activeSubscriptionId}";
            }
            catch (Exception ex)
            {
                _statusText.text += $"\nError subscribing to events: {ex.Message}";
            }
        }
        
        private void HandleEventReceived(NostrEvent evt, string relayUrl)
        {
            // Add the event to the UI on the main thread
            UnityMainThreadHelper.QueueOnMainThread(() => 
            {
                string formattedTime = DateTime.UnixEpoch.AddSeconds(evt.CreatedAt).ToString("HH:mm:ss");
                string content = evt.Content;
                
                // Limit the size of the displayed content
                if (content.Length > 100)
                {
                    content = content.Substring(0, 100) + "...";
                }
                
                string newEvent = $"[{formattedTime}] {content}\n";
                _eventsText.text = newEvent + _eventsText.text;
                
                // Limit the total size of the text to avoid UI performance issues
                if (_eventsText.text.Length > 2000)
                {
                    _eventsText.text = _eventsText.text.Substring(0, 2000);
                }
            });
        }
        
        private void HandleRelayStateChanged(string relayUrl, RelayState state)
        {
            // Update the status text on the main thread
            UnityMainThreadHelper.QueueOnMainThread(() => 
            {
                // Extract just the host from the URL for cleaner display
                Uri uri = new Uri(relayUrl);
                string host = uri.Host;
                
                _statusText.text = $"Relay {host} is now {state}";
            });
        }
        
        private void UpdateUI(bool keyLoaded)
        {
            _sendMessageButton.interactable = keyLoaded;
            _messageInput.interactable = keyLoaded;
        }
    }
    
    /// <summary>
    /// Helper class for running code on the main thread
    /// </summary>
    public static class UnityMainThreadHelper
    {
        private static readonly object _lock = new object();
        private static System.Collections.Generic.Queue<Action> _actions = new System.Collections.Generic.Queue<Action>();
        
        public static void QueueOnMainThread(Action action)
        {
            if (action == null)
                return;
                
            lock (_lock)
            {
                _actions.Enqueue(action);
            }
        }
        
        public static void ExecuteQueuedActions()
        {
            lock (_lock)
            {
                while (_actions.Count > 0)
                {
                    Action action = _actions.Dequeue();
                    action();
                }
            }
        }
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            // Create a persistent GameObject to run the update loop
            GameObject go = new GameObject("UnityMainThreadHelper");
            go.AddComponent<UnityMainThreadDispatcher>();
            UnityEngine.Object.DontDestroyOnLoad(go);
        }
        
        private class UnityMainThreadDispatcher : MonoBehaviour
        {
            private void Update()
            {
                ExecuteQueuedActions();
            }
        }
    }
} 