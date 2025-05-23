# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Future features will be listed here

### Changed
- Future changes will be listed here

### Fixed
- Future fixes will be listed here

## [0.1.0] - 2025-01-02

### Added
- **Core Nostr Protocol Support**
  - Event creation and signing with Schnorr signatures
  - Key pair generation and management (hex and nsec formats)
  - Bech32 encoding for npub/nsec keys
  - NIP-01 compliance for basic Nostr event structure
  - NIP-19 support (note, npub, nsec encoding/decoding)

- **WebSocket Relay Communication**
  - Multi-relay connection management with `NostrRelayManager`
  - Automatic reconnection with exponential backoff
  - Rate limiting for message sending (configurable)
  - Message queuing for offline scenarios
  - WebSocket connection pooling

- **Unity Integration**
  - Unity Package Manager support via Git URL
  - MonoBehaviour-friendly async operations
  - Coroutine-based reconnection handling
  - `DontDestroyOnLoad` GameObject for persistent connections
  - Unity 2021.3+ compatibility

- **Event Publishing and Subscription**
  - Text note publishing (kind 1 events)
  - Subscription management with filters
  - Event validation and verification
  - Automatic event ID generation
  - Timestamp handling with Unix epoch conversion

- **Developer Experience**
  - Comprehensive example scripts in `Samples~` folder
  - Debug logging with detailed connection status
  - Clear error messages and troubleshooting guide
  - Simple API for common operations
  - View event links for popular Nostr web clients

- **Security Features**
  - Secure key generation using NBitcoin.Secp256k1
  - Private key validation
  - Event signature verification
  - Support for both development and production key formats

### Technical Implementation
- **Dependencies**: NBitcoin.Secp256k1, Newtonsoft.Json, System.Text.Json
- **Architecture**: Modular design with separate concerns for crypto, networking, and Unity integration
- **Performance**: Rate-limited message sending to prevent spam
- **Error Handling**: Graceful degradation with retry mechanisms

### Known Limitations
- Manual DLL installation required (NBitcoin.Secp256k1.dll, Newtonsoft.Json.dll, System.Text.Json.dll)
- WebGL platform support requires verification
- Limited automated testing suite
- Basic error handling (will be enhanced in future versions)

### Fixed
- **WebSocket Connection Stability**
  - Improved WebSocket close handling during Unity play mode exit
  - Fixed race conditions in connection state management
  - Enhanced cancellation token handling for proper async cleanup
  - Prevented duplicate close events and cascading errors
  - Added proper resource disposal to prevent memory leaks
  - Improved error handling for incomplete close handshakes
  - Added application pause/focus event handling for better lifecycle management

### Development Notes
- Built with Unity 2021.3 LTS compatibility
- Follows Unity package development best practices
- Clean C# code with comprehensive XML documentation
- Modular architecture for easy extension and maintenance

---

## Release Planning

### [0.2.0] - Planned
- Enhanced dependency management (embedded DLLs or package dependencies)
- Expanded example collection
- Improved error handling with custom exception types
- Performance optimizations
- Additional NIP implementations

### Future Considerations
- Automated testing framework
- WebGL platform verification and optimization
- Configuration management system
- Advanced security features
- Comprehensive API documentation

---

## Installation

### Unity Package Manager (Recommended)
```
https://github.com/nostrgamer/Csharp_Nostr_Unity_SDK.git
```

### Requirements
- Unity 2021.3 or later
- .NET Standard 2.1 compatible
- Required DLLs (see documentation)

For detailed installation instructions, troubleshooting, and usage examples, see [README.md](README.md). 