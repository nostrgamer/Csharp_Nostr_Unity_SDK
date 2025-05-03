# Nostr Unity SDK

A Unity SDK for integrating Nostr protocol into Unity applications.

## Requirements

- Unity 2021.3 or later
- .NET Standard 2.0 / 2.1 .NET 4.x compatibility

## Installation

### 1. Download Required DLLs

You need the following DLLs (exact names):
- `BouncyCastle.Crypto.dll` (from the official BouncyCastle NuGet package, netstandard2.0 or netstandard2.1)
- `NBitcoin.dll` (from the official NBitcoin NuGet package, netstandard2.1)
- `Newtonsoft.Json.dll` (from the official Newtonsoft.Json NuGet package, netstandard2.0 or netstandard2.1)
- `Microsoft.Extensions.Logging.Abstractions.dll` (from Microsoft.Extensions.Logging.Abstractions NuGet, netstandard2.0 or netstandard2.1)

**Do NOT use `BouncyCastle.Cryptography.dll` or any other similarly named DLLs.**

### 2. Place DLLs in Unity

1. Create a folder in your Unity project: `Assets/Plugins/`
2. Copy all the above DLLs into `Assets/Plugins/`
3. In Unity, select each DLL and ensure "Any Platform" is checked in the Inspector.

### 3. Unity Project Settings

- Go to `Edit > Project Settings > Player > Other Settings > Api Compatibility Level` and set to **.NET Standard 2.1** (recommended for Unity 6 and above).

### 4. Assembly Definition

- The SDK's `NostrSDK.asmdef` is already configured to reference these DLLs. No changes needed.

### 5. Clean and Reimport (if needed)

- If you have errors, close Unity, delete the `Library/` and `Temp/` folders, and reopen Unity.

## Dependencies

This SDK requires the following external libraries (DLLs):

| Library Name                                      | DLL Name                                    | Source (NuGet)                                      |
|---------------------------------------------------|---------------------------------------------|-----------------------------------------------------|
| BouncyCastle (official)                           | BouncyCastle.Crypto.dll                     | [BouncyCastle](https://www.nuget.org/packages/BouncyCastle/) |
| NBitcoin                                          | NBitcoin.dll                                | [NBitcoin](https://www.nuget.org/packages/NBitcoin/)         |
| Newtonsoft.Json                                   | Newtonsoft.Json.dll                         | [Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json/) |
| Microsoft.Extensions.Logging.Abstractions         | Microsoft.Extensions.Logging.Abstractions.dll| [Microsoft.Extensions.Logging.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Abstractions/) |

**All DLLs must be from the `netstandard2.1` (or `netstandard2.0` if available) folder of the NuGet package.**

## Getting Started

### Basic Setup

1. Create a new GameObject in your scene
2. Add the `NostrClient` component to it
3. Add the `NostrKeyManager` component to the same GameObject

### Key Management

```csharp
// Get a reference to the key manager
NostrKeyManager keyManager = GetComponent<NostrKeyManager>();

// Generate a new key pair
string privateKey = keyManager.GenerateNewKey();
string publicKey = keyManager.GetPublicKey(privateKey);

// Store keys securely with encryption
keyManager.StoreKeys(privateKey, "your-secure-password");

// Load previously stored keys
string loadedPrivateKey = keyManager.LoadPrivateKey("your-secure-password");
```

### Connecting to Relays

```csharp
// Get a reference to the client
NostrClient client = GetComponent<NostrClient>();

// Connect to a single relay
string relayUrl = "wss://relay.damus.io";
client.ConnectToRelay(relayUrl, (success) => {
    if (success) {
        Debug.Log("Connected to relay successfully");
    } else {
        Debug.LogError("Failed to connect to relay");
    }
});

// Connect to multiple relays
List<string> relayUrls = new List<string> {
    "wss://relay.damus.io",
    "wss://nos.lol",
    "wss://relay.snort.social"
};

foreach (string url in relayUrls) {
    client.ConnectToRelay(url, (success) => {
        if (success) {
            Debug.Log($"Connected to {url}");
        }
    });
}
```

### Publishing Events

```csharp
// Create a new event
NostrEvent event = new NostrEvent {
    Kind = 1, // Text note
    Content = "Hello from Unity!",
    CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
    Tags = new List<string[]>()
};

// Sign the event with your private key
event.Sign(privateKey);

// Publish the event
client.PublishEvent(event, (success) => {
    if (success) {
        Debug.Log("Event published successfully");
    } else {
        Debug.LogError("Failed to publish event");
    }
});
```

### Subscribing to Events

```csharp
// Create a filter for the events you want to receive
Filter filter = new Filter {
    Kinds = new int[] { 1 }, // Text notes
    Authors = new string[] { "your-public-key" }
};

// Subscribe to events matching the filter
string subscriptionId = client.Subscribe(filter, (event) => {
    Debug.Log($"Received event: {event.Content}");
});

// Later, when you want to unsubscribe
client.Unsubscribe(subscriptionId);
```

### Error Handling

The SDK includes error handling for common scenarios:
- Connection timeouts (10 seconds)
- Invalid keys
- Failed event publishing
- Network errors

Check the Unity console for detailed error messages and use the success callbacks to handle errors appropriately.

## Features

- **Key Management**: Generate new NSEC keys or securely store existing ones
- **Relay Connections**: Connect to one or more Nostr relays
- **Message Posting**: Publish events to the Nostr network
- **Event Subscriptions**: Subscribe to specific event types or users
- **Cross-Platform Support**: Works on all platforms Unity supports
- **Pure C# Implementation**: No external dependencies required

## Advanced Usage

### Secure Key Storage

```csharp
// Store keys securely (encrypted in PlayerPrefs)
keyManager.StoreKeys(privateKey, encrypt: true);

// Load previously stored keys
string loadedPrivateKey = keyManager.LoadPrivateKey();
```

### Multiple Relay Connections

```csharp
// Connect to multiple relays
List<string> relayUrls = new List<string>
{
    "wss://relay.damus.io",
    "wss://nos.lol",
    "wss://relay.snort.social"
};

await client.ConnectToRelays(relayUrls);
```

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Acknowledgements

- [Nostr Protocol](https://github.com/nostr-protocol/nips)
- [NativeWebSocket](https://github.com/endel/NativeWebSocket) for WebSocket communication 