using System;
using NostrUnity.Crypto;
using NostrUnity.Utils;
using UnityEngine;

namespace NostrUnity.Utils
{
    /// <summary>
    /// Utility class for NIP-19 formatted Nostr identifiers
    /// </summary>
    public static class NIP19Formatter
    {
        /// <summary>
        /// Converts a hex-encoded event ID to NIP-19 format (note1...)
        /// </summary>
        /// <param name="hexEventId">The event ID in hex format</param>
        /// <returns>The event ID in NIP-19 format (note1...)</returns>
        public static string EventToNote(string hexEventId)
        {
            if (string.IsNullOrEmpty(hexEventId))
                throw new ArgumentException("Event ID cannot be null or empty", nameof(hexEventId));
            
            try
            {
                // Convert hex to bytes
                byte[] eventIdBytes = CryptoUtils.HexToBytes(hexEventId);
                
                // Encode using Bech32 with prefix "note"
                return Bech32.Encode("note", eventIdBytes);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error converting event ID to note: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Gets a URL for viewing an event on common Nostr clients
        /// </summary>
        /// <param name="eventId">The event ID in hex format</param>
        /// <param name="client">The client to generate a link for</param>
        /// <returns>A URL to view the event</returns>
        public static string GetEventUrl(string eventId, NostrWebClient client = NostrWebClient.Snort)
        {
            if (string.IsNullOrEmpty(eventId))
                throw new ArgumentException("Event ID cannot be null or empty", nameof(eventId));
                
            string noteId = EventToNote(eventId);
            
            return client switch
            {
                NostrWebClient.Snort => $"https://snort.social/e/{noteId}",
                NostrWebClient.Iris => $"https://iris.to/post/{noteId}",
                NostrWebClient.Primal => $"https://primal.net/e/{noteId}",
                NostrWebClient.Coracle => $"https://coracle.social/e/{noteId}",
                _ => $"https://nostr.band/note/{noteId}"
            };
        }
    }
    
    /// <summary>
    /// Enumeration of common Nostr web clients
    /// </summary>
    public enum NostrWebClient
    {
        Snort,
        Iris,
        Primal,
        Coracle,
        NostrBand
    }
} 