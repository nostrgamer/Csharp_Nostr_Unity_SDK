using System;
using System.Security.Cryptography;
using UnityEngine;

// These will be commented out until the BouncyCastle library is added
/*
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;
*/

namespace Nostr.Unity.Crypto.Recovery
{
    /// <summary>
    /// Handles recoverable signatures for Nostr, which include a recovery ID
    /// that allows recovering the public key from the signature and message
    /// </summary>
    public class NostrRecoverableSignature
    {
        /// <summary>
        /// The 64-byte signature (r and s values concatenated)
        /// </summary>
        public byte[] Signature { get; private set; }
        
        /// <summary>
        /// The recovery ID (0, 1, 2, or 3)
        /// </summary>
        public byte RecoveryId { get; private set; }
        
        /// <summary>
        /// The complete 65-byte recoverable signature (recovery ID + signature)
        /// </summary>
        public byte[] RecoverableSignature
        {
            get
            {
                byte[] result = new byte[65];
                result[0] = RecoveryId;
                Buffer.BlockCopy(Signature, 0, result, 1, 64);
                return result;
            }
        }
        
        /// <summary>
        /// Creates a new recoverable signature from r, s values and computes the recovery ID
        /// </summary>
        /// <param name="r">The r value (32 bytes)</param>
        /// <param name="s">The s value (32 bytes)</param>
        /// <param name="messageHash">The 32-byte hash of the message</param>
        /// <param name="publicKey">The expected public key for recovery ID computation</param>
        public NostrRecoverableSignature(byte[] r, byte[] s, byte[] messageHash, byte[] publicKey)
        {
            // Concatenate r and s to form the signature
            Signature = new byte[64];
            Buffer.BlockCopy(r, 0, Signature, 0, r.Length > 32 ? 32 : r.Length);
            Buffer.BlockCopy(s, 0, Signature, 32, s.Length > 32 ? 32 : s.Length);
            
            // Compute the recovery ID
            RecoveryId = ComputeRecoveryId(Signature, messageHash, publicKey);
            
            Debug.Log($"Created recoverable signature with recovery ID: {RecoveryId}");
        }
        
        /// <summary>
        /// Creates a recoverable signature from an existing signature and a pre-computed recovery ID
        /// </summary>
        /// <param name="signature">The 64-byte signature (r and s concatenated)</param>
        /// <param name="recoveryId">The recovery ID (0, 1, 2, or 3)</param>
        public NostrRecoverableSignature(byte[] signature, byte recoveryId)
        {
            if (signature == null || signature.Length != 64)
            {
                throw new ArgumentException("Signature must be 64 bytes");
            }
            
            if (recoveryId > 3)
            {
                throw new ArgumentException("Recovery ID must be 0, 1, 2, or 3");
            }
            
            Signature = new byte[64];
            Buffer.BlockCopy(signature, 0, Signature, 0, 64);
            RecoveryId = recoveryId;
        }
        
        /// <summary>
        /// Creates a recoverable signature from a 65-byte recoverable signature
        /// </summary>
        /// <param name="recoverableSignature">The 65-byte recoverable signature (recovery ID + signature)</param>
        public NostrRecoverableSignature(byte[] recoverableSignature)
        {
            if (recoverableSignature == null || recoverableSignature.Length != 65)
            {
                throw new ArgumentException("Recoverable signature must be 65 bytes");
            }
            
            if (recoverableSignature[0] > 3)
            {
                throw new ArgumentException("Recovery ID must be 0, 1, 2, or 3");
            }
            
            RecoveryId = recoverableSignature[0];
            Signature = new byte[64];
            Buffer.BlockCopy(recoverableSignature, 1, Signature, 0, 64);
        }
        
        /// <summary>
        /// Computes the recovery ID for a signature by trying all possible values
        /// and checking if the recovered public key matches the expected one
        /// </summary>
        /// <param name="signature">The 64-byte signature</param>
        /// <param name="messageHash">The 32-byte hash of the message</param>
        /// <param name="expectedPublicKey">The expected public key for verification</param>
        /// <returns>The recovery ID (0, 1, 2, or 3)</returns>
        private byte ComputeRecoveryId(byte[] signature, byte[] messageHash, byte[] expectedPublicKey)
        {
            /* Will be uncommented when BouncyCastle is available
            try
            {
                // Extract r and s values from the signature
                byte[] rBytes = new byte[32];
                byte[] sBytes = new byte[32];
                Array.Copy(signature, 0, rBytes, 0, 32);
                Array.Copy(signature, 32, sBytes, 0, 32);
                
                BigInteger r = new BigInteger(1, rBytes);
                BigInteger s = new BigInteger(1, sBytes);
                
                // Get curve parameters
                var curve = SecNamedCurves.GetByName("secp256k1");
                var curveParams = new ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H);
                
                // Try each recovery ID (0-3)
                for (byte i = 0; i < 4; i++)
                {
                    try
                    {
                        // Recover the public key with this recovery ID
                        ECPoint q = RecoverPublicKey(r, s, messageHash, i, curveParams);
                        
                        // Convert to compressed format
                        byte[] recoveredKey = q.GetEncoded(true);
                        
                        // Check if it matches the expected public key
                        if (CompareByteArrays(recoveredKey, expectedPublicKey))
                        {
                            return i;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Failed to recover with ID {i}: {ex.Message}");
                        continue;
                    }
                }
                
                // If we reach here, no recovery ID worked
                Debug.LogError("Could not compute recovery ID - no recovery ID matched the expected public key");
                return 0; // Default to 0 as a fallback
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error computing recovery ID: {ex.Message}");
                return 0; // Default to 0 as a fallback
            }
            */
            
            // Temporary placeholder
            return 0;
        }
        
        /* Will be uncommented when BouncyCastle is available
        /// <summary>
        /// Recovers the public key from a signature, message hash, and recovery ID
        /// </summary>
        private ECPoint RecoverPublicKey(BigInteger r, BigInteger s, byte[] messageHash, byte recoveryId, ECDomainParameters curveParams)
        {
            bool isYEven = (recoveryId & 1) == 0;
            bool isSecondKey = (recoveryId >> 1) == 1;
            
            // Convert message hash to BigInteger
            BigInteger e = new BigInteger(1, messageHash);
            
            // Get curve order
            BigInteger n = curveParams.N;
            
            // If second key, add curve order to r
            BigInteger x = isSecondKey ? r.Add(n) : r;
            
            // Find the curve point with x coordinate
            ECCurve curve = curveParams.Curve;
            ECFieldElement xFieldElement = curve.FromBigInteger(x);
            
            // Calculate y coordinate (y² = x³ + 7 for secp256k1)
            ECFieldElement alpha = xFieldElement.Multiply(xFieldElement.Square().Add(curve.FromBigInteger(new BigInteger("7"))));
            ECFieldElement beta = alpha.Sqrt();
            
            // Choose correct y coordinate based on isYEven
            bool betaIsEven = beta.ToBigInteger().TestBit(0) == false;
            ECFieldElement y = betaIsEven == isYEven ? beta : curve.FromBigInteger(curve.Q.Subtract(beta.ToBigInteger()));
            
            // Create point R
            ECPoint R = curve.CreatePoint(xFieldElement.ToBigInteger(), y.ToBigInteger());
            
            // Calculate public key Q = (s * R - e * G) / r
            ECPoint G = curveParams.G;
            BigInteger rInverse = r.ModInverse(n);
            
            ECPoint Q = R.Multiply(s).Subtract(G.Multiply(e)).Multiply(rInverse.ModInverse(n));
            
            return Q;
        }
        
        /// <summary>
        /// Compare two byte arrays for equality
        /// </summary>
        private bool CompareByteArrays(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
                return false;
                
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                    return false;
            }
            
            return true;
        }
        */
    }
} 