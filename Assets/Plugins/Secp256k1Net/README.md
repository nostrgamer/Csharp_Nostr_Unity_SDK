# Secp256k1.Net Integration

This directory contains the integration of the Secp256k1.Net library for the Nostr Unity SDK.

## Included Files

- `Secp256k1.Net.dll` - The C# wrapper for the native secp256k1 library
- `secp256k1.dll` - The native secp256k1 library for Windows

## About Secp256k1.Net

Secp256k1.Net is a C# wrapper around the native secp256k1 library, which is the same cryptographic library used by Bitcoin Core. It provides high-performance implementations of:

- Key generation
- Public key derivation
- Message signing
- Signature verification

## Usage in the SDK

The SDK uses Secp256k1.Net through the `Secp256k1Manager` class, which provides a convenient API for cryptographic operations used by Nostr.

## Cross-Platform Support

The current implementation includes native libraries for Windows only. If you need to build for other platforms, you'll need to add the appropriate native library:

- For macOS: `libsecp256k1.dylib`
- For Linux: `libsecp256k1.so`
- For iOS/Android: Consult the Secp256k1.Net documentation

## Credits

Secp256k1.Net is maintained by zone117x: https://github.com/zone117x/Secp256k1.Net

The native secp256k1 library is developed by the Bitcoin Core developers. 