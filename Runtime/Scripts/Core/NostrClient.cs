using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Nostr.Unity.Utils;
using Newtonsoft.Json.Linq;
using NostrUnity.Models;
using System.Text.Json;
using NostrUnity.Crypto;
using System.Linq;

namespace NostrUnity.Core
{
    /// <summary>
    /// Main client for interacting with Nostr network
    /// </summary>
    public class NostrClient : MonoBehaviour
    {
        private ClientWebSocket _webSocket;
        private CancellationTokenSource _cts;
        private bool _isConnected;
        private readonly object _lock = new object();
        
        private readonly List<string> _relayUrls = new List<string>();
        private readonly Dictionary<string, List<Action<NostrEvent>>> _subscriptions = new Dictionary<string, List<Action<NostrEvent>>>();
        
        private const int RECONNECT_DELAY_MS = 5000;
        private const int MAX_RECONNECT_ATTEMPTS = 3;
        
        // Dictionary to track event publishing errors
        private Dictionary<string, string> _eventErrors = new Dictionary<string, string>();
        
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
        /// Event triggered when a message is sent to a relay
        /// </summary>
        public event EventHandler<string> MessageSent;

        /// <summary>
        /// Event triggered when connection status changes
        /// </summary>
        public event EventHandler<bool> ConnectionStatusChanged;

        /// <summary>
        /// Gets a value indicating whether the client is connected to any relay
        /// </summary>
        public bool IsConnected
        {
            get
            {
                lock (_lock)
                {
                    return _isConnected;
                }
            }
            private set
            {
                lock (_lock)
                {
                    _isConnected = value;
                }
            }
        }
        
        /// <summary>
        /// Gets the list of connected relay URLs
        /// </summary>
        public List<string> ConnectedRelays => _relayUrls;
        
        /// <summary>
        /// Initializes a new instance of the NostrClient class
        /// </summary>
        public NostrClient()
        {
            _webSocket = new ClientWebSocket();
            _cts = new CancellationTokenSource();
        }
        
        /// <summary>
        /// Connects to a Nostr relay
        /// </summary>
        /// <param name="relayUri">The WebSocket URI of the relay (e.g., "wss://relay.damus.io")</param>
        public async Task ConnectAsync(string relayUri)
        {
            try
            {
                if (IsConnected)
                {
                    Debug.LogWarning("Already connected to relay");
                    return;
                }

                await _webSocket.ConnectAsync(new Uri(relayUri), _cts.Token);
                _relayUrls.Add(relayUri);
                IsConnected = true;
                Connected?.Invoke(this, relayUri);
                
                Debug.Log($"Connected to {relayUri}");
                _ = ReceiveMessagesAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to connect to relay: {ex.Message}");
                Error?.Invoke(this, $"Connection failed: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Publishes a Nostr event to the relay
        /// </summary>
        /// <param name="ev">The event to publish</param>
        public async Task PublishEvent(NostrEvent ev)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Not connected to relay");
            }

            try
            {
                // Create the event message
                var message = new
                {
                    type = "EVENT",
                    event_data = ev
                };

                string json = System.Text.Json.JsonSerializer.Serialize(message);
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts.Token);
                Debug.Log($"Published event: {ev.Id}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to publish event: {ex.Message}");
                Error?.Invoke(this, $"Publish failed: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Receives messages from the relay
        /// </summary>
        private async Task ReceiveMessagesAsync()
        {
            var buffer = new byte[4096];
            
            while (IsConnected)
            {
                try
                {
                    var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        IsConnected = false;
                        Debug.Log("Connection closed by server");
                        Disconnected?.Invoke(this, _relayUrls[0]);
                        break;
                    }

                    string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    Debug.Log($"Received message: {message}");
                    HandleMessage(message, _relayUrls[0]);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error receiving messages: {ex.Message}");
                    IsConnected = false;
                    break;
                }
            }
        }
        
        /// <summary>
        /// Disconnects from the relay
        /// </summary>
        public async Task DisconnectAsync()
        {
            try
            {
                if (!IsConnected)
                {
                    return;
                }

                _cts.Cancel();
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnecting", CancellationToken.None);
                IsConnected = false;
                Disconnected?.Invoke(this, _relayUrls[0]);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error during disconnect: {ex.Message}");
                Error?.Invoke(this, $"Disconnect error: {ex.Message}");
            }
            finally
            {
                _webSocket.Dispose();
                _webSocket = new ClientWebSocket();
                _cts = new CancellationTokenSource();
                _relayUrls.Clear();
                _subscriptions.Clear();
                Debug.Log("Disconnected");
            }
        }

        private void OnDestroy()
        {
            DisconnectAsync().Wait();
        }
        
        /// <summary>
        /// Publishes an event to all connected relays
        /// </summary>
        /// <param name="nostrEvent">The event to publish</param>
        /// <param name="onComplete">Callback for when the event is published</param>
        /// <returns>True if the event was published successfully to at least one relay</returns>
        public IEnumerator PublishEvent(NostrEvent nostrEvent, Action<bool> onComplete = null)
        {
            bool success = false;
            
            // Validate input outside try-catch
            if (nostrEvent == null)
                throw new ArgumentNullException(nameof(nostrEvent));
            if (string.IsNullOrEmpty(nostrEvent.Id))
                throw new ArgumentException("Event ID cannot be null or empty", nameof(nostrEvent));
            if (string.IsNullOrEmpty(nostrEvent.Sig))
                throw new ArgumentException("Event signature cannot be null or empty", nameof(nostrEvent));
                
            // Double-check signature before sending
            Debug.Log($"[DEBUG] Pre-publish verification check for event {nostrEvent.Id}");
            bool verificationResult = NostrCrypto.VerifySignature(nostrEvent.Pubkey, nostrEvent.Id, nostrEvent.Sig);
            Debug.Log($"[DEBUG] Local signature verification result: {verificationResult}");
            
            if (!verificationResult)
            {
                Debug.LogError("[DEBUG] ⚠️ Event signature verification failed - relays will reject this event!");
                Debug.LogError("[DEBUG] This usually means the private key doesn't match the public key used in the event.");
                Debug.LogError($"[DEBUG] Event ID: {nostrEvent.Id}");
                Debug.LogError($"[DEBUG] Public Key: {nostrEvent.Pubkey}");
                Debug.LogError($"[DEBUG] Signature: {nostrEvent.Sig}");
                throw new ArgumentException("Event signature verification failed", nameof(nostrEvent));
            }
            
            Debug.Log($"[DEBUG] Publishing event with ID: {nostrEvent.Id}");
            Debug.Log($"[DEBUG] Event public key: {nostrEvent.Pubkey}");
            Debug.Log($"[DEBUG] Event signature: {nostrEvent.Sig}");
            Debug.Log($"[DEBUG] Event kind: {nostrEvent.Kind}");
            Debug.Log($"[DEBUG] Event timestamp: {nostrEvent.CreatedAt}");
            
            string jsonMessage = null;
            string eventJson = null;
            
            try
            {
                // Use the serialized complete event directly for proper JSON structure
                eventJson = nostrEvent.SerializeComplete();
                Debug.Log($"[DEBUG] Serialized event JSON: {eventJson}");
                
                // Create the proper Nostr message array: ["EVENT", {event}]
                JArray message = new JArray();
                message.Add("EVENT");
                message.Add(JObject.Parse(eventJson));
                
                jsonMessage = message.ToString(Formatting.None);
                Debug.Log($"[DEBUG] Publishing event message: {jsonMessage}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DEBUG] Error preparing event for publishing: {ex.Message}");
                Debug.LogError($"[DEBUG] Stack trace: {ex.StackTrace}");
                onComplete?.Invoke(false);
                yield break;
            }
            
            if (_relayUrls.Count == 0)
            {
                Debug.LogWarning("[DEBUG] No connected relays to publish event to");
                onComplete?.Invoke(false);
                yield break;
            }
            
            Debug.Log($"[DEBUG] Attempting to publish to {_relayUrls.Count} relays...");
            
            // Track responses from each relay
            Dictionary<string, string> relayResponses = new Dictionary<string, string>();
            List<Task> sendTasks = new List<Task>();
            
            // Clear any previous errors for this event
            _eventErrors.Remove(nostrEvent.Id);
            
            // Initiate sending to all connected relays
            for (int i = 0; i < _relayUrls.Count; i++)
            {
                string relayUrl = i < _relayUrls.Count ? _relayUrls[i] : "unknown";
                
                if (!IsConnected)
                {
                    Debug.LogWarning($"[DEBUG] Skipping {relayUrl} - WebSocket state is {_webSocket.State}");
                    relayResponses[relayUrl] = $"WebSocket not open (state: {_webSocket.State})";
                    continue;
                }
                
                try
                {
                    Debug.Log($"[DEBUG] Sending to relay: {relayUrl}");
                    byte[] messageBytes = Encoding.UTF8.GetBytes(jsonMessage);
                    
                    // Create a new task for this send operation
                    var sendTask = _webSocket.SendAsync(
                        new ArraySegment<byte>(messageBytes),
                        WebSocketMessageType.Text,
                        true,
                        _cts.Token
                    );
                    
                    sendTasks.Add(sendTask);
                    
                    // Track which task belongs to which relay
                    int relayIndex = i;
                    sendTask.ContinueWith(t => {
                        if (t.IsFaulted)
                        {
                            string errorMsg = t.Exception?.InnerException?.Message ?? "Unknown error";
                            relayResponses[relayUrl] = $"Send failed: {errorMsg}";
                            Debug.LogError($"[DEBUG] Failed to send to {relayUrl}: {errorMsg}");
                        }
                        else if (t.IsCompleted)
                        {
                            relayResponses[relayUrl] = "Message sent";
                            Debug.Log($"[DEBUG] Successfully sent to {relayUrl}");
                            success = true;
                        }
                    }, TaskContinuationOptions.ExecuteSynchronously);
                }
                catch (Exception ex)
                {
                    relayResponses[relayUrl] = $"Exception: {ex.Message}";
                    Debug.LogError($"[DEBUG] Exception sending to relay {relayUrl}: {ex.Message}");
                }
            }
            
            // Wait for all send tasks to complete
            while (sendTasks.Count > 0)
            {
                // Remove completed tasks
                sendTasks.RemoveAll(t => t.IsCompleted || t.IsFaulted || t.IsCanceled);
                
                if (sendTasks.Count > 0)
                    yield return null;
            }
            
            // Check responses and determine if we had any successes
            Debug.Log($"[DEBUG] Publication status for event {nostrEvent.Id}:");
            foreach (var relayResponse in relayResponses)
            {
                Debug.Log($"[DEBUG] - {relayResponse.Key}: {relayResponse.Value}");
            }
            
            // Store any errors for the event
            if (!success)
            {
                string combinedErrors = string.Join("; ", relayResponses.Values);
                _eventErrors[nostrEvent.Id] = combinedErrors;
                Debug.LogError($"[DEBUG] Event {nostrEvent.Id} failed to publish to any relays: {combinedErrors}");
            }
            else
            {
                Debug.Log($"[DEBUG] Event {nostrEvent.Id} published successfully to at least one relay");
            }
            
            onComplete?.Invoke(success);
        }
        
        /// <summary>
        /// Subscribes to events matching a filter
        /// </summary>
        /// <param name="filter">The filter to match events against</param>
        /// <param name="onEventReceived">Callback for when an event is received</param>
        /// <returns>The subscription ID</returns>
        public string Subscribe(Filter filter, Action<NostrEvent> onEventReceived)
        {
            if (filter == null)
                throw new ArgumentNullException(nameof(filter));
            
            if (onEventReceived == null)
                throw new ArgumentNullException(nameof(onEventReceived));
            
            string subscriptionId = Guid.NewGuid().ToString();
            _subscriptions[subscriptionId] = new List<Action<NostrEvent>> { onEventReceived };
            
            // Send subscription to all connected relays
            var subscriptionMessage = new object[] { "REQ", subscriptionId, filter };
            string jsonMessage = JsonConvert.SerializeObject(subscriptionMessage);
            SendToAll(jsonMessage);
            
            return subscriptionId;
        }
        
        /// <summary>
        /// Unsubscribes from events
        /// </summary>
        /// <param name="subscriptionId">The subscription ID to unsubscribe from</param>
        public void Unsubscribe(string subscriptionId)
        {
            if (string.IsNullOrEmpty(subscriptionId))
                throw new ArgumentException("Subscription ID cannot be null or empty", nameof(subscriptionId));
            
            if (_subscriptions.Remove(subscriptionId))
            {
                // Send close message to all connected relays
                var closeMessage = new object[] { "CLOSE", subscriptionId };
                string jsonMessage = JsonConvert.SerializeObject(closeMessage);
                SendToAll(jsonMessage);
            }
        }
        
        private IEnumerator ReceiveMessagesCoroutine(ClientWebSocket webSocket, string relayUrl)
        {
            var buffer = new byte[4096];
            int reconnectAttempts = 0;
            while (!_cts.Token.IsCancellationRequested)
            {
                bool shouldReconnect = false;
                bool hasError = false;
                
                // Check websocket state
                try
                {
                    if (webSocket.State != WebSocketState.Open)
                    {
                        if (reconnectAttempts < MAX_RECONNECT_ATTEMPTS)
                        {
                            Debug.Log($"Attempting to reconnect to {relayUrl} (attempt {reconnectAttempts + 1}/{MAX_RECONNECT_ATTEMPTS})");
                            shouldReconnect = true;
                        }
                        else
                        {
                            Debug.LogError($"Max reconnection attempts reached for {relayUrl}");
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error checking WebSocket state: {ex.Message}");
                    hasError = true;
                }
                
                if (hasError)
                {
                    yield return new WaitForSeconds(RECONNECT_DELAY_MS / 1000f);
                    continue;
                }
                
                // Handle reconnection
                if (shouldReconnect)
                {
                    yield return new WaitForSeconds(RECONNECT_DELAY_MS / 1000f);
                    Task reconnectTask = null;
                    bool reconnectStarted = false;
                    
                    try
                    {
                        reconnectTask = webSocket.ConnectAsync(new Uri(relayUrl), _cts.Token);
                        reconnectStarted = true;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error starting reconnect: {ex.Message}");
                    }
                    
                    if (!reconnectStarted)
                    {
                        continue;
                    }
                    
                    yield return reconnectTask.AsCoroutine();
                    reconnectAttempts = 0;
                    Debug.Log($"Reconnected to relay: {relayUrl}");
                }
                
                // Try to receive messages
                Task<WebSocketReceiveResult> receiveTask = null;
                bool receiveStarted = false;
                
                try
                {
                    receiveTask = webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                    receiveStarted = true;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error starting receive: {ex.Message}");
                }
                
                if (!receiveStarted)
                {
                    yield return new WaitForSeconds(RECONNECT_DELAY_MS / 1000f);
                    continue;
                }
                
                yield return receiveTask.AsCoroutine();
                
                // Access the result safely after task completes
                WebSocketReceiveResult result = null;
                bool resultObtained = false;
                
                try 
                {
                    result = receiveTask.GetAwaiter().GetResult();
                    resultObtained = true;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error getting WebSocket result: {ex.Message}");
                }
                
                if (!resultObtained)
                {
                    yield return new WaitForSeconds(RECONNECT_DELAY_MS / 1000f);
                    continue;
                }
                
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Debug.Log($"Received close message from {relayUrl}");
                    break;
                }
                
                string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                HandleMessage(message, relayUrl);
            }
        }
        
        private void HandleMessage(string message, string relayUrl)
        {
            try
            {
                var messageArray = JsonConvert.DeserializeObject<object[]>(message);
                
                if (messageArray == null || messageArray.Length < 2)
                {
                    Debug.LogWarning($"Invalid message format from {relayUrl}: {message}");
                    return;
                }
                
                string messageType = messageArray[0]?.ToString();
                
                switch (messageType)
                {
                    case "EVENT":
                        HandleEventMessage(messageArray, relayUrl);
                        break;
                    case "EOSE":
                        HandleEoseMessage(messageArray, relayUrl);
                        break;
                    case "NOTICE":
                        HandleNoticeMessage(messageArray, relayUrl);
                        break;
                    case "OK":
                        HandleOkMessage(messageArray, relayUrl);
                        break;
                    default:
                        Debug.LogWarning($"Unknown message type from {relayUrl}: {messageType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error handling message from {relayUrl}: {ex.Message}");
            }
        }
        
        private void HandleEventMessage(object[] messageArray, string relayUrl)
        {
            try
            {
                if (messageArray.Length < 3)
                {
                    Debug.LogWarning($"Invalid EVENT message format from {relayUrl}");
                    return;
                }
                
                string subscriptionId = messageArray[1]?.ToString();
                if (string.IsNullOrEmpty(subscriptionId))
                {
                    Debug.LogWarning($"Invalid subscription ID in EVENT message from {relayUrl}");
                    return;
                }
                
                if (!_subscriptions.TryGetValue(subscriptionId, out var callbacks))
                {
                    Debug.LogWarning($"Received event for unknown subscription {subscriptionId} from {relayUrl}");
                    return;
                }
                
                var eventJson = messageArray[2].ToString();
                var nostrEvent = JsonConvert.DeserializeObject<NostrEvent>(eventJson);
                
                if (nostrEvent == null)
                {
                    Debug.LogWarning($"Failed to deserialize event from {relayUrl}");
                    return;
                }
                
                if (!nostrEvent.VerifySignature())
                {
                    Debug.LogWarning($"Invalid event signature from {relayUrl}");
                    return;
                }
                
                // Trigger the EventReceived event
                EventReceived?.Invoke(this, new NostrEventArgs(nostrEvent, subscriptionId, relayUrl));
                
                foreach (var callback in callbacks)
                {
                    try
                    {
                        callback(nostrEvent);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error in event callback: {ex.Message}");
                        Error?.Invoke(this, $"Error in event callback: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error handling EVENT message from {relayUrl}: {ex.Message}");
                Error?.Invoke(this, $"Error handling EVENT message: {ex.Message}");
            }
        }
        
        private void HandleEoseMessage(object[] messageArray, string relayUrl)
        {
            try
            {
                if (messageArray.Length < 2)
                {
                    Debug.LogWarning($"Invalid EOSE message format from {relayUrl}");
                    return;
                }
                
                string subscriptionId = messageArray[1]?.ToString();
                if (string.IsNullOrEmpty(subscriptionId))
                {
                    Debug.LogWarning($"Invalid subscription ID in EOSE message from {relayUrl}");
                    return;
                }
                
                Debug.Log($"End of stored events for subscription {subscriptionId} from {relayUrl}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error handling EOSE message from {relayUrl}: {ex.Message}");
            }
        }
        
        private void HandleNoticeMessage(object[] messageArray, string relayUrl)
        {
            try
            {
                if (messageArray.Length < 2)
                {
                    Debug.LogWarning($"Invalid NOTICE message format from {relayUrl}");
                    return;
                }
                
                string notice = messageArray[1]?.ToString();
                if (string.IsNullOrEmpty(notice))
                {
                    Debug.LogWarning($"Empty notice from {relayUrl}");
                    return;
                }
                
                Debug.Log($"Notice from {relayUrl}: {notice}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error handling NOTICE message from {relayUrl}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Handles "OK" messages from the relay
        /// </summary>
        private void HandleOkMessage(object[] okMessage, string relayUrl)
        {
            try
            {
                if (okMessage.Length < 3)
                {
                    Debug.LogWarning($"[DEBUG] Received invalid OK message format from {relayUrl}");
                    return;
                }
                
                string eventId = okMessage[1]?.ToString();
                bool success = okMessage[2]?.ToString() == "true";
                string errorMessage = okMessage.Length > 3 ? okMessage[3]?.ToString() : null;
                
                Debug.Log($"[DEBUG] Relay {relayUrl} responded to event {eventId} with status: {(success ? "ACCEPTED" : "REJECTED")}");
                
                if (success)
                {
                    Debug.Log($"[DEBUG] Event {eventId} successfully published to {relayUrl}");
                    // Remove any previous errors for this event
                    if (_eventErrors.ContainsKey(eventId))
                    {
                        _eventErrors.Remove(eventId);
                    }
                }
                else
                {
                    Debug.LogError($"[DEBUG] Event {eventId} was REJECTED by {relayUrl}");
                    Debug.LogError($"[DEBUG] Rejection reason: {errorMessage ?? "No reason provided"}");
                    
                    // Categorize common rejection reasons for easier debugging
                    if (!string.IsNullOrEmpty(errorMessage))
                    {
                        if (errorMessage.Contains("signature"))
                        {
                            Debug.LogError($"[DEBUG] ⚠️ SIGNATURE ERROR - Event rejected due to INVALID SIGNATURE");
                            Debug.LogError($"[DEBUG] This typically means the signature verification failed on the relay");
                        }
                        else if (errorMessage.Contains("duplicate"))
                        {
                            Debug.Log($"[DEBUG] Duplicate event - already seen by relay (not an error)");
                        }
                        else if (errorMessage.Contains("pow") || errorMessage.Contains("proof of work"))
                        {
                            Debug.LogError($"[DEBUG] Proof of Work Error - Event needs more PoW to be accepted");
                        }
                        else if (errorMessage.Contains("rate"))
                        {
                            Debug.LogWarning($"[DEBUG] Rate Limit - Relay is throttling your submissions");
                        }
                    }
                    
                    // Store the error for this event
                    _eventErrors[eventId] = $"Rejected by {relayUrl}: {errorMessage}";
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DEBUG] Error handling OK message from {relayUrl}: {ex.Message}");
            }
        }
        
        private void SendToAll(string message)
        {
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);
            
            foreach (var webSocket in _relayUrls.Select(url => _webSocket))
            {
                try
                {
                    if (webSocket.State == WebSocketState.Open)
                    {
                        webSocket.SendAsync(new ArraySegment<byte>(messageBytes), WebSocketMessageType.Text, true, _cts.Token);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to send message to relay: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Checks if an event has encountered errors during publishing
        /// </summary>
        /// <param name="eventId">The ID of the event to check</param>
        /// <param name="error">Returns the error message if any</param>
        /// <returns>True if the event has errors, otherwise false</returns>
        public bool HasEventErrors(string eventId, out string error)
        {
            if (_eventErrors.TryGetValue(eventId, out error))
            {
                return true;
            }
            
            error = null;
            return false;
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