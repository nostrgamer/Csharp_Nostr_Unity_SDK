using UnityEngine;

namespace NostrUnity.Utils
{
    /// <summary>
    /// Unified logging utility for the Nostr SDK
    /// </summary>
    public static class Logger
    {
        /// <summary>
        /// Whether detailed debug logging is enabled
        /// </summary>
        public static bool DebugLoggingEnabled = false;
        
        /// <summary>
        /// Log an informational message
        /// </summary>
        /// <param name="message">The message to log</param>
        public static void Log(string message)
        {
            Debug.Log($"[NostrSDK] {message}");
        }
        
        /// <summary>
        /// Log a warning message
        /// </summary>
        /// <param name="message">The warning message to log</param>
        public static void LogWarning(string message)
        {
            Debug.LogWarning($"[NostrSDK] {message}");
        }
        
        /// <summary>
        /// Log an error message
        /// </summary>
        /// <param name="message">The error message to log</param>
        public static void LogError(string message)
        {
            Debug.LogError($"[NostrSDK] {message}");
        }
        
        /// <summary>
        /// Log a debug message (only if debug logging is enabled)
        /// </summary>
        /// <param name="message">The debug message to log</param>
        public static void LogDebug(string message)
        {
            if (DebugLoggingEnabled)
            {
                Debug.Log($"[NostrSDK-Debug] {message}");
            }
        }
    }
} 