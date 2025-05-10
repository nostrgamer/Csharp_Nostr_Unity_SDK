using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using NostrUnity.Models;
using NostrUnity.Crypto;

namespace NostrUnity.Protocol
{
    /// <summary>
    /// Provides validation for Nostr events according to NIP-01
    /// </summary>
    public static class NostrValidator
    {
        // Maximum event content length in bytes (NIP-01 defaults to 64 KB)
        private const int MaxContentLength = 64 * 1024;
        
        // Regex for validating hex strings
        private static readonly Regex HexRegex = new Regex("^[0-9a-fA-F]+$", RegexOptions.Compiled);
        
        /// <summary>
        /// Validates a Nostr event according to NIP-01
        /// </summary>
        /// <param name="event">The event to validate</param>
        /// <returns>A ValidationResult object containing the result and any error messages</returns>
        public static ValidationResult ValidateEvent(NostrEvent @event)
        {
            if (@event == null)
                return new ValidationResult(false, "Event cannot be null");
            
            // Check required fields
            if (string.IsNullOrEmpty(@event.Id))
                return new ValidationResult(false, "Event ID is missing");
            
            if (string.IsNullOrEmpty(@event.Pubkey))
                return new ValidationResult(false, "Public key is missing");
            
            if (string.IsNullOrEmpty(@event.Sig))
                return new ValidationResult(false, "Signature is missing");
            
            if (@event.CreatedAt <= 0)
                return new ValidationResult(false, "Invalid creation timestamp");
            
            // Validate field formats
            if (!IsValidHex(@event.Id) || @event.Id.Length != 64)
                return new ValidationResult(false, "Invalid event ID format - must be 64 hex characters");
            
            if (!IsValidHex(@event.Pubkey) || @event.Pubkey.Length != 64)
                return new ValidationResult(false, "Invalid public key format - must be 64 hex characters");
            
            if (!IsValidHex(@event.Sig) || @event.Sig.Length != 128)
                return new ValidationResult(false, "Invalid signature format - must be 128 hex characters");
            
            // Validate content length
            if (@event.Content != null && System.Text.Encoding.UTF8.GetByteCount(@event.Content) > MaxContentLength)
                return new ValidationResult(false, $"Content exceeds maximum length of {MaxContentLength} bytes");
            
            // Validate tags
            if (@event.Tags != null)
            {
                foreach (var tag in @event.Tags)
                {
                    if (tag.Length == 0)
                        return new ValidationResult(false, "Empty tag array found");
                    
                    // Tag name should be a single letter or short string
                    if (string.IsNullOrEmpty(tag[0]))
                        return new ValidationResult(false, "Tag name is missing");
                }
            }
            
            // Validate the event ID matches the serialized content
            string serialized = NostrSerializer.SerializeForId(@event);
            string computedId = NostrSerializer.ComputeId(serialized);
            
            if (!string.Equals(computedId, @event.Id, StringComparison.OrdinalIgnoreCase))
                return new ValidationResult(false, $"Event ID does not match serialized content (Expected: {@event.Id}, Computed: {computedId})");
            
            // Deep signature validation not performed here - that's handled by VerifySignature
            
            return new ValidationResult(true, "Event is valid");
        }
        
        /// <summary>
        /// Verifies the cryptographic signature of a Nostr event
        /// </summary>
        /// <param name="event">The event to verify</param>
        /// <returns>A ValidationResult object containing the result and any error messages</returns>
        public static ValidationResult VerifySignature(NostrEvent @event)
        {
            if (@event == null)
                return new ValidationResult(false, "Event cannot be null");
            
            if (string.IsNullOrEmpty(@event.Id))
                return new ValidationResult(false, "Event ID is missing");
            
            if (string.IsNullOrEmpty(@event.Pubkey))
                return new ValidationResult(false, "Public key is missing");
            
            if (string.IsNullOrEmpty(@event.Sig))
                return new ValidationResult(false, "Signature is missing");
            
            try
            {
                // Try with both key prefixes if no compressed key is available
                if (string.IsNullOrEmpty(@event.CompressedPublicKey))
                {
                    // Try with 02 prefix
                    string pubKey02 = "02" + @event.Pubkey;
                    bool result02 = KeyPair.VerifySignature(pubKey02, @event.Id, @event.Sig);
                    
                    if (result02)
                    {
                        Debug.Log($"[VALIDATOR] Signature verification successful with 02 prefix");
                        return new ValidationResult(true, "Signature is valid");
                    }
                    
                    // Try with 03 prefix
                    string pubKey03 = "03" + @event.Pubkey;
                    bool result03 = KeyPair.VerifySignature(pubKey03, @event.Id, @event.Sig);
                    
                    if (result03)
                    {
                        Debug.Log($"[VALIDATOR] Signature verification successful with 03 prefix");
                        return new ValidationResult(true, "Signature is valid");
                    }
                    
                    return new ValidationResult(false, "Signature verification failed with both key prefixes");
                }
                else
                {
                    // Use the stored compressed key
                    bool result = KeyPair.VerifySignature(@event.CompressedPublicKey, @event.Id, @event.Sig);
                    
                    if (result)
                    {
                        Debug.Log($"[VALIDATOR] Signature verification successful with compressed key");
                        return new ValidationResult(true, "Signature is valid");
                    }
                    
                    return new ValidationResult(false, "Signature verification failed with compressed key");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VALIDATOR] Signature verification error: {ex.Message}");
                return new ValidationResult(false, $"Signature verification error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Performs comprehensive validation including NIP-01 rules and cryptographic signature
        /// </summary>
        /// <param name="event">The event to validate</param>
        /// <returns>A ValidationResult object containing the result and any error messages</returns>
        public static ValidationResult ValidateEventComplete(NostrEvent @event)
        {
            // First validate basic structure
            ValidationResult structureResult = ValidateEvent(@event);
            if (!structureResult.IsValid)
                return structureResult;
            
            // Then verify signature
            ValidationResult signatureResult = VerifySignature(@event);
            if (!signatureResult.IsValid)
                return signatureResult;
            
            return new ValidationResult(true, "Event is valid and signature verified");
        }
        
        /// <summary>
        /// Validates a subscription filter
        /// </summary>
        /// <param name="filter">The filter to validate</param>
        /// <returns>A ValidationResult object containing the result and any error messages</returns>
        public static ValidationResult ValidateFilter(Filter filter)
        {
            if (filter == null)
                return new ValidationResult(false, "Filter cannot be null");
            
            // Validate IDs if present
            if (filter.Ids != null && filter.Ids.Length > 0)
            {
                foreach (var id in filter.Ids)
                {
                    if (!IsValidHex(id) || id.Length != 64)
                        return new ValidationResult(false, $"Invalid event ID in filter: {id}");
                }
            }
            
            // Validate authors if present
            if (filter.Authors != null && filter.Authors.Length > 0)
            {
                foreach (var author in filter.Authors)
                {
                    if (!IsValidHex(author) || author.Length != 64)
                        return new ValidationResult(false, $"Invalid author pubkey in filter: {author}");
                }
            }
            
            // Validate since/until
            if (filter.Since.HasValue && filter.Until.HasValue && filter.Since.Value > filter.Until.Value)
                return new ValidationResult(false, "Filter 'since' cannot be later than 'until'");
            
            // Validate limit
            if (filter.Limit.HasValue && filter.Limit.Value <= 0)
                return new ValidationResult(false, "Filter 'limit' must be a positive number");
            
            return new ValidationResult(true, "Filter is valid");
        }
        
        /// <summary>
        /// Validates a hex string
        /// </summary>
        /// <param name="hex">The hex string to validate</param>
        /// <returns>True if valid, false otherwise</returns>
        public static bool IsValidHex(string hex)
        {
            if (string.IsNullOrEmpty(hex))
                return false;
            
            return HexRegex.IsMatch(hex);
        }
        
        /// <summary>
        /// Validates a relay URL
        /// </summary>
        /// <param name="url">The relay URL to validate</param>
        /// <returns>A ValidationResult object containing the result and any error messages</returns>
        public static ValidationResult ValidateRelayUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return new ValidationResult(false, "Relay URL cannot be null or empty");
            
            if (!url.StartsWith("wss://") && !url.StartsWith("ws://"))
                return new ValidationResult(false, "Relay URL must start with ws:// or wss://");
            
            try
            {
                var uri = new Uri(url);
                if (string.IsNullOrEmpty(uri.Host))
                    return new ValidationResult(false, "Invalid relay URL (no host)");
                
                return new ValidationResult(true, "Relay URL is valid");
            }
            catch (Exception ex)
            {
                return new ValidationResult(false, $"Invalid relay URL: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// Represents the result of a validation operation
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// Gets whether the validation passed
        /// </summary>
        public bool IsValid { get; }
        
        /// <summary>
        /// Gets the validation message
        /// </summary>
        public string Message { get; }
        
        /// <summary>
        /// Creates a new validation result
        /// </summary>
        /// <param name="isValid">Whether the validation passed</param>
        /// <param name="message">The validation message</param>
        public ValidationResult(bool isValid, string message)
        {
            IsValid = isValid;
            Message = message;
        }
        
        /// <summary>
        /// Returns a string representation of the validation result
        /// </summary>
        public override string ToString()
        {
            return $"{(IsValid ? "Valid" : "Invalid")}: {Message}";
        }
    }
} 