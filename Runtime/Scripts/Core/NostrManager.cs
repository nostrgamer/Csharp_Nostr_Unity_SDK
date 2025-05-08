using System;
using System.Threading.Tasks;
using UnityEngine;
using NostrUnity.Models;
using NostrUnity.Core;
using NostrUnity.Crypto;
using NostrUnity.Utils;

namespace NostrUnity
{
    /// <summary>
    /// High-level manager for Nostr operations
    /// </summary>
    public class NostrManager : MonoBehaviour
    {
        private NostrClient _client;
        private byte[] _privateKey;
        private byte[] _publicKey;
        private bool _isInitialized;

        public event Action<string> OnConnectionStatusChanged;
        public event Action<string> OnError;
        public event Action<string> OnMessageSent;

        private void Awake()
        {
            _client = new NostrClient();
            _client.ConnectionStatusChanged += (sender, status) => OnConnectionStatusChanged?.Invoke(status.ToString());
            _client.Error += (sender, error) => OnError?.Invoke(error);
            _client.MessageSent += (sender, message) => OnMessageSent?.Invoke(message);
        }

        /// <summary>
        /// Initialize the manager with a new key pair
        /// </summary>
        public void Initialize()
        {
            try
            {
                _privateKey = NostrCrypto.GeneratePrivateKey();
                _publicKey = NostrCrypto.GetPublicKey(_privateKey);
                _isInitialized = true;
                Debug.Log($"Initialized with public key: {GetNpub()}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to initialize: {ex.Message}");
                OnError?.Invoke($"Initialization failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Initialize the manager with an existing private key
        /// </summary>
        public void InitializeWithPrivateKey(string nsec)
        {
            try
            {
                string privateKeyHex = NostrCrypto.DecodeNsec(nsec);
                _privateKey = NostrCrypto.HexToBytes(privateKeyHex);
                _publicKey = NostrCrypto.GetPublicKey(_privateKey);
                _isInitialized = true;
                Debug.Log($"Initialized with public key: {GetNpub()}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to initialize with private key: {ex.Message}");
                OnError?.Invoke($"Initialization failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the npub (encoded public key)
        /// </summary>
        public string GetNpub()
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("NostrManager not initialized");
            }
            return NostrCrypto.GetNpub(BitConverter.ToString(_publicKey).Replace("-", "").ToLowerInvariant());
        }

        /// <summary>
        /// Get the nsec (encoded private key)
        /// </summary>
        public string GetNsec()
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("NostrManager not initialized");
            }
            return NostrCrypto.GetNsec(BitConverter.ToString(_privateKey).Replace("-", "").ToLowerInvariant());
        }

        /// <summary>
        /// Connect to a Nostr relay
        /// </summary>
        public async Task ConnectAsync(string relayUri)
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("NostrManager not initialized");
            }
            await _client.ConnectAsync(relayUri);
        }

        /// <summary>
        /// Post a text note to the connected relay
        /// </summary>
        public async Task PostTextNoteAsync(string content)
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("NostrManager not initialized");
            }

            try
            {
                string publicKeyHex = BitConverter.ToString(_publicKey).Replace("-", "").ToLowerInvariant();
                long createdAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var ev = NostrEvent.CreateTextNote(content, publicKeyHex, createdAt);
                ev.PrivateKey = _privateKey;
                await _client.PublishEvent(ev);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to post text note: {ex.Message}");
                OnError?.Invoke($"Failed to post text note: {ex.Message}");
            }
        }

        private void OnDestroy()
        {
            if (_client != null)
            {
                _client.DisconnectAsync().ConfigureAwait(false);
            }
        }
    }
} 