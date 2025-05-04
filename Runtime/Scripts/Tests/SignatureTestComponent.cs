using System;
using System.Collections;
using System.Text;
using UnityEngine;
using Nostr.Unity;
using Nostr.Unity.Crypto;

/// <summary>
/// Simplified test component for verifying Nostr signature functionality
/// </summary>
public class SignatureTestComponent : MonoBehaviour
{
    [Header("Test Configuration")]
    [SerializeField] private bool runTestsOnStart = true;
    [SerializeField] private bool sendEventToRelay = false;
    [SerializeField] private string relayUrl = "wss://relay.damus.io";
    
    [Header("Test Keys")]
    [SerializeField] private string testPrivateKey = "";
    [TextArea(3, 5)]
    [SerializeField] private string testResults = "";
    
    private NostrKeyManager _keyManager;
    private StringBuilder _logBuilder = new StringBuilder();
    private NostrClient _nostrClient;
    
    private void Awake()
    {
        _keyManager = new NostrKeyManager();
        
        if (sendEventToRelay)
        {
            _nostrClient = FindAnyObjectByType<NostrClient>();
            if (_nostrClient == null)
            {
                GameObject clientObj = new GameObject("TestNostrClient");
                _nostrClient = clientObj.AddComponent<NostrClient>();
                DontDestroyOnLoad(clientObj);
            }
        }
    }
    
    private void Start()
    {
        if (runTestsOnStart)
        {
            StartCoroutine(RunAllTests());
        }
    }
    
    /// <summary>
    /// Runs basic signature tests
    /// </summary>
    public IEnumerator RunAllTests()
    {
        _logBuilder.Clear();
        Log("===== STARTING NOSTR SIGNATURE TESTS =====");
        
        // Connect to relay if needed
        if (sendEventToRelay && _nostrClient != null)
        {
            Log($"Connecting to test relay: {relayUrl}");
            bool connected = false;
            yield return _nostrClient.ConnectToRelay(relayUrl, result => connected = result);
            Log($"Relay connection result: {connected}");
            
            if (!connected)
            {
                Log("WARNING: Could not connect to relay, will perform local tests only");
                sendEventToRelay = false;
            }
        }
        
        // Prepare test keys
        yield return PrepareTestKeys();
        
        // Test event signing and verification
        yield return TestEventSigning();
        
        // Test sending to relay if enabled
        if (sendEventToRelay && _nostrClient != null)
        {
            yield return TestRelaySending();
        }
        
        Log("===== COMPLETED NOSTR SIGNATURE TESTS =====");
        testResults = _logBuilder.ToString();
    }
    
    /// <summary>
    /// Prepares test keys, either using provided ones or generating new ones
    /// </summary>
    private IEnumerator PrepareTestKeys()
    {
        Log("--- Preparing Test Keys ---");
        
        if (string.IsNullOrEmpty(testPrivateKey))
        {
            Log("No test private key provided, generating new one");
            testPrivateKey = _keyManager.GeneratePrivateKey();
        }
        
        // Ensure the key is valid hex
        if (!IsValidHexString(testPrivateKey) || testPrivateKey.Length != 64)
        {
            Log($"WARNING: Invalid test key format: {testPrivateKey}");
            Log("Generating new test keys");
            testPrivateKey = _keyManager.GeneratePrivateKey();
        }
        
        // Get the public key
        string publicKey = _keyManager.GetPublicKey(testPrivateKey, true);
        Log($"Test Public Key: {publicKey}");
        
        // Verify key pair
        try
        {
            string message = "Test message";
            string signature = _keyManager.SignMessage(message, testPrivateKey);
            bool verified = _keyManager.VerifySignature(message, signature, publicKey);
            Log($"Basic key pair verification: {verified}");
            
            if (!verified)
            {
                Log("WARNING: Key pair failed basic verification!");
            }
        }
        catch (Exception ex)
        {
            Log($"ERROR in key verification: {ex.Message}");
        }
        
        yield return null;
    }
    
    /// <summary>
    /// Tests event signing and verification
    /// </summary>
    private IEnumerator TestEventSigning()
    {
        Log("--- Testing Event Signing ---");
        
        // Get the public key
        string publicKey = _keyManager.GetPublicKey(testPrivateKey, true);
        NostrEvent nostrEvent = null;
        
        try
        {
            // Create and sign a simple event
            string content = "Test message from SignatureTestComponent";
            nostrEvent = new NostrEvent(publicKey, 1, content);
            
            Log("Signing event...");
            nostrEvent.Sign(testPrivateKey);
            
            // Log the event details
            Log($"Event ID: {nostrEvent.Id}");
            Log($"Event signature: {nostrEvent.Signature}");
            
            // Verify the event signature
            bool verified = nostrEvent.VerifySignature();
            Log($"Event verification result: {verified}");
            
            if (!verified)
            {
                Log("ERROR: Event signature verification failed!");
            }
        }
        catch (Exception ex)
        {
            Log($"ERROR in event signing test: {ex.Message}");
        }
        
        yield return null;
    }
    
    /// <summary>
    /// Tests sending an event to an actual relay
    /// </summary>
    private IEnumerator TestRelaySending()
    {
        Log("--- Testing Relay Publishing ---");
        
        // Get the public key
        string publicKey = _keyManager.GetPublicKey(testPrivateKey, true);
        NostrEvent nostrEvent = null;
        bool published = false;
        
        try
        {
            // Create a test event with timestamp
            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            string content = $"Signature test message at {timestamp}";
            
            // Create and sign the event
            nostrEvent = new NostrEvent(publicKey, 1, content);
            nostrEvent.Sign(testPrivateKey);
            
            // Verify locally first
            bool verified = nostrEvent.VerifySignature();
            Log($"Local verification before sending: {verified}");
            
            if (!verified)
            {
                Log("WARNING: Event failed local verification, relay will likely reject it");
            }
            
            // Send to relay
            Log($"Sending event to relay: {relayUrl}");
        }
        catch (Exception ex)
        {
            Log($"ERROR in relay test preparation: {ex.Message}");
            yield break;
        }
            
        // Operations involving yield must be outside try-catch
        yield return _nostrClient.PublishEvent(nostrEvent, result => published = result);
        Log($"Relay publish result: {published}");
        
        yield return new WaitForSeconds(1);
        
        try
        {
            // Check relay response
            if (published)
            {
                Log("Event was successfully published to relay!");
            }
            else
            {
                Log("Event may not have been published successfully");
            }
        }
        catch (Exception ex)
        {
            Log($"ERROR in relay response processing: {ex.Message}");
        }
        
        yield return null;
    }
    
    /// <summary>
    /// Logs a message to both Unity console and the internal log builder
    /// </summary>
    private void Log(string message)
    {
        Debug.Log($"[SigTest] {message}");
        _logBuilder.AppendLine(message);
    }
    
    /// <summary>
    /// Checks if a string is a valid hex string
    /// </summary>
    private bool IsValidHexString(string hex)
    {
        if (string.IsNullOrEmpty(hex))
            return false;
            
        foreach (char c in hex)
        {
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Button handler for UI
    /// </summary>
    public void RunTests()
    {
        StartCoroutine(RunAllTests());
    }
} 