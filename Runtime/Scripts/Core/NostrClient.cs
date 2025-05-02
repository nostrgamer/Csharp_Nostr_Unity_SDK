using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nostr.Unity
{
    /// <summary>
    /// Main client for interacting with Nostr network
    /// </summary>
    public class NostrClient
    {
        private WebSocketClient _webSocketClient;
        private Dictionary<string, Filter> _subscriptions = new Dictionary<string, Filter>();
        private Dictionary<string, string> _relayConnections = new Dictionary<string, string>();
        
        /// <summary>
        /// Event triggered when a new event is received from a relay
        /// </summary>
        public event EventHandler<NostrEventArgs> EventReceived;
        
        /// <summary>
        /// Event triggered when the client connects to a relay
        /// </summary>
        public event EventHandler<string> Connected;
        
        /// <summary>
        /// Event triggered when the client disconnects from a relay
        /// </summary>
        public event EventHandler<string> Disconnected;
        
        /// <summary>
        /// Event triggered when an error occurs
        /// </summary>
        public event EventHandler<string> Error;

        /// <summary>
        /// Gets a value indicating whether the client is connected to any relay
        /// </summary>
        public bool IsConnected => _webSocketClient != null && _webSocketClient.IsAnyConnectionOpen;
        
        /// <summary>
        /// Gets the list of connected relay URLs
        /// </summary>
        public List<string> ConnectedRelays
        {
            get
            {
                List<string> relays = new List<string>();
                foreach (var kvp in _relayConnections)
                {
                    if (!string.IsNullOrEmpty(kvp.Value))
                    {
                        relays.Add(kvp.Value);
                    }
                }
                return relays;
            }
        }
        
        /// <summary>
        /// Initializes a new instance of the NostrClient class
        /// </summary>
        public NostrClient()
        {
            _webSocketClient = new WebSocketClient();
            _webSocketClient.MessageReceived += OnMessageReceived;
            _webSocketClient.Connected += OnConnected;
            _webSocketClient.Disconnected += OnDisconnected;
            _webSocketClient.Error += OnError;
        }
        
        /// <summary>
        /// Connects to a Nostr relay
        /// </summary>
        /// <param name="relayUrl">The URL of the relay to connect to</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task ConnectToRelay(string relayUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(relayUrl))
                {
                    throw new ArgumentException("Relay URL cannot be null or empty", nameof(relayUrl));
                }
                
                await _webSocketClient.Connect(relayUrl);
                _relayConnections[relayUrl] = relayUrl;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error connecting to relay {relayUrl}: {ex.Message}");
                OnError(this, $"Connection error: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Connects to multiple Nostr relays
        /// </summary>
        /// <param name="relayUrls">List of relay URLs to connect to</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task ConnectToRelays(List<string> relayUrls)
        {
            if (relayUrls == null || relayUrls.Count == 0)
            {
                throw new ArgumentException("Relay URL list cannot be null or empty", nameof(relayUrls));
            }
            
            List<Exception> exceptions = new List<Exception>();
            
            foreach (var relayUrl in relayUrls)
            {
                try
                {
                    await ConnectToRelay(relayUrl);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                    Debug.LogWarning($"Failed to connect to relay {relayUrl}: {ex.Message}");
                }
            }
            
            if (exceptions.Count > 0 && exceptions.Count == relayUrls.Count)
            {
                throw new AggregateException("Failed to connect to any relay", exceptions);
            }
        }
        
        /// <summary>
        /// Disconnects from all relays
        /// </summary>
        public void Disconnect()
        {
            try
            {
                _webSocketClient.DisconnectAll();
                _relayConnections.Clear();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error during disconnect: {ex.Message}");
                OnError(this, $"Disconnect error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Disconnects from a specific relay
        /// </summary>
        /// <param name="relayUrl">The URL of the relay to disconnect from</param>
        public void DisconnectFromRelay(string relayUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(relayUrl))
                {
                    throw new ArgumentException("Relay URL cannot be null or empty", nameof(relayUrl));
                }
                
                _webSocketClient.Disconnect(relayUrl);
                _relayConnections.Remove(relayUrl);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error disconnecting from relay {relayUrl}: {ex.Message}");
                OnError(this, $"Disconnect error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Publishes an event to connected relays
        /// </summary>
        /// <param name="nostrEvent">The event to publish</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task PublishEvent(NostrEvent nostrEvent)
        {
            try
            {
                if (nostrEvent == null)
                {
                    throw new ArgumentNullException(nameof(nostrEvent), "Event cannot be null");
                }
                
                // Validate the event
                if (string.IsNullOrEmpty(nostrEvent.Id))
                {
                    nostrEvent.Id = nostrEvent.ComputeId();
                }
                
                if (string.IsNullOrEmpty(nostrEvent.Signature))
                {
                    throw new InvalidOperationException("Event must be signed before publishing");
                }
                
                // Verify the signature before sending
                if (!nostrEvent.VerifySignature())
                {
                    throw new InvalidOperationException("Event has an invalid signature");
                }
                
                // Create the EVENT message: ["EVENT", <event JSON>]
                string eventJson = JsonSerializer.Serialize(nostrEvent, new JsonSerializerOptions 
                { 
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false
                });
                
                string message = $"[\"EVENT\",{eventJson}]";
                
                // Send to all connected relays
                await _webSocketClient.SendToAll(message);
                
                Debug.Log($"Published event {nostrEvent.Id} to {ConnectedRelays.Count} relays");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error publishing event: {ex.Message}");
                OnError(this, $"Publish error: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Subscribes to events based on the provided filter
        /// </summary>
        /// <param name="filter">The filter criteria for the subscription</param>
        /// <returns>The subscription ID</returns>
        public string Subscribe(Filter filter)
        {
            try
            {
                if (filter == null)
                {
                    throw new ArgumentNullException(nameof(filter), "Filter cannot be null");
                }
                
                // Generate a subscription ID
                string subscriptionId = Guid.NewGuid().ToString().Substring(0, 8);
                
                // Store the subscription
                _subscriptions[subscriptionId] = filter;
                
                // Create a subscription message: ["REQ", <subscription_id>, <filter JSON>]
                string subscriptionJson = $"[\"REQ\",\"{subscriptionId}\",{filter.ToJson()}]";
                
                // Send the subscription to all connected relays
                _webSocketClient.SendToAll(subscriptionJson).ContinueWith(task =>
                {
                    if (task.IsFaulted)
                    {
                        Debug.LogError($"Error subscribing: {task.Exception?.Message}");
                        OnError(this, $"Subscribe error: {task.Exception?.Message}");
                    }
                });
                
                return subscriptionId;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error creating subscription: {ex.Message}");
                OnError(this, $"Subscribe error: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Unsubscribes from events for the given subscription ID
        /// </summary>
        /// <param name="subscriptionId">The subscription ID to unsubscribe</param>
        public void Unsubscribe(string subscriptionId)
        {
            try
            {
                if (string.IsNullOrEmpty(subscriptionId))
                {
                    throw new ArgumentNullException(nameof(subscriptionId), "Subscription ID cannot be null or empty");
                }
                
                // Remove the subscription
                if (_subscriptions.ContainsKey(subscriptionId))
                {
                    _subscriptions.Remove(subscriptionId);
                }
                
                // Create a close message: ["CLOSE", <subscription_id>]
                string closeJson = $"[\"CLOSE\",\"{subscriptionId}\"]";
                
                // Send the close message to all connected relays
                _webSocketClient.SendToAll(closeJson).ContinueWith(task =>
                {
                    if (task.IsFaulted)
                    {
                        Debug.LogError($"Error unsubscribing: {task.Exception?.Message}");
                        OnError(this, $"Unsubscribe error: {task.Exception?.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error unsubscribing: {ex.Message}");
                OnError(this, $"Unsubscribe error: {ex.Message}");
            }
        }
        
        private void OnMessageReceived(object sender, MessageReceivedEventArgs e)
        {
            try
            {
                string message = e.Message;
                string relayUrl = e.RelayUrl;
                
                // Parse the message based on NIP-01
                if (string.IsNullOrEmpty(message) || message.Length < 2 || message[0] != '[')
                {
                    Debug.LogWarning($"Received invalid message from {relayUrl}: {message}");
                    return;
                }
                
                // Try to parse as JSON array
                var messageArray = JsonSerializer.Deserialize<object[]>(message);
                
                if (messageArray == null || messageArray.Length < 2)
                {
                    Debug.LogWarning($"Received invalid message format from {relayUrl}: {message}");
                    return;
                }
                
                // Get the message type
                string messageType = messageArray[0].ToString();
                
                // Handle different message types
                switch (messageType)
                {
                    case "EVENT":
                        HandleEventMessage(messageArray, relayUrl);
                        break;
                        
                    case "NOTICE":
                        HandleNoticeMessage(messageArray, relayUrl);
                        break;
                        
                    case "EOSE":
                        HandleEndOfStoredEventsMessage(messageArray, relayUrl);
                        break;
                        
                    case "OK":
                        HandleOkMessage(messageArray, relayUrl);
                        break;
                        
                    default:
                        Debug.LogWarning($"Received unknown message type from {relayUrl}: {messageType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error processing message: {ex.Message}");
            }
        }
        
        private void HandleEventMessage(object[] messageArray, string relayUrl)
        {
            if (messageArray.Length < 3)
            {
                Debug.LogWarning($"Received invalid EVENT message from {relayUrl}");
                return;
            }
            
            try
            {
                // The second element is the subscription ID
                string subscriptionId = messageArray[1].ToString();
                
                // The third element is the event JSON
                var eventJson = messageArray[2].ToString();
                
                // Deserialize the event
                var nostrEvent = JsonSerializer.Deserialize<NostrEvent>(eventJson, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true
                });
                
                if (nostrEvent == null)
                {
                    Debug.LogWarning($"Failed to deserialize event from {relayUrl}");
                    return;
                }
                
                // Check if this is a valid event
                if (string.IsNullOrEmpty(nostrEvent.Id) || string.IsNullOrEmpty(nostrEvent.Signature))
                {
                    Debug.LogWarning($"Received event with missing ID or signature from {relayUrl}");
                    return;
                }
                
                // Verify the signature
                if (!nostrEvent.VerifySignature())
                {
                    Debug.LogWarning($"Received event with invalid signature from {relayUrl}");
                    return;
                }
                
                // Raise the event received event
                EventReceived?.Invoke(this, new NostrEventArgs(nostrEvent, subscriptionId, relayUrl));
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error handling EVENT message: {ex.Message}");
            }
        }
        
        private void HandleNoticeMessage(object[] messageArray, string relayUrl)
        {
            if (messageArray.Length < 2)
            {
                Debug.LogWarning($"Received invalid NOTICE message from {relayUrl}");
                return;
            }
            
            try
            {
                // The second element is the notice message
                string notice = messageArray[1].ToString();
                
                Debug.Log($"Received NOTICE from {relayUrl}: {notice}");
                
                // Raise the error event for notices (they're often warnings/errors)
                OnError(this, $"Relay notice: {notice}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error handling NOTICE message: {ex.Message}");
            }
        }
        
        private void HandleEndOfStoredEventsMessage(object[] messageArray, string relayUrl)
        {
            if (messageArray.Length < 2)
            {
                Debug.LogWarning($"Received invalid EOSE message from {relayUrl}");
                return;
            }
            
            try
            {
                // The second element is the subscription ID
                string subscriptionId = messageArray[1].ToString();
                
                Debug.Log($"Received EOSE for subscription {subscriptionId} from {relayUrl}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error handling EOSE message: {ex.Message}");
            }
        }
        
        private void HandleOkMessage(object[] messageArray, string relayUrl)
        {
            if (messageArray.Length < 3)
            {
                Debug.LogWarning($"Received invalid OK message from {relayUrl}");
                return;
            }
            
            try
            {
                // The second element is the event ID
                string eventId = messageArray[1].ToString();
                
                // The third element is a boolean success flag
                bool success = bool.Parse(messageArray[2].ToString());
                
                // The fourth element (if present) is an error message
                string errorMessage = messageArray.Length > 3 ? messageArray[3].ToString() : null;
                
                if (success)
                {
                    Debug.Log($"Event {eventId} successfully published to {relayUrl}");
                }
                else
                {
                    Debug.LogWarning($"Failed to publish event {eventId} to {relayUrl}: {errorMessage}");
                    OnError(this, $"Publish error: {errorMessage}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error handling OK message: {ex.Message}");
            }
        }
        
        private void OnConnected(object sender, string relayUrl)
        {
            Debug.Log($"Connected to relay: {relayUrl}");
            
            // Send subscriptions to the newly connected relay
            foreach (var subscription in _subscriptions)
            {
                string subscriptionJson = $"[\"REQ\",\"{subscription.Key}\",{subscription.Value.ToJson()}]";
                _webSocketClient.Send(relayUrl, subscriptionJson).ContinueWith(task =>
                {
                    if (task.IsFaulted)
                    {
                        Debug.LogError($"Error subscribing to {relayUrl}: {task.Exception?.Message}");
                    }
                });
            }
            
            Connected?.Invoke(this, relayUrl);
        }
        
        private void OnDisconnected(object sender, string relayUrl)
        {
            Debug.Log($"Disconnected from relay: {relayUrl}");
            
            _relayConnections.Remove(relayUrl);
            
            Disconnected?.Invoke(this, relayUrl);
        }
        
        private void OnError(object sender, string error)
        {
            Debug.LogError($"WebSocket error: {error}");
            
            Error?.Invoke(this, error);
        }
    }
    
    /// <summary>
    /// Event arguments for Nostr events
    /// </summary>
    public class NostrEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the Nostr event
        /// </summary>
        public NostrEvent Event { get; }
        
        /// <summary>
        /// Gets the subscription ID that received the event
        /// </summary>
        public string SubscriptionId { get; }
        
        /// <summary>
        /// Gets the URL of the relay that sent the event
        /// </summary>
        public string RelayUrl { get; }
        
        /// <summary>
        /// Initializes a new instance of the NostrEventArgs class
        /// </summary>
        /// <param name="nostrEvent">The Nostr event</param>
        /// <param name="subscriptionId">The subscription ID</param>
        /// <param name="relayUrl">The relay URL</param>
        public NostrEventArgs(NostrEvent nostrEvent, string subscriptionId, string relayUrl)
        {
            Event = nostrEvent;
            SubscriptionId = subscriptionId;
            RelayUrl = relayUrl;
        }
    }
} 