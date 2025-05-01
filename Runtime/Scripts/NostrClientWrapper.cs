using System;
using System.Threading.Tasks;
using NNostr.Client;

namespace NNostrUnitySDK
{
    public class NostrClientWrapper : INostrClient
    {
        private readonly NostrClient _client;
        private bool _disposed;

        public bool IsConnected => _client?.IsConnected ?? false;
        public string[] Relays { get; }

        public event EventHandler<string> MessageReceived;
        public event EventHandler<Exception> ErrorOccurred;

        public NostrClientWrapper(string[] relays)
        {
            Relays = relays ?? throw new ArgumentNullException(nameof(relays));
            _client = new NostrClient();
        }

        public async Task ConnectAsync()
        {
            try
            {
                foreach (var relay in Relays)
                {
                    await _client.ConnectToRelayAsync(relay);
                }
                _client.MessageReceived += (s, e) => MessageReceived?.Invoke(this, e.Message);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
                throw;
            }
        }

        public async Task DisconnectAsync()
        {
            if (IsConnected)
            {
                await _client.DisconnectAsync();
            }
        }

        public async Task SendMessageAsync(string message)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Client is not connected to any relay.");
            }
            await _client.SendMessageAsync(message);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _client?.Dispose();
                _disposed = true;
            }
        }
    }
} 