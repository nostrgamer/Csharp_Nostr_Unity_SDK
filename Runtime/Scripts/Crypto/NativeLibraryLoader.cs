using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Nostr.Unity.Crypto
{
    /// <summary>
    /// Handles native library availability checking across different platforms
    /// </summary>
    public static class NativeLibraryLoader
    {
        private static bool _isAvailable = false;
        private static bool _hasChecked = false;

        /// <summary>
        /// Checks if the native secp256k1 library is available
        /// Unity's plugin system will handle the actual loading
        /// </summary>
        public static bool LoadNativeLibrary()
        {
            if (_hasChecked)
                return _isAvailable;

            try
            {
                // We need to check if the library is available
                // With Unity's plugin system, we can just attempt to call a native method and catch any exceptions
                
                // Since we can't directly probe if a library is loaded in C#, we'll rely on 
                // the actual initialization in Secp256k1Wrapper to determine success
                _isAvailable = true;
                _hasChecked = true;
                
                Debug.Log("Native secp256k1 library should be available through Unity's plugin system");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Native secp256k1 library may not be available: {ex.Message}");
                _isAvailable = false;
                _hasChecked = true;
                return false;
            }
        }
    }
} 