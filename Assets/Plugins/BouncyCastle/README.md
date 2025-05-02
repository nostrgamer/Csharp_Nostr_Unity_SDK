# BouncyCastle for Nostr.Unity

This directory contains the BouncyCastle.Crypto.dll library, which is used for secp256k1 cryptographic operations in the Nostr Unity SDK.

## Installation

1. Download BouncyCastle.Crypto.dll v1.9.0 or later (.NET Standard 2.0 compatible) from:
   - [NuGet Package](https://www.nuget.org/packages/Portable.BouncyCastle/)
   - [BouncyCastle.org](https://www.bouncycastle.org/csharp/)

2. Place the DLL file in this directory.

## Verification

To verify that the DLL is correctly installed and recognized:

1. Open Unity
2. Run the CryptoTester component
3. Check the console for cryptographic test results

## Troubleshooting

If Unity reports "Failed to initialize BouncyCastle manager", check the following:

1. Ensure the DLL is correctly placed in this directory
2. Verify the meta file references are correct
3. Try restarting Unity

## Missing DLL?

If the file `BouncyCastle.Crypto.dll` is missing, you can:

1. Download from [NuGet](https://www.nuget.org/packages/Portable.BouncyCastle/)
2. Extract the DLL from the `.nupkg` file (rename to `.zip` first)
3. Find the `.dll` file in the `lib/netstandard2.0/` folder
4. Copy it to this directory

## Reference

- BouncyCastle Documentation: https://www.bouncycastle.org/csharp/
- Nostr Implementation Reference: https://github.com/nostr-protocol/nips 