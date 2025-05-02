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

See the [Documentation](https://github.com/nostrgamer/Csharp_Nostr_Unity_SDK/wiki) for information on how to use the SDK.

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