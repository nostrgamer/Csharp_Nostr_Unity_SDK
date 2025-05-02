using System;

namespace Nostr.Unity
{
    /// <summary>
    /// Interface for cryptographic providers used by the Nostr SDK
    /// </summary>
    public interface ICryptographyProvider
    {
        /// <summary>
        /// Gets a value indicating whether this provider is initialized
        /// </summary>
        bool IsInitialized { get; }
        
        /// <summary>
        /// Generates a new random private key
        /// </summary>
        /// <returns>A 32-byte array representing the private key</returns>
        byte[] GeneratePrivateKey();
        
        /// <summary>
        /// Derives a public key from a private key
        /// </summary>
        /// <param name="privateKey">The 32-byte private key</param>
        /// <returns>The public key (33 bytes compressed)</returns>
        byte[] GetPublicKey(byte[] privateKey);
        
        /// <summary>
        /// Computes the SHA256 hash of a message
        /// </summary>
        /// <param name="message">The message to hash</param>
        /// <returns>The 32-byte hash of the message</returns>
        byte[] ComputeMessageHash(string message);
        
        /// <summary>
        /// Signs a message hash with a private key
        /// </summary>
        /// <param name="messageHash">The 32-byte hash of the message</param>
        /// <param name="privateKey">The 32-byte private key</param>
        /// <returns>The 64-byte signature</returns>
        byte[] Sign(byte[] messageHash, byte[] privateKey);
        
        /// <summary>
        /// Signs a message hash with a private key and returns a recoverable signature
        /// </summary>
        /// <param name="messageHash">The 32-byte hash of the message</param>
        /// <param name="privateKey">The 32-byte private key</param>
        /// <returns>The 65-byte recoverable signature</returns>
        byte[] SignRecoverable(byte[] messageHash, byte[] privateKey);
        
        /// <summary>
        /// Verifies a signature against a message hash and public key
        /// </summary>
        /// <param name="signature">The signature to verify</param>
        /// <param name="messageHash">The 32-byte hash of the message</param>
        /// <param name="publicKey">The public key</param>
        /// <returns>True if valid, false otherwise</returns>
        bool Verify(byte[] signature, byte[] messageHash, byte[] publicKey);
    }
} 