using UnityEngine;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using System.Net.WebSockets;
using System.Text;
using System.Threading;

namespace NostrUnity.Services
{
    public class NostrWebSocketService : MonoBehaviour
    {
        private ClientWebSocket _webSocket;
        private string _relayUrl;
        private bool _isConnected;
        private Action<bool> _onConnectionStatusChanged;
        private Action<string> _onMessageReceived;
        private Action<string> _onError;
        private CancellationTokenSource _cancellationTokenSource;

        public async void Connect(string relayUrl, Action<bool> onConnectionStatusChanged = null, Action<string> onMessageReceived = null, Action<string> onError = null)
        {
            _relayUrl = relayUrl;
            _onConnectionStatusChanged = onConnectionStatusChanged;
            _onMessageReceived = onMessageReceived;
            _onError = onError;
            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                _webSocket = new ClientWebSocket();
                await _webSocket.ConnectAsync(new Uri(relayUrl), _cancellationTokenSource.Token);
                
                _isConnected = true;
                Debug.Log($"Connected to relay: {relayUrl}");
                _onConnectionStatusChanged?.Invoke(true);

                // Start listening for messages
                _ = ReceiveMessagesAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error connecting to relay: {ex.Message}");
                _onError?.Invoke(ex.Message);
            }
        }

        private async Task ReceiveMessagesAsync()
        {
            var buffer = new byte[4096];
            try
            {
                while (_isConnected && !_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellationTokenSource.Token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _isConnected = false;
                        Debug.Log("WebSocket connection closed by server");
                        _onConnectionStatusChanged?.Invoke(false);
                        break;
                    }

                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    Debug.Log($"Received message: {message}");
                    _onMessageReceived?.Invoke(message);
                }
            }
            catch (Exception ex)
            {
                if (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    Debug.LogError($"Error receiving message: {ex.Message}");
                    _onError?.Invoke(ex.Message);
                }
            }
        }

        public async void PublishEvent(string eventJson)
        {
            try
            {
                // Validate event JSON
                if (string.IsNullOrEmpty(eventJson))
                {
                    Debug.LogError("Cannot publish null or empty event");
                    return;
                }
                
                // Ensure the event has the required fields
                if (!eventJson.Contains("\"id\":") || !eventJson.Contains("\"pubkey\":") || 
                    !eventJson.Contains("\"sig\":") || !eventJson.Contains("\"created_at\":"))
                {
                    Debug.LogError("Event is missing required fields");
                    return;
                }
                
                // Format the message in the expected relay format: ["EVENT", {...event json...}]
                string message = $"[\"EVENT\",{eventJson}]";
                Debug.Log($"Publishing event: {message}");
                
                // Validate the message is valid JSON
                try
                {
                    var token = Newtonsoft.Json.Linq.JToken.Parse(message);
                    if (token.Type != Newtonsoft.Json.Linq.JTokenType.Array)
                    {
                        Debug.LogError("Message is not a valid JSON array");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Invalid JSON message: {ex.Message}");
                    return;
                }
                
                // Send the message
                if (_webSocket.State == WebSocketState.Open)
                {
                    await _webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(message)), 
                        WebSocketMessageType.Text, true, CancellationToken.None);
                }
                else
                {
                    Debug.LogError("Cannot publish event - WebSocket is not connected");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error publishing event: {ex.Message}");
            }
        }

        public async void Disconnect()
        {
            if (_webSocket != null)
            {
                _isConnected = false;
                _cancellationTokenSource?.Cancel();
                
                try
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error closing WebSocket: {ex.Message}");
                }
                
                _webSocket.Dispose();
                _webSocket = null;
            }
        }

        public async void SendWebSocketMessage(string messageJson)
        {
            try
            {
                if (string.IsNullOrEmpty(messageJson))
                {
                    Debug.LogError("Cannot send null or empty message");
                    return;
                }
                
                // Validate the message is valid JSON
                try
                {
                    var token = JToken.Parse(messageJson);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Invalid JSON message: {ex.Message}");
                    return;
                }
                
                // Send the message
                if (_webSocket != null && _webSocket.State == WebSocketState.Open)
                {
                    await _webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(messageJson)), 
                        WebSocketMessageType.Text, true, CancellationToken.None);
                    Debug.Log($"Sent WebSocket message: {messageJson}");
                }
                else
                {
                    Debug.LogError("Cannot send message - WebSocket is not connected");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error sending message: {ex.Message}");
            }
        }

        private void OnDestroy()
        {
            Disconnect();
            _cancellationTokenSource?.Dispose();
        }
    }
} 