# Testing the Nostr Unity SDK

This guide will help you set up a simple test scene to verify the basic functionality of the Nostr Unity SDK.

## Setup

1. Import the package into your Unity project using the Package Manager:
   - Go to `Window > Package Manager`
   - Click the `+` button and select "Add package from disk..."
   - Navigate to and select the `package.json` file

2. Create a new scene:
   - Go to `File > New Scene`
   - Save the scene (e.g., "NostrTest.unity")

## Create a Test UI

1. Add a Canvas to your scene:
   - Right-click in the Hierarchy, then select `UI > Canvas`
   - Right-click on the Canvas, then select `UI > Panel` to create a background
   - Set the Panel's color to a semi-transparent dark color

2. Add UI elements to the Canvas:
   - Add a Button for "Generate Keys"
   - Add a Button for "Connect to Relays"
   - Add a Button for "Post Test Message"
   - Add a TMP_InputField for "NSEC Key Input"
   - Add a Button for "Import NSEC Key"
   - Add a TextMeshPro - Text field to display status messages (requires TextMeshPro package)

## Add Nostr Manager

1. Create an empty GameObject named "NostrManager" in your scene
2. Add the NostrManager component to it:
   - With the GameObject selected, click "Add Component" in the Inspector
   - Search for "NostrManager" and add it
3. Configure the NostrManager component in the Inspector:
   - You can leave "Default Relays" empty to use the default ones
   - Set "Connect On Start" to false so we can control it with our UI
   - Set "Auto Generate Keys" to false so we can trigger it manually

## Create a Test Script

Create a new C# script named "NostrTest.cs" and attach it to your Canvas:

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Nostr.Unity;

public class NostrTest : MonoBehaviour
{
    [SerializeField]
    private Button generateKeysButton;
    
    [SerializeField]
    private Button connectButton;
    
    [SerializeField]
    private Button postMessageButton;
    
    [SerializeField]
    private TMP_InputField nsecInputField;
    
    [SerializeField]
    private Button importNsecButton;
    
    [SerializeField]
    private TMP_Text statusText;
    
    private NostrManager nostrManager;
    
    void Start()
    {
        nostrManager = FindObjectOfType<NostrManager>();
        
        if (nostrManager == null)
        {
            Debug.LogError("NostrManager not found in scene!");
            return;
        }
        
        // Subscribe to NostrManager events
        nostrManager.OnConnected += OnConnected;
        nostrManager.OnDisconnected += OnDisconnected;
        nostrManager.OnError += OnError;
        nostrManager.OnEventReceived += OnEventReceived;
        
        // Set up button listeners
        generateKeysButton.onClick.AddListener(GenerateKeys);
        connectButton.onClick.AddListener(Connect);
        postMessageButton.onClick.AddListener(PostMessage);
        
        // Set up NSEC import button
        if (importNsecButton != null)
        {
            importNsecButton.onClick.AddListener(ImportNsecKey);
        }
        
        UpdateStatus("Ready to test. Start by generating or importing keys.");
    }
    
    void GenerateKeys()
    {
        nostrManager.LoadOrCreateKeys();
        UpdateStatus("Keys loaded or generated.");
    }
    
    void ImportNsecKey()
    {
        if (nsecInputField == null || string.IsNullOrEmpty(nsecInputField.text))
        {
            UpdateStatus("Please enter an NSEC key first.");
            return;
        }
        
        string nsecKey = nsecInputField.text.Trim();
        
        try
        {
            nostrManager.SetPrivateKey(nsecKey);
            UpdateStatus($"Key imported successfully! Public key: {nostrManager.ShortPublicKey}");
            
            // Clear the input field for security
            nsecInputField.text = "";
        }
        catch (System.Exception ex)
        {
            UpdateStatus($"Error importing key: {ex.Message}");
        }
    }
    
    void Connect()
    {
        UpdateStatus("Connecting to relays...");
        nostrManager.ConnectToRelays();
    }
    
    void PostMessage()
    {
        UpdateStatus("Posting test message...");
        nostrManager.PostTextNote("Hello from Unity Nostr SDK! This is a test message.");
    }
    
    void OnConnected(string relay)
    {
        UpdateStatus($"Connected to relay: {relay}");
    }
    
    void OnDisconnected(string relay)
    {
        UpdateStatus($"Disconnected from relay: {relay}");
    }
    
    void OnError(string error)
    {
        UpdateStatus($"Error: {error}");
    }
    
    void OnEventReceived(NostrEvent nostrEvent)
    {
        UpdateStatus($"Received event: {nostrEvent.Content}");
    }
    
    void UpdateStatus(string message)
    {
        Debug.Log(message);
        if (statusText != null)
        {
            statusText.text = message;
        }
    }
    
    void OnDestroy()
    {
        // Unsubscribe from events
        if (nostrManager != null)
        {
            nostrManager.OnConnected -= OnConnected;
            nostrManager.OnDisconnected -= OnDisconnected;
            nostrManager.OnError -= OnError;
            nostrManager.OnEventReceived -= OnEventReceived;
        }
    }
}
```

## Assign References in the Inspector

1. Select your Canvas in the Hierarchy
2. In the Inspector, find the NostrTest component
3. Drag the appropriate UI elements to the corresponding fields:
   - Drag your "Generate Keys" button to the "Generate Keys Button" field
   - Drag your "Connect" button to the "Connect Button" field
   - Drag your "Post Message" button to the "Post Message Button" field
   - Drag your NSEC input field to the "Nsec Input Field" field
   - Drag your "Import NSEC" button to the "Import Nsec Button" field
   - Drag your TMP_Text to the "Status Text" field

## Test the Implementation

1. Run the scene
2. You can either:
   - Click "Generate Keys" - this should generate a new key pair, or
   - Enter an NSEC key into the input field and click "Import NSEC Key" 
3. Click "Connect to Relays" - this should connect to relays
4. Click "Post Message" - this should post a message to connected relays

Note that the WebSocket connection will attempt to connect to real relays, but the cryptography implementation is a placeholder at this stage.

## Expected Results

- The console should show debug messages for each operation
- The status text should update with each action
- When importing an NSEC key, you should see the derived public key
- When connecting to relays, you should see connection status messages
- When posting a message, you should see confirmation in the status text 