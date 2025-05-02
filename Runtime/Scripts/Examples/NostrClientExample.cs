using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Nostr.Unity;
using System;
using System.Threading.Tasks;

/// <summary>
/// Example script that demonstrates basic Nostr functionality
/// Attach this to a GameObject in your scene
/// </summary>
public class NostrClientExample : MonoBehaviour
{
    // Reference to the Nostr client
    private NostrClient client;
    private NostrKeyManager keyManager;
    
    // Settings
    public string relayUrl = "wss://relay.damus.io";
    public string messageToSend = "Hello from Unity!";
    public bool connectOnStart = false;
    
    // Status tracking
    public bool isConnected = false;
    public string publicKey = "";
    private string privateKey = "";
    
    void Start()
    {
        // Initialize on start if enabled
        if (connectOnStart)
        {
            InitializeNostr();
        }
    }
    
    void OnDestroy()
    {
        // Clean up when destroyed
        DisconnectFromRelay();
    }
    
    /// <summary>
    /// Initialize the Nostr client and key manager
    /// </summary>
    public async void InitializeNostr()
    {
        try
        {
            Debug.Log("Initializing Nostr client...");
            
            // Initialize the client and key manager
            client = new NostrClient();
            keyManager = new NostrKeyManager();
            
            // Generate or load keys
            privateKey = keyManager.GeneratePrivateKey();
            publicKey = keyManager.GetPublicKey(privateKey);
            
            Debug.Log($"Generated keys. Public key: {publicKey}");
            
            // Connect to relay
            await ConnectToRelay();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error initializing Nostr: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Connect to the Nostr relay
    /// </summary>
    public async Task ConnectToRelay()
    {
        try
        {
            if (client == null)
            {
                Debug.LogWarning("Nostr client not initialized. Call InitializeNostr first.");
                return;
            }
            
            Debug.Log($"Connecting to relay: {relayUrl}");
            await client.ConnectToRelay(relayUrl);
            isConnected = true;
            Debug.Log("Connected to relay successfully");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error connecting to relay: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Disconnect from the relay
    /// </summary>
    public void DisconnectFromRelay()
    {
        try
        {
            if (client != null && isConnected)
            {
                client.Disconnect();
                isConnected = false;
                Debug.Log("Disconnected from relay");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error disconnecting: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Send a text note to the relay
    /// </summary>
    public async void SendMessage()
    {
        try
        {
            if (!isConnected || client == null)
            {
                Debug.LogWarning("Not connected to relay. Please connect first.");
                return;
            }
            
            // Create and send a note
            NostrEvent noteEvent = new NostrEvent
            {
                Kind = NostrEventKind.TextNote,
                Content = messageToSend,
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
            
            // Sign and publish the event
            noteEvent.Sign(privateKey);
            await client.PublishEvent(noteEvent);
            
            Debug.Log($"Message sent: {messageToSend}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error sending message: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Subscribe to receive text notes
    /// </summary>
    public void SubscribeToNotes()
    {
        try
        {
            if (!isConnected || client == null)
            {
                Debug.LogWarning("Not connected to relay. Please connect first.");
                return;
            }
            
            // Subscribe to text notes
            Filter filter = new Filter
            {
                Kinds = new[] { NostrEventKind.TextNote },
                Limit = 10
            };
            
            string subscriptionId = client.Subscribe(filter);
            client.EventReceived += OnEventReceived;
            
            Debug.Log($"Subscribed to text notes with ID: {subscriptionId}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error subscribing: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Event handler for received events
    /// </summary>
    private void OnEventReceived(object sender, NostrEventArgs e)
    {
        Debug.Log($"Received event: {e.Event.Content}");
    }
} 