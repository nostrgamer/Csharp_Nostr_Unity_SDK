using System;
using System.Security.Cryptography;
using UnityEngine;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;

namespace Nostr.Unity
{
    /// <summary>
    /// Implementation of the ICryptographyProvider interface using BouncyCastle
    /// </summary>
    public class BouncyCryptography : ICryptographyProvider
    {
        private readonly object _curveParams;
        private readonly object _secureRandom;
        
        /// <summary>
        /// Initializes a new instance of the BouncyCryptography class
        /// </summary>
        public BouncyCryptography()
        {
            try
            {
                BouncyCastleManager.EnsureInitialized();
                
                // Get the secp256k1 curve parameters from BouncyCastle
                var ecParams = BouncyCastleManager.GetSecp256k1Parameters();
                _curveParams = ecParams;
                
                // Create a secure random generator
                _secureRandom = new SecureRandom();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error initializing BouncyCryptography: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Gets a value indicating whether this provider is initialized
        /// </summary>
        public bool IsInitialized => _curveParams != null && _secureRandom != null;
        
        /// <summary>
        /// Generates a new random private key
        /// </summary>
        /// <returns>A 32-byte array representing the private key</returns>
        public byte[] GeneratePrivateKey()
        {
            try
            {
                BouncyCastleManager.EnsureInitialized();
                
                // Generate a secure private key within the curve order
                ECKeyPairGenerator keyGen = new ECKeyPairGenerator();
                keyGen.Init(new ECKeyGenerationParameters((ECDomainParameters)_curveParams, (SecureRandom)_secureRandom));
                
                AsymmetricCipherKeyPair keyPair = keyGen.GenerateKeyPair();
                ECPrivateKeyParameters privParams = (ECPrivateKeyParameters)keyPair.Private;
                
                // Convert to byte array (32 bytes) ensuring consistent length
                byte[] privateKey = privParams.D.ToByteArrayUnsigned();
                if (privateKey.Length < 32)
                {
                    byte[] paddedKey = new byte[32];
                    Array.Copy(privateKey, 0, paddedKey, 32 - privateKey.Length, privateKey.Length);
                    privateKey = paddedKey;
                }
                else if (privateKey.Length > 32)
                {
                    byte[] trimmedKey = new byte[32];
                    Array.Copy(privateKey, privateKey.Length - 32, trimmedKey, 0, 32);
                    privateKey = trimmedKey;
                }
                
                return privateKey;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error generating private key: {ex.Message}");
                throw new CryptographicException("Failed to generate private key", ex);
            }
        }

        /// <summary>
        /// Derives a public key from a private key
        /// </summary>
        /// <param name="privateKey">The 32-byte private key</param>
        /// <returns>The 33-byte compressed public key</returns>
        public byte[] GetPublicKey(byte[] privateKey)
        {
            try
            {
                if (privateKey == null || privateKey.Length != 32)
                {
                    throw new ArgumentException("Private key must be 32 bytes");
                }
                
                BouncyCastleManager.EnsureInitialized();
                
                // Convert the private key to a BigInteger
                BigInteger d = new BigInteger(1, privateKey);
                ValidatePrivateKey(d);
                
                // Derive the public key from the private key using the curve's generator point
                ECDomainParameters domain = (ECDomainParameters)_curveParams;
                var q = domain.G.Multiply(d);
                
                // Convert to compressed public key format (33 bytes with prefix)
                byte[] publicKey = q.GetEncoded(true);
                return publicKey;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error deriving public key: {ex.Message}");
                throw new CryptographicException("Failed to derive public key", ex);
            }
        }

        /// <summary>
        /// Computes the SHA256 hash of a message (for signing)
        /// </summary>
        /// <param name="message">The message to hash</param>
        /// <returns>The 32-byte hash of the message</returns>
        public byte[] ComputeMessageHash(string message)
        {
            using (var sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(message));
            }
        }

        /// <summary>
        /// Signs a message hash with a private key
        /// </summary>
        /// <param name="messageHash">The 32-byte hash of the message to sign</param>
        /// <param name="privateKey">The 32-byte private key to sign with</param>
        /// <returns>The 64-byte signature (r, s concatenated)</returns>
        public byte[] Sign(byte[] messageHash, byte[] privateKey)
        {
            try
            {
                if (messageHash == null || messageHash.Length != 32)
                {
                    throw new ArgumentException("Message hash must be 32 bytes");
                }
                
                if (privateKey == null || privateKey.Length != 32)
                {
                    throw new ArgumentException("Private key must be 32 bytes");
                }
                
                BouncyCastleManager.EnsureInitialized();
                
                // Convert the private key to a BigInteger
                BigInteger d = new BigInteger(1, privateKey);
                ValidatePrivateKey(d);
                
                // Create an EC private key parameters
                ECDomainParameters domain = (ECDomainParameters)_curveParams;
                ECPrivateKeyParameters privateKeyParams = new ECPrivateKeyParameters(d, domain);
                
                // Create a deterministic ECDSA signer (RFC 6979)
                var signer = new ECDsaSigner(new HMacDsaKCalculator(new Org.BouncyCastle.Crypto.Digests.Sha256Digest()));
                signer.Init(true, privateKeyParams);
                
                // Sign the message hash
                BigInteger[] signature = signer.GenerateSignature(messageHash);
                BigInteger r = signature[0];
                BigInteger s = signature[1];
                
                // Normalize S value to lower half of curve order (BIP-0062)
                BigInteger curveOrder = domain.N;
                BigInteger halfCurveOrder = curveOrder.ShiftRight(1);
                if (s.CompareTo(halfCurveOrder) > 0)
                {
                    s = curveOrder.Subtract(s);
                }
                
                // Convert to byte arrays
                byte[] rBytes = r.ToByteArrayUnsigned();
                byte[] sBytes = s.ToByteArrayUnsigned();
                
                // Ensure each value is 32 bytes with padding if needed
                byte[] signatureBytes = new byte[64];
                
                // Pad r value to 32 bytes
                if (rBytes.Length < 32)
                {
                    Array.Copy(rBytes, 0, signatureBytes, 32 - rBytes.Length, rBytes.Length);
                }
                else
                {
                    Array.Copy(rBytes, 0, signatureBytes, 0, 32);
                }
                
                // Pad s value to 32 bytes
                if (sBytes.Length < 32)
                {
                    Array.Copy(sBytes, 0, signatureBytes, 64 - sBytes.Length, sBytes.Length);
                }
                else
                {
                    Array.Copy(sBytes, 0, signatureBytes, 32, 32);
                }
                
                return signatureBytes;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error signing message: {ex.Message}");
                throw new CryptographicException("Failed to sign message", ex);
            }
        }

        /// <summary>
        /// Signs a message hash with a private key and returns a recoverable signature
        /// </summary>
        /// <param name="messageHash">The 32-byte hash of the message to sign</param>
        /// <param name="privateKey">The 32-byte private key to sign with</param>
        /// <returns>The 64-byte signature with recovery info</returns>
        public byte[] SignRecoverable(byte[] messageHash, byte[] privateKey)
        {
            // For now, this just returns a regular signature
            // Proper recoverable signature implementation will be added
            return Sign(messageHash, privateKey);
        }

        /// <summary>
        /// Verifies a signature against a message hash and public key
        /// </summary>
        /// <param name="signature">The 64-byte signature (r, s concatenated)</param>
        /// <param name="messageHash">The 32-byte hash of the message</param>
        /// <param name="publicKey">The 33-byte compressed public key</param>
        /// <returns>True if the signature is valid, otherwise false</returns>
        public bool Verify(byte[] signature, byte[] messageHash, byte[] publicKey)
        {
            try
            {
                if (messageHash == null || messageHash.Length != 32)
                {
                    throw new ArgumentException("Message hash must be 32 bytes");
                }
                
                if (signature == null || signature.Length != 64)
                {
                    throw new ArgumentException("Signature must be 64 bytes");
                }
                
                if (publicKey == null || publicKey.Length != 33)
                {
                    throw new ArgumentException("Public key must be 33 bytes (compressed format)");
                }
                
                BouncyCastleManager.EnsureInitialized();
                
                // Extract r and s from the signature
                byte[] rBytes = new byte[32];
                byte[] sBytes = new byte[32];
                Array.Copy(signature, 0, rBytes, 0, 32);
                Array.Copy(signature, 32, sBytes, 0, 32);
                
                BigInteger r = new BigInteger(1, rBytes);
                BigInteger s = new BigInteger(1, sBytes);
                
                // Create an EC public key from the compressed format
                ECDomainParameters domain = (ECDomainParameters)_curveParams;
                ECPoint q = domain.Curve.DecodePoint(publicKey);
                ECPublicKeyParameters publicKeyParams = new ECPublicKeyParameters(q, domain);
                
                // Create a signer and initialize with public key for verification
                var signer = new ECDsaSigner();
                signer.Init(false, publicKeyParams);
                
                // Verify the signature
                return signer.VerifySignature(messageHash, r, s);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error verifying signature: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Validates that a private key is within the allowed range for the curve
        /// </summary>
        /// <param name="d">The private key as a BigInteger</param>
        private void ValidatePrivateKey(BigInteger d)
        {
            if (d.SignValue <= 0)
            {
                throw new CryptographicException("Invalid private key: must be positive");
            }
            
            ECDomainParameters domain = (ECDomainParameters)_curveParams;
            if (d.CompareTo(domain.N) >= 0)
            {
                throw new CryptographicException("Invalid private key: outside curve order");
            }
        }
    }
} 