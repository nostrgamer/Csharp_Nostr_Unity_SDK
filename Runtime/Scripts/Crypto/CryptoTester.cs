using System;
using System.Text;
using UnityEngine;

namespace Nostr.Unity
{
    /// <summary>
    /// Test component for verifying the cryptographic implementation
    /// </summary>
    public class CryptoTester : MonoBehaviour
    {
        [Header("Key Generation")]
        [SerializeField]
        private bool generateOnStart = false;
        
        [Header("Results")]
        [SerializeField]
        private string privateKeyHex = "";
        
        [SerializeField]
        private string publicKeyHex = "";
        
        [SerializeField]
        private string messageToSign = "Hello, Nostr!";
        
        [SerializeField]
        private string signatureHex = "";
        
        [SerializeField]
        private string recoverableSignatureHex = "";
        
        [SerializeField]
        private bool signatureValid = false;
        
        private byte[] privateKey;
        private byte[] publicKey;
        private byte[] messageHash;
        private byte[] signature;
        private byte[] recoverableSignature;
        
        private void Start()
        {
            // Initialize Secp256k1Manager
            if (!Secp256k1Manager.Initialize())
            {
                Debug.LogError("Failed to initialize Secp256k1Manager");
                return;
            }
            
            if (generateOnStart)
            {
                RunTest();
            }
        }
        
        /// <summary>
        /// Runs a full cryptographic test
        /// </summary>
        public void RunTest()
        {
            try
            {
                Debug.Log("Starting cryptographic test...");
                
                // Generate a new private key
                privateKey = Secp256k1Manager.GeneratePrivateKey();
                privateKeyHex = BytesToHex(privateKey);
                Debug.Log($"Generated private key: {privateKeyHex}");
                
                // Derive the public key
                publicKey = Secp256k1Manager.GetPublicKey(privateKey);
                publicKeyHex = BytesToHex(publicKey);
                Debug.Log($"Derived public key: {publicKeyHex}");
                
                // Hash the message
                messageHash = Secp256k1Manager.ComputeMessageHash(messageToSign);
                Debug.Log($"Message hash: {BytesToHex(messageHash)}");
                
                // Sign the message
                signature = Secp256k1Manager.Sign(messageHash, privateKey);
                signatureHex = BytesToHex(signature);
                Debug.Log($"Signature: {signatureHex}");
                
                // Generate a recoverable signature
                recoverableSignature = Secp256k1Manager.SignRecoverable(messageHash, privateKey);
                recoverableSignatureHex = BytesToHex(recoverableSignature);
                Debug.Log($"Recoverable signature: {recoverableSignatureHex}");
                
                // Verify the signature
                signatureValid = Secp256k1Manager.Verify(messageHash, signature, publicKey);
                Debug.Log($"Signature valid: {signatureValid}");
                
                // Verify the recoverable signature
                bool recoverableSignatureValid = Secp256k1Manager.Verify(messageHash, recoverableSignature, publicKey);
                Debug.Log($"Recoverable signature valid: {recoverableSignatureValid}");
                
                // Future: Recover public key from signature
                // byte[] recoveredPublicKey = Secp256k1Manager.RecoverPublicKey(recoverableSignature, messageHash);
                // Debug.Log($"Recovered public key: {BytesToHex(recoveredPublicKey)}");
                
                Debug.Log("Cryptographic test completed successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error during test: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Converts a byte array to a hex string
        /// </summary>
        private string BytesToHex(byte[] bytes)
        {
            if (bytes == null)
                return "";
                
            StringBuilder hex = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
            {
                hex.AppendFormat("{0:x2}", b);
            }
            return hex.ToString();
        }
    }
} 