using System;
using System.Collections.Generic;

namespace NostrUnity.Models
{
    /// <summary>
    /// Represents a subscription to Nostr events
    /// </summary>
    public class NostrSubscription
    {
        /// <summary>
        /// Gets the unique ID for this subscription
        /// </summary>
        public string Id { get; }
        
        /// <summary>
        /// Gets the filter for this subscription
        /// </summary>
        public Filter Filter { get; }
        
        /// <summary>
        /// Gets the time when the subscription was created
        /// </summary>
        public DateTime CreatedAt { get; }
        
        /// <summary>
        /// Gets or sets whether the subscription has received the EOSE (End of Stored Events) message
        /// </summary>
        public bool ReceivedEose { get; set; }
        
        /// <summary>
        /// Gets or sets the list of relay URLs this subscription is active on
        /// </summary>
        public List<string> ActiveRelays { get; }
        
        /// <summary>
        /// Gets or sets the callback for events matching this subscription
        /// </summary>
        public Action<NostrEvent, string> Callback { get; set; }
        
        /// <summary>
        /// Gets or sets an optional tag to categorize this subscription
        /// </summary>
        public string Tag { get; set; }
        
        /// <summary>
        /// Creates a new subscription with the specified ID and filter
        /// </summary>
        /// <param name="id">The unique subscription ID</param>
        /// <param name="filter">The filter to apply</param>
        /// <param name="callback">Optional callback for matching events</param>
        public NostrSubscription(string id, Filter filter, Action<NostrEvent, string> callback = null)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("Subscription ID cannot be null or empty", nameof(id));
            
            if (filter == null)
                throw new ArgumentNullException(nameof(filter), "Filter cannot be null");
            
            Id = id;
            Filter = filter;
            Callback = callback;
            CreatedAt = DateTime.UtcNow;
            ReceivedEose = false;
            ActiveRelays = new List<string>();
        }
        
        /// <summary>
        /// Determines if a relay is active for this subscription
        /// </summary>
        /// <param name="relayUrl">The relay URL to check</param>
        /// <returns>True if the relay is active, false otherwise</returns>
        public bool IsActiveOnRelay(string relayUrl)
        {
            if (string.IsNullOrEmpty(relayUrl))
                throw new ArgumentException("Relay URL cannot be null or empty", nameof(relayUrl));
            
            return ActiveRelays.Contains(relayUrl);
        }
        
        /// <summary>
        /// Adds a relay to the list of active relays
        /// </summary>
        /// <param name="relayUrl">The relay URL to add</param>
        public void AddRelay(string relayUrl)
        {
            if (string.IsNullOrEmpty(relayUrl))
                throw new ArgumentException("Relay URL cannot be null or empty", nameof(relayUrl));
            
            if (!ActiveRelays.Contains(relayUrl))
            {
                ActiveRelays.Add(relayUrl);
            }
        }
        
        /// <summary>
        /// Removes a relay from the list of active relays
        /// </summary>
        /// <param name="relayUrl">The relay URL to remove</param>
        /// <returns>True if the relay was removed, false if it wasn't found</returns>
        public bool RemoveRelay(string relayUrl)
        {
            if (string.IsNullOrEmpty(relayUrl))
                throw new ArgumentException("Relay URL cannot be null or empty", nameof(relayUrl));
            
            return ActiveRelays.Remove(relayUrl);
        }
        
        /// <summary>
        /// Processes an event and invokes the callback if it matches the filter
        /// </summary>
        /// <param name="event">The event to process</param>
        /// <param name="relayUrl">The relay URL the event came from</param>
        public void ProcessEvent(NostrEvent @event, string relayUrl)
        {
            if (@event == null)
                throw new ArgumentNullException(nameof(@event), "Event cannot be null");
            
            if (string.IsNullOrEmpty(relayUrl))
                throw new ArgumentException("Relay URL cannot be null or empty", nameof(relayUrl));
            
            // We assume the event matches the filter (this should be checked before calling this method)
            Callback?.Invoke(@event, relayUrl);
        }
        
        /// <summary>
        /// Handles the receipt of an EOSE (End of Stored Events) message from a relay
        /// </summary>
        /// <param name="relayUrl">The relay URL that sent the EOSE message</param>
        public void HandleEose(string relayUrl)
        {
            if (string.IsNullOrEmpty(relayUrl))
                throw new ArgumentException("Relay URL cannot be null or empty", nameof(relayUrl));
            
            // Mark that we've received at least one EOSE message
            ReceivedEose = true;
        }
        
        /// <summary>
        /// Generates a default subscription ID
        /// </summary>
        /// <returns>A unique subscription ID</returns>
        public static string GenerateId()
        {
            return $"sub_{Guid.NewGuid().ToString().Substring(0, 8)}";
        }
        
        /// <summary>
        /// Checks if an event matches a filter
        /// </summary>
        /// <param name="event">The event to check</param>
        /// <param name="filter">The filter to check against</param>
        /// <returns>True if the event matches the filter, false otherwise</returns>
        public static bool EventMatchesFilter(NostrEvent @event, Filter filter)
        {
            if (@event == null || filter == null)
                return false;
            
            // Check IDs if specified
            if (filter.Ids != null && filter.Ids.Length > 0)
            {
                bool idMatched = false;
                foreach (var id in filter.Ids)
                {
                    if (id.Equals(@event.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        idMatched = true;
                        break;
                    }
                }
                
                if (!idMatched)
                    return false;
            }
            
            // Check authors if specified
            if (filter.Authors != null && filter.Authors.Length > 0)
            {
                bool authorMatched = false;
                foreach (var author in filter.Authors)
                {
                    if (author.Equals(@event.Pubkey, StringComparison.OrdinalIgnoreCase))
                    {
                        authorMatched = true;
                        break;
                    }
                }
                
                if (!authorMatched)
                    return false;
            }
            
            // Check kinds if specified
            if (filter.Kinds != null && filter.Kinds.Length > 0)
            {
                bool kindMatched = false;
                foreach (var kind in filter.Kinds)
                {
                    if (kind == @event.Kind)
                    {
                        kindMatched = true;
                        break;
                    }
                }
                
                if (!kindMatched)
                    return false;
            }
            
            // Check 'since' timestamp if specified
            if (filter.Since.HasValue && @event.CreatedAt < filter.Since.Value)
                return false;
            
            // Check 'until' timestamp if specified
            if (filter.Until.HasValue && @event.CreatedAt > filter.Until.Value)
                return false;
            
            // Check event tags (#e) if specified
            if (filter.EventTags != null && filter.EventTags.Length > 0)
            {
                bool eTagMatched = false;
                
                if (@event.Tags != null)
                {
                    foreach (var tag in @event.Tags)
                    {
                        if (tag.Length > 1 && tag[0] == "e")
                        {
                            foreach (var eventTag in filter.EventTags)
                            {
                                if (eventTag.Equals(tag[1], StringComparison.OrdinalIgnoreCase))
                                {
                                    eTagMatched = true;
                                    break;
                                }
                            }
                            
                            if (eTagMatched)
                                break;
                        }
                    }
                }
                
                if (!eTagMatched)
                    return false;
            }
            
            // Check pubkey tags (#p) if specified
            if (filter.PubkeyTags != null && filter.PubkeyTags.Length > 0)
            {
                bool pTagMatched = false;
                
                if (@event.Tags != null)
                {
                    foreach (var tag in @event.Tags)
                    {
                        if (tag.Length > 1 && tag[0] == "p")
                        {
                            foreach (var pubkeyTag in filter.PubkeyTags)
                            {
                                if (pubkeyTag.Equals(tag[1], StringComparison.OrdinalIgnoreCase))
                                {
                                    pTagMatched = true;
                                    break;
                                }
                            }
                            
                            if (pTagMatched)
                                break;
                        }
                    }
                }
                
                if (!pTagMatched)
                    return false;
            }
            
            // All conditions passed
            return true;
        }
    }
} 