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

### Basic Setup

1. Create a new GameObject in your scene
2. Add the `NostrClient.cs` script as a component to it
3. NostrKeyManager is not a MonoBehaviour - instead, create an instance of it:

```csharp
// Create an instance of NostrKeyManager
NostrKeyManager keyManager = new NostrKeyManager();

// Or in the MonoBehaviour's Start/Awake method
private NostrKeyManager _keyManager;

void Awake()
{
    _keyManager = new NostrKeyManager();
}
```

### Key Management

```csharp
// If NostrKeyManager is a field in your MonoBehaviour
private NostrKeyManager _keyManager;

void Awake()
{
    _keyManager = new NostrKeyManager();
}

void Start()
{
    // Generate a new key pair
    string privateKey = _keyManager.GeneratePrivateKey();
    string publicKey = _keyManager.GetPublicKey(privateKey);

    // Store keys securely with encryption
    bool stored = _keyManager.StoreKeys(privateKey, "your-secure-password");

    // Load previously stored keys
    string loadedPrivateKey = _keyManager.LoadPrivateKey("your-secure-password");
}
```

### Connecting to Relays

```csharp
// Get a reference to the client
NostrClient client = GetComponent<NostrClient>();

// Connect to a relay using the coroutine pattern
StartCoroutine(client.ConnectToRelay("wss://relay.damus.io", (success) => {
    if (success) {
        Debug.Log("Connected to relay successfully");
    } else {
        Debug.LogError("Failed to connect to relay");
    }
}));

// Connect to multiple relays
List<string> relayUrls = new List<string> {
    "wss://relay.damus.io",
    "wss://nos.lol",
    "wss://relay.snort.social"
};

foreach (string url in relayUrls) {
    StartCoroutine(client.ConnectToRelay(url, (success) => {
        if (success) {
            Debug.Log($"Connected to {url}");
        }
    }));
}
```

### Publishing Events

```csharp
// Create a new event
var nostrEvent = new NostrEvent(
    publicKey,                 // your public key
    (int)NostrEventKind.TextNote,  // kind 1 = text note
    "Hello from Unity!"        // content
);

// Sign the event with your private key
nostrEvent.Sign(privateKey);

// Publish the event
StartCoroutine(client.PublishEvent(nostrEvent, (success) => {
    if (success) {
        Debug.Log("Event published successfully");
    } else {
        Debug.LogError("Failed to publish event");
    }
}));
```

### Subscribing to Events

```csharp
// Create a filter for the events you want to receive
var filter = new Filter();
filter.Kinds = new List<int> { (int)NostrEventKind.TextNote };
filter.Authors = new List<string> { publicKey };

// Subscribe to events matching the filter
string subscriptionId = client.Subscribe(filter, (nostrEvent) => {
    Debug.Log($"Received event: {nostrEvent.Content}");
});

// Later, when you want to unsubscribe
client.Unsubscribe(subscriptionId);
```

### Error Handling

The SDK includes error handling for common scenarios with event-based notifications:
- Connection timeouts and auto-reconnect
- Invalid keys and signature verification
- Failed event publishing
- Network errors

Use the Connected, Disconnected, and Error events on the NostrClient to handle different scenarios:

```csharp
client.Connected += (sender, relayUrl) => {
    Debug.Log($"Connected to {relayUrl}");
};

client.Disconnected += (sender, relayUrl) => {
    Debug.Log($"Disconnected from {relayUrl}");
};

client.Error += (sender, errorMessage) => {
    Debug.LogError($"Error: {errorMessage}");
};

client.EventReceived += (sender, args) => {
    Debug.Log($"Event received from {args.RelayUrl}: {args.Event.Content}");
};
```

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

## Csharp Nostr Unity SDK

A Unity SDK for interacting with the Nostr protocol, written in C#.

### Features
- .NET Standard 2.0 / 2.1 .NET 4.x compatibility
- Key management and cryptographic operations
- Event creation and signing
- Relay connection management
- Secure key storage

### Quick Start

1. Add the SDK to your Unity project
2. Create a new GameObject in your scene
3. Add the `NostrClient.cs` script as a component to it
4. Create a new script in your Unity project (e.g., `NostrExample.cs`) and attach it to a GameObject
5. Copy and paste the following example code:

```csharp
using UnityEngine;
using Nostr.Unity;

public class NostrExample : MonoBehaviour
{
    private NostrManager nostrManager;
    
    void Start()
    {
        // Create a GameObject to hold the NostrManager if it doesn't exist
        GameObject nostrObject = new GameObject("NostrManager");
        nostrManager = nostrObject.AddComponent<NostrManager>();
        
        // The NostrManager will automatically:
        // 1. Initialize the key manager
        // 2. Load existing keys or generate new ones
        // 3. Connect to default relays
        
        // Subscribe to events
        nostrManager.OnConnected += (relay) => Debug.Log($"Connected to relay: {relay}");
        nostrManager.OnDisconnected += (relay) => Debug.Log($"Disconnected from relay: {relay}");
        nostrManager.OnError += (error) => Debug.Log($"Error: {error}");
        nostrManager.OnEventReceived += (nostrEvent) => Debug.Log($"Received event: {nostrEvent.Content}");
    }
    
    // Example of how to publish a text note
    public async void PublishTextNote(string content)
    {
        if (nostrManager != null && nostrManager.Client != null)
        {
            var textNote = new NostrEvent(
                nostrManager.PublicKey,
                (int)NostrEventKind.TextNote,
                content
            );
            
            // The event will be automatically signed using your private key
            await nostrManager.Client.PublishEvent(textNote);
        }
    }
}
```

### Advanced Usage

For more control over key management, you can use the `NostrKeyManager` directly:

```csharp
using UnityEngine;
using Nostr.Unity;

public class AdvancedNostrExample : MonoBehaviour
{
    private NostrKeyManager keyManager;
    
    void Start()
    {
        // Initialize the key manager
        keyManager = new NostrKeyManager();
        
        // Generate a new private key (returns hex format by default)
        string privateKey = keyManager.GeneratePrivateKey();
        
        // Get the corresponding public key
        string publicKey = keyManager.GetPublicKey(privateKey);
        
        // Store keys securely (with encryption)
        bool stored = keyManager.StoreKeys(privateKey, "your-secure-password");
        
        // Load stored keys later
        string loadedPrivateKey = keyManager.LoadPrivateKey("your-secure-password");
        
        // Sign a message
        string message = "Hello, Nostr!";
        string signature = keyManager.SignMessage(message, privateKey);
        
        // Verify a signature
        bool isValid = keyManager.VerifySignature(message, signature, publicKey);
    }
}
```

### Best Practices

1. Always store private keys securely using the built-in encryption
2. Use try-catch blocks when performing cryptographic operations
3. Check connection status before publishing events
4. Subscribe to events to handle connection state changes
5. Use the `NostrManager` for simplified integration
6. Keep your password secure and never hardcode it

### Documentation

For more detailed documentation and examples, see the [Wiki](link-to-wiki).

### License

[Your License] 