using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Nostr.Unity
{
    /// <summary>
    /// Main client for interacting with Nostr network
    /// </summary>
    public class NostrClient
    {
        private WebSocketClient _webSocketClient;
        private Dictionary<string, Filter> _subscriptions = new Dictionary<string, Filter>();
        
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
            await _webSocketClient.Connect(relayUrl);
        }
        
        /// <summary>
        /// Connects to multiple Nostr relays
        /// </summary>
        /// <param name="relayUrls">List of relay URLs to connect to</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task ConnectToRelays(List<string> relayUrls)
        {
            foreach (var relayUrl in relayUrls)
            {
                await ConnectToRelay(relayUrl);
            }
        }
        
        /// <summary>
        /// Disconnects from all relays
        /// </summary>
        public void Disconnect()
        {
            _webSocketClient.DisconnectAll();
        }
        
        /// <summary>
        /// Publishes an event to connected relays
        /// </summary>
        /// <param name="nostrEvent">The event to publish</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task PublishEvent(NostrEvent nostrEvent)
        {
            string json = JsonUtility.ToJson(new EventMessage("EVENT", nostrEvent));
            await _webSocketClient.SendToAll(json);
        }
        
        /// <summary>
        /// Subscribes to events based on the provided filter
        /// </summary>
        /// <param name="filter">The filter criteria for the subscription</param>
        /// <returns>The subscription ID</returns>
        public string Subscribe(Filter filter)
        {
            if (filter == null)
                throw new ArgumentNullException(nameof(filter));
            
            // Generate a subscription ID
            string subscriptionId = Guid.NewGuid().ToString().Substring(0, 8);
            
            // Store the subscription
            _subscriptions[subscriptionId] = filter;
            
            // Create a subscription message
            string subscriptionJson = $"[\"REQ\",\"{subscriptionId}\",{filter.ToJson()}]";
            
            // Send the subscription to all connected relays
            _webSocketClient.SendToAll(subscriptionJson).ContinueWith(task =>
            {
                if (task.IsFaulted)
                    Debug.LogError($"Error subscribing: {task.Exception?.Message}");
            });
            
            return subscriptionId;
        }
        
        /// <summary>
        /// Unsubscribes from events for the given subscription ID
        /// </summary>
        /// <param name="subscriptionId">The subscription ID to unsubscribe</param>
        public void Unsubscribe(string subscriptionId)
        {
            if (string.IsNullOrEmpty(subscriptionId))
                throw new ArgumentNullException(nameof(subscriptionId));
            
            // Remove the subscription
            if (_subscriptions.ContainsKey(subscriptionId))
                _subscriptions.Remove(subscriptionId);
            
            // Create a close message
            string closeJson = $"[\"CLOSE\",\"{subscriptionId}\"]";
            
            // Send the close message to all connected relays
            _webSocketClient.SendToAll(closeJson).ContinueWith(task =>
            {
                if (task.IsFaulted)
                    Debug.LogError($"Error unsubscribing: {task.Exception?.Message}");
            });
        }
        
        private void OnMessageReceived(object sender, string message)
        {
            // TODO: Parse the message
            Debug.Log($"Received message: {message}");
            
            // In a real implementation, you would parse the message and trigger the EventReceived event
            // For now, we'll just log it
        }
        
        private void OnConnected(object sender, string relayUrl)
        {
            Connected?.Invoke(this, relayUrl);
            
            // Re-subscribe to all subscriptions
            foreach (var subscription in _subscriptions)
            {
                string subscriptionJson = $"[\"REQ\",\"{subscription.Key}\",{subscription.Value.ToJson()}]";
                _webSocketClient.SendToRelay(relayUrl, subscriptionJson).ContinueWith(task =>
                {
                    if (task.IsFaulted)
                        Debug.LogError($"Error re-subscribing: {task.Exception?.Message}");
                });
            }
        }
        
        private void OnDisconnected(object sender, string relayUrl)
        {
            Disconnected?.Invoke(this, relayUrl);
        }
        
        private void OnError(object sender, string error)
        {
            Error?.Invoke(this, error);
        }
        
        // Inner message types for serialization
        [Serializable]
        private class EventMessage
        {
            public string type;
            public NostrEvent evt;
            
            public EventMessage(string type, NostrEvent evt)
            {
                this.type = type;
                this.evt = evt;
            }
        }
    }
    
    /// <summary>
    /// Event arguments for NostrEvent received events
    /// </summary>
    public class NostrEventArgs : EventArgs
    {
        public NostrEvent Event { get; }
        
        public NostrEventArgs(NostrEvent nostrEvent)
        {
            Event = nostrEvent;
        }
    }
} 