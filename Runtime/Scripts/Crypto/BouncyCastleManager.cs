using System;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Nostr.Unity.Crypto
{
    /// <summary>
    /// Manages the integration with BouncyCastle cryptography library
    /// </summary>
    public static class BouncyCastleManager
    {
        private static bool _isInitialized = false;
        private static bool _isBouncyCastleAvailable = false;

        /// <summary>
        /// Static initializer to validate BouncyCastle availability
        /// </summary>
        static BouncyCastleManager()
        {
            try
            {
                // Try to access a BouncyCastle type to confirm the library is loaded
                var typeName = "Org.BouncyCastle.Crypto.Digests.Sha256Digest";
                var type = Type.GetType(typeName) ?? 
                           AppDomain.CurrentDomain.GetAssemblies()
                           .SelectMany(a => a.GetTypes())
                           .FirstOrDefault(t => t.FullName == typeName);
                
                if (type != null)
                {
                    _isBouncyCastleAvailable = true;
                    Debug.Log("BouncyCastle library loaded successfully.");
                }
                else
                {
                    Debug.LogWarning("BouncyCastle type not found. The library may not be properly loaded.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error initializing BouncyCastle: {ex.Message}");
                _isBouncyCastleAvailable = false;
            }
        }

        /// <summary>
        /// Initializes the BouncyCastle library
        /// </summary>
        /// <returns>True if initialization was successful</returns>
        public static bool Initialize()
        {
            if (_isInitialized)
                return _isBouncyCastleAvailable;
            
            try
            {
                if (!_isBouncyCastleAvailable)
                {
                    Debug.LogError("BouncyCastle library is not available. Cannot initialize.");
                    return false;
                }
                
                // Additional initialization if needed
                _isInitialized = true;
                Debug.Log("BouncyCastle manager initialized successfully.");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error initializing BouncyCastle manager: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if BouncyCastle is available and initialized
        /// </summary>
        /// <returns>True if ready to use</returns>
        public static bool IsReady()
        {
            return _isBouncyCastleAvailable && _isInitialized;
        }

        /// <summary>
        /// Ensures the library is initialized before use
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if initialization fails</exception>
        internal static void EnsureInitialized()
        {
            if (!_isInitialized && !Initialize())
            {
                throw new InvalidOperationException("BouncyCastle library is not initialized");
            }
            
            if (!_isBouncyCastleAvailable)
            {
                throw new InvalidOperationException("BouncyCastle library is not available");
            }
        }
    }
} 