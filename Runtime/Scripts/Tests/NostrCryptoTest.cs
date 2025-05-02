using UnityEngine;
using System;
using System.Threading.Tasks;

namespace Nostr.Unity.Tests
{
    public class NostrCryptoTest : MonoBehaviour
    {
        private NostrKeyManager _keyManager;
        private NostrClient _client;
        
        private void Start()
        {
            _keyManager = new NostrKeyManager();
            _client = new NostrClient();
            
            // Run the tests
            RunTests();
        }
        
        private async void RunTests()
        {
            try
            {
                Debug.Log("Starting Nostr crypto tests...");
                
                // Test 1: Key Generation and Storage
                TestKeyGeneration();
                
                // Test 2: Signing and Verification
                TestSigningAndVerification();
                
                // Test 3: Event Creation and Signing
                TestEventSigning();
                
                // Test 4: Connect to Relay and Publish
                await TestRelayConnection();
                
                Debug.Log("All tests completed successfully!");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Test failed: {ex.Message}");
            }
        }
        
        private void TestKeyGeneration()
        {
            Debug.Log("Testing key generation...");
            
            // Generate a new key pair
            string privateKey = _keyManager.GeneratePrivateKey();
            string publicKey = _keyManager.GetPublicKey(privateKey);
            
            Debug.Log($"Generated private key: {privateKey}");
            Debug.Log($"Generated public key: {publicKey}");
            
            // Store the keys
            bool stored = _keyManager.StoreKeys(privateKey, encrypt: true, password: "test123");
            if (!stored)
            {
                throw new Exception("Failed to store keys");
            }
            
            // Load the keys
            string loadedPrivateKey = _keyManager.LoadPrivateKey(password: "test123");
            if (loadedPrivateKey != privateKey)
            {
                throw new Exception("Loaded private key does not match generated key");
            }
            
            Debug.Log("Key generation test passed!");
        }
        
        private void TestSigningAndVerification()
        {
            Debug.Log("Testing signing and verification...");
            
            // Generate a new key pair
            string privateKey = _keyManager.GeneratePrivateKey();
            string publicKey = _keyManager.GetPublicKey(privateKey);
            
            // Create a test message
            string message = "Hello, Nostr!";
            
            // Sign the message
            string signature = _keyManager.SignMessage(message, privateKey);
            
            // Verify the signature
            bool verified = _keyManager.VerifySignature(message, signature, publicKey);
            if (!verified)
            {
                throw new Exception("Signature verification failed");
            }
            
            // Test with invalid signature
            string invalidSignature = signature.Substring(0, signature.Length - 2) + "00";
            bool invalidVerified = _keyManager.VerifySignature(message, invalidSignature, publicKey);
            if (invalidVerified)
            {
                throw new Exception("Invalid signature was verified as valid");
            }
            
            Debug.Log("Signing and verification test passed!");
        }
        
        private void TestEventSigning()
        {
            Debug.Log("Testing event signing...");
            
            // Generate a new key pair
            string privateKey = _keyManager.GeneratePrivateKey();
            string publicKey = _keyManager.GetPublicKey(privateKey);
            
            // Create a test event
            var nostrEvent = new NostrEvent(publicKey, (int)NostrEventKind.TextNote, "Test message");
            
            // Sign the event
            nostrEvent.Sign(privateKey);
            
            // Verify the signature
            bool verified = nostrEvent.VerifySignature();
            if (!verified)
            {
                throw new Exception("Event signature verification failed");
            }
            
            Debug.Log("Event signing test passed!");
        }
        
        private async Task TestRelayConnection()
        {
            Debug.Log("Testing relay connection...");
            
            try
            {
                // Connect to a test relay
                await _client.ConnectToRelay("wss://relay.damus.io");
                
                // Create a test event
                string privateKey = _keyManager.GeneratePrivateKey();
                string publicKey = _keyManager.GetPublicKey(privateKey);
                
                var nostrEvent = new NostrEvent(publicKey, (int)NostrEventKind.TextNote, "Test message from Unity SDK");
                nostrEvent.Sign(privateKey);
                
                // Publish the event
                await _client.PublishEvent(nostrEvent);
                
                Debug.Log("Event published successfully!");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Relay test failed: {ex.Message}");
                throw;
            }
        }
    }
} 