# Cryptography Dependencies

## BouncyCastle

This project uses BouncyCastle for secp256k1 cryptography. You need to add the BouncyCastle DLL to your project before using the crypto features:

### Manual Installation (Recommended)

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

This library uses a pure C# implementation of secp256k1 via BouncyCastle, which has several advantages:

1. No native dependencies - works on all platforms including mobile and WebGL
2. Well-established and widely used cryptography library
3. Provides all necessary functionality for Nostr (key generation, signing, verification)

If for some reason the BouncyCastle implementation fails, the system will fall back to a simplified implementation that provides basic functionality for development, but it's not recommended for production use. 