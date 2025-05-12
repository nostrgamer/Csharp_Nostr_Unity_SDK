# Nostr Unity SDK

A Unity SDK for interacting with the Nostr protocol, providing easy-to-use functionality for creating, signing, and publishing Nostr events.

## Features

- Schnorr signature support using NBitcoin.Secp256k1
- WebSocket communication with Nostr relays
- Key pair generation and management
- Bech32 encoding for npub/nsec keys
- Event creation and signing
- NIP-19 support (note, npub, nsec)
- Simple API for posting text notes
- Support for both hex and nsec format private keys
- View event links for multiple Nostr web clients
- Relay connection management

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
2. Add the NostrPublishExample.cs script to it
3. Input your nsec
4. Modify the relays, as needed

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

4. **Private Key Format Issues**
   - For nsec format keys, ensure they start with "nsec1"
   - For hex format keys, ensure they are valid hexadecimal (0-9, a-f) and have the correct length (64 characters)

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
