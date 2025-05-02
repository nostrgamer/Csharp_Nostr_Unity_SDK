using System;
using System.IO;
using UnityEngine;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto.Parameters;

namespace Nostr.Unity
{
    /// <summary>
    /// Manages initialization and setup of the BouncyCastle library
    /// </summary>
    public static class BouncyCastleManager
    {
        private static bool _initialized = false;
        private static ECDomainParameters _secp256k1Parameters;
        
        /// <summary>
        /// Gets a value indicating whether the manager is initialized
        /// </summary>
        public static bool IsInitialized => _initialized;
        
        /// <summary>
        /// Initializes the BouncyCastle library and validates it's available
        /// </summary>
        /// <returns>True if initialization was successful</returns>
        public static bool Initialize()
        {
            if (_initialized)
                return true;
                
            try
            {
                Debug.Log("Initializing BouncyCastleManager...");
                
                // Verify BouncyCastle library is available
                Type ecParamsType = typeof(ECDomainParameters);
                if (ecParamsType == null)
                {
                    Debug.LogError("BouncyCastle.Crypto library not found");
                    return false;
                }
                
                // Get the secp256k1 curve parameters
                X9ECParameters curve = SecNamedCurves.GetByName("secp256k1");
                if (curve == null)
                {
                    Debug.LogError("Failed to get secp256k1 curve parameters");
                    return false;
                }
                
                _secp256k1Parameters = new ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H);
                
                _initialized = true;
                Debug.Log("BouncyCastleManager initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error initializing BouncyCastleManager: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Ensures that the manager is initialized
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if initialization fails</exception>
        public static void EnsureInitialized()
        {
            if (!_initialized && !Initialize())
            {
                throw new InvalidOperationException("BouncyCastleManager failed to initialize");
            }
        }
        
        /// <summary>
        /// Gets the secp256k1 curve parameters
        /// </summary>
        /// <returns>The curve parameters</returns>
        /// <exception cref="InvalidOperationException">Thrown if the manager is not initialized</exception>
        public static ECDomainParameters GetSecp256k1Parameters()
        {
            EnsureInitialized();
            return _secp256k1Parameters;
        }
    }
} 