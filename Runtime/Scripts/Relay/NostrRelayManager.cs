using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NostrUnity.Models;

namespace NostrUnity.Relay
{
    /// <summary>
    /// Manages multiple Nostr relay connections
    /// </summary>
    public class NostrRelayManager : MonoBehaviour
    {
        [SerializeField] private List<string> _defaultRelayUrls = new List<string>();
        
        private readonly Dictionary<string, NostrRelay> _relays = new Dictionary<string, NostrRelay>();
        private readonly HashSet<string> _activeSubscriptions = new HashSet<string>();
        
        /// <summary>
        /// Event triggered when a Nostr event is received from any relay
        /// </summary>
        public event Action<NostrEvent, string> OnEventReceived;
        
        /// <summary>
        /// Event triggered when a relay connection state changes
        /// </summary>
        public event Action<string, RelayState> OnRelayStateChanged;
        
        /// <summary>
        /// Event triggered when a relay reports an error
        /// </summary>
        public event Action<string, string> OnRelayError;
        
        private void Awake()
        {
            // Connect to default relays if any are specified
            foreach (string url in _defaultRelayUrls)
            {
                AddRelay(url);
            }
        }
        
        private void Update()
        {
            // Update all relays
            foreach (var relay in _relays.Values)
            {
                relay.Update();
            }
        }
        
        private void OnDestroy()
        {
            // Disconnect from all relays
            foreach (var relay in _relays.Values)
            {
                relay.Disconnect();
            }
            
            _relays.Clear();
        }
        
        /// <summary>
        /// Adds a new relay connection
        /// </summary>
        /// <param name="relayUrl">The URL of the relay (wss://...)</param>
        /// <param name="autoConnect">Whether to automatically connect to the relay</param>
        /// <returns>True if the relay was added, false if it already exists</returns>
        public bool AddRelay(string relayUrl, bool autoConnect = true)
        {
            if (string.IsNullOrEmpty(relayUrl))
                throw new ArgumentException("Relay URL cannot be null or empty", nameof(relayUrl));
            
            // Check if the relay already exists
            if (_relays.ContainsKey(relayUrl))
            {
                Debug.LogWarning($"Relay {relayUrl} is already added");
                return false;
            }
            
            try
            {
                // Create and add the relay
                var relay = new NostrRelay(relayUrl);
                
                // Set up event handlers
                relay.OnEventReceived += (e) => OnEventReceived?.Invoke(e, relayUrl);
                relay.OnStateChanged += (state) => HandleRelayStateChanged(relayUrl, state);
                relay.OnError += (error) => OnRelayError?.Invoke(relayUrl, error);
                
                _relays.Add(relayUrl, relay);
                
                // Connect if requested
                if (autoConnect)
                {
                    relay.Connect();
                }
                
                Debug.Log($"Added relay: {relayUrl}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to add relay {relayUrl}: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Removes a relay connection
        /// </summary>
        /// <param name="relayUrl">The URL of the relay to remove</param>
        /// <returns>True if the relay was removed, false if it wasn't found</returns>
        public bool RemoveRelay(string relayUrl)
        {
            if (string.IsNullOrEmpty(relayUrl))
                throw new ArgumentException("Relay URL cannot be null or empty", nameof(relayUrl));
            
            if (!_relays.TryGetValue(relayUrl, out NostrRelay relay))
            {
                Debug.LogWarning($"Relay {relayUrl} not found");
                return false;
            }
            
            // Disconnect and remove the relay
            relay.Disconnect();
            _relays.Remove(relayUrl);
            
            Debug.Log($"Removed relay: {relayUrl}");
            return true;
        }
        
        /// <summary>
        /// Gets a list of all connected relay URLs
        /// </summary>
        /// <returns>Array of relay URLs</returns>
        public string[] GetRelayUrls()
        {
            return _relays.Keys.ToArray();
        }
        
        /// <summary>
        /// Gets the state of a specific relay
        /// </summary>
        /// <param name="relayUrl">The URL of the relay</param>
        /// <returns>The relay's connection state</returns>
        public RelayState GetRelayState(string relayUrl)
        {
            if (string.IsNullOrEmpty(relayUrl))
                throw new ArgumentException("Relay URL cannot be null or empty", nameof(relayUrl));
            
            if (!_relays.TryGetValue(relayUrl, out NostrRelay relay))
            {
                Debug.LogWarning($"Relay {relayUrl} not found");
                return RelayState.Disconnected;
            }
            
            return relay.State;
        }
        
        /// <summary>
        /// Connects to all relays
        /// </summary>
        public void ConnectAll()
        {
            foreach (var relay in _relays.Values)
            {
                relay.Connect();
            }
        }
        
        /// <summary>
        /// Disconnects from all relays
        /// </summary>
        public void DisconnectAll()
        {
            foreach (var relay in _relays.Values)
            {
                relay.Disconnect();
            }
        }
        
        /// <summary>
        /// Publishes an event to all connected relays
        /// </summary>
        /// <param name="nostrEvent">The event to publish</param>
        public void PublishEvent(NostrEvent nostrEvent)
        {
            if (nostrEvent == null)
                throw new ArgumentNullException(nameof(nostrEvent), "Event cannot be null");
            
            if (string.IsNullOrEmpty(nostrEvent.Sig))
                throw new InvalidOperationException("Cannot publish an unsigned event. Please sign the event first.");
            
            var connectedRelays = _relays.Values.Where(r => r.State == RelayState.Connected).ToList();
            
            if (connectedRelays.Count == 0)
            {
                Debug.LogWarning("No connected relays to publish event to");
                return;
            }
            
            foreach (var relay in connectedRelays)
            {
                relay.PublishEvent(nostrEvent);
            }
            
            Debug.Log($"Published event to {connectedRelays.Count} relay(s)");
        }
        
        /// <summary>
        /// Subscribes to events matching the filter on all connected relays
        /// </summary>
        /// <param name="subscriptionId">Unique ID for this subscription</param>
        /// <param name="filter">The filter to apply</param>
        public void Subscribe(string subscriptionId, Filter filter)
        {
            if (string.IsNullOrEmpty(subscriptionId))
                throw new ArgumentException("Subscription ID cannot be null or empty", nameof(subscriptionId));
            
            if (filter == null)
                throw new ArgumentNullException(nameof(filter), "Filter cannot be null");
            
            var connectedRelays = _relays.Values.Where(r => r.State == RelayState.Connected).ToList();
            
            if (connectedRelays.Count == 0)
            {
                Debug.LogWarning("No connected relays to subscribe to");
                return;
            }
            
            // Track the subscription
            _activeSubscriptions.Add(subscriptionId);
            
            // Subscribe on all connected relays
            foreach (var relay in connectedRelays)
            {
                relay.Subscribe(subscriptionId, filter);
            }
            
            Debug.Log($"Subscribed to events with filter on {connectedRelays.Count} relay(s)");
        }
        
        /// <summary>
        /// Unsubscribes from a subscription on all relays
        /// </summary>
        /// <param name="subscriptionId">The ID of the subscription to cancel</param>
        public void Unsubscribe(string subscriptionId)
        {
            if (string.IsNullOrEmpty(subscriptionId))
                throw new ArgumentException("Subscription ID cannot be null or empty", nameof(subscriptionId));
            
            // Remove from active subscriptions
            _activeSubscriptions.Remove(subscriptionId);
            
            // Unsubscribe from all relays
            foreach (var relay in _relays.Values)
            {
                relay.Unsubscribe(subscriptionId);
            }
            
            Debug.Log($"Unsubscribed from subscription {subscriptionId} on all relays");
        }
        
        /// <summary>
        /// Gets a list of all active subscription IDs
        /// </summary>
        /// <returns>Array of subscription IDs</returns>
        public string[] GetActiveSubscriptions()
        {
            return _activeSubscriptions.ToArray();
        }
        
        private void HandleRelayStateChanged(string relayUrl, RelayState state)
        {
            // Notify listeners
            OnRelayStateChanged?.Invoke(relayUrl, state);
            
            // If the relay just connected, re-subscribe to all active subscriptions
            if (state == RelayState.Connected)
            {
                ResubscribeToRelay(relayUrl);
            }
        }
        
        private void ResubscribeToRelay(string relayUrl)
        {
            if (!_relays.TryGetValue(relayUrl, out NostrRelay relay))
                return;
            
            if (_activeSubscriptions.Count == 0)
                return;
            
            Debug.Log($"Resubscribing to {_activeSubscriptions.Count} subscription(s) on relay {relayUrl}");
            
            // We need to resubscribe with the same IDs but don't have the filters
            // This is a simplification - in a more complete implementation, we would store the filters
            foreach (string subId in _activeSubscriptions)
            {
                // Create a minimal filter for reconnection
                var filter = new Filter();
                relay.Subscribe(subId, filter);
            }
        }
    }
} 