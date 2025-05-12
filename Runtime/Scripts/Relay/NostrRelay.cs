using System;
using System.Collections.Generic;
using UnityEngine;
using NostrUnity.Models;
using System.Threading.Tasks;
using NostrUnity.Utils;
using System.Text.Json;

namespace NostrUnity.Relay
{
    /// <summary>
    /// Enumeration of possible relay states
    /// </summary>
    public enum RelayState
    {
        Disconnected,
        Connecting,
        Connected,
        Failed
    }
    
    /// <summary>
    /// Represents a connection to a Nostr relay
    /// </summary>
    public class NostrRelay
    {
        private readonly NostrWebSocket _webSocket;
        private readonly Dictionary<string, Action<NostrEvent>> _subscriptions = new Dictionary<string, Action<NostrEvent>>();
        private readonly string _url;
        private RelayState _state = RelayState.Disconnected;
        
        /// <summary>
        /// Gets the URL of this relay
        /// </summary>
        public string Url => _url;
        
        /// <summary>
        /// Gets the current state of the relay connection
        /// </summary>
        public RelayState State => _state;
        
        /// <summary>
        /// Event triggered when the relay connection state changes
        /// </summary>
        public event Action<RelayState> OnStateChanged;
        
        /// <summary>
        /// Event triggered when an event is received from the relay
        /// </summary>
        public event Action<NostrEvent> OnEventReceived;
        
        /// <summary>
        /// Event triggered when an error occurs
        /// </summary>
        public event Action<string> OnError;
        
        /// <summary>
        /// Creates a new Nostr relay connection
        /// </summary>
        /// <param name="url">The WebSocket URL of the relay (wss://...)</param>
        public NostrRelay(string url)
        {
            if (string.IsNullOrEmpty(url))
                throw new ArgumentException("Relay URL cannot be null or empty", nameof(url));
            
            if (!url.StartsWith("wss://") && !url.StartsWith("ws://"))
                throw new ArgumentException("Relay URL must start with ws:// or wss://", nameof(url));
            
            _url = url;
            _webSocket = new NostrWebSocket(url);
            
            // Set up WebSocket event handlers
            _webSocket.OnConnected += HandleConnected;
            _webSocket.OnDisconnected += HandleDisconnected;
            _webSocket.OnError += HandleError;
            _webSocket.OnMessageReceived += HandleMessage;
        }
        
        /// <summary>
        /// Connects to the relay
        /// </summary>
        public async Task ConnectAsync()
        {
            if (_state == RelayState.Connected || _state == RelayState.Connecting)
                return;
            
            try
            {
                SetState(RelayState.Connecting);
                _webSocket.Connect();
                await Task.CompletedTask; // For async compatibility
            }
            catch (Exception ex)
            {
                NostrErrorHandler.HandleError(ex, $"Relay.Connect ({_url})", NostrErrorHandler.NostrErrorSeverity.Error);
                SetState(RelayState.Failed);
                throw;
            }
        }
        
        /// <summary>
        /// Disconnects from the relay
        /// </summary>
        public async Task DisconnectAsync()
        {
            if (_state == RelayState.Disconnected)
                return;
            
            try
            {
                _webSocket.Disconnect();
                SetState(RelayState.Disconnected);
                await Task.CompletedTask; // For async compatibility
            }
            catch (Exception ex)
            {
                NostrErrorHandler.HandleError(ex, $"Relay.Disconnect ({_url})", NostrErrorHandler.NostrErrorSeverity.Warning);
                SetState(RelayState.Failed);
            }
        }
        
        /// <summary>
        /// Updates the WebSocket connection (should be called from MonoBehaviour.Update)
        /// </summary>
        public void Update()
        {
            _webSocket.Update();
        }
        
        /// <summary>
        /// Publishes an event to the relay
        /// </summary>
        /// <param name="nostrEvent">The Nostr event to publish</param>
        public async Task PublishEventAsync(NostrEvent nostrEvent)
        {
            if (_state != RelayState.Connected)
            {
                Debug.LogWarning($"Attempting to publish event while relay {_url} is not connected. Current state: {_state}");
                // We'll still try to send - the WebSocket will queue it
            }
            
            if (string.IsNullOrEmpty(nostrEvent.Sig))
            {
                throw new InvalidOperationException("Cannot publish an unsigned event. Please sign the event first.");
            }
            
            try
            {
                // First, serialize the event object properly using the serializer
                string eventJson = NostrUnity.Protocol.NostrSerializer.SerializeComplete(nostrEvent);
                
                // Create the EVENT message array with the event as a proper JSON object, not a string
                string message = $"[\"EVENT\",{eventJson}]";
                
                Debug.Log($"Publishing event {nostrEvent.Id} to {_url}: {message}");
                _webSocket.SendMessage(message);
                await Task.CompletedTask; // For async compatibility
            }
            catch (Exception ex)
            {
                NostrErrorHandler.HandleError(ex, $"Relay.PublishEvent ({_url})", NostrErrorHandler.NostrErrorSeverity.Error);
                throw;
            }
        }
        
        /// <summary>
        /// Subscribes to events matching the specified filter
        /// </summary>
        /// <param name="subscriptionId">Unique ID for this subscription</param>
        /// <param name="filter">The filter to apply</param>
        /// <param name="callback">Optional callback for events matching this subscription</param>
        public async Task SubscribeAsync(string subscriptionId, Filter filter, Action<NostrEvent> callback = null)
        {
            if (string.IsNullOrEmpty(subscriptionId))
                throw new ArgumentException("Subscription ID cannot be null or empty", nameof(subscriptionId));
            
            if (filter == null)
                throw new ArgumentNullException(nameof(filter), "Filter cannot be null");
            
            // Add callback to subscriptions dictionary if provided
            if (callback != null)
            {
                if (_subscriptions.ContainsKey(subscriptionId))
                {
                    _subscriptions[subscriptionId] = callback;
                }
                else
                {
                    _subscriptions.Add(subscriptionId, callback);
                }
            }
            
            // Prepare the subscription message
            string filterJson = JsonSerializer.Serialize(filter);
            string message = JsonSerializer.Serialize(new object[] { "REQ", subscriptionId, filterJson });
            
            _webSocket.SendMessage(message);
            await Task.CompletedTask; // For async compatibility
        }
        
        /// <summary>
        /// Unsubscribes from a previous subscription
        /// </summary>
        /// <param name="subscriptionId">The ID of the subscription to cancel</param>
        public async Task UnsubscribeAsync(string subscriptionId)
        {
            if (string.IsNullOrEmpty(subscriptionId))
                throw new ArgumentException("Subscription ID cannot be null or empty", nameof(subscriptionId));
            
            // Remove from subscriptions dictionary
            _subscriptions.Remove(subscriptionId);
            
            // Send unsubscribe message
            string message = JsonSerializer.Serialize(new object[] { "CLOSE", subscriptionId });
            _webSocket.SendMessage(message);
            await Task.CompletedTask; // For async compatibility
        }
        
        /// <summary>
        /// Connects to the relay (non-async version)
        /// </summary>
        public void Connect()
        {
            // Call the async method without waiting
            _ = ConnectAsync();
        }
        
        /// <summary>
        /// Disconnects from the relay (non-async version)
        /// </summary>
        public void Disconnect()
        {
            // Call the async method without waiting
            _ = DisconnectAsync();
        }
        
        /// <summary>
        /// Subscribes to events matching the specified filter (non-async version)
        /// </summary>
        /// <param name="subscriptionId">Unique ID for this subscription</param>
        /// <param name="filter">The filter to apply</param>
        /// <param name="callback">Optional callback for events matching this subscription</param>
        public void Subscribe(string subscriptionId, Filter filter, Action<NostrEvent> callback = null)
        {
            // Call the async method without waiting
            _ = SubscribeAsync(subscriptionId, filter, callback);
        }
        
        /// <summary>
        /// Unsubscribes from a previous subscription (non-async version)
        /// </summary>
        /// <param name="subscriptionId">The ID of the subscription to cancel</param>
        public void Unsubscribe(string subscriptionId)
        {
            // Call the async method without waiting
            _ = UnsubscribeAsync(subscriptionId);
        }
        
        /// <summary>
        /// Publishes an event to the relay (non-async version)
        /// </summary>
        /// <param name="nostrEvent">The Nostr event to publish</param>
        public void PublishEvent(NostrEvent nostrEvent)
        {
            // Call the async method without waiting
            _ = PublishEventAsync(nostrEvent);
        }
        
        #region Event Handlers
        
        private void HandleConnected()
        {
            SetState(RelayState.Connected);
        }
        
        private void HandleDisconnected(string reason)
        {
            SetState(RelayState.Disconnected);
            Debug.Log($"Disconnected from relay {_url}: {reason}");
        }
        
        private void HandleError(string errorMsg)
        {
            SetState(RelayState.Failed);
            OnError?.Invoke(errorMsg);
        }
        
        private void HandleMessage(string message)
        {
            try
            {
                // Parse the message which should be a JSON array
                using var jsonDoc = JsonDocument.Parse(message);
                var root = jsonDoc.RootElement;
                
                if (root.GetArrayLength() < 2)
                {
                    Debug.LogWarning($"Invalid message format from relay {_url}: {message}");
                    return;
                }
                
                string messageType = root[0].GetString();
                
                switch (messageType)
                {
                    case "EVENT":
                        HandleEventMessage(root);
                        break;
                        
                    case "EOSE":
                        // End of stored events
                        string subId = root[1].GetString();
                        Debug.Log($"End of stored events for subscription {subId}");
                        break;
                        
                    case "NOTICE":
                        string notice = root[1].GetString();
                        Debug.Log($"Notice from relay {_url}: {notice}");
                        break;
                        
                    case "OK":
                        // Event publish response
                        if (root.GetArrayLength() >= 3)
                        {
                            string eventId = root[1].GetString();
                            bool success = root[2].GetBoolean();
                            string resultMessage = root.GetArrayLength() > 3 ? root[3].GetString() : string.Empty;
                            
                            Debug.Log($"Publish result for event {eventId}: {(success ? "Success" : "Failed")} {resultMessage}");
                        }
                        break;
                        
                    default:
                        Debug.LogWarning($"Unknown message type from relay {_url}: {messageType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error processing message from relay {_url}: {ex.Message}");
                OnError?.Invoke($"Message processing error: {ex.Message}");
            }
        }
        
        private void HandleEventMessage(JsonElement root)
        {
            try
            {
                if (root.GetArrayLength() < 3)
                {
                    Debug.LogWarning("Invalid EVENT message format: insufficient elements");
                    return;
                }
                
                string subscriptionId = root[1].GetString();
                string eventJson = root[2].GetRawText();
                
                // Parse the event JSON
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                NostrEvent nostrEvent = JsonSerializer.Deserialize<NostrEvent>(eventJson, options);
                
                // Invoke the subscription callback if one exists
                if (_subscriptions.TryGetValue(subscriptionId, out Action<NostrEvent> callback))
                {
                    callback?.Invoke(nostrEvent);
                }
                
                // Also invoke the general event handler
                OnEventReceived?.Invoke(nostrEvent);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error handling EVENT message: {ex.Message}");
            }
        }
        
        private void SetState(RelayState newState)
        {
            if (_state == newState)
                return;
            
            _state = newState;
            OnStateChanged?.Invoke(_state);
        }
        
        #endregion
    }
} 