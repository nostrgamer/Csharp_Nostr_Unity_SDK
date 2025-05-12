using UnityEngine;
using NostrUnity.Crypto;
using NostrUnity.Utils;
using NostrUnity.Models;
using System;

namespace NostrUnity.Examples
{
    /// <summary>
    /// Simple component for testing and validating Nostr keys using the Inspector
    /// </summary>
    public class NostrKeyTester : MonoBehaviour
    {
        [Header("Key Input")]
        [Tooltip("Enter a private key (hex or nsec format)")]
        [SerializeField] private string privateKeyInput = "";
        
        [Tooltip("Expected npub (optional) - used to verify key matches expected identity")]
        [SerializeField] private string expectedNpubInput = "";
        
        [Header("Test Event")]
        [Tooltip("Custom content for the test event")]
        [SerializeField] private string testEventContent = "Test event from Unity Nostr SDK";
        
        [Header("Actions (During Play Mode)")]
        [Tooltip("Click to validate the provided key")]
        [SerializeField] private bool validateKey = false;
        
        [Tooltip("Click to generate a new key pair")]
        [SerializeField] private bool generateNewKey = false;
        
        [Tooltip("Click to create a test event")]
        [SerializeField] private bool createTestEvent = false;
        
        [Header("Status (Read Only)")]
        [Multiline(5)]
        [SerializeField] private string statusText = "Ready to validate/generate keys";
        
        [Header("Key Info (Read Only)")]
        [SerializeField] private string privateKeyHex = "";
        [SerializeField] private string privateKeyNsec = "";
        [SerializeField] private string publicKeyHex = "";
        [SerializeField] private string publicKeyNpub = "";
        
        [Header("Event Info (Read Only)")]
        [SerializeField] private string eventId = "";
        [SerializeField] private string eventContent = "";
        [SerializeField] private string eventSignature = "";
        [SerializeField] private bool signatureValid = false;
        
        private KeyPair _keyPair;
        
        private void Start()
        {
            Debug.Log("[NostrKeyTester] Started - Use the Inspector to input keys and trigger actions");
            Debug.Log("[NostrKeyTester] You can also right-click the component and select the actions from the context menu");
            
            // Auto-validate if there's already a key in the input field
            if (!string.IsNullOrEmpty(privateKeyInput))
            {
                Debug.Log($"[NostrKeyTester] Found key in input field: {privateKeyInput.Substring(0, 10)}...");
                ValidateKey();
            }
        }
        
        private void Update()
        {
            // Check if any of the action toggles are enabled
            if (validateKey)
            {
                validateKey = false; // Reset toggle
                ValidateKey();
            }
            
            if (generateNewKey)
            {
                generateNewKey = false; // Reset toggle
                GenerateNewKey();
            }
            
            if (createTestEvent)
            {
                createTestEvent = false; // Reset toggle
                CreateTestEvent();
            }
        }
        
        /// <summary>
        /// Validates the entered private key and checks if it produces the expected npub
        /// </summary>
        [ContextMenu("Validate Key")]
        public void ValidateKey()
        {
            Debug.Log("[NostrKeyTester] Validating key...");
            
            if (string.IsNullOrEmpty(privateKeyInput))
            {
                statusText = "Please enter a private key (hex or nsec format)";
                Debug.LogWarning("[NostrKeyTester] No private key provided");
                return;
            }
            
            try
            {
                // Create a key pair from the entered private key
                _keyPair = new KeyPair(privateKeyInput);
                Debug.Log("[NostrKeyTester] Successfully created key pair!");
                
                // Update the key info fields
                privateKeyHex = _keyPair.PrivateKeyHex;
                privateKeyNsec = _keyPair.Nsec;
                publicKeyHex = _keyPair.PublicKeyHex;
                publicKeyNpub = _keyPair.Npub;
                
                // Display the results
                statusText = "Key loaded successfully!";
                Debug.Log($"[NostrKeyTester] Public Key (hex): {_keyPair.PublicKeyHex}");
                Debug.Log($"[NostrKeyTester] Public Key (npub): {_keyPair.Npub}");
                
                // Check if an expected npub was provided and verify it matches
                if (!string.IsNullOrEmpty(expectedNpubInput))
                {
                    Debug.Log($"[NostrKeyTester] Checking against expected npub: {expectedNpubInput}");
                    if (expectedNpubInput.Equals(_keyPair.Npub, StringComparison.OrdinalIgnoreCase))
                    {
                        statusText += "\nSUCCESS: The derived npub matches the expected npub!";
                        Debug.Log("[NostrKeyTester] MATCH: The derived npub matches the expected npub!");
                    }
                    else
                    {
                        statusText += "\nWARNING: The derived npub does NOT match the expected npub!";
                        Debug.LogWarning("[NostrKeyTester] MISMATCH: The derived npub does NOT match the expected npub!");
                        
                        // Try to get the hex of the expected npub for comparison
                        try
                        {
                            string expectedHex = CryptoUtils.ConvertIdFormat(expectedNpubInput, "hex");
                            Debug.LogWarning($"[NostrKeyTester] Expected public key hex: {expectedHex}");
                            Debug.LogWarning($"[NostrKeyTester] Actual public key hex: {_keyPair.PublicKeyHex}");
                        }
                        catch (Exception ex)
                        {
                            statusText += "\nCould not decode expected npub - it may be in an invalid format.";
                            Debug.LogError($"[NostrKeyTester] Could not decode expected npub: {ex.Message}");
                        }
                    }
                }
                
                // Add links for exploration
                Debug.Log($"[NostrKeyTester] Profile URL: https://snort.social/p/{_keyPair.Npub}");
            }
            catch (Exception ex)
            {
                statusText = $"Error validating key: {ex.Message}";
                Debug.LogError($"[NostrKeyTester] Key validation error: {ex.Message}");
                Debug.LogException(ex);
            }
        }
        
        /// <summary>
        /// Generates a new random key pair
        /// </summary>
        [ContextMenu("Generate New Key")]
        public void GenerateNewKey()
        {
            Debug.Log("[NostrKeyTester] Generating new key pair...");
            
            try
            {
                // Generate a new key pair
                _keyPair = new KeyPair();
                Debug.Log("[NostrKeyTester] Successfully generated new key pair!");
                
                // Update the fields
                privateKeyInput = _keyPair.Nsec; // Auto-fill the input field
                privateKeyHex = _keyPair.PrivateKeyHex;
                privateKeyNsec = _keyPair.Nsec;
                publicKeyHex = _keyPair.PublicKeyHex;
                publicKeyNpub = _keyPair.Npub;
                
                statusText = "Generated new key pair! SAVE THIS INFORMATION SECURELY!";
                Debug.Log($"[NostrKeyTester] Private Key (hex): {_keyPair.PrivateKeyHex}");
                Debug.Log($"[NostrKeyTester] Private Key (nsec): {_keyPair.Nsec}");
                Debug.Log($"[NostrKeyTester] Public Key (hex): {_keyPair.PublicKeyHex}");
                Debug.Log($"[NostrKeyTester] Public Key (npub): {_keyPair.Npub}");
                Debug.Log($"[NostrKeyTester] IMPORTANT: Save this information securely!");
            }
            catch (Exception ex)
            {
                statusText = $"Error generating key pair: {ex.Message}";
                Debug.LogError($"[NostrKeyTester] Key generation error: {ex.Message}");
                Debug.LogException(ex);
            }
        }
        
        /// <summary>
        /// Creates a test event using the current key pair
        /// </summary>
        [ContextMenu("Create Test Event")]
        public void CreateTestEvent()
        {
            Debug.Log("[NostrKeyTester] Creating test event...");
            
            if (_keyPair == null)
            {
                statusText = "Please validate or generate a key pair first";
                Debug.LogWarning("[NostrKeyTester] No key pair available. Please validate or generate a key first.");
                return;
            }
            
            try
            {
                // Create a test event
                string content = string.IsNullOrEmpty(testEventContent) 
                    ? $"Test event from Unity Nostr SDK at {DateTime.UtcNow}" 
                    : testEventContent;
                    
                Debug.Log($"[NostrKeyTester] Creating event with content: {content}");
                NostrEvent testEvent = new NostrEvent(_keyPair.PublicKeyHex, 1, content);
                Debug.Log($"[NostrKeyTester] Event created with ID: {testEvent.Id}");
                
                // Sign the event
                Debug.Log("[NostrKeyTester] Signing event...");
                testEvent.Sign(_keyPair.PrivateKeyHex);
                Debug.Log($"[NostrKeyTester] Event signed with signature: {testEvent.Sig.Substring(0, 20)}...");
                
                // Update the event info fields
                eventId = testEvent.Id;
                eventContent = testEvent.Content;
                eventSignature = testEvent.Sig;
                signatureValid = testEvent.VerifySignature();
                
                // Display the information
                statusText = $"Created and signed test event! Event ID: {testEvent.Id}";
                Debug.Log($"[NostrKeyTester] Test event created successfully:");
                Debug.Log($"[NostrKeyTester] ID: {testEvent.Id}");
                Debug.Log($"[NostrKeyTester] Content: {testEvent.Content}");
                Debug.Log($"[NostrKeyTester] Signature: {testEvent.Sig.Substring(0, 20)}...");
                Debug.Log($"[NostrKeyTester] Signature valid: {signatureValid}");
                Debug.Log("[NostrKeyTester] To publish this event, you'll need to connect to a relay using NostrExampleBasic");
            }
            catch (Exception ex)
            {
                statusText = $"Error creating test event: {ex.Message}";
                Debug.LogError($"[NostrKeyTester] Event creation error: {ex.Message}");
                Debug.LogException(ex);
            }
        }
        
        /// <summary>
        /// Reset method for the Unity Inspector, clears generated data
        /// </summary>
        [ContextMenu("Reset Generated Data")]
        public void ResetGeneratedData()
        {
            Debug.Log("[NostrKeyTester] Resetting generated data...");
            
            // Only reset the output fields, not the input
            statusText = "Ready to validate/generate keys";
            
            privateKeyHex = "";
            privateKeyNsec = "";
            publicKeyHex = "";
            publicKeyNpub = "";
            
            eventId = "";
            eventContent = "";
            eventSignature = "";
            signatureValid = false;
            
            _keyPair = null;
            
            Debug.Log("[NostrKeyTester] Data reset complete");
        }
    }
} 