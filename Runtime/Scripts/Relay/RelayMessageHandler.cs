using System;
using UnityEngine;
using System.Text.Json;
using NostrUnity.Models;
using NostrUnity.Protocol;

namespace NostrUnity.Relay
{
    /// <summary>
    /// Handles parsing and processing of Nostr relay messages
    /// </summary>
    public static class RelayMessageHandler
    {
        /// <summary>
        /// Represents the different types of Nostr relay messages
        /// </summary>
        public enum MessageType
        {
            Unknown,
            Event,
            Notice,
            EndOfStoredEvents,
            Ok,
            AuthChallenge,
            AuthResponse
        }
        
        /// <summary>
        /// Parses a relay message and determines its type
        /// </summary>
        /// <param name="message">The JSON message from the relay</param>
        /// <returns>The message type</returns>
        public static MessageType GetMessageType(string message)
        {
            if (string.IsNullOrEmpty(message))
                return MessageType.Unknown;
            
            try
            {
                using var jsonDoc = JsonDocument.Parse(message);
                var root = jsonDoc.RootElement;
                
                if (root.GetArrayLength() < 1)
                    return MessageType.Unknown;
                
                string type = root[0].GetString();
                
                switch (type)
                {
                    case "EVENT":
                        return MessageType.Event;
                    case "NOTICE":
                        return MessageType.Notice;
                    case "EOSE":
                        return MessageType.EndOfStoredEvents;
                    case "OK":
                        return MessageType.Ok;
                    case "AUTH":
                        return MessageType.AuthChallenge;
                    case "AUTH_RESPONSE":
                        return MessageType.AuthResponse;
                    default:
                        return MessageType.Unknown;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error parsing relay message: {ex.Message}");
                return MessageType.Unknown;
            }
        }
        
        /// <summary>
        /// Processes an EVENT message from a relay
        /// </summary>
        /// <param name="message">The relay message</param>
        /// <param name="subscriptionId">Output parameter for the subscription ID</param>
        /// <param name="event">Output parameter for the parsed event</param>
        /// <returns>True if successful, false otherwise</returns>
        public static bool ProcessEventMessage(string message, out string subscriptionId, out NostrEvent @event)
        {
            subscriptionId = null;
            @event = null;
            
            try
            {
                using var jsonDoc = JsonDocument.Parse(message);
                var root = jsonDoc.RootElement;
                
                if (root.GetArrayLength() < 3 || root[0].GetString() != "EVENT")
                {
                    Debug.LogWarning("Invalid EVENT message format");
                    return false;
                }
                
                subscriptionId = root[1].GetString();
                string eventJson = root[2].GetRawText();
                
                // Parse the event JSON
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                @event = JsonSerializer.Deserialize<NostrEvent>(eventJson, options);
                
                // Validate the event
                ValidationResult validationResult = NostrValidator.ValidateEvent(@event);
                if (!validationResult.IsValid)
                {
                    Debug.LogWarning($"Received invalid event: {validationResult.Message}");
                    return false;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error processing EVENT message: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Processes a NOTICE message from a relay
        /// </summary>
        /// <param name="message">The relay message</param>
        /// <param name="notice">Output parameter for the notice text</param>
        /// <returns>True if successful, false otherwise</returns>
        public static bool ProcessNoticeMessage(string message, out string notice)
        {
            notice = null;
            
            try
            {
                using var jsonDoc = JsonDocument.Parse(message);
                var root = jsonDoc.RootElement;
                
                if (root.GetArrayLength() < 2 || root[0].GetString() != "NOTICE")
                {
                    Debug.LogWarning("Invalid NOTICE message format");
                    return false;
                }
                
                notice = root[1].GetString();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error processing NOTICE message: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Processes an EOSE (End of Stored Events) message from a relay
        /// </summary>
        /// <param name="message">The relay message</param>
        /// <param name="subscriptionId">Output parameter for the subscription ID</param>
        /// <returns>True if successful, false otherwise</returns>
        public static bool ProcessEndOfStoredEventsMessage(string message, out string subscriptionId)
        {
            subscriptionId = null;
            
            try
            {
                using var jsonDoc = JsonDocument.Parse(message);
                var root = jsonDoc.RootElement;
                
                if (root.GetArrayLength() < 2 || root[0].GetString() != "EOSE")
                {
                    Debug.LogWarning("Invalid EOSE message format");
                    return false;
                }
                
                subscriptionId = root[1].GetString();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error processing EOSE message: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Processes an OK message (publish result) from a relay
        /// </summary>
        /// <param name="message">The relay message</param>
        /// <param name="eventId">Output parameter for the event ID</param>
        /// <param name="success">Output parameter indicating success/failure</param>
        /// <param name="reason">Output parameter for the success/failure reason</param>
        /// <returns>True if successfully parsed, false otherwise</returns>
        public static bool ProcessOkMessage(string message, out string eventId, out bool success, out string reason)
        {
            eventId = null;
            success = false;
            reason = null;
            
            try
            {
                using var jsonDoc = JsonDocument.Parse(message);
                var root = jsonDoc.RootElement;
                
                if (root.GetArrayLength() < 3 || root[0].GetString() != "OK")
                {
                    Debug.LogWarning("Invalid OK message format");
                    return false;
                }
                
                eventId = root[1].GetString();
                success = root[2].GetBoolean();
                
                if (root.GetArrayLength() > 3)
                {
                    reason = root[3].GetString();
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error processing OK message: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Creates a relay message for publishing an event
        /// </summary>
        /// <param name="event">The event to publish</param>
        /// <returns>The formatted relay message</returns>
        public static string CreateEventMessage(NostrEvent @event)
        {
            if (@event == null)
                throw new ArgumentNullException(nameof(@event), "Event cannot be null");
            
            return NostrSerializer.CreatePublishMessage(@event);
        }
        
        /// <summary>
        /// Creates a relay message for subscribing to events
        /// </summary>
        /// <param name="subscriptionId">The subscription ID</param>
        /// <param name="filter">The filter to apply</param>
        /// <returns>The formatted relay message</returns>
        public static string CreateSubscribeMessage(string subscriptionId, Filter filter)
        {
            if (string.IsNullOrEmpty(subscriptionId))
                throw new ArgumentException("Subscription ID cannot be null or empty", nameof(subscriptionId));
            
            if (filter == null)
                throw new ArgumentNullException(nameof(filter), "Filter cannot be null");
                
            return NostrSerializer.CreateSubscribeMessage(subscriptionId, filter);
        }
        
        /// <summary>
        /// Creates a relay message for unsubscribing from events
        /// </summary>
        /// <param name="subscriptionId">The subscription ID to unsubscribe</param>
        /// <returns>The formatted relay message</returns>
        public static string CreateUnsubscribeMessage(string subscriptionId)
        {
            if (string.IsNullOrEmpty(subscriptionId))
                throw new ArgumentException("Subscription ID cannot be null or empty", nameof(subscriptionId));
                
            return NostrSerializer.CreateUnsubscribeMessage(subscriptionId);
        }
    }
} 