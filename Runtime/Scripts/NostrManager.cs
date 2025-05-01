using UnityEngine;
using System;
using System.Threading.Tasks;
using UnityEngine.Events;

namespace NNostrUnitySDK
{
    public class NostrManager : MonoBehaviour
    {
        [SerializeField] private string[] relayUrls = new[] { "wss://relay.damus.io" };

        private INostrClient _client;
        private bool _isConnecting;
        private bool _isDisconnecting;

        public bool IsConnected => _client?.IsConnected ?? false;

        // Unity events for better Unity integration
        [System.Serializable]
        public class StringUnityEvent : UnityEvent<string> { }

        public StringUnityEvent onMessageReceived = new StringUnityEvent();
        public StringUnityEvent onError = new StringUnityEvent();

        private void Awake()
        {
            try
            {
                _client = new NostrClientWrapper(relayUrls);
                _client.MessageReceived += HandleMessageReceived;
                _client.ErrorOccurred += HandleError;
            }
            catch (Exception ex)
            {
                HandleError(this, ex);
            }
        }

        private void OnDestroy()
        {
            if (_client != null)
            {
                try
                {
                    if (IsConnected)
                    {
                        // Force synchronous disconnect on destroy
                        _client.DisconnectAsync().GetAwaiter().GetResult();
                    }
                    _client.Dispose();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error during cleanup: {ex.Message}");
                }
            }
        }

        private void HandleMessageReceived(object sender, string message)
        {
            // Ensure callbacks happen on the main thread
            if (this == null) return;
            UnityMainThreadDispatcher.Instance.Enqueue(() => onMessageReceived?.Invoke(message));
        }

        private void HandleError(object sender, Exception ex)
        {
            if (this == null) return;
            string errorMessage = ex?.Message ?? "Unknown error";
            Debug.LogError($"Nostr error: {errorMessage}");
            UnityMainThreadDispatcher.Instance.Enqueue(() => onError?.Invoke(errorMessage));
        }

        public async void ConnectToRelay()
        {
            if (_isConnecting || IsConnected) return;

            try
            {
                _isConnecting = true;
                if (_client == null)
                {
                    throw new InvalidOperationException("Nostr client not initialized");
                }
                await _client.ConnectAsync();
            }
            catch (Exception ex)
            {
                HandleError(this, ex);
            }
            finally
            {
                _isConnecting = false;
            }
        }

        public async void DisconnectFromRelay()
        {
            if (_isDisconnecting || !IsConnected) return;

            try
            {
                _isDisconnecting = true;
                if (_client != null)
                {
                    await _client.DisconnectAsync();
                }
            }
            catch (Exception ex)
            {
                HandleError(this, ex);
            }
            finally
            {
                _isDisconnecting = false;
            }
        }

        public async void SendNostrMessage(string message)
        {
            try
            {
                if (_client == null)
                {
                    throw new InvalidOperationException("Nostr client not initialized");
                }
                if (!IsConnected)
                {
                    throw new InvalidOperationException("Not connected to relay");
                }
                await _client.SendMessageAsync(message);
            }
            catch (Exception ex)
            {
                HandleError(this, ex);
            }
        }
    }
} 