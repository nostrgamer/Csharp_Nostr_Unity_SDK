# Cryptography Dependencies

## BouncyCastle Library (REQUIRED)

This project **requires** the BouncyCastle library for secp256k1 cryptography. 

**IMPORTANT:** The crypto functionality will not work without this library.

### Installation Steps

1. Download the BouncyCastle DLL from the official website: [https://www.bouncycastle.org/csharp/](https://www.bouncycastle.org/csharp/)
2. Download the "BouncyCastle.Crypto.dll" file (not the signed version)
3. Copy the DLL to your Unity project's `Assets/Plugins` folder
4. If the `Plugins` folder doesn't exist, create it first

### Installation Verification

After installing the DLL, you can verify it's working correctly by:

1. Running the `CryptoTest.cs` script in your Unity project
2. Check that key generation, signing, and verification work without errors

## About the Implementation

This library uses a pure C# implementation of secp256k1 via BouncyCastle, which offers:

1. No native dependencies - works on all platforms including mobile and WebGL
2. Industry-standard cryptography that's widely used and reviewed
3. Full implementation of the secp256k1 curve needed for Nostr

## Technical Details

- The implementation is in `Secp256k1BouncyCastleManager.cs`
- The public API is provided through `Secp256k1Manager.cs`
- All cryptographic operations require initialization via `Secp256k1Manager.Initialize()` 