using UnityEngine;
using NostrUnity.Crypto;
using NostrUnity.Services;
using System;
using System.Text;
using Newtonsoft.Json;

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
        [SerializeField] private string privateKeyHex; // Set this in the Unity Inspector
        [SerializeField] private string relayUrl = "wss://relay.damus.io"; // Default relay

        private NostrWebSocketService _webSocketService;

        private void Start()
        {
            // Generate a new key pair if none is set
            if (string.IsNullOrEmpty(privateKeyHex))
            {
                var (privateKey, publicKey) = NostrCrypto.GenerateKeyPair();
                privateKeyHex = privateKey;
                Debug.Log($"Generated new key pair:\nPrivate Key: {privateKey}\nPublic Key: {publicKey}");
                Debug.Log($"Nsec: {NostrCrypto.GetNsec(privateKey)}\nNpub: {NostrCrypto.GetNpub(publicKey)}");
            }

            // Setup WebSocket service
            _webSocketService = gameObject.AddComponent<NostrWebSocketService>();
            _webSocketService.Connect(relayUrl, 
                onConnectionStatusChanged: (connected) => 
                {
                    if (connected)
                    {
                        Debug.Log("Connected to relay, creating event...");
                        CreateAndSignTestEvent();
                    }
                },
                onMessageReceived: (message) => 
                {
                    Debug.Log($"Received from relay: {message}");
                },
                onError: (error) => 
                {
                    Debug.LogError($"Relay error: {error}");
                }
            );
        }

        private void CreateAndSignTestEvent()
        {
            try
            {
                Debug.Log("Starting event creation...");
                
                // Create a simple text note event
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var content = "Hello from Unity!";
                Debug.Log($"Timestamp: {timestamp}");
                
                // Get the public key in the correct format (32 bytes)
                string publicKeyHex = NostrCrypto.GetPublicKey(privateKeyHex);
                Debug.Log($"Public Key (32 bytes): {publicKeyHex}");
                
                // Create the event object
                var eventData = new NostrEvent
                {
                    id = null, // Will be set after hashing
                    pubkey = publicKeyHex,
                    created_at = timestamp,
                    kind = 1,
                    tags = new string[0][],
                    content = content,
                    sig = null // Will be set after signing
                };

                // Convert to JSON and hash it to get the event ID
                string eventJson = JsonConvert.SerializeObject(eventData, new JsonSerializerSettings 
                { 
                    NullValueHandling = NullValueHandling.Ignore,
                    ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()
                });
                Debug.Log($"Event JSON before hashing:\n{eventJson}");
                
                // Remove the sig field for hashing
                eventJson = eventJson.Replace(",\"sig\":null", "");
                Debug.Log($"Event JSON after removing sig:\n{eventJson}");
                
                byte[] eventIdBytes = System.Security.Cryptography.SHA256.Create()
                    .ComputeHash(Encoding.UTF8.GetBytes(eventJson));
                string eventId = BitConverter.ToString(eventIdBytes).Replace("-", "").ToLowerInvariant();
                Debug.Log($"Event ID: {eventId}");

                // Sign the event
                string signature = NostrCrypto.SignEvent(eventId, privateKeyHex);
                Debug.Log($"Signature: {signature}");
                
                // Add the signature and ID to the event
                eventData.id = eventId;
                eventData.sig = signature;

                // Final event JSON
                string finalEventJson = JsonConvert.SerializeObject(eventData, new JsonSerializerSettings 
                { 
                    NullValueHandling = NullValueHandling.Ignore,
                    Formatting = Formatting.Indented
                });

                Debug.Log($"Final event JSON:\n{finalEventJson}");
                
                // Verify the signature
                bool isValid = NostrCrypto.VerifySignature(publicKeyHex, eventId, signature);
                Debug.Log($"Signature verification result: {isValid}");

                if (isValid)
                {
                    // Publish the event to the relay
                    _webSocketService.PublishEvent(finalEventJson);
                }
                else
                {
                    Debug.LogError("Event signature verification failed, not publishing");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error creating event: {ex.Message}\nStack trace: {ex.StackTrace}");
            }
        }

        private void OnDestroy()
        {
            if (_webSocketService != null)
            {
                _webSocketService.Disconnect();
            }
        }
    }
} 