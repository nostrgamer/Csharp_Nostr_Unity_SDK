using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;
using Org.BouncyCastle.Security;

namespace Nostr.Unity.Crypto
{
    /// <summary>
    /// Provides secp256k1 functionality using the BouncyCastle library
    /// </summary>
    public static class Secp256k1BouncyCastleManager
    {
        // Get the named curve parameters for secp256k1
        private static readonly X9ECParameters Curve = ECNamedCurveTable.GetByName("secp256k1");
        private static readonly ECDomainParameters DomainParams = new ECDomainParameters(
            Curve.Curve, Curve.G, Curve.N, Curve.H, Curve.GetSeed());

        /// <summary>
        /// Generates a new random private key
        /// </summary>
        /// <returns>A 32-byte array containing the private key</returns>
        public static byte[] GeneratePrivateKey()
        {
            try
            {
                // Create a secure random number generator
                var secureRandom = new SecureRandom();
                
                // Generate a random integer less than the curve order
                var privateKeyInt = new BigInteger(1, Curve.N.BitLength, secureRandom);
                privateKeyInt = privateKeyInt.Mod(Curve.N.Subtract(BigInteger.Two)).Add(BigInteger.One);
                
                // Convert to byte array and ensure it's 32 bytes
                byte[] privateKey = privateKeyInt.ToByteArrayUnsigned();
                if (privateKey.Length < 32)
                {
                    // Pad with zeros if needed
                    byte[] paddedKey = new byte[32];
                    Array.Copy(privateKey, 0, paddedKey, 32 - privateKey.Length, privateKey.Length);
                    privateKey = paddedKey;
                }
                
                return privateKey;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error generating private key: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Derives the public key from a private key
        /// </summary>
        /// <param name="privateKey">The 32-byte private key</param>
        /// <returns>The 33-byte compressed public key</returns>
        public static byte[] GetPublicKey(byte[] privateKey)
        {
            try
            {
                if (privateKey == null || privateKey.Length != 32)
                {
                    throw new ArgumentException("Private key must be 32 bytes");
                }
                
                // Create the key parameters
                var d = new BigInteger(1, privateKey);
                var q = DomainParams.G.Multiply(d);
                
                // Get the public key in compressed format
                return q.GetEncoded(true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error deriving public key: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Computes the SHA256 hash of a message
        /// </summary>
        /// <param name="message">The message to hash</param>
        /// <returns>The 32-byte hash of the message</returns>
        public static byte[] ComputeMessageHash(string message)
        {
            try
            {
                if (string.IsNullOrEmpty(message))
                {
                    Debug.LogWarning("Empty message provided for hashing");
                    message = "";
                }
                
                // Convert the message to bytes and hash it using SHA256
                byte[] messageBytes = Encoding.UTF8.GetBytes(message);
                using (var sha256 = SHA256.Create())
                {
                    return sha256.ComputeHash(messageBytes);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error computing message hash: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Signs a message hash with a private key
        /// </summary>
        /// <param name="messageHash">The 32-byte hash of the message</param>
        /// <param name="privateKey">The 32-byte private key</param>
        /// <returns>The 64-byte signature (R and S concatenated)</returns>
        public static byte[] Sign(byte[] messageHash, byte[] privateKey)
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
                
                // Create the signer
                var signer = new ECDsaSigner();
                
                // Create the key parameters
                var privateKeyParameters = new ECPrivateKeyParameters(
                    new BigInteger(1, privateKey), DomainParams);
                
                // Initialize for signing
                signer.Init(true, privateKeyParameters);
                
                // Generate the signature
                BigInteger[] signature = signer.GenerateSignature(messageHash);
                BigInteger r = signature[0];
                BigInteger s = signature[1];
                
                // Check if s is in the lower half of the curve order
                BigInteger halfN = Curve.N.ShiftRight(1);
                if (s.CompareTo(halfN) > 0)
                {
                    // If not, use N - s instead (this is for compatibility with Bitcoin's signatures)
                    s = Curve.N.Subtract(s);
                }
                
                // Encode the signature as a 64-byte array (r + s)
                byte[] rBytes = r.ToByteArrayUnsigned();
                byte[] sBytes = s.ToByteArrayUnsigned();
                byte[] result = new byte[64];
                
                // Ensure correct length by padding with zeros if needed
                Array.Copy(rBytes, 0, result, 32 - rBytes.Length, rBytes.Length);
                Array.Copy(sBytes, 0, result, 64 - sBytes.Length, sBytes.Length);
                
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error signing message: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Verifies a signature
        /// </summary>
        /// <param name="messageHash">The 32-byte hash of the message</param>
        /// <param name="signature">The 64-byte signature (R and S concatenated)</param>
        /// <param name="publicKey">The public key (33 bytes compressed)</param>
        /// <returns>True if the signature is valid, otherwise false</returns>
        public static bool Verify(byte[] messageHash, byte[] signature, byte[] publicKey)
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
                
                if (publicKey == null || (publicKey.Length != 33 && publicKey.Length != 65))
                {
                    throw new ArgumentException("Public key must be 33 bytes (compressed) or 65 bytes (uncompressed)");
                }
                
                // Create the signer
                var signer = new ECDsaSigner();
                
                // Parse the public key
                var q = Curve.Curve.DecodePoint(publicKey);
                var publicKeyParameters = new ECPublicKeyParameters(q, DomainParams);
                
                // Initialize for verification
                signer.Init(false, publicKeyParameters);
                
                // Parse the signature
                BigInteger r = new BigInteger(1, signature, 0, 32);
                BigInteger s = new BigInteger(1, signature, 32, 32);
                
                // Verify the signature
                return signer.VerifySignature(messageHash, r, s);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error verifying signature: {ex.Message}");
                return false;
            }
        }
    }
} 