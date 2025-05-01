using System;
using UnityEngine;
using System.Runtime.InteropServices;
using System.IO;

namespace Nostr.Unity.Crypto
{
    /// <summary>
    /// Low-level wrapper around the secp256k1 native library
    /// Avoids using the Secp256k1.Net enums directly to prevent Unity compatibility issues
    /// </summary>
    public class Secp256k1Wrapper : IDisposable
    {
        // Handle to the native secp256k1 context
        private IntPtr _contextPtr;
        
        // Flag values
        private const int FLAG_COMPRESSED = 2;      // SECP256K1_EC_COMPRESSED (1 << 1)
        private const int FLAG_UNCOMPRESSED = 0;    // SECP256K1_EC_UNCOMPRESSED (0 << 1)
        
        // Buffer sizes
        public const int PRIVKEY_LENGTH = 32;
        public const int PUBKEY_LENGTH = 64;  // Serialized internal format
        public const int SERIALIZED_COMPRESSED_PUBKEY_LENGTH = 33;
        public const int SERIALIZED_UNCOMPRESSED_PUBKEY_LENGTH = 65;
        public const int SIGNATURE_LENGTH = 64;
        
        // P/Invoke declarations for secp256k1 native functions
        // The library is loaded automatically by Unity's plugin system
        [DllImport("secp256k1", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr secp256k1_context_create(uint flags);
        
        [DllImport("secp256k1", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr secp256k1_context_destroy(IntPtr context);
        
        [DllImport("secp256k1", CallingConvention = CallingConvention.Cdecl)]
        private static extern int secp256k1_ec_seckey_verify(IntPtr context, byte[] seckey);
        
        [DllImport("secp256k1", CallingConvention = CallingConvention.Cdecl)]
        private static extern int secp256k1_ec_pubkey_create(IntPtr context, byte[] pubkey, byte[] seckey);
        
        [DllImport("secp256k1", CallingConvention = CallingConvention.Cdecl)]
        private static extern int secp256k1_ec_pubkey_serialize(IntPtr context, byte[] output, ref int outputlen, byte[] pubkey, uint flags);
        
        [DllImport("secp256k1", CallingConvention = CallingConvention.Cdecl)]
        private static extern int secp256k1_ec_pubkey_parse(IntPtr context, byte[] pubkey, byte[] input, int inputlen);
        
        [DllImport("secp256k1", CallingConvention = CallingConvention.Cdecl)]
        private static extern int secp256k1_ecdsa_sign(IntPtr context, byte[] signature, byte[] msg32, byte[] seckey, IntPtr noncefp, IntPtr ndata);
        
        [DllImport("secp256k1", CallingConvention = CallingConvention.Cdecl)]
        private static extern int secp256k1_ecdsa_verify(IntPtr context, byte[] signature, byte[] msg32, byte[] pubkey);
        
        /// <summary>
        /// Initializes a new instance of the Secp256k1Wrapper
        /// </summary>
        public Secp256k1Wrapper()
        {
            try
            {
                // Create the secp256k1 context
                const uint flags = 0x0101 | 0x0202; // SECP256K1_CONTEXT_SIGN | SECP256K1_CONTEXT_VERIFY
                _contextPtr = secp256k1_context_create(flags);
                
                if (_contextPtr == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Failed to create secp256k1 context");
                }
                
                // Let the NativeLibraryLoader know the library is available
                NativeLibraryLoader.LoadNativeLibrary();
                
                Debug.Log("Secp256k1 context created successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error initializing secp256k1: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Disposes the wrapper and frees native resources
        /// </summary>
        public void Dispose()
        {
            if (_contextPtr != IntPtr.Zero)
            {
                secp256k1_context_destroy(_contextPtr);
                _contextPtr = IntPtr.Zero;
            }
        }
        
        /// <summary>
        /// Verifies that a private key is valid for the curve
        /// </summary>
        public bool SecretKeyVerify(byte[] seckey)
        {
            if (seckey == null || seckey.Length != PRIVKEY_LENGTH)
                return false;
                
            return secp256k1_ec_seckey_verify(_contextPtr, seckey) == 1;
        }
        
        /// <summary>
        /// Creates a public key from a private key
        /// </summary>
        public bool PublicKeyCreate(byte[] pubkey, byte[] seckey)
        {
            if (pubkey == null || pubkey.Length != PUBKEY_LENGTH || seckey == null || seckey.Length != PRIVKEY_LENGTH)
                return false;
                
            return secp256k1_ec_pubkey_create(_contextPtr, pubkey, seckey) == 1;
        }
        
        /// <summary>
        /// Serializes a public key to compressed or uncompressed format
        /// </summary>
        public bool PublicKeySerialize(byte[] output, byte[] pubkey, bool compressed)
        {
            if (output == null || pubkey == null || pubkey.Length != PUBKEY_LENGTH)
                return false;
                
            int outputLength = compressed ? SERIALIZED_COMPRESSED_PUBKEY_LENGTH : SERIALIZED_UNCOMPRESSED_PUBKEY_LENGTH;
            
            if (output.Length != outputLength)
                return false;
                
            uint flags = (uint)(compressed ? FLAG_COMPRESSED : FLAG_UNCOMPRESSED);
            return secp256k1_ec_pubkey_serialize(_contextPtr, output, ref outputLength, pubkey, flags) == 1;
        }
        
        /// <summary>
        /// Parses a serialized public key
        /// </summary>
        public bool PublicKeyParse(byte[] pubkey, byte[] input)
        {
            if (pubkey == null || pubkey.Length != PUBKEY_LENGTH || input == null)
                return false;
                
            return secp256k1_ec_pubkey_parse(_contextPtr, pubkey, input, input.Length) == 1;
        }
        
        /// <summary>
        /// Signs a 32-byte message hash with a private key
        /// </summary>
        public bool Sign(byte[] signature, byte[] messageHash, byte[] seckey)
        {
            if (signature == null || signature.Length != SIGNATURE_LENGTH || 
                messageHash == null || messageHash.Length != 32 || 
                seckey == null || seckey.Length != PRIVKEY_LENGTH)
                return false;
                
            return secp256k1_ecdsa_sign(_contextPtr, signature, messageHash, seckey, IntPtr.Zero, IntPtr.Zero) == 1;
        }
        
        /// <summary>
        /// Verifies a signature
        /// </summary>
        public bool Verify(byte[] signature, byte[] messageHash, byte[] pubkey)
        {
            if (signature == null || signature.Length != SIGNATURE_LENGTH || 
                messageHash == null || messageHash.Length != 32 || 
                pubkey == null || pubkey.Length != PUBKEY_LENGTH)
                return false;
                
            return secp256k1_ecdsa_verify(_contextPtr, signature, messageHash, pubkey) == 1;
        }
    }
} 