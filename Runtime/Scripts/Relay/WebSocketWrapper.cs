using System;
using System.Collections;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace NostrUnity.Relay
{
    /// <summary>
    /// WebSocket close codes
    /// </summary>
    public enum WebSocketCloseCode
    {
        Normal = 1000,
        Away = 1001,
        ProtocolError = 1002,
        UnsupportedData = 1003,
        Undefined = 1004,
        NoStatus = 1005,
        Abnormal = 1006,
        InvalidData = 1007,
        PolicyViolation = 1008,
        TooBig = 1009,
        MandatoryExtension = 1010,
        ServerError = 1011,
        TlsHandshakeFailure = 1015
    }

    /// <summary>
    /// Wrapper around System.Net.WebSockets.ClientWebSocket to provide a consistent API 
    /// that mimics the NativeWebSocket package
    /// </summary>
    public class WebSocket
    {
        private readonly ClientWebSocket _webSocket;
        private readonly Uri _uri;
        private readonly CancellationTokenSource _cts;
        private readonly int _receiveBufferSize = 8192;
        
        private bool _isConnected = false;
        private Task _receiveTask;

        // Events
        public event Action OnOpen;
        public event Action<byte[]> OnMessage;
        public event Action<WebSocketCloseCode> OnClose;
        public event Action<string> OnError;

        /// <summary>
        /// Creates a new WebSocket instance
        /// </summary>
        /// <param name="url">The WebSocket server URL (wss://...)</param>
        public WebSocket(string url)
        {
            _uri = new Uri(url);
            _webSocket = new ClientWebSocket();
            _cts = new CancellationTokenSource();
        }

        /// <summary>
        /// Connects to the WebSocket server
        /// </summary>
        public async Task Connect()
        {
            try
            {
                await _webSocket.ConnectAsync(_uri, _cts.Token);
                _isConnected = true;
                OnOpen?.Invoke();
                
                // Start receiving messages
                _receiveTask = ReceiveLoop();
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Sends a text message to the server
        /// </summary>
        /// <param name="message">The text message to send</param>
        public async Task SendText(string message)
        {
            if (!_isConnected)
                throw new InvalidOperationException("WebSocket is not connected");
            
            byte[] buffer = Encoding.UTF8.GetBytes(message);
            await _webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, _cts.Token);
        }

        /// <summary>
        /// Closes the WebSocket connection
        /// </summary>
        public async Task Close()
        {
            if (!_isConnected)
                return;
            
            _isConnected = false; // Set this first to prevent race conditions
            
            try
            {
                // Cancel the cancellation token to stop ongoing operations
                if (!_cts.IsCancellationRequested)
                {
                    _cts.Cancel();
                }
                
                // Wait for receive task to complete with a timeout
                if (_receiveTask != null && !_receiveTask.IsCompleted)
                {
                    await Task.WhenAny(_receiveTask, Task.Delay(1000));
                }
                
                // Attempt to close gracefully if WebSocket is still open
                if (_webSocket.State == WebSocketState.Open)
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by client", _cts.Token);
                }
                
                OnClose?.Invoke(WebSocketCloseCode.Normal);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation token is triggered
                OnClose?.Invoke(WebSocketCloseCode.Normal);
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex.Message);
                OnClose?.Invoke(WebSocketCloseCode.Abnormal);
            }
            finally
            {
                try
                {
                    _webSocket?.Dispose();
                    _cts?.Dispose();
                }
                catch (Exception)
                {
                    // Ignore dispose errors
                }
            }
        }

        /// <summary>
        /// Synchronously closes the WebSocket connection - used during application quit
        /// </summary>
        public void CloseSync()
        {
            if (!_isConnected)
                return;
            
            _isConnected = false; // Set this first to prevent race conditions
            
            try
            {
                // Cancel any ongoing operations first
                if (!_cts.IsCancellationRequested)
                {
                    _cts.Cancel();
                }
                
                // Give a brief moment for operations to cancel gracefully
                if (_receiveTask != null && !_receiveTask.IsCompleted)
                {
                    Task.WhenAny(_receiveTask, Task.Delay(100)).Wait(200);
                }
                
                // Attempt graceful close if still open
                if (_webSocket?.State == WebSocketState.Open)
                {
                    try
                    {
                        _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Application closing", CancellationToken.None).Wait(500);
                    }
                    catch (TimeoutException)
                    {
                        // Timeout during close is acceptable during shutdown
                    }
                }
                
                // Only notify of closure if we haven't already
                OnClose?.Invoke(WebSocketCloseCode.Away);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Error during sync close: {ex.Message}");
            }
            finally
            {
                try
                {
                    _webSocket?.Dispose();
                    _cts?.Dispose();
                }
                catch (Exception)
                {
                    // Ignore dispose errors during shutdown
                }
            }
        }

        /// <summary>
        /// Process message queue - this should be called from Update()
        /// Unity WebGL doesn't support multi-threading so we need to process messages on main thread
        /// </summary>
        public void DispatchMessageQueue()
        {
            // In standalone builds this is handled automatically by the ReceiveLoop task
        }

        /// <summary>
        /// Continuously receives messages from the server
        /// </summary>
        private async Task ReceiveLoop()
        {
            var buffer = new byte[_receiveBufferSize];
            
            try
            {
                while (_isConnected && !_cts.IsCancellationRequested && _webSocket.State == WebSocketState.Open)
                {
                    var segment = new ArraySegment<byte>(buffer);
                    WebSocketReceiveResult result = await _webSocket.ReceiveAsync(segment, _cts.Token);
                    
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        if (_isConnected) // Only process close if we haven't already handled it
                        {
                            _isConnected = false;
                            WebSocketCloseCode code = result.CloseStatus.HasValue ? 
                                (WebSocketCloseCode)(int)result.CloseStatus.Value : 
                                WebSocketCloseCode.Abnormal;
                            OnClose?.Invoke(code);
                        }
                        break;
                    }
                    
                    if (result.MessageType == WebSocketMessageType.Text && _isConnected)
                    {
                        byte[] message = new byte[result.Count];
                        Array.Copy(buffer, message, result.Count);
                        OnMessage?.Invoke(message);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation - don't treat as error
                if (_isConnected)
                {
                    _isConnected = false;
                    OnClose?.Invoke(WebSocketCloseCode.Normal);
                }
            }
            catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
            {
                // Remote closed connection without proper handshake
                if (_isConnected)
                {
                    _isConnected = false;
                    OnClose?.Invoke(WebSocketCloseCode.Abnormal);
                }
            }
            catch (Exception ex) when (!_cts.IsCancellationRequested && _isConnected)
            {
                _isConnected = false;
                OnError?.Invoke(ex.Message);
                OnClose?.Invoke(WebSocketCloseCode.Abnormal);
            }
        }
    }
} 