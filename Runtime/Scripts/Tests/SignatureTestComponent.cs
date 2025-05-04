using System;
using System.Collections;
using System.Text;
using System.Security.Cryptography;
using UnityEngine;
using Nostr.Unity;
using Nostr.Unity.Crypto;
using Nostr.Unity.Utils;

/// <summary>
/// Test component for diagnosing Nostr signature generation and verification issues
/// </summary>
public class SignatureTestComponent : MonoBehaviour
{
    [Header("Test Configuration")]
    [SerializeField] private bool runTestsOnStart = true;
    [SerializeField] private bool sendEventToRelay = true;
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
        
        // Set up NostrClient if needed
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
    /// Runs all signature tests
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
        
        // 1. Prepare test keys
        yield return PrepareTestKeys();
        
        // 2. Run basic signing test
        yield return TestBasicSigning();
        
        // 3. Test full event signing and verification
        yield return TestEventSigning();
        
        // 4. Test sending to relay if enabled
        if (sendEventToRelay && _nostrClient != null)
        {
            yield return TestRelaySending();
        }
        
        Log("===== COMPLETED NOSTR SIGNATURE TESTS =====");
        testResults = _logBuilder.ToString();
        
        // Copy results to clipboard for easy sharing
        GUIUtility.systemCopyBuffer = testResults;
        Log("Test results copied to clipboard");
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
        
        // Get the public key in both formats
        string compressedPublicKey = _keyManager.GetPublicKey(testPrivateKey, true);
        string uncompressedPublicKey = compressedPublicKey.StartsWith("02") || compressedPublicKey.StartsWith("03") 
            ? compressedPublicKey.Substring(2) 
            : compressedPublicKey;
            
        Log($"Test Private Key: {testPrivateKey}");
        Log($"Test Compressed Public Key: {compressedPublicKey}");
        Log($"Test Uncompressed Public Key: {uncompressedPublicKey}");
        
        // Verify key pair
        try
        {
            string message = "Test message";
            string signature = _keyManager.SignMessage(message, testPrivateKey);
            bool verified = _keyManager.VerifySignature(message, signature, compressedPublicKey);
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
    /// Tests basic message signing and verification
    /// </summary>
    private IEnumerator TestBasicSigning()
    {
        Log("--- Testing Basic Signing ---");
        
        try
        {
            // Sign a simple hex string directly 
            string testData = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
            Log($"Test data: {testData}");
            
            // Convert to bytes
            byte[] dataBytes = NostrSigner.HexToBytes(testData);
            
            // Sign with our wrapper
            string signature = NostrSigner.SignEventId(testData, testPrivateKey);
            Log($"Generated signature: {signature}");
            
            // Get the public key for verification
            string publicKey = _keyManager.GetPublicKey(testPrivateKey, true);
            
            // Verify the signature
            bool verified = NostrSigner.VerifySignatureHex(testData, signature, publicKey);
            Log($"Signature verification result: {verified}");
            
            if (!verified)
            {
                Log("ERROR: Basic signature verification failed!");
            }
            
            // Test with canonicalization explicitly
            byte[] signatureBytes = NostrSigner.HexToBytes(signature);
            byte[] canonicalSig = NostrSigner.GetCanonicalSignature(signatureBytes);
            string canonicalHex = NostrSigner.BytesToHex(canonicalSig);
            
            Log($"Original signature: {signature}");
            Log($"Canonical signature: {canonicalHex}");
            Log($"Signatures match: {signature == canonicalHex}");
            
            // Verify the canonical signature
            bool verifiedCanonical = NostrSigner.VerifySignatureHex(testData, canonicalHex, publicKey);
            Log($"Canonical signature verification: {verifiedCanonical}");
        }
        catch (Exception ex)
        {
            Log($"ERROR in basic signing test: {ex.Message}");
        }
        
        yield return null;
    }
    
    /// <summary>
    /// Tests full Nostr event signing and verification
    /// </summary>
    private IEnumerator TestEventSigning()
    {
        Log("--- Testing Nostr Event Signing ---");
        
        try
        {
            // Get the public key for the event
            string publicKey = _keyManager.GetPublicKey(testPrivateKey, true);
            string uncompressedPublicKey = publicKey.StartsWith("02") || publicKey.StartsWith("03") 
                ? publicKey.Substring(2) 
                : publicKey;
                
            // Create a test event
            string content = "Test message from SignatureTestComponent";
            string[][] tags = new string[0][];
            
            // Create the event using the NostrEvent class
            var nostrEvent = new NostrEvent(
                uncompressedPublicKey, 
                1, // Kind 1 = text note
                content, 
                tags,
                publicKey // Pass compressed key for verification
            );
            
            // Sign the event
            Log("Signing event...");
            nostrEvent.Sign(testPrivateKey);
            
            // Log the event details
            Log($"Event ID: {nostrEvent.Id}");
            Log($"Event pubkey: {nostrEvent.PublicKey}");
            Log($"Event signature: {nostrEvent.Signature}");
            
            // Verify the event signature locally
            bool verified = nostrEvent.VerifySignature();
            Log($"Event verification result: {verified}");
            
            if (!verified)
            {
                Log("ERROR: Event signature verification failed!");
            }
            
            // Do a deep debug verification
            bool deepVerified = nostrEvent.DeepDebugVerification();
            Log($"Deep verification result: {deepVerified}");
            
            // Get the serialized event for debugging
            string serializedEvent = nostrEvent.SerializeComplete();
            Log($"Complete serialized event: {serializedEvent}");
            
            // Verify with direct methods
            bool directVerify = NostrSigner.VerifySignatureHex(
                nostrEvent.Id, 
                nostrEvent.Signature, 
                publicKey
            );
            Log($"Direct verification result: {directVerify}");
        }
        catch (Exception ex)
        {
            Log($"ERROR in event signing test: {ex.Message}\n{ex.StackTrace}");
        }
        
        yield return null;
    }
    
    /// <summary>
    /// Tests sending an event to an actual relay
    /// </summary>
    private IEnumerator TestRelaySending()
    {
        Log("--- Testing Relay Publishing ---");
        
        try
        {
            // Get the public key for the event
            string publicKey = _keyManager.GetPublicKey(testPrivateKey, true);
            string uncompressedPublicKey = publicKey.StartsWith("02") || publicKey.StartsWith("03") 
                ? publicKey.Substring(2) 
                : publicKey;
                
            // Create a test event with timestamp and unique content
            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            string content = $"Signature test message at {timestamp}";
            string[][] tags = new string[0][];
            
            // Create the event
            var nostrEvent = new NostrEvent(
                uncompressedPublicKey, 
                1, 
                content, 
                tags,
                publicKey
            );
            
            // Sign the event
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
            bool published = false;
            yield return _nostrClient.PublishEvent(nostrEvent, result => published = result);
            
            Log($"Relay publish result: {published}");
            
            // Wait a bit to get relay response
            yield return new WaitForSeconds(2);
            
            Log("Checking relay response...");
            // Check if we have any errors or confirmations in the client
            if (_nostrClient.HasEventErrors(nostrEvent.Id, out string error))
            {
                Log($"Relay error: {error}");
            }
            else if (published)
            {
                Log("Event was successfully published to relay!");
            }
            else
            {
                Log("No explicit errors reported, but event may not have been published");
            }
        }
        catch (Exception ex)
        {
            Log($"ERROR in relay test: {ex.Message}");
        }
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