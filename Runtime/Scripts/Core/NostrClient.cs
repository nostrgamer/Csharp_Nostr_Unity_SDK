using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;
using Nostr.Unity.Utils;
using System.Collections;

namespace Nostr.Unity
{
    /// <summary>
    /// Main client for interacting with Nostr network
    /// </summary>
    public class NostrClient : MonoBehaviour
    {
        private readonly List<ClientWebSocket> _webSockets = new List<ClientWebSocket>();
        private readonly List<string> _relayUrls = new List<string>();
        private readonly Dictionary<string, List<Action<NostrEvent>>> _subscriptions = new Dictionary<string, List<Action<NostrEvent>>>();
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        
        private const int RECONNECT_DELAY_MS = 5000;
        private const int MAX_RECONNECT_ATTEMPTS = 3;
        
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
        public bool IsConnected => _webSockets.Count > 0;
        
        /// <summary>
        /// Gets the list of connected relay URLs
        /// </summary>
        public List<string> ConnectedRelays => _relayUrls;
        
        /// <summary>
        /// Initializes a new instance of the NostrClient class
        /// </summary>
        public NostrClient()
        {
            // Initialize any other necessary components
        }
        
        /// <summary>
        /// Connects to a Nostr relay
        /// </summary>
        /// <param name="relayUrl">The WebSocket URL of the relay</param>
        /// <param name="onComplete">Callback for when the connection is complete</param>
        /// <returns>True if the connection was successful</returns>
        public IEnumerator ConnectToRelay(string relayUrl, Action<bool> onComplete = null)
        {
            if (string.IsNullOrEmpty(relayUrl))
            {
                onComplete?.Invoke(false);
                yield break;
            }
            if (_relayUrls.Contains(relayUrl))
            {
                onComplete?.Invoke(true);
                yield break;
            }
            bool connected = false;
            Exception connectionError = null;
            var ws = new ClientWebSocket();
            Task connectTask = null;
            try
            {
                connectTask = ws.ConnectAsync(new Uri(relayUrl), _cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                connectionError = ex;
            }
            if (connectTask != null)
            {
                while (!connectTask.IsCompleted)
                {
                    yield return null;
                }
                if (connectTask.IsFaulted)
                {
                    connectionError = connectTask.Exception;
                }
                else
                {
                    connected = true;
                }
            }
            if (connected)
            {
                _webSockets.Add(ws);
                _relayUrls.Add(relayUrl);
                StartCoroutine(ReceiveMessagesCoroutine(ws, relayUrl));
                Connected?.Invoke(this, relayUrl);
            }
            else if (connectionError != null)
            {
                Debug.LogError($"Error connecting to relay {relayUrl}: {connectionError.Message}");
                Error?.Invoke(this, $"Error connecting to relay {relayUrl}: {connectionError.Message}");
            }
            onComplete?.Invoke(connected);
        }
        
        /// <summary>
        /// Disconnects from all relays
        /// </summary>
        /// <param name="onComplete">Callback for when the disconnection is complete</param>
        /// <returns>True if the disconnection was successful</returns>
        public IEnumerator Disconnect(Action onComplete = null)
        {
            _cancellationTokenSource.Cancel();
            
            List<string> disconnectedRelays = new List<string>(_relayUrls);
            
            foreach (var ws in _webSockets)
            {
                Task closeTask = null;
                try
                {
                    if (ws.State == WebSocketState.Open)
                    {
                        closeTask = ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnecting", CancellationToken.None);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error closing WebSocket: {ex.Message}");
                    Error?.Invoke(this, $"Error disconnecting: {ex.Message}");
                }
                if (closeTask != null)
                {
                    while (!closeTask.IsCompleted)
                    {
                        yield return null;
                    }
                }
            }
            
            // Notify disconnection for each relay
            foreach (var relayUrl in disconnectedRelays)
            {
                Disconnected?.Invoke(this, relayUrl);
            }
            
            _webSockets.Clear();
            _relayUrls.Clear();
            _subscriptions.Clear();
            onComplete?.Invoke();
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
            if (string.IsNullOrEmpty(nostrEvent.Signature))
                throw new ArgumentException("Event signature cannot be null or empty", nameof(nostrEvent));
            if (!nostrEvent.VerifySignature())
                throw new ArgumentException("Event signature verification failed", nameof(nostrEvent));
            var eventMessage = new object[] { "EVENT", nostrEvent };
            string jsonMessage = JsonConvert.SerializeObject(eventMessage);
            foreach (var webSocket in _webSockets)
            {
                if (webSocket.State == WebSocketState.Open)
                {
                    Task sendTask = null;
                    try
                    {
                        sendTask = webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(jsonMessage)), WebSocketMessageType.Text, true, _cancellationTokenSource.Token);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Failed to start sending event to relay: {ex.Message}");
                        Error?.Invoke(this, $"Failed to send event: {ex.Message}");
                        continue;
                    }
                    yield return sendTask.AsCoroutine();
                    success = true;
                }
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
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
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
                        reconnectTask = webSocket.ConnectAsync(new Uri(relayUrl), _cancellationTokenSource.Token);
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
                    receiveTask = webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellationTokenSource.Token);
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
        /// Handles OK messages received from relays (event publish confirmations)
        /// </summary>
        /// <param name="messageArray">The message array from the relay</param>
        /// <param name="relayUrl">The URL of the relay that sent the message</param>
        private void HandleOkMessage(object[] messageArray, string relayUrl)
        {
            try
            {
                if (messageArray.Length < 3)
                {
                    Debug.LogWarning($"Invalid OK message format from {relayUrl}");
                    return;
                }
                
                string eventId = messageArray[1]?.ToString();
                bool accepted = false;
                
                // The third element is a boolean indicating if the event was accepted
                if (messageArray[2] != null)
                {
                    // Convert to bool - could be a bool or a string "true"/"false"
                    if (messageArray[2] is bool boolValue)
                    {
                        accepted = boolValue;
                    }
                    else
                    {
                        bool.TryParse(messageArray[2].ToString(), out accepted);
                    }
                }
                
                string message = accepted ? 
                    $"Event {eventId} was accepted by {relayUrl}" : 
                    $"Event {eventId} was rejected by {relayUrl}";
                
                if (messageArray.Length >= 4 && messageArray[3] != null)
                {
                    // The fourth element is an optional message
                    message += $": {messageArray[3]}";
                }
                
                Debug.Log(message);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error handling OK message from {relayUrl}: {ex.Message}");
            }
        }
        
        private void SendToAll(string message)
        {
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);
            
            foreach (var webSocket in _webSockets)
            {
                try
                {
                    if (webSocket.State == WebSocketState.Open)
                    {
                        webSocket.SendAsync(new ArraySegment<byte>(messageBytes), WebSocketMessageType.Text, true, _cancellationTokenSource.Token);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to send message to relay: {ex.Message}");
                }
            }
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