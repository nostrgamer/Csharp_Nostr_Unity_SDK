using System;
using System.Text;
using NBitcoin.Secp256k1;
using UnityEngine;

namespace NostrUnity.Crypto
{
    /// <summary>
    /// Represents a Nostr key pair for signing and verification
    /// </summary>
    public class KeyPair
    {
        private readonly byte[] _privateKeyBytes;
        private readonly byte[] _publicKeyBytes;
        private readonly string _privateKeyHex;
        private readonly string _publicKeyHex;
        private readonly string _npub;
        private readonly string _nsec;

        /// <summary>
        /// Gets the hex-encoded private key
        /// </summary>
        public string PrivateKeyHex => _privateKeyHex;

        /// <summary>
        /// Gets the hex-encoded public key
        /// </summary>
        public string PublicKeyHex => _publicKeyHex;

        /// <summary>
        /// Gets the Bech32-encoded public key (npub)
        /// </summary>
        public string Npub => _npub;

        /// <summary>
        /// Gets the Bech32-encoded private key (nsec)
        /// </summary>
        public string Nsec => _nsec;

        /// <summary>
        /// Creates a new KeyPair from a private key
        /// </summary>
        /// <param name="privateKey">The private key (hex or nsec format)</param>
        public KeyPair(string privateKey = null)
        {
            try
            {
                // Generate or use provided private key
                if (string.IsNullOrEmpty(privateKey))
                {
                    _privateKeyBytes = CryptoUtils.GenerateRandomBytes32();
                    _privateKeyHex = CryptoUtils.BytesToHex(_privateKeyBytes);
                }
                else if (privateKey.StartsWith("nsec1"))
                {
                    _privateKeyBytes = Bech32.Decode(privateKey);
                    _privateKeyHex = CryptoUtils.BytesToHex(_privateKeyBytes);
                }
                else
                {
                    _privateKeyHex = privateKey;
                    _privateKeyBytes = CryptoUtils.HexToBytes(privateKey);
                }

                // Derive public key
                _publicKeyBytes = DerivePublicKey(_privateKeyBytes);
                _publicKeyHex = CryptoUtils.BytesToHex(_publicKeyBytes);

                // Generate formatted keys
                _npub = Bech32.Encode("npub", _publicKeyBytes);
                _nsec = Bech32.Encode("nsec", _privateKeyBytes);

                Debug.Log($"KeyPair initialized: {_npub}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to initialize KeyPair: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Derives a public key from a private key using secp256k1
        /// </summary>
        /// <param name="privateKeyBytes">Private key bytes (32 bytes)</param>
        /// <returns>Public key bytes (32 bytes)</returns>
        private byte[] DerivePublicKey(byte[] privateKeyBytes)
        {
            if (privateKeyBytes == null || privateKeyBytes.Length != 32)
                throw new ArgumentException("Private key must be 32 bytes", nameof(privateKeyBytes));

            try
            {
                var context = new Context();
                if (!ECPrivKey.TryCreate(privateKeyBytes, out ECPrivKey privateKey))
                    throw new ArgumentException("Invalid private key");

                // Get the x-only public key (32 bytes) as per NIP-01
                var xOnlyPubKey = privateKey.CreateXOnlyPubKey();
                return xOnlyPubKey.ToBytes();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error deriving public key: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Signs a Nostr event ID using the private key with Schnorr signature
        /// </summary>
        /// <param name="eventId">The event ID to sign (32 bytes, hex-encoded)</param>
        /// <returns>The signature (64 bytes, hex-encoded)</returns>
        public string SignId(string eventId)
        {
            if (string.IsNullOrEmpty(eventId))
                throw new ArgumentException("Event ID cannot be null or empty", nameof(eventId));

            try
            {
                byte[] eventIdBytes = CryptoUtils.HexToBytes(eventId);
                if (eventIdBytes.Length != 32)
                    throw new ArgumentException("Event ID must be 32 bytes", nameof(eventId));

                var context = new Context();
                if (!ECPrivKey.TryCreate(_privateKeyBytes, out ECPrivKey privateKey))
                    throw new ArgumentException("Invalid private key");

                // Sign using BIP-340 Schnorr (with a null auxiliary random value for deterministic signing)
                if (!privateKey.TrySignBIP340(eventIdBytes, null, out SecpSchnorrSignature signature))
                    throw new InvalidOperationException("Failed to create signature");

                // Get signature bytes
                byte[] signatureBytes = new byte[64];
                signature.WriteToSpan(signatureBytes);

                return CryptoUtils.BytesToHex(signatureBytes);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error signing event: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Verifies a signature against an event ID
        /// </summary>
        /// <param name="eventId">The event ID (32 bytes, hex-encoded)</param>
        /// <param name="signature">The signature to verify (64 bytes, hex-encoded)</param>
        /// <returns>True if the signature is valid, false otherwise</returns>
        public bool VerifySignature(string eventId, string signature)
        {
            if (string.IsNullOrEmpty(eventId))
                throw new ArgumentException("Event ID cannot be null or empty", nameof(eventId));
            
            if (string.IsNullOrEmpty(signature))
                throw new ArgumentException("Signature cannot be null or empty", nameof(signature));

            try
            {
                byte[] eventIdBytes = CryptoUtils.HexToBytes(eventId);
                byte[] signatureBytes = CryptoUtils.HexToBytes(signature);

                if (eventIdBytes.Length != 32)
                    throw new ArgumentException("Event ID must be 32 bytes", nameof(eventId));
                
                if (signatureBytes.Length != 64)
                    throw new ArgumentException("Signature must be 64 bytes", nameof(signature));

                var context = new Context();
                if (!ECXOnlyPubKey.TryCreate(_publicKeyBytes, out ECXOnlyPubKey pubKey))
                    throw new ArgumentException("Invalid public key");

                if (!SecpSchnorrSignature.TryCreate(signatureBytes, out SecpSchnorrSignature schnorrSignature))
                    throw new ArgumentException("Invalid signature format");

                return pubKey.SigVerifyBIP340(schnorrSignature, eventIdBytes);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error verifying signature: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Verifies a signature from another public key
        /// </summary>
        /// <param name="pubkey">The public key that signed the data (hex or npub)</param>
        /// <param name="eventId">The event ID that was signed (hex)</param>
        /// <param name="signature">The signature to verify (hex)</param>
        /// <returns>True if the signature is valid, false otherwise</returns>
        public static bool VerifySignature(string pubkey, string eventId, string signature)
        {
            if (string.IsNullOrEmpty(pubkey))
                throw new ArgumentException("Public key cannot be null or empty", nameof(pubkey));
            
            if (string.IsNullOrEmpty(eventId))
                throw new ArgumentException("Event ID cannot be null or empty", nameof(eventId));
            
            if (string.IsNullOrEmpty(signature))
                throw new ArgumentException("Signature cannot be null or empty", nameof(signature));

            try
            {
                // Convert pubkey if needed
                byte[] pubkeyBytes;
                var context = new Context();
                
                if (pubkey.StartsWith("npub1"))
                {
                    // Handle bech32 encoded key
                    pubkeyBytes = Bech32.Decode(pubkey);
                }
                else if ((pubkey.Length == 66) && (pubkey.StartsWith("02") || pubkey.StartsWith("03")))
                {
                    // Handle compressed format (33 bytes with prefix)
                    pubkeyBytes = CryptoUtils.HexToBytes(pubkey);
                    
                    // Convert the compressed public key to x-only for verification
                    if (!ECPubKey.TryCreate(pubkeyBytes, context, out bool compressed, out ECPubKey fullPubKey))
                        throw new ArgumentException("Invalid compressed public key format", nameof(pubkey));
                    
                    // Convert to x-only for verification
                    var xOnlyPubKey = fullPubKey.ToXOnlyPubKey();
                    pubkeyBytes = xOnlyPubKey.ToBytes();
                }
                else if (pubkey.Length == 64)
                {
                    // Handle standard x-only pubkey (32 bytes, hex-encoded)
                    pubkeyBytes = CryptoUtils.HexToBytes(pubkey);
                }
                else
                {
                    throw new ArgumentException($"Invalid public key format. Must be 64 chars (x-only) or 66 chars (compressed with 02/03 prefix). Got {pubkey.Length} chars.", nameof(pubkey));
                }
                
                byte[] eventIdBytes = CryptoUtils.HexToBytes(eventId);
                byte[] signatureBytes = CryptoUtils.HexToBytes(signature);

                if (pubkeyBytes.Length != 32)
                    throw new ArgumentException($"Public key must be 32 bytes (was {pubkeyBytes.Length} bytes)", nameof(pubkey));
                
                if (eventIdBytes.Length != 32)
                    throw new ArgumentException("Event ID must be 32 bytes", nameof(eventId));
                
                if (signatureBytes.Length != 64)
                    throw new ArgumentException("Signature must be 64 bytes", nameof(signature));

                if (!ECXOnlyPubKey.TryCreate(pubkeyBytes, out ECXOnlyPubKey pubKey))
                    throw new ArgumentException("Invalid public key");

                if (!SecpSchnorrSignature.TryCreate(signatureBytes, out SecpSchnorrSignature schnorrSignature))
                    throw new ArgumentException("Invalid signature format");

                return pubKey.SigVerifyBIP340(schnorrSignature, eventIdBytes);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error verifying signature: {ex.Message}");
                return false;
            }
        }
    }
} 