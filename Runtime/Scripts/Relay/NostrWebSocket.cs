using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using NostrUnity.Utils;

namespace NostrUnity.Relay
{
    /// <summary>
    /// Handles WebSocket connections to Nostr relays
    /// </summary>
    internal class NostrWebSocket
    {
        private WebSocket _webSocket;
        private readonly string _relayUrl;
        private readonly Queue<string> _messageQueue = new Queue<string>();
        private bool _isConnected;
        private bool _isConnecting;
        private bool _autoReconnect = true;
        private float _reconnectDelay = 2f;
        private int _reconnectAttempts = 0;
        private const int MaxReconnectAttempts = 5;
        
        // Rate limiting
        private RateLimiter _rateLimiter;
        private const int DefaultMaxMessagesPerInterval = 10;
        private const float DefaultIntervalSeconds = 1.0f;

        // Events
        public event Action<string> OnMessageReceived;
        public event Action OnConnected;
        public event Action<string> OnDisconnected;
        public event Action<string> OnError;

        /// <summary>
        /// Creates a new WebSocket client for a specific relay
        /// </summary>
        /// <param name="relayUrl">The URL of the relay (wss://...)</param>
        /// <param name="maxMessagesPerInterval">Maximum number of messages allowed per interval (default 10)</param>
        /// <param name="intervalSeconds">The interval length in seconds (default 1 second)</param>
        public NostrWebSocket(string relayUrl, int maxMessagesPerInterval = DefaultMaxMessagesPerInterval, float intervalSeconds = DefaultIntervalSeconds)
        {
            _relayUrl = relayUrl;
            _rateLimiter = new RateLimiter(maxMessagesPerInterval, intervalSeconds);
            
            // Register with CoroutineRunner for application quit handling
            CoroutineRunner.RegisterWebSocket(this);
        }

        /// <summary>
        /// Establishes connection to the relay
        /// </summary>
        public async void Connect()
        {
            if (_isConnected || _isConnecting)
                return;

            _isConnecting = true;

            try
            {
                // Create WebSocket instance with standard subprotocols for Nostr
                _webSocket = new WebSocket(_relayUrl);

                // Set up event handlers
                _webSocket.OnOpen += HandleOpen;
                _webSocket.OnMessage += HandleMessage;
                _webSocket.OnClose += HandleClose;
                _webSocket.OnError += HandleError;

                // Connect to the WebSocket server
                await _webSocket.Connect();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to connect to relay {_relayUrl}: {ex.Message}");
                _isConnecting = false;
                OnError?.Invoke($"Connection error: {ex.Message}");
            }
        }

        /// <summary>
        /// Disconnects from the relay
        /// </summary>
        public async void Disconnect()
        {
            _autoReconnect = false;
            
            if (_webSocket != null && (_isConnected || _isConnecting))
            {
                try
                {
                    await _webSocket.Close();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error disconnecting from relay {_relayUrl}: {ex.Message}");
                }
            }
            
            _isConnected = false;
            _isConnecting = false;
            
            // Unregister from CoroutineRunner
            CoroutineRunner.UnregisterWebSocket(this);
        }

        /// <summary>
        /// Sends a message to the relay with rate limiting
        /// </summary>
        /// <param name="message">JSON message to send</param>
        public void SendMessage(string message)
        {
            if (!_isConnected)
            {
                // Queue message if not connected
                _messageQueue.Enqueue(message);
                
                if (!_isConnecting)
                {
                    Connect();
                }
                
                return;
            }

            // Use rate limiter to send or queue the message
            _rateLimiter.TrySend(message, SendMessageInternal);
        }

        /// <summary>
        /// Internal method to actually send a message
        /// </summary>
        /// <param name="message">The message to send</param>
        private async void SendMessageInternal(string message)
        {
            if (!_isConnected)
            {
                // Queue message if not connected
                _messageQueue.Enqueue(message);
                return;
            }

            try
            {
                await _webSocket.SendText(message);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error sending message to relay {_relayUrl}: {ex.Message}");
                OnError?.Invoke($"Send error: {ex.Message}");
                
                // Queue the message to try again
                _messageQueue.Enqueue(message);
                
                // Connection might be broken, try to reconnect
                if (_autoReconnect)
                {
                    _isConnected = false;
                    Connect();
                }
            }
        }

        /// <summary>
        /// Updates the WebSocket client (should be called from MonoBehaviour.Update)
        /// </summary>
        public void Update()
        {
            if (_webSocket != null)
            {
                #if !UNITY_WEBGL || UNITY_EDITOR
                _webSocket.DispatchMessageQueue();
                #endif
                
                // Update rate limiter to process any queued messages
                _rateLimiter.Update();
                
                // Process connection queue if connected and rate limiter has no queued messages
                if (_isConnected && _messageQueue.Count > 0 && !_rateLimiter.HasQueuedMessages)
                {
                    string message = _messageQueue.Dequeue();
                    SendMessage(message);
                }
            }
        }

        #region Event Handlers

        private void HandleOpen()
        {
            Debug.Log($"Connected to relay: {_relayUrl}");
            _isConnected = true;
            _isConnecting = false;
            _reconnectAttempts = 0;
            OnConnected?.Invoke();
            
            // Send any queued messages
            while (_isConnected && _messageQueue.Count > 0)
            {
                string message = _messageQueue.Dequeue();
                SendMessage(message);
            }
        }

        private void HandleMessage(byte[] data)
        {
            string message = Encoding.UTF8.GetString(data);
            Debug.Log($"Received from {_relayUrl}: {message}");
            OnMessageReceived?.Invoke(message);
        }

        private void HandleClose(WebSocketCloseCode closeCode)
        {
            // Prevent duplicate close handling
            if (!_isConnected && !_isConnecting)
                return;
                
            string reason = $"WebSocket closed with code: {closeCode}";
            Debug.Log($"Disconnected from relay {_relayUrl}: {reason}");
            
            _isConnected = false;
            _isConnecting = false;
            
            OnDisconnected?.Invoke(reason);
            
            // Only attempt to reconnect if auto-reconnect is enabled and we haven't exceeded attempts
            // Also don't reconnect during application shutdown
            if (_autoReconnect && _reconnectAttempts < MaxReconnectAttempts && Application.isPlaying)
            {
                _reconnectAttempts++;
                float delay = _reconnectDelay * _reconnectAttempts;
                Debug.Log($"Attempting to reconnect in {delay} seconds (attempt {_reconnectAttempts}/{MaxReconnectAttempts})");
                
                // Use MonoBehaviour-less coroutine to handle reconnection
                CoroutineRunner.Instance.StartCoroutine(ReconnectAfterDelay(delay));
            }
        }

        private void HandleError(string errorMsg)
        {
            Debug.LogError($"WebSocket error for {_relayUrl}: {errorMsg}");
            OnError?.Invoke(errorMsg);
        }

        private IEnumerator ReconnectAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            
            // Double-check that we should still reconnect
            if (!_isConnected && !_isConnecting && _autoReconnect && Application.isPlaying)
            {
                Debug.Log($"Attempting to reconnect to {_relayUrl}");
                Connect();
            }
        }

        #endregion

        /// <summary>
        /// Cleans up WebSocket resources when the application quits
        /// </summary>
        public void CleanupOnQuit()
        {
            // Disable auto-reconnect first to prevent any reconnection attempts
            _autoReconnect = false;
            
            // Clear any queued messages
            _messageQueue.Clear();
            
            if (_webSocket != null && (_isConnected || _isConnecting))
            {
                // Set disconnecting state to prevent event handling
                _isConnecting = false;
                _isConnected = false;
                
                try
                {
                    // Use synchronous close to ensure it happens during application quit
                    _webSocket.CloseSync();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Error during WebSocket cleanup for {_relayUrl}: {ex.Message}");
                }
            }
        }
    }
    
    /// <summary>
    /// Helper class to run coroutines without a MonoBehaviour
    /// </summary>
    internal class CoroutineRunner : MonoBehaviour
    {
        private static CoroutineRunner _instance;
        private static List<NostrWebSocket> _activeWebSockets = new List<NostrWebSocket>();
        
        public static CoroutineRunner Instance
        {
            get
            {
                if (_instance == null)
                {
                    // Create a new GameObject with the CoroutineRunner
                    var go = new GameObject("NostrCoroutineRunner");
                    _instance = go.AddComponent<CoroutineRunner>();
                    DontDestroyOnLoad(go);
                }
                
                return _instance;
            }
        }
        
        /// <summary>
        /// Registers a WebSocket with the CoroutineRunner for application quit handling
        /// </summary>
        /// <param name="webSocket">The WebSocket to register</param>
        public static void RegisterWebSocket(NostrWebSocket webSocket)
        {
            if (!_activeWebSockets.Contains(webSocket))
            {
                _activeWebSockets.Add(webSocket);
            }
        }
        
        /// <summary>
        /// Unregisters a WebSocket from the CoroutineRunner
        /// </summary>
        /// <param name="webSocket">The WebSocket to unregister</param>
        public static void UnregisterWebSocket(NostrWebSocket webSocket)
        {
            _activeWebSockets.Remove(webSocket);
        }
        
        private void OnApplicationQuit()
        {
            Debug.Log("NostrCoroutineRunner: Application quit detected, cleaning up WebSockets");
            CleanupAllWebSockets();
        }
        
        private void OnApplicationPause(bool pauseStatus)
        {
            // When the application is paused (e.g., ALT+TAB, minimized), we should also cleanup
            if (pauseStatus)
            {
                Debug.Log("NostrCoroutineRunner: Application paused, cleaning up WebSockets");
                CleanupAllWebSockets();
            }
        }
        
        private void OnApplicationFocus(bool hasFocus)
        {
            // When the application loses focus, cleanup WebSockets to prevent connection issues
            if (!hasFocus)
            {
                Debug.Log("NostrCoroutineRunner: Application focus lost, cleaning up WebSockets");
                CleanupAllWebSockets();
            }
        }
        
        private void CleanupAllWebSockets()
        {
            // Make a copy of the collection to avoid modification during enumeration
            var websockets = new List<NostrWebSocket>(_activeWebSockets);
            
            // Disable auto-reconnect and disconnect all active WebSockets
            foreach (var webSocket in websockets)
            {
                try
                {
                    webSocket.CleanupOnQuit();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Error during WebSocket cleanup: {ex.Message}");
                }
            }
            
            _activeWebSockets.Clear();
        }
    }
} 