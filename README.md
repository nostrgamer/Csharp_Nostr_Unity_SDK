# Nostr Unity SDK

A Unity SDK for integrating Nostr protocol into Unity applications.

## Requirements

- Unity 2021.3 or later
- .NET Standard 2.0 / 2.1 .NET 4.x compatibility

## Installation

### 1. Download Required DLLs

You need the following DLLs (exact names):
- `BouncyCastle.Crypto.dll` (from the official BouncyCastle NuGet package, netstandard2.0 or netstandard2.1)
- `Newtonsoft.Json.dll` (from the official Newtonsoft.Json NuGet package, netstandard2.0 or netstandard2.1)
- `Microsoft.Extensions.Logging.Abstractions.dll` (from Microsoft.Extensions.Logging.Abstractions NuGet, netstandard2.0 or netstandard2.1)

**Do NOT use `BouncyCastle.Cryptography.dll` or any other similarly named DLLs.**

### 2. Place DLLs in Unity

1. Create a folder in your Unity project: `Assets/Plugins/`
2. Copy all the above DLLs into `Assets/Plugins/`
3. In Unity, select each DLL and ensure "Any Platform" is checked in the Inspector.

### 3. Unity Project Settings

- Go to `Edit > Project Settings > Player > Other Settings > Api Compatibility Level` and set to **.NET Standard 2.1** (recommended for Unity 2021 and above).

### 4. Assembly Definition

- The SDK's `NostrSDK.asmdef` is already configured to reference these DLLs. No changes needed.

### 5. Clean and Reimport (if needed)

- If you have errors, close Unity, delete the `Library/` and `Temp/` folders, and reopen Unity.

## Dependencies

This SDK requires the following external libraries (DLLs):

| Library Name                                      | DLL Name                                    | Source (NuGet)                                      |
|---------------------------------------------------|---------------------------------------------|-----------------------------------------------------|
| BouncyCastle (official)                           | BouncyCastle.Crypto.dll                     | [BouncyCastle](https://www.nuget.org/packages/BouncyCastle/) |
| Newtonsoft.Json                                   | Newtonsoft.Json.dll                         | [Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json/) |
| Microsoft.Extensions.Logging.Abstractions         | Microsoft.Extensions.Logging.Abstractions.dll| [Microsoft.Extensions.Logging.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Abstractions/) |

**All DLLs must be from the `netstandard2.1` (or `netstandard2.0` if available) folder of the NuGet package.**

## Getting Started

### Quick Setup

1. Add the SDK to your Unity project
2. Create a GameObject in your scene
3. Add the `NostrManager.cs` script as a component to it
4. Create a script for your application logic and attach it to a GameObject
5. Use the following example code as a starting point:

```csharp
using UnityEngine;
using Nostr.Unity;
using System.Collections;

public class MyNostrApp : MonoBehaviour
{
    // Reference to the NostrManager component
    [SerializeField] private NostrManager nostrManager;
    
    void Start()
    {
        // If not assigned in the Inspector, find the NostrManager component
        if (nostrManager == null)
        {
            nostrManager = FindObjectOfType<NostrManager>();
            if (nostrManager == null)
            {
                Debug.LogError("NostrManager component not found in scene. Please add it to a GameObject first.");
                return;
            }
        }
        
        // Subscribe to events
        nostrManager.OnConnected += OnRelayConnected;
        nostrManager.OnDisconnected += OnRelayDisconnected;
        nostrManager.OnError += OnError;
        nostrManager.OnEventReceived += OnEventReceived;
    }
    
    // Event handlers
    private void OnRelayConnected(string relayUrl)
    {
        Debug.Log($"Connected to relay: {relayUrl}");
    }
    
    private void OnRelayDisconnected(string relayUrl)
    {
        Debug.Log($"Disconnected from relay: {relayUrl}");
    }
    
    private void OnError(string errorMessage)
    {
        Debug.LogError($"Nostr error: {errorMessage}");
    }
    
    private void OnEventReceived(NostrEvent nostrEvent)
    {
        Debug.Log($"Event received: {nostrEvent.Content}");
    }
    
    // Example: Publish a text note
    public void PublishTextNote(string content)
    {
        if (nostrManager == null) return;
        
        // The NostrManager handles key management and signing internally
        nostrManager.PostTextNote(content);
    }
    
    // Example: Connect to default relays
    public void ConnectToDefaultRelays()
    {
        if (nostrManager == null) return;
        
        nostrManager.ConnectToRelays();
    }
    
    // Example: Subscribe to notes from a specific user
    public void SubscribeToUser(string publicKey)
    {
        if (nostrManager == null || nostrManager.Client == null) return;
        
        var filter = new Filter();
        filter.Authors = new string[] { publicKey };
        filter.Kinds = new int[] { (int)NostrEventKind.TextNote };
        
        string subscriptionId = nostrManager.Client.Subscribe(filter, OnSubscriptionEventReceived);
        Debug.Log($"Subscribed to user with ID: {subscriptionId}");
    }
    
    // Helper method to handle events from subscription
    private void OnSubscriptionEventReceived(NostrEvent nostrEvent)
    {
        Debug.Log($"Received note: {nostrEvent.Content}");
    }
}
```

### What NostrManager Does For You

The NostrManager component automatically:
1. Initializes cryptography
2. Generates or loads keys (keys are automatically generated if they don't exist)
3. Manages relay connections
4. Handles event signing
5. Provides a simple API for common operations

### Testing Your Connection

Once you have the SDK set up, you can test your connection by sending a test message:

#### Method 1: Using Code

Simply call the `SendTestMessage` method on your NostrManager instance:

```csharp
// Reference to the NostrManager
private NostrManager nostrManager;

// In your Start or another method
void SendTest()
{
    if (nostrManager != null)
    {
        // Send with default test message
        nostrManager.SendTestMessage();
        
        // Or with custom message
        nostrManager.SendTestMessage("My custom test message from Unity!");
    }
}
```

#### Method 2: Using the TestMessageSender Component

For a quick UI-based test:

1. Create a new UI Button in your scene
2. Create a GameObject and attach the `TestMessageSender` script to it
3. In the Inspector, assign your NostrManager to the script
4. (Optional) Add a UI InputField for custom messages
5. Assign the Button to the `sendButton` field
6. Press Play and click the button to send a test message

You can then check your test message on a web-based Nostr client like [Coracle](https://coracle.social) or [Primal](https://primal.net) by searching for your public key.

#### Method 3: Using the Unity Editor (Easiest)

The SDK includes a custom editor extension for NostrManager that adds testing tools right in the Inspector:

1. Select your NostrManager GameObject in the Hierarchy
2. Enter Play mode
3. Scroll down to the "Test Tools" section in the Inspector
4. Type your test message (or use the default)
5. Click the "Send Test Message" button

The custom editor also shows you the current connection status and lists all connected relays.

You can then check your test message on a web-based Nostr client like [Coracle](https://coracle.social) or [Primal](https://primal.net) by searching for your public key.

### Manual Key Management (If Needed)

If you need direct access to the key management functionality, you can use:

```csharp
// Get access to the key manager
NostrKeyManager keyManager = nostrManager.KeyManager;

// Example: Get your public key
string myPublicKey = nostrManager.PublicKey;

// Example: Sign a custom message (rarely needed)
string signature = keyManager.SignMessage("Custom message", nostrManager.PrivateKey);
```

### Best Practices

1. Always use the NostrManager component for the simplest integration
2. Secure your keys with strong passwords
3. Handle connection errors gracefully
4. Test with multiple relays for reliability
5. Subscribe to specific event types to avoid unnecessary network traffic

## Features

- **Key Management**: Generate new keys, sign messages, and verify signatures
- **Custom Bech32 Implementation**: Built-in Bech32 encoding/decoding for npub/nsec formatting
- **Relay Connections**: Connect to one or more Nostr relays with auto-reconnect
- **Message Posting**: Publish events to the Nostr network
- **Event Subscriptions**: Subscribe to specific event types or users
- **Event-Based Architecture**: Events for connection, disconnection, and error handling
- **Cross-Platform Support**: Works on all platforms Unity supports

## Implementation Notes

- This SDK uses BouncyCastle for cryptographic operations
- WebSockets are used for relay communication
- A custom Bech32 implementation is used for human-readable encoding of keys

### Components

- **NostrClient**: Manages relay connections and event publishing/subscribing
- **NostrKeyManager**: Handles key generation, signing, and verification
- **NostrEvent**: Represents a Nostr event with proper validation and serialization
- **Bech32Util**: Custom implementation for Bech32 encoding/decoding

## Security Considerations

- Store private keys securely
- The key storage implementation uses encryption but is intended for development use
- For production use, consider enhancing the key storage mechanism

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Acknowledgements

- [Nostr Protocol](https://github.com/nostr-protocol/nips)
- [BouncyCastle](https://www.bouncycastle.org/)