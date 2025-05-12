using System;
using UnityEngine;
using System.Collections.Generic;

namespace NostrUnity.Utils
{
    /// <summary>
    /// Centralized error handling for Nostr operations
    /// </summary>
    public static class NostrErrorHandler
    {
        // Event for UI/external error handling
        public static event Action<string, NostrErrorSeverity> OnError;
        
        // Keep track of recent errors
        private static readonly Queue<NostrError> _recentErrors = new Queue<NostrError>();
        private const int MaxStoredErrors = 50;
        
        public enum NostrErrorSeverity
        {
            Info,
            Warning,
            Error,
            Critical
        }
        
        public class NostrError
        {
            public DateTime Timestamp { get; }
            public string Message { get; }
            public NostrErrorSeverity Severity { get; }
            public string Context { get; }
            
            public NostrError(string message, NostrErrorSeverity severity, string context)
            {
                Timestamp = DateTime.UtcNow;
                Message = message;
                Severity = severity;
                Context = context;
            }
        }
        
        /// <summary>
        /// Handles an error with the specified severity
        /// </summary>
        public static void HandleError(Exception ex, string context, NostrErrorSeverity severity = NostrErrorSeverity.Error)
        {
            string message = $"[{context}] {ex.Message}";
            
            // Log to Unity console
            switch (severity)
            {
                case NostrErrorSeverity.Info:
                    Debug.Log(message);
                    break;
                case NostrErrorSeverity.Warning:
                    Debug.LogWarning(message);
                    break;
                case NostrErrorSeverity.Error:
                case NostrErrorSeverity.Critical:
                    Debug.LogError(message);
                    if (ex.StackTrace != null)
                        Debug.LogError($"Stack trace: {ex.StackTrace}");
                    break;
            }
            
            // Store error
            var error = new NostrError(message, severity, context);
            _recentErrors.Enqueue(error);
            if (_recentErrors.Count > MaxStoredErrors)
                _recentErrors.Dequeue();
            
            // Notify subscribers
            OnError?.Invoke(message, severity);
        }
        
        /// <summary>
        /// Handles an error message with the specified severity
        /// </summary>
        public static void HandleError(string message, string context, NostrErrorSeverity severity = NostrErrorSeverity.Error)
        {
            HandleError(new Exception(message), context, severity);
        }
        
        /// <summary>
        /// Gets recent errors for debugging purposes
        /// </summary>
        public static NostrError[] GetRecentErrors()
        {
            return _recentErrors.ToArray();
        }
        
        /// <summary>
        /// Clears the error history
        /// </summary>
        public static void ClearErrors()
        {
            _recentErrors.Clear();
        }
    }
} 