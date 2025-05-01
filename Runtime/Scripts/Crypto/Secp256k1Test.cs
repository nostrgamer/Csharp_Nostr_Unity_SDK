using UnityEngine;
using System;

namespace Nostr.Unity
{
    /// <summary>
    /// Test script to validate Secp256k1 implementation
    /// </summary>
    public class Secp256k1Test : MonoBehaviour
    {
        private void Start()
        {
            Debug.Log("Starting Secp256k1 test...");
            TestSecp256k1();
        }

        public void TestSecp256k1()
        {
            try
            {
                // Initialize the Secp256k1 library
                bool initialized = Secp256k1Manager.Initialize();
                Debug.Log($"Secp256k1 initialization: {(initialized ? "SUCCESS" : "FAILED")}");

                if (!initialized)
                {
                    Debug.LogError("Failed to initialize Secp256k1 library.");
                    return;
                }
                
                // Check if using fallback
                bool usingFallback = Secp256k1Manager.IsUsingFallback();
                Debug.Log($"Using fallback implementation: {usingFallback}");
                
                if (usingFallback)
                {
                    Debug.LogWarning("Native secp256k1 library could not be loaded. Using pure C# fallback implementation.");
                    Debug.LogWarning("This is not suitable for production use as it doesn't perform actual secp256k1 operations.");
                    Debug.LogWarning("Please make sure the native libraries are properly installed and compatible with your platform.");
                }

                // Generate a private key
                byte[] privateKey = Secp256k1Manager.GeneratePrivateKey();
                string privateKeyHex = BytesToHex(privateKey);
                Debug.Log($"Generated private key: {privateKeyHex}");

                // Derive public key
                byte[] publicKey = Secp256k1Manager.GetPublicKey(privateKey);
                string publicKeyHex = BytesToHex(publicKey);
                Debug.Log($"Derived public key ({publicKey.Length} bytes): {publicKeyHex}");

                // Sign a test message
                string testMessage = "Hello, Nostr!";
                Debug.Log($"Test message: \"{testMessage}\"");
                
                byte[] messageHash = Secp256k1Manager.ComputeMessageHash(testMessage);
                string messageHashHex = BytesToHex(messageHash);
                Debug.Log($"Message hash: {messageHashHex}");
                
                byte[] signature = Secp256k1Manager.Sign(messageHash, privateKey);
                string signatureHex = BytesToHex(signature);
                Debug.Log($"Created signature ({signature.Length} bytes): {signatureHex}");

                // Verify the signature
                bool verified = Secp256k1Manager.Verify(messageHash, signature, publicKey);
                Debug.Log($"Signature verification: {(verified ? "SUCCESS" : "FAILED")}");

                // Create a NostrKeyManager for higher-level operations
                var keyManager = new NostrKeyManager();
                
                // Test with Nostr key formats
                string nsec = keyManager.GeneratePrivateKey(false);
                Debug.Log($"Generated nsec: {nsec}");
                
                string npub = keyManager.GetPublicKey(nsec, false);
                Debug.Log($"Derived npub: {npub}");
                
                string hexPrivateKey = keyManager.GeneratePrivateKey(true);
                Debug.Log($"Generated hex private key: {hexPrivateKey}");
                
                string hexPublicKey = keyManager.GetPublicKey(hexPrivateKey, true);
                Debug.Log($"Derived hex public key: {hexPublicKey}");
                
                // Sign a message with the NostrKeyManager
                string signature2 = keyManager.SignMessage(testMessage, nsec);
                Debug.Log($"Signed message with nsec, signature: {signature2}");
                
                // Verify the signature with the NostrKeyManager
                bool verified2 = keyManager.VerifySignature(testMessage, signature2, npub);
                Debug.Log($"Signature verification with npub: {(verified2 ? "SUCCESS" : "FAILED")}");

                Debug.Log("==== Secp256k1 test completed successfully! ====");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Secp256k1 test failed with exception: {ex.Message}");
                Debug.LogException(ex);
            }
        }

        // Helper function to convert bytes to hex string
        private string BytesToHex(byte[] bytes)
        {
            if (bytes == null) return "null";
            
            System.Text.StringBuilder hex = new System.Text.StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
            {
                hex.AppendFormat("{0:x2}", b);
            }
            return hex.ToString();
        }
    }
} 