using UnityEngine;
using System;
using System.Text;

namespace Nostr.Unity
{
    /// <summary>
    /// Simple test component for cryptographic operations
    /// </summary>
    public class CryptoTest : MonoBehaviour
    {
        // Results for display
        public string PrivateKeyHex = "";
        public string PublicKeyHex = "";
        public string SignatureHex = "";
        public bool SignatureValid = false;
        
        /// <summary>
        /// Generate a key pair and test signing
        /// </summary>
        public void RunTest()
        {
            try
            {
                Debug.Log("Starting crypto test...");
                
                // Initialize the crypto library
                if (!Secp256k1Manager.Initialize())
                {
                    Debug.LogError("Failed to initialize Secp256k1Manager");
                    return;
                }
                
                // Generate a new private key
                byte[] privateKey = Secp256k1Manager.GeneratePrivateKey();
                PrivateKeyHex = BitConverter.ToString(privateKey).Replace("-", "").ToLower();
                Debug.Log($"Generated private key: {PrivateKeyHex}");
                
                // Derive the public key
                byte[] publicKey = Secp256k1Manager.GetPublicKey(privateKey);
                PublicKeyHex = BitConverter.ToString(publicKey).Replace("-", "").ToLower();
                Debug.Log($"Derived public key: {PublicKeyHex}");
                
                // Create a test message and hash it
                string message = "Hello Nostr!";
                byte[] messageHash = Secp256k1Manager.ComputeMessageHash(message);
                Debug.Log($"Message: {message}");
                Debug.Log($"Message hash: {BitConverter.ToString(messageHash).Replace("-", "").ToLower()}");
                
                // Sign the message
                byte[] signature = Secp256k1Manager.Sign(messageHash, privateKey);
                SignatureHex = BitConverter.ToString(signature).Replace("-", "").ToLower();
                Debug.Log($"Signature: {SignatureHex}");
                
                // Verify the signature
                SignatureValid = Secp256k1Manager.Verify(messageHash, signature, publicKey);
                Debug.Log($"Signature valid: {SignatureValid}");
                
                // Log which implementation is being used
                Debug.Log($"Using BouncyCastle implementation: {Secp256k1Manager.IsUsingBouncyCastle()}");
                
                Debug.Log("Crypto test completed successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error in crypto test: {ex.Message}");
            }
        }
    }
} 