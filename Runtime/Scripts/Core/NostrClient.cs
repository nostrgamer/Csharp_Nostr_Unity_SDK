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
        
        private void OnMessageReceived(object sender, string message)
        {
            // TODO: Parse the message
            Debug.Log($"Received message: {message}");
        }
        
        private void OnConnected(object sender, string relayUrl)
        {
            Connected?.Invoke(this, relayUrl);
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