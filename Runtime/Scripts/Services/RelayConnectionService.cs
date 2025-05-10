using System;
using System.Collections.Generic;
using UnityEngine;
using NostrUnity.Relay;
using NostrUnity.Models;

namespace NostrUnity.Services
{
    /// <summary>
    /// Service for managing multiple relay connections
    /// </summary>
    public class RelayConnectionService : MonoBehaviour
    {
        [SerializeField] private List<string> _defaultRelays = new List<string>();
        
        private Dictionary<string, NostrRelay> _relays = new Dictionary<string, NostrRelay>();
        
        public event Action<string, RelayState> OnRelayStateChanged;
        public event Action<NostrEvent, string> OnEventReceived;
        
        private void Awake()
        {
            // Connect to default relays
            foreach (var relayUrl in _defaultRelays)
            {
                AddRelay(relayUrl);
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
        }
        
        /// <summary>
        /// Adds a relay and connects to it
        /// </summary>
        /// <param name="relayUrl">The URL of the relay to connect to</param>
        /// <returns>True if successful</returns>
        public bool AddRelay(string relayUrl)
        {
            if (string.IsNullOrEmpty(relayUrl))
                return false;
                
            if (_relays.ContainsKey(relayUrl))
                return false;
                
            try
            {
                // Create and configure relay
                NostrRelay relay = new NostrRelay(relayUrl);
                relay.OnStateChanged += (state) => HandleRelayStateChanged(relayUrl, state);
                relay.OnEventReceived += (evt) => HandleEventReceived(evt, relayUrl);
                
                // Add to dictionary and connect
                _relays.Add(relayUrl, relay);
                relay.Connect();
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to add relay: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Removes a relay and disconnects from it
        /// </summary>
        /// <param name="relayUrl">The URL of the relay to remove</param>
        /// <returns>True if successful</returns>
        public bool RemoveRelay(string relayUrl)
        {
            if (!_relays.TryGetValue(relayUrl, out NostrRelay relay))
                return false;
                
            try
            {
                relay.Disconnect();
                _relays.Remove(relayUrl);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to remove relay: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Subscribes to events matching the provided filter
        /// </summary>
        /// <param name="subscriptionId">The ID for this subscription</param>
        /// <param name="filter">The filter to apply</param>
        public void Subscribe(string subscriptionId, Filter filter)
        {
            foreach (var relay in _relays.Values)
            {
                relay.Subscribe(subscriptionId, filter);
            }
        }
        
        /// <summary>
        /// Unsubscribes from a subscription
        /// </summary>
        /// <param name="subscriptionId">The subscription ID to unsubscribe from</param>
        public void Unsubscribe(string subscriptionId)
        {
            foreach (var relay in _relays.Values)
            {
                relay.Unsubscribe(subscriptionId);
            }
        }
        
        /// <summary>
        /// Publishes an event to all connected relays
        /// </summary>
        /// <param name="nostrEvent">The event to publish</param>
        public void PublishEvent(NostrEvent nostrEvent)
        {
            if (nostrEvent == null)
                throw new ArgumentNullException(nameof(nostrEvent));
                
            foreach (var relay in _relays.Values)
            {
                relay.PublishEvent(nostrEvent);
            }
        }
        
        /// <summary>
        /// Gets a list of connected relay URLs
        /// </summary>
        public List<string> GetConnectedRelays()
        {
            return new List<string>(_relays.Keys);
        }
        
        /// <summary>
        /// Gets the connection state of a specific relay
        /// </summary>
        /// <param name="relayUrl">The URL of the relay</param>
        /// <returns>The relay state</returns>
        public RelayState GetRelayState(string relayUrl)
        {
            if (_relays.TryGetValue(relayUrl, out NostrRelay relay))
                return relay.State;
                
            return RelayState.Disconnected;
        }
        
        private void HandleRelayStateChanged(string relayUrl, RelayState state)
        {
            OnRelayStateChanged?.Invoke(relayUrl, state);
        }
        
        private void HandleEventReceived(NostrEvent evt, string relayUrl)
        {
            OnEventReceived?.Invoke(evt, relayUrl);
        }
    }
} 