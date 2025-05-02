# C# Nostr Unity SDK - Project Structure

This document tracks all files and folders in the project.

## Root Files

- `.gitignore`: Git ignore file for Unity projects
- `LICENSE`: MIT license file
- `LICENSE.meta`: Meta file for LICENSE
- `package.json`: Unity package manifest
- `package.json.meta`: Unity meta file for package.json
- `README.md`: Project documentation
- `README.md.meta`: Meta file for README.md
- `Runtime.meta`: Unity meta file for Runtime folder
- `PROJECT_STRUCTURE.md`: This file, tracking project structure
- `PROJECT_STRUCTURE.md.meta`: Meta file for PROJECT_STRUCTURE.md
- `TESTING.md`: Testing guide for Unity developers
- `TESTING.md.meta`: Meta file for TESTING.md

## Folder Structure

### Runtime/
Contains the core SDK implementation

- `NostrSDK.asmdef`: Assembly definition file
- `NostrSDK.asmdef.meta`: Meta file for assembly definition

#### Runtime/Scripts/
Contains all C# scripts

- `Scripts.meta`: Meta file for Scripts folder

##### Runtime/Scripts/Core/
Core functionality for the SDK

- `Core.meta`: Meta file for Core folder
- `NostrClient.cs`: Main client for interacting with Nostr network
- `NostrClient.cs.meta`: Meta file for NostrClient.cs
- `NostrConstants.cs`: Constants for the Nostr protocol
- `NostrConstants.cs.meta`: Meta file for NostrConstants.cs
- `NostrManager.cs`: MonoBehaviour wrapper for Nostr SDK functionality
- `NostrManager.cs.meta`: Meta file for NostrManager.cs

##### Runtime/Scripts/Models/
Data models and types

- `Models.meta`: Meta file for Models folder
- `NostrEvent.cs`: Represents a Nostr event according to NIP-01
- `NostrEvent.cs.meta`: Meta file for NostrEvent.cs
- `NostrEventKind.cs`: Enum for standard Nostr event kinds
- `NostrEventKind.cs.meta`: Meta file for NostrEventKind.cs

##### Runtime/Scripts/Crypto/
Cryptographic utilities

- `Crypto.meta`: Meta file for Crypto folder
- `NostrKeyManager.cs`: Manages Nostr private and public keys
- `NostrKeyManager.cs.meta`: Meta file for NostrKeyManager.cs
- `Secp256k1Manager.cs`: Custom implementation of Secp256k1 cryptographic operations
- `Secp256k1Manager.cs.meta`: Meta file for Secp256k1Manager.cs

##### Runtime/Scripts/WebSocket/
WebSocket communication

- `WebSocket.meta`: Meta file for WebSocket folder
- `WebSocketClient.cs`: Client for WebSocket connections to relays
- `WebSocketClient.cs.meta`: Meta file for WebSocketClient.cs

##### Runtime/Scripts/Utils/
Utility classes

- `Utils.meta`: Meta file for Utils folder
- `Bech32.cs`: Bech32 encoding/decoding utility
- `Bech32.cs.meta`: Meta file for Bech32.cs
- `Bech32Tests.cs`: Tests for Bech32 encoding/decoding
- `Bech32Tests.cs.meta`: Meta file for Bech32Tests.cs
- `NostrExtensions.cs`: Extension methods for Nostr types
- `NostrExtensions.cs.meta`: Meta file for NostrExtensions.cs

## Dependencies

- `Newtonsoft.Json`: Used for JSON serialization/deserialization (via Unity's package)

## Meta Files

For each file and folder, Unity requires a corresponding .meta file. These files contain GUIDs that Unity uses to track assets. The meta files are generated automatically by Unity when new files are created, but we're creating them manually here to ensure consistency.

## TODO

- [x] Create README.md.meta and LICENSE.meta
- [x] Implement WebSocketClient class
- [x] Create Unity MonoBehaviour components for easier integration
- [x] Implement Bech32 encoding/decoding
- [x] Add Secp256k1 support (placeholder implementation)
- [ ] Replace placeholder Secp256k1 implementation with full implementation
- [ ] Implement JSON serialization utilities
- [ ] Add example scenes and documentation 