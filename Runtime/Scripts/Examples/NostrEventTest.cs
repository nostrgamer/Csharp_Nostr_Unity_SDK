using UnityEngine;
using NostrUnity.Crypto;
using NostrUnity.Services;
using System;
using System.Text;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Linq;
using System.Collections;

namespace NostrUnity.Examples
{
    [Serializable]
    public class NostrEvent
    {
        public string id;
        public string pubkey;
        public long created_at;
        public int kind;
        public string[][] tags;
        public string content;
        public string sig;
    }

    public class NostrEventTest : MonoBehaviour
    {
        [Header("Nostr Connection Settings")]
        [SerializeField] private string[] relayUrls = new[] { 
            "wss://relay.damus.io",
            "wss://relay.nostr.band",
            "wss://nos.lol",
            "wss://relay.snort.social"
        }; // Multiple relays
        [SerializeField] private int minRelaysToWaitFor = 1; // Minimum relays to connect before sending
        [SerializeField] private float relayConnectTimeoutSec = 5f; // How long to wait for relay connections
        
        [Header("Nostr Key Settings")]
        [Tooltip("Enter your private key (hex format or nsec format)")]
        [SerializeField] private string privateKeyInput; // Set this in the Unity Inspector
        [Tooltip("Whether to use the private key provided above")]
        [SerializeField] private bool useProvidedKey = false; // Use the key provided in the inspector
        
        private NostrWebSocketService _webSocketService;
        private string privateKeyHex; // Store the converted or generated private key
        private string publicKeyHex; // Store the public key
        private Dictionary<string, NostrWebSocketService> _relayServices = new Dictionary<string, NostrWebSocketService>();
        private Dictionary<string, bool> _relayConnected = new Dictionary<string, bool>();
        private int _connectedRelays = 0;
        private bool _eventCreated = false;
        private string _lastEventJson = null;

        private void Start()
        {
            // Use provided key or generate a new one
            if (useProvidedKey && !string.IsNullOrEmpty(privateKeyInput))
            {
                // Convert nsec to hex if needed
                if (privateKeyInput.StartsWith("nsec1"))
                {
                    try
                    {
                        privateKeyHex = NostrCrypto.DecodeNsec(privateKeyInput);
                        Debug.Log($"Converted nsec to hex: {privateKeyHex}");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error converting nsec to hex: {ex.Message}");
                        Debug.LogError("Generating new key pair instead.");
                        GenerateNewKeyPair();
                        return;
                    }
                }
                else
                {
                    // Assume it's already in hex format
                    privateKeyHex = privateKeyInput;
                }

                try
                {
                    // Derive the public key from the private key
                    publicKeyHex = NostrCrypto.GetPublicKey(privateKeyHex);
                    
                    Debug.Log($"Using provided key:");
                    Debug.Log($"Public Key: {publicKeyHex}");
                    string npub = NostrCrypto.GetNpub(publicKeyHex);
                    Debug.Log($"Calculated Npub: {npub}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error using provided key: {ex.Message}");
                    Debug.LogError("Generating new key pair instead.");
                    GenerateNewKeyPair();
                }
            }
            else
            {
                GenerateNewKeyPair();
            }

            // Connect to multiple relays
            foreach (var relayUrl in relayUrls)
            {
                _relayConnected[relayUrl] = false;
                ConnectToRelay(relayUrl);
            }
            
            // Start a timer to check connections and create event after timeout
            StartCoroutine(WaitForRelaysAndCreateEvent());
        }

        private void GenerateNewKeyPair()
        {
            // Generate a new key pair
            var (privateKey, publicKey) = NostrCrypto.GenerateKeyPair();
            privateKeyHex = privateKey;
            publicKeyHex = publicKey;
            Debug.Log($"Generated new key pair:\nPrivate Key: {privateKey}\nPublic Key: {publicKey}");
            Debug.Log($"Npub: {NostrCrypto.GetNpub(publicKey)}");
            Debug.Log($"Nsec: {NostrCrypto.GetNsec(privateKey)}");
        }

        private IEnumerator WaitForRelaysAndCreateEvent()
        {
            // Wait for a few seconds to give relays time to connect
            float startTime = Time.time;
            
            while (_connectedRelays < minRelaysToWaitFor && Time.time - startTime < relayConnectTimeoutSec)
            {
                Debug.Log($"Waiting for relays to connect: {_connectedRelays}/{relayUrls.Length} connected");
                yield return new WaitForSeconds(0.5f);
            }
            
            // Create event if we have enough relays or we've timed out
            if (_connectedRelays > 0)
            {
                Debug.Log($"Connected to {_connectedRelays} relays, creating event...");
                CreateAndSignTestEvent();
            }
            else
            {
                Debug.LogWarning("Failed to connect to any relays within timeout period");
            }
        }

        private void ConnectToRelay(string relayUrl)
        {
            try
            {
                // Setup WebSocket service for this relay
                NostrWebSocketService webSocketService = gameObject.AddComponent<NostrWebSocketService>();
                _relayServices[relayUrl] = webSocketService;
                
                webSocketService.Connect(relayUrl, 
                    onConnectionStatusChanged: (connected) => 
                    {
                        if (connected)
                        {
                            Debug.Log($"Connected to relay: {relayUrl}");
                            _relayConnected[relayUrl] = true;
                            _connectedRelays++;
                            
                            // Save a reference to the first relay for testing
                            if (_webSocketService == null)
                            {
                                _webSocketService = webSocketService;
                            }
                            
                            // If we already created an event, broadcast it to this newly connected relay
                            if (_eventCreated && !string.IsNullOrEmpty(_lastEventJson))
                            {
                                Debug.Log($"Broadcasting previously created event to newly connected relay: {relayUrl}");
                                webSocketService.PublishEvent(_lastEventJson);
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"Disconnected from relay: {relayUrl}");
                            _relayConnected[relayUrl] = false;
                            _connectedRelays--;
                        }
                    },
                    onMessageReceived: (message) => 
                    {
                        Debug.Log($"Received from relay {relayUrl}: {message}");
                        
                        // Try to parse message to check for our event
                        if (!string.IsNullOrEmpty(_lastPublishedEventId) && message.Contains(_lastPublishedEventId))
                        {
                            Debug.Log($"Found our event on relay: {relayUrl}");
                        }
                    },
                    onError: (error) => 
                    {
                        Debug.LogError($"Relay error from {relayUrl}: {error}");
                    }
                );
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error connecting to relay {relayUrl}: {ex.Message}");
            }
        }

        private void CreateAndSignTestEvent()
        {
            try
            {
                Debug.Log("Starting event creation...");
                
                // Check that our keys are consistent
                string npubFromHex = NostrCrypto.GetNpub(publicKeyHex);
                Debug.Log($"Key verification - Public key hex: {publicKeyHex}");
                Debug.Log($"Key verification - Derived Npub: {npubFromHex}");
                
                // Create event object
                NostrEvent eventData = new NostrEvent
                {
                    kind = 1, // Text note
                    created_at = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    pubkey = publicKeyHex,
                    content = $"Hello from Unity! Message sent at {DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")} UTC",
                    tags = new string[0][] // Empty tags array for simplicity
                };
                
                // Calculate the event ID (sha256 of the serialized event)
                string idHex = CalculateEventId(eventData);
                eventData.id = idHex;
                
                // Sign the event using NostrCrypto
                eventData.sig = NostrCrypto.SignEvent(eventData.id, privateKeyHex);
                Debug.Log($"Event signature: {eventData.sig}");
                
                // Verify the signature locally before sending
                bool isValid = VerifyEventSignature(eventData);
                Debug.Log($"Local signature verification: {isValid}");
                
                if (!isValid)
                {
                    // IMPORTANT: We're still going to send it to see what the relay says
                    Debug.LogWarning("Event signature verification failed locally, but sending anyway to see relay response");
                }

                // Output the values used for verification by external relays
                Debug.Log($"Values that will be verified by relays:");
                Debug.Log($"Public key (npub): {npubFromHex}");
                Debug.Log($"Event ID (hex): {eventData.id} (length: {eventData.id.Length})");
                Debug.Log($"Signature (hex): {eventData.sig} (length: {eventData.sig.Length})");
                
                // Verify the event ID was correctly calculated
                string recomputedId = CalculateEventId(eventData);
                bool idValid = recomputedId == eventData.id;
                Debug.Log($"Event ID verification: {idValid}");
                
                // Check if any fields are null or empty
                if (string.IsNullOrEmpty(eventData.id) || 
                    string.IsNullOrEmpty(eventData.pubkey) || 
                    string.IsNullOrEmpty(eventData.sig) ||
                    string.IsNullOrEmpty(eventData.content))
                {
                    Debug.LogError("Event has null or empty required fields!");
                    foreach (var prop in typeof(NostrEvent).GetProperties())
                    {
                        Debug.Log($"{prop.Name}: {prop.GetValue(eventData)}");
                    }
                }
                
                // Save the event ID for verification
                _lastPublishedEventId = eventData.id;
                
                // Convert final event to JSON
                string finalJson = JsonConvert.SerializeObject(eventData);
                _lastEventJson = finalJson;
                _eventCreated = true;
                Debug.Log($"Final event JSON: {finalJson}");
                
                // Broadcast to all connected relays
                BroadcastToAllRelays(finalJson);
                
                // After a short delay, check if our event exists on the relay
                if (_verifyEventCoroutine != null)
                {
                    StopCoroutine(_verifyEventCoroutine);
                }
                _verifyEventCoroutine = StartCoroutine(VerifyEventPublished(eventData.id, 3.0f));
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error creating event: {ex.Message}\nStack trace: {ex.StackTrace}");
            }
        }
        
        private string _lastPublishedEventId;
        private Coroutine _verifyEventCoroutine;
        
        private System.Collections.IEnumerator VerifyEventPublished(string eventId, float delay)
        {
            yield return new WaitForSeconds(delay);
            
            Debug.Log("Verifying event was published and can be retrieved...");
            
            try 
            {
                // Create a subscription filter for the specific event
                var filter = new Dictionary<string, object>
                {
                    { "ids", new string[] { eventId } }
                };
                
                // Create the subscription request
                var reqId = $"verify_{System.Guid.NewGuid().ToString().Substring(0, 8)}";
                var request = new object[] { "REQ", reqId, filter };
                string requestJson = JsonConvert.SerializeObject(request);
                
                Debug.Log($"Sending verification request: {requestJson}");
                
                // Send the request to the relay
                _webSocketService.SendWebSocketMessage(requestJson);
                
                // Note: the response will come through the OnMessageReceived callback
                Debug.Log($"Sent verification request. Check logs for response with subscription ID: {reqId}");
                
                // Try alternative approach - check for our public key events
                var pubkeyFilter = new Dictionary<string, object>
                {
                    { "authors", new string[] { publicKeyHex } },
                    { "kinds", new int[] { 1 } },
                    { "limit", 20 }
                };
                
                // Create the subscription request
                var pubkeyReqId = $"verify_pubkey_{System.Guid.NewGuid().ToString().Substring(0, 8)}";
                var pubkeyRequest = new object[] { "REQ", pubkeyReqId, pubkeyFilter };
                string pubkeyRequestJson = JsonConvert.SerializeObject(pubkeyRequest);
                
                Debug.Log($"Sending pubkey events request: {pubkeyRequestJson}");
                
                // Send the request to the relay
                _webSocketService.SendWebSocketMessage(pubkeyRequestJson);
            }
            catch (Exception ex) 
            {
                Debug.LogError($"Error verifying event: {ex.Message}");
            }
        }
        
        private bool VerifyEventSignature(NostrEvent eventData)
        {
            if (string.IsNullOrEmpty(eventData.id) || string.IsNullOrEmpty(eventData.pubkey) || string.IsNullOrEmpty(eventData.sig))
            {
                Debug.LogError("Event is missing required fields for verification");
                return false;
            }
            
            return NostrCrypto.VerifySignature(eventData.pubkey, eventData.id, eventData.sig);
        }
        
        private string CalculateEventId(NostrEvent eventData)
        {
            try
            {
                // For empty tags, use an empty array [] instead of [[]]
                var tags = eventData.tags != null && eventData.tags.Length > 0 ? eventData.tags : new string[0][];
                
                // Create an array with the specific order required by NIP-01
                var eventForId = new object[]
                {
                    0,
                    eventData.pubkey,
                    eventData.created_at,
                    eventData.kind,
                    tags,
                    eventData.content
                };
                
                // Use standard JavaScript-like serialization settings to match Nostr relay expectations
                // This is crucial for signature verification across different implementations
                string json = JsonConvert.SerializeObject(eventForId, new JsonSerializerSettings 
                { 
                    Formatting = Formatting.None,
                    NullValueHandling = NullValueHandling.Ignore,
                    StringEscapeHandling = StringEscapeHandling.Default
                });
                
                Debug.Log($"JSON for ID calculation: {json}");
                
                // Calculate SHA256 hash
                using (SHA256 sha256 = SHA256.Create())
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(json);
                    byte[] hash = sha256.ComputeHash(bytes);
                    string hex = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    Debug.Log($"Calculated event ID: {hex}");
                    return hex;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error calculating event ID: {ex.Message}\nStack trace: {ex.StackTrace}");
                throw;
            }
        }
        
        private byte[] StringToByteArray(string hex)
        {
            int numberChars = hex.Length;
            byte[] bytes = new byte[numberChars / 2];
            for (int i = 0; i < numberChars; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            }
            return bytes;
        }

        private void OnDestroy()
        {
            if (_webSocketService != null)
            {
                _webSocketService.Disconnect();
            }
        }

        private void BroadcastToAllRelays(string eventJson)
        {
            Debug.Log($"Broadcasting event to {_relayServices.Count} relays ({_connectedRelays} connected)...");
            
            int broadcastCount = 0;
            foreach (var relay in _relayServices)
            {
                string relayUrl = relay.Key;
                NostrWebSocketService service = relay.Value;
                
                try
                {
                    // Only send to connected relays
                    if (_relayConnected.ContainsKey(relayUrl) && _relayConnected[relayUrl])
                    {
                        Debug.Log($"Sending event to relay: {relayUrl}");
                        service.PublishEvent(eventJson);
                        broadcastCount++;
                    }
                    else
                    {
                        Debug.Log($"Skipping relay {relayUrl} - not connected yet");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error sending to relay {relayUrl}: {ex.Message}");
                }
            }
            
            Debug.Log($"Event broadcast to {broadcastCount} relays");
        }
    }
} 