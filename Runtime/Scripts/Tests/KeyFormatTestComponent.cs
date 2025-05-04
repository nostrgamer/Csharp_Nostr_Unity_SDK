using System;
using System.Collections;
using System.Text;
using UnityEngine;
using Nostr.Unity;
using Nostr.Unity.Crypto;
using Nostr.Unity.Utils;

/// <summary>
/// Test component for verifying key format compatibility between local verification and relay standards
/// </summary>
public class KeyFormatTestComponent : MonoBehaviour
{
    [Header("Test Configuration")]
    [SerializeField] private bool runTestOnStart = true;
    [SerializeField] private string relayUrl = "wss://relay.damus.io";
    
    [Header("Test Results")]
    [TextArea(5, 10)]
    [SerializeField] private string testResults = "";
    
    private NostrKeyManager _keyManager;
    private NostrClient _nostrClient;
    private StringBuilder _logBuilder = new StringBuilder();
    
    private void Awake()
    {
        _keyManager = new NostrKeyManager();
        
        _nostrClient = FindAnyObjectByType<NostrClient>();
        if (_nostrClient == null)
        {
            GameObject clientObj = new GameObject("TestNostrClient");
            _nostrClient = clientObj.AddComponent<NostrClient>();
            DontDestroyOnLoad(clientObj);
        }
    }
    
    private void Start()
    {
        if (runTestOnStart)
        {
            StartCoroutine(RunKeyFormatTest());
        }
    }
    
    /// <summary>
    /// Main test coroutine
    /// </summary>
    public IEnumerator RunKeyFormatTest()
    {
        _logBuilder.Clear();
        Log("=== KEY FORMAT COMPATIBILITY TEST ===");
        
        // Connect to relay
        Log($"Connecting to relay: {relayUrl}");
        bool connected = false;
        yield return _nostrClient.ConnectToRelay(relayUrl, result => connected = result);
        Log($"Relay connection result: {connected}");
        
        if (!connected)
        {
            Log("ERROR: Could not connect to relay. Test cannot continue.");
            testResults = _logBuilder.ToString();
            yield break;
        }
        
        // Generate test keys
        Log("Generating test keys...");
        string privateKey = _keyManager.GeneratePrivateKey();
        Log($"Generated private key: {privateKey}");
        
        // Get public key in both formats
        string uncompressedPublicKey = _keyManager.GetPublicKey(privateKey, false);
        string compressedPublicKey = _keyManager.GetPublicKey(privateKey, true);
        
        Log($"Uncompressed public key (64 chars): {uncompressedPublicKey}");
        Log($"Compressed public key (66 chars): {compressedPublicKey}");
        
        // Test creating events with different key formats
        Log("\n--- Testing event with uncompressed key ---");
        yield return TestEventCreation(uncompressedPublicKey, privateKey, "Event with uncompressed key");
        
        Log("\n--- Testing event with compressed key ---");
        yield return TestEventCreation(compressedPublicKey, privateKey, "Event with compressed key");
        
        Log("\n=== TEST COMPLETE ===");
        testResults = _logBuilder.ToString();
    }
    
    /// <summary>
    /// Tests event creation, signing, verification and publishing
    /// </summary>
    private IEnumerator TestEventCreation(string publicKey, string privateKey, string message)
    {
        NostrEvent nostrEvent = null;
        bool localVerification = false;
        
        // --- Step 1: Create and sign the event ---
        try
        {
            // Create the event
            Log($"Creating event with public key: {publicKey}");
            nostrEvent = new NostrEvent(publicKey, 1, message);
            
            // Sign the event
            Log("Signing event...");
            nostrEvent.Sign(privateKey);
            Log($"Event ID: {nostrEvent.Id}");
            Log($"Signature: {nostrEvent.Signature}");
            
            // Verify locally
            localVerification = nostrEvent.VerifySignature();
            Log($"Local verification result: {localVerification}");
        }
        catch (Exception ex)
        {
            Log($"Error in event creation or signing: {ex.Message}");
            Log($"Stack trace: {ex.StackTrace}");
            yield break;
        }
        
        if (!localVerification || nostrEvent == null)
        {
            Log("ERROR: Local verification failed or event is null!");
            yield break;
        }
        
        // --- Step 2: Publish to relay ---
        Log($"Publishing to relay: {relayUrl}");
        bool published = false;
        yield return _nostrClient.PublishEvent(nostrEvent, result => published = result);
        Log($"Publish result: {published}");
        
        // Wait a bit for relay response
        yield return new WaitForSeconds(2);
        
        // --- Step 3: Check for errors ---
        string error;
        if (_nostrClient.HasEventErrors(nostrEvent.Id, out error))
        {
            Log($"Relay reported error: {error}");
            
            if (error.Contains("signature"))
            {
                Log("KEY FORMAT ISSUE DETECTED: Signature validation failed on relay");
                Log("This likely means the public key format is incompatible with the relay's expectations");
            }
        }
        else if (published)
        {
            Log("SUCCESS: Event was accepted by relay!");
            
            try
            {
                // Create readable npub to help check on a web client
                string nPub = Bech32Util.EncodeHex("npub", nostrEvent.PublicKey);
                Log($"You can verify using nPub: {nPub}");
            }
            catch (Exception ex)
            {
                Log($"Error creating nPub: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// Logs a message to both Unity console and the internal log builder
    /// </summary>
    private void Log(string message)
    {
        Debug.Log($"[KeyFormatTest] {message}");
        _logBuilder.AppendLine(message);
    }
    
    /// <summary>
    /// Button handler for UI
    /// </summary>
    public void RunTest()
    {
        StartCoroutine(RunKeyFormatTest());
    }
} 