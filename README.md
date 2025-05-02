# C# Nostr Unity SDK

A Unity SDK for integrating the [Nostr protocol](https://github.com/nostr-protocol/nips) into Unity applications, enabling games to connect and interact with the Nostr network.

## Features

- **Key Management**: Generate new NSEC keys or securely store existing ones
- **Relay Connections**: Connect to one or more Nostr relays
- **Message Posting**: Publish events to the Nostr network
- **Event Subscriptions**: Subscribe to specific event types or users
- **Cross-Platform Support**: Works on all platforms Unity supports
- **Pure C# Implementation**: No external dependencies required

## Installation

### Option 1: Unity Package Manager (Git URL)

1. Open your Unity project
2. Go to `Window > Package Manager`
3. Click the `+` button and select "Add package from git URL..."
4. Enter `https://github.com/nostrgamer/Csharp_Nostr_Unity_SDK.git`
5. Click "Add"

### Option 2: Manual Installation

1. Download this repository
2. Copy the contents into your Unity project's `Assets` folder

## Getting Started

### Basic Setup

```csharp
using Nostr.Unity;

// Initialize the Nostr client
NostrClient client = new NostrClient();

// Connect to a relay
await client.ConnectToRelay("wss://relay.damus.io");

// Generate or load keys
NostrKeyManager keyManager = new NostrKeyManager();
string privateKey = keyManager.GeneratePrivateKey();
string publicKey = keyManager.GetPublicKey(privateKey);

// Create and send a note
NostrEvent noteEvent = new NostrEvent
{
    Kind = NostrEventKind.TextNote,
    Content = "Hello from Unity!",
    CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
};

// Sign and publish the event
noteEvent.Sign(privateKey);
await client.PublishEvent(noteEvent);

// Disconnect when done
client.Disconnect();
```

### Subscribing to Events

```csharp
// Subscribe to text notes
Filter filter = new Filter
{
    Kinds = new[] { NostrEventKind.TextNote },
    Limit = 10
};

string subscriptionId = client.Subscribe(filter);
client.EventReceived += (sender, e) =>
{
    Debug.Log($"Received event: {e.Event.Content}");
};

// Unsubscribe when no longer needed
client.Unsubscribe(subscriptionId);
```

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