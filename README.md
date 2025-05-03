# Nostr Unity SDK

A Unity SDK for integrating Nostr protocol into Unity applications.

## Requirements

- Unity 2021.3 or later
- .NET Standard 2.0 / .NET 4.x compatibility

## Installation

### Option 1: Using Unity Package Manager

1. Open the Package Manager window in Unity (Window > Package Manager)
2. Click the "+" button and select "Add package from git URL..."
3. Enter the following URL: `https://github.com/nostrgamer/Csharp_Nostr_Unity_SDK.git`

### Option 2: Manual Installation

1. Download the latest release from the [Releases](https://github.com/nostrgamer/Csharp_Nostr_Unity_SDK/releases) page
2. Import the package into your Unity project

## Dependencies

This package relies on:
- NBitcoin (for Bech32 encoding/decoding)
- Newtonsoft.Json (for JSON serialization/deserialization)
- BouncyCastle.Crypto (for cryptographic operations)
- Microsoft.Extensions (required dependency)

### Installing Dependencies

To install the required dependencies:

1. First, install the NuGet Custom package:
   - Download the NuGet package from the [NuGet release page](https://github.com/GlitchEnzo/NuGetForUnity/releases)
   - In Unity, go to Assets > Import Package > Custom Package
   - Navigate to and select the downloaded NuGet package

2. Once installed, go to NuGet > Manage NuGet Packages in Unity
3. Search for and install the following packages:
   - BouncyCastle.Cryptography (this will automatically install Microsoft.Extensions)
   - NBitcoin
   - Newtonsoft.Json

The packages will be installed in the appropriate location and the assembly definition file in the SDK is configured to reference these dependencies correctly.

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