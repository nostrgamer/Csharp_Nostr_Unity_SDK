using System;
using System.Text;
using System.Security.Cryptography;
using UnityEngine;
using NostrUnity.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NostrUnity.Protocol
{
    /// <summary>
    /// Handles serialization of Nostr events according to the NIP-01 specification
    /// </summary>
    public static class NostrSerializer
    {
        private static readonly JsonSerializerOptions DefaultOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        
        /// <summary>
        /// Serializes an event for ID computation according to NIP-01
        /// </summary>
        /// <param name="event">The event to serialize</param>
        /// <returns>The serialized event string</returns>
        public static string SerializeForId(NostrEvent @event)
        {
            if (@event == null)
                throw new ArgumentNullException(nameof(@event), "Event cannot be null");
            
            // Create the serialization array with fixed order:
            // [0, pubkey, created_at, kind, tags, content]
            var array = new object[] 
            { 
                0, 
                @event.Pubkey, 
                @event.CreatedAt, 
                @event.Kind, 
                @event.Tags ?? Array.Empty<string[]>(), 
                @event.Content 
            };
            
            // Use System.Text.Json for standardized serialization
            string serialized = JsonSerializer.Serialize(array);
            Debug.Log($"[SERIALIZER] Serialized event for ID: {serialized}");
            
            return serialized;
        }
        
        /// <summary>
        /// Computes the event ID from the serialized event
        /// </summary>
        /// <param name="serializedEvent">The serialized event string</param>
        /// <returns>The event ID (32-byte hex-encoded string)</returns>
        public static string ComputeId(string serializedEvent)
        {
            if (string.IsNullOrEmpty(serializedEvent))
                throw new ArgumentException("Serialized event cannot be null or empty", nameof(serializedEvent));
            
            using var sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(serializedEvent));
            string id = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            
            Debug.Log($"[SERIALIZER] Computed event ID: {id}");
            return id;
        }
        
        /// <summary>
        /// Serializes a complete event including ID and signature
        /// </summary>
        /// <param name="event">The event to serialize</param>
        /// <returns>The complete serialized event</returns>
        public static string SerializeComplete(NostrEvent @event)
        {
            if (@event == null)
                throw new ArgumentNullException(nameof(@event), "Event cannot be null");
            
            // Create a strongly-typed object to ensure proper order
            var completeEvent = new
            {
                id = @event.Id?.ToLowerInvariant(),
                pubkey = @event.Pubkey?.ToLowerInvariant(),
                created_at = @event.CreatedAt,
                kind = @event.Kind,
                tags = @event.Tags,
                content = @event.Content,
                sig = @event.Sig?.ToLowerInvariant()
            };
            
            // Use settings matching Nostr relay expectations
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            string serialized = JsonSerializer.Serialize(completeEvent, options);
            Debug.Log($"[SERIALIZER] Serialized complete event: {serialized}");
            
            return serialized;
        }
        
        /// <summary>
        /// Serializes an event to be sent as part of a relay message
        /// </summary>
        /// <param name="event">The event to serialize</param>
        /// <returns>The serialized event string</returns>
        public static string SerializeForRelay(NostrEvent @event)
        {
            if (@event == null)
                throw new ArgumentNullException(nameof(@event), "Event cannot be null");
            
            // Use System.Text.Json with appropriate settings
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            
            string serialized = JsonSerializer.Serialize(@event, options);
            Debug.Log($"[SERIALIZER] Serialized event for relay: {serialized}");
            
            return serialized;
        }
        
        /// <summary>
        /// Creates a relay message for publishing an event
        /// </summary>
        /// <param name="event">The event to publish</param>
        /// <returns>The complete relay message</returns>
        public static string CreatePublishMessage(NostrEvent @event)
        {
            if (@event == null)
                throw new ArgumentNullException(nameof(@event), "Event cannot be null");
            
            string eventJson = SerializeForRelay(@event);
            string message = JsonSerializer.Serialize(new object[] { "EVENT", eventJson });
            
            return message;
        }
        
        /// <summary>
        /// Creates a relay message for subscribing to events
        /// </summary>
        /// <param name="subscriptionId">The subscription ID</param>
        /// <param name="filter">The filter to apply</param>
        /// <returns>The complete relay message</returns>
        public static string CreateSubscribeMessage(string subscriptionId, Filter filter)
        {
            if (string.IsNullOrEmpty(subscriptionId))
                throw new ArgumentException("Subscription ID cannot be null or empty", nameof(subscriptionId));
            
            if (filter == null)
                throw new ArgumentNullException(nameof(filter), "Filter cannot be null");
            
            var options = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            
            string filterJson = JsonSerializer.Serialize(filter, options);
            string message = JsonSerializer.Serialize(new object[] { "REQ", subscriptionId, filterJson });
            
            return message;
        }
        
        /// <summary>
        /// Creates a relay message for unsubscribing from events
        /// </summary>
        /// <param name="subscriptionId">The subscription ID to close</param>
        /// <returns>The complete relay message</returns>
        public static string CreateUnsubscribeMessage(string subscriptionId)
        {
            if (string.IsNullOrEmpty(subscriptionId))
                throw new ArgumentException("Subscription ID cannot be null or empty", nameof(subscriptionId));
            
            string message = JsonSerializer.Serialize(new object[] { "CLOSE", subscriptionId });
            return message;
        }
        
        /// <summary>
        /// Parses a relay message into its components
        /// </summary>
        /// <param name="message">The relay message to parse</param>
        /// <returns>The message parts as a JsonDocument</returns>
        public static JsonDocument ParseRelayMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                throw new ArgumentException("Message cannot be null or empty", nameof(message));
            
            try
            {
                return JsonDocument.Parse(message);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SERIALIZER] Error parsing relay message: {ex.Message}");
                throw new FormatException($"Invalid relay message format: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Serializes a request message
        /// </summary>
        public static string SerializeRequest(string type, string subscriptionId, Filter filter)
        {
            var message = new object[] { type, subscriptionId, filter };
            return JsonSerializer.Serialize(message, DefaultOptions);
        }

        /// <summary>
        /// Serializes an event message
        /// </summary>
        public static string SerializeEvent(string type, NostrEvent nostrEvent)
        {
            var message = new object[] { type, nostrEvent };
            return JsonSerializer.Serialize(message, DefaultOptions);
        }

        /// <summary>
        /// Deserializes a message from a relay
        /// </summary>
        public static (string type, string subscriptionId, NostrEvent nostrEvent) DeserializeMessage(string json)
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            
            string type = root[0].GetString();
            string subscriptionId = root[1].GetString();
            NostrEvent nostrEvent = null;
            
            if (root.GetArrayLength() > 2)
            {
                nostrEvent = JsonSerializer.Deserialize<NostrEvent>(root[2].GetRawText(), DefaultOptions);
            }
            
            return (type, subscriptionId, nostrEvent);
        }
    }
} 