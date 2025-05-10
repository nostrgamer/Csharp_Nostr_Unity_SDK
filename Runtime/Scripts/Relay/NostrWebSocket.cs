using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

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

        // Events
        public event Action<string> OnMessageReceived;
        public event Action OnConnected;
        public event Action<string> OnDisconnected;
        public event Action<string> OnError;

        /// <summary>
        /// Creates a new WebSocket client for a specific relay
        /// </summary>
        /// <param name="relayUrl">The URL of the relay (wss://...)</param>
        public NostrWebSocket(string relayUrl)
        {
            _relayUrl = relayUrl;
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
        }

        /// <summary>
        /// Sends a message to the relay
        /// </summary>
        /// <param name="message">JSON message to send</param>
        public async void SendMessage(string message)
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
                
                // Process queued messages if connected
                if (_isConnected && _messageQueue.Count > 0)
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
            string reason = $"WebSocket closed with code: {closeCode}";
            Debug.Log($"Disconnected from relay {_relayUrl}: {reason}");
            
            _isConnected = false;
            _isConnecting = false;
            
            OnDisconnected?.Invoke(reason);
            
            // Attempt to reconnect if auto-reconnect is enabled
            if (_autoReconnect && _reconnectAttempts < MaxReconnectAttempts)
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
            
            if (!_isConnected && !_isConnecting && _autoReconnect)
            {
                Debug.Log($"Attempting to reconnect to {_relayUrl}");
                Connect();
            }
        }

        #endregion
    }
    
    /// <summary>
    /// Helper class to run coroutines without a MonoBehaviour
    /// </summary>
    internal class CoroutineRunner : MonoBehaviour
    {
        private static CoroutineRunner _instance;
        
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
    }
} 