# Nostr Unity SDK

A Unity SDK for interacting with the Nostr protocol, providing easy-to-use functionality for creating, signing, and publishing Nostr events.

## Features

- Schnorr signature support using NBitcoin.Secp256k1
- WebSocket communication with Nostr relays
- Key pair generation and management
- Bech32 encoding for npub/nsec keys
- Event creation and signing
- Simple API for posting text notes

## Requirements

- Unity 2021.3 or later
- Required DLLs (place in `Assets/Plugins` folder):
  - NBitcoin.Secp256k1.dll
  - Newtonsoft.Json.dll
  - System.Text.Json.dll

## Installation

### Option 1: Unity Package Manager (Recommended)

1. Open your Unity project
2. Go to Window > Package Manager
3. Click the + button in the top-left corner
4. Select "Add package from git URL"
5. Enter: `https://github.com/nostrgamer/Csharp_Nostr_Unity_SDK.git`
6. Click "Add"

### Option 2: Manual Installation

1. Clone this repository or download it as a ZIP
2. Copy the entire SDK folder into your project's `Packages` folder
3. Ensure all required DLLs are in your `Assets/Plugins` folder

## Quick Start

1. Create a new GameObject in your scene
2. Add the `NostrManager` component to it
3. Use the following code to get started:

```csharp
using NostrUnity;

public class YourScript : MonoBehaviour
{
    private NostrManager _nostrManager;

    void Start()
    {
        // Initialize with a new key pair
        _nostrManager = gameObject.AddComponent<NostrManager>();
        _nostrManager.Initialize();

        // Or initialize with an existing private key
        // _nostrManager.Initialize("your_private_key_here");

        // Post a text note
        _nostrManager.PostTextNote("Hello from Unity!", "wss://relay.damus.io");
    }
}
```

## Basic Usage

### Key Management

```csharp
// Generate a new key pair
_nostrManager.Initialize();

// Get your public key in npub format
string npub = _nostrManager.GetNpub();

// Get your private key in nsec format
string nsec = _nostrManager.GetNsec();
```

### Posting Notes

```csharp
// Post a text note to a relay
await _nostrManager.PostTextNote("Your message here", "wss://relay.damus.io");
```

### Event Handling

```csharp
// Subscribe to events
_nostrManager.OnConnected += (relay) => Debug.Log($"Connected to {relay}");
_nostrManager.OnDisconnected += (relay) => Debug.Log($"Disconnected from {relay}");
_nostrManager.OnError += (error) => Debug.LogError($"Error: {error}");
_nostrManager.OnEventReceived += (ev) => Debug.Log($"Received event: {ev.Content}");
```

## Example Scene

The package includes a basic example scene demonstrating the SDK's functionality:

1. Open the Package Manager
2. Find the Nostr Unity SDK
3. Click "Import" under the "Samples" section
4. Open the "Basic Usage" scene from the Samples folder

The example scene includes:
- Text input for messages
- Send button
- Status display
- Public key display

## Troubleshooting

### Common Issues

1. **Missing DLLs**
   - Ensure all required DLLs are in your `Assets/Plugins` folder
   - Check that the DLLs are compatible with your Unity version

2. **WebSocket Connection Issues**
   - Verify your internet connection
   - Check if the relay URL is correct
   - Ensure the relay is online and accepting connections

3. **Signature Verification Failures**
   - Verify that your private key matches your public key
   - Check that the event is properly formatted
   - Ensure the timestamp is not in the future

### Debug Logging

The SDK includes detailed debug logging. Enable it in your Unity Console window to see:
- Connection status
- Event publishing status
- Signature verification results
- Error messages

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Support

For support, please:
1. Check the troubleshooting section above
2. Search existing issues
3. Create a new issue if needed

## Acknowledgments

- NBitcoin.Secp256k1 for Schnorr signature support
- Newtonsoft.Json for JSON handling
- System.Text.Json for NIP-01 serialization
