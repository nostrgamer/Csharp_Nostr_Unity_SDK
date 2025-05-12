using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using NostrUnity.Models;
using NostrUnity.Utils;

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
                _ = AddRelayAsync(url);
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
        
        private async void OnDestroy()
        {
            // Disconnect from all relays
            await DisconnectAllAsync();
            _relays.Clear();
        }
        
        #region Async Methods
        
        /// <summary>
        /// Adds a new relay connection
        /// </summary>
        /// <param name="relayUrl">The URL of the relay (wss://...)</param>
        /// <param name="autoConnect">Whether to automatically connect to the relay</param>
        /// <returns>True if the relay was added, false if it already exists</returns>
        public async Task<bool> AddRelayAsync(string relayUrl, bool autoConnect = true)
        {
            try
            {
                if (string.IsNullOrEmpty(relayUrl))
                    throw new ArgumentException("Relay URL cannot be null or empty", nameof(relayUrl));
                
                // Check if the relay already exists
                if (_relays.ContainsKey(relayUrl))
                {
                    NostrErrorHandler.HandleError($"Relay {relayUrl} is already added", "NostrRelayManager.AddRelayAsync", NostrErrorHandler.NostrErrorSeverity.Warning);
                    return false;
                }
                
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
                    await relay.ConnectAsync();
                }
                
                NostrErrorHandler.HandleError($"Added relay: {relayUrl}", "NostrRelayManager.AddRelayAsync", NostrErrorHandler.NostrErrorSeverity.Info);
                return true;
            }
            catch (Exception ex)
            {
                NostrErrorHandler.HandleError(ex, "NostrRelayManager.AddRelayAsync", NostrErrorHandler.NostrErrorSeverity.Error);
                return false;
            }
        }
        
        /// <summary>
        /// Removes a relay connection
        /// </summary>
        /// <param name="relayUrl">The URL of the relay to remove</param>
        /// <returns>True if the relay was removed, false if it wasn't found</returns>
        public async Task<bool> RemoveRelayAsync(string relayUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(relayUrl))
                    throw new ArgumentException("Relay URL cannot be null or empty", nameof(relayUrl));
                
                if (!_relays.TryGetValue(relayUrl, out NostrRelay relay))
                {
                    NostrErrorHandler.HandleError($"Relay {relayUrl} not found", "NostrRelayManager.RemoveRelayAsync", NostrErrorHandler.NostrErrorSeverity.Warning);
                    return false;
                }
                
                // Disconnect and remove the relay
                await relay.DisconnectAsync();
                _relays.Remove(relayUrl);
                
                NostrErrorHandler.HandleError($"Removed relay: {relayUrl}", "NostrRelayManager.RemoveRelayAsync", NostrErrorHandler.NostrErrorSeverity.Info);
                return true;
            }
            catch (Exception ex)
            {
                NostrErrorHandler.HandleError(ex, "NostrRelayManager.RemoveRelayAsync", NostrErrorHandler.NostrErrorSeverity.Error);
                return false;
            }
        }
        
        /// <summary>
        /// Connects to all relays
        /// </summary>
        public async Task ConnectAllAsync()
        {
            var tasks = new List<Task>();
            foreach (var relay in _relays.Values)
            {
                tasks.Add(relay.ConnectAsync());
            }
            
            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                NostrErrorHandler.HandleError(ex, "NostrRelayManager.ConnectAllAsync", NostrErrorHandler.NostrErrorSeverity.Error);
                throw;
            }
        }
        
        /// <summary>
        /// Disconnects from all relays
        /// </summary>
        public async Task DisconnectAllAsync()
        {
            var tasks = new List<Task>();
            foreach (var relay in _relays.Values)
            {
                tasks.Add(relay.DisconnectAsync());
            }
            
            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                NostrErrorHandler.HandleError(ex, "NostrRelayManager.DisconnectAllAsync", NostrErrorHandler.NostrErrorSeverity.Error);
                // Don't rethrow here to allow cleanup to continue
            }
        }
        
        /// <summary>
        /// Publishes an event to all connected relays
        /// </summary>
        /// <param name="nostrEvent">The event to publish</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task PublishEventAsync(NostrEvent nostrEvent)
        {
            try
            {
                if (nostrEvent == null)
                    throw new ArgumentNullException(nameof(nostrEvent), "Event cannot be null");
                
                if (_relays.Count == 0)
                {
                    NostrErrorHandler.HandleError("No relays connected to publish event", "NostrRelayManager.PublishEventAsync", NostrErrorHandler.NostrErrorSeverity.Warning);
                    return;
                }
                
                var tasks = new List<Task>();
                foreach (var relay in _relays.Values)
                {
                    tasks.Add(relay.PublishEventAsync(nostrEvent));
                }
                
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                NostrErrorHandler.HandleError(ex, "NostrRelayManager.PublishEventAsync", NostrErrorHandler.NostrErrorSeverity.Error);
                throw;
            }
        }
        
        /// <summary>
        /// Subscribes to events matching the provided filter on all connected relays
        /// </summary>
        /// <param name="subscriptionId">The ID for this subscription</param>
        /// <param name="filter">The filter to apply</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task SubscribeAsync(string subscriptionId, Filter filter)
        {
            try
            {
                if (string.IsNullOrEmpty(subscriptionId))
                    throw new ArgumentException("Subscription ID cannot be null or empty", nameof(subscriptionId));
                
                if (filter == null)
                    throw new ArgumentNullException(nameof(filter), "Filter cannot be null");
                
                if (_relays.Count == 0)
                {
                    NostrErrorHandler.HandleError("No relays connected to subscribe", "NostrRelayManager.SubscribeAsync", NostrErrorHandler.NostrErrorSeverity.Warning);
                    return;
                }
                
                _activeSubscriptions.Add(subscriptionId);
                
                var tasks = new List<Task>();
                foreach (var relay in _relays.Values)
                {
                    tasks.Add(relay.SubscribeAsync(subscriptionId, filter));
                }
                
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                NostrErrorHandler.HandleError(ex, "NostrRelayManager.SubscribeAsync", NostrErrorHandler.NostrErrorSeverity.Error);
                throw;
            }
        }
        
        /// <summary>
        /// Unsubscribes from a subscription on all connected relays
        /// </summary>
        /// <param name="subscriptionId">The subscription ID to unsubscribe from</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task UnsubscribeAsync(string subscriptionId)
        {
            try
            {
                if (string.IsNullOrEmpty(subscriptionId))
                    throw new ArgumentException("Subscription ID cannot be null or empty", nameof(subscriptionId));
                
                if (_relays.Count == 0)
                {
                    NostrErrorHandler.HandleError("No relays connected to unsubscribe", "NostrRelayManager.UnsubscribeAsync", NostrErrorHandler.NostrErrorSeverity.Warning);
                    return;
                }
                
                _activeSubscriptions.Remove(subscriptionId);
                
                var tasks = new List<Task>();
                foreach (var relay in _relays.Values)
                {
                    tasks.Add(relay.UnsubscribeAsync(subscriptionId));
                }
                
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                NostrErrorHandler.HandleError(ex, "NostrRelayManager.UnsubscribeAsync", NostrErrorHandler.NostrErrorSeverity.Error);
                throw;
            }
        }
        
        #endregion
        
        #region Synchronous Methods
        
        /// <summary>
        /// Gets a list of all relay URLs
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
                NostrErrorHandler.HandleError($"Relay {relayUrl} not found", "NostrRelayManager.GetRelayState", NostrErrorHandler.NostrErrorSeverity.Warning);
                return RelayState.Disconnected;
            }
            
            return relay.State;
        }
        
        /// <summary>
        /// Gets a list of active subscription IDs
        /// </summary>
        /// <returns>Array of subscription IDs</returns>
        public string[] GetActiveSubscriptions()
        {
            return _activeSubscriptions.ToArray();
        }
        
        /// <summary>
        /// Adds a new relay connection (synchronous version)
        /// </summary>
        /// <param name="relayUrl">The URL of the relay (wss://...)</param>
        /// <returns>True if the relay was added, false if it already exists</returns>
        public bool AddRelay(string relayUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(relayUrl))
                    return false;
                
                // Check if the relay already exists
                if (_relays.ContainsKey(relayUrl))
                    return false;
                
                // Create and add the relay
                var relay = new NostrRelay(relayUrl);
                
                // Set up event handlers
                relay.OnEventReceived += (e) => OnEventReceived?.Invoke(e, relayUrl);
                relay.OnStateChanged += (state) => HandleRelayStateChanged(relayUrl, state);
                relay.OnError += (error) => OnRelayError?.Invoke(relayUrl, error);
                
                _relays.Add(relayUrl, relay);
                
                // Connect immediately
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
        /// Removes a relay connection (synchronous version)
        /// </summary>
        /// <param name="relayUrl">The URL of the relay to remove</param>
        /// <returns>True if the relay was removed, false if it wasn't found</returns>
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
        /// Connects to all relays (synchronous version)
        /// </summary>
        public void ConnectAll()
        {
            foreach (var relay in _relays.Values)
            {
                relay.Connect();
            }
        }
        
        /// <summary>
        /// Publishes an event to all connected relays (synchronous version)
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
        /// Subscribes to events matching the provided filter (synchronous version)
        /// </summary>
        /// <param name="subscriptionId">The ID for this subscription</param>
        /// <param name="filter">The filter to apply</param>
        public void Subscribe(string subscriptionId, Filter filter)
        {
            if (string.IsNullOrEmpty(subscriptionId))
                throw new ArgumentException("Subscription ID cannot be null or empty", nameof(subscriptionId));
            
            if (filter == null)
                throw new ArgumentNullException(nameof(filter));
            
            _activeSubscriptions.Add(subscriptionId);
            
            foreach (var relay in _relays.Values)
            {
                relay.Subscribe(subscriptionId, filter);
            }
        }
        
        /// <summary>
        /// Unsubscribes from a subscription (synchronous version)
        /// </summary>
        /// <param name="subscriptionId">The subscription ID to unsubscribe from</param>
        public void Unsubscribe(string subscriptionId)
        {
            if (string.IsNullOrEmpty(subscriptionId))
                throw new ArgumentException("Subscription ID cannot be null or empty", nameof(subscriptionId));
            
            _activeSubscriptions.Remove(subscriptionId);
            
            foreach (var relay in _relays.Values)
            {
                relay.Unsubscribe(subscriptionId);
            }
        }
        
        /// <summary>
        /// Gets a list of connected relay URLs
        /// </summary>
        public List<string> GetConnectedRelays()
        {
            return new List<string>(_relays.Keys);
        }
        
        #endregion
        
        #region Event Handlers
        
        /// <summary>
        /// Handles relay state change events
        /// </summary>
        private void HandleRelayStateChanged(string relayUrl, RelayState state)
        {
            try
            {
                OnRelayStateChanged?.Invoke(relayUrl, state);
                
                if (state == RelayState.Connected)
                {
                    // Resubscribe to active subscriptions when a relay connects
                    if (_relays.TryGetValue(relayUrl, out NostrRelay relay))
                    {
                        foreach (string subscriptionId in _activeSubscriptions)
                        {
                            // We don't have the filters here, so we'd need to store them
                            // This is left as a future improvement
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                NostrErrorHandler.HandleError(ex, "NostrRelayManager.HandleRelayStateChanged", NostrErrorHandler.NostrErrorSeverity.Error);
            }
        }
        
        #endregion
    }
} 