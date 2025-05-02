# Cryptography Dependencies

## BouncyCastle

This project uses BouncyCastle for secp256k1 cryptography. You need to add the BouncyCastle DLL to your project:

### Manual Installation

1. Download the BouncyCastle DLL from the official website: [https://www.bouncycastle.org/csharp/](https://www.bouncycastle.org/csharp/)
2. Download "BouncyCastle.Crypto.dll" (not the signed version)
3. Copy the DLL to your Unity project's `Assets/Plugins` folder
4. If the `Plugins` folder doesn't exist, create it first

### NuGet Installation (Alternative)

If you're using Unity with .NET 4.x or higher, you can also add BouncyCastle through NuGet:

```
Install-Package BouncyCastle.NetCore
```

## Secp256k1 Implementation

The project uses a pure C# implementation of secp256k1 via BouncyCastle. This implementation:

1. Does not require any native libraries
2. Works on all platforms including mobile and WebGL
3. Provides all necessary cryptographic functionality for Nostr

If BouncyCastle is not available for any reason, the system will fall back to a simplified implementation that provides the same API but with limited security guarantees. 