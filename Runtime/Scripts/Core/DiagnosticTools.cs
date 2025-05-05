using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Nostr.Unity
{
    /// <summary>
    /// Provides diagnostic tools for troubleshooting Nostr relay communication issues
    /// </summary>
    public class DiagnosticTools : MonoBehaviour
    {
        /// <summary>
        /// Tests direct WebSocket connectivity to a given relay
        /// </summary>
        /// <param name="relayUrl">URL of the relay to test</param>
        /// <returns>A coroutine that reports the test results</returns>
        public IEnumerator TestRelayConnection(string relayUrl, Action<DiagnosticResult> onComplete = null)
        {
            Debug.Log($"[DIAGNOSTICS] Testing connection to relay: {relayUrl}");
            
            DiagnosticResult result = new DiagnosticResult
            {
                RelayUrl = relayUrl,
                StartTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            
            using (var ws = new ClientWebSocket())
            {
                // Attempt connection
                Debug.Log($"[DIAGNOSTICS] Attempting connection to {relayUrl}");
                
                Task connectTask = null;
                
                try
                {
                    connectTask = ws.ConnectAsync(new Uri(relayUrl), CancellationToken.None);
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Error = $"Connection failed: {ex.Message}";
                    result.EndTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    onComplete?.Invoke(result);
                    yield break;
                }
                
                // Wait for connection (with timeout)
                DateTime startTime = DateTime.Now;
                TimeSpan timeout = TimeSpan.FromSeconds(10);
                
                while (connectTask != null && !connectTask.IsCompleted && !connectTask.IsFaulted && !connectTask.IsCanceled)
                {
                    if (DateTime.Now - startTime > timeout)
                    {
                        result.Success = false;
                        result.Error = "Connection timeout after 10 seconds";
                        result.EndTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        onComplete?.Invoke(result);
                        yield break;
                    }
                    yield return null;
                }
                
                if (connectTask != null && connectTask.IsFaulted)
                {
                    result.Success = false;
                    result.Error = $"Connection failed: {connectTask.Exception?.InnerException?.Message ?? "Unknown error"}";
                    result.EndTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    onComplete?.Invoke(result);
                    yield break;
                }
                
                Debug.Log($"[DIAGNOSTICS] Connected to {relayUrl}");
                
                // Connection successful, now try sending a simple request
                string reqId = $"test_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
                JArray requestMessage = new JArray();
                requestMessage.Add("REQ");
                requestMessage.Add(reqId);
                requestMessage.Add(new JObject
                {
                    ["limit"] = 1,
                    ["kinds"] = new JArray { 0 }
                });
                
                string requestJson = requestMessage.ToString(Formatting.None);
                byte[] requestBytes = Encoding.UTF8.GetBytes(requestJson);
                
                Debug.Log($"[DIAGNOSTICS] Sending test request: {requestJson}");
                
                Task sendTask = null;
                
                try
                {
                    sendTask = ws.SendAsync(
                        new ArraySegment<byte>(requestBytes),
                        WebSocketMessageType.Text,
                        true,
                        CancellationToken.None
                    );
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Error = $"Failed to send message: {ex.Message}";
                    result.EndTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    onComplete?.Invoke(result);
                    yield break;
                }
                
                // Wait for send to complete
                while (sendTask != null && !sendTask.IsCompleted && !sendTask.IsFaulted && !sendTask.IsCanceled)
                {
                    yield return null;
                }
                
                if (sendTask != null && sendTask.IsFaulted)
                {
                    result.Success = false;
                    result.Error = $"Failed to send message: {sendTask.Exception?.InnerException?.Message ?? "Unknown error"}";
                    result.EndTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    onComplete?.Invoke(result);
                    yield break;
                }
                
                Debug.Log($"[DIAGNOSTICS] Request sent successfully to {relayUrl}");
                
                // Wait for a response with a timeout
                var buffer = new byte[8192];
                bool receivedResponse = false;
                
                for (int attempts = 0; attempts < 5; attempts++)
                {
                    Task<WebSocketReceiveResult> receiveTask = null;
                    bool receiveError = false;
                    string errorMessage = "";
                    
                    try
                    {
                        receiveTask = ws.ReceiveAsync(
                            new ArraySegment<byte>(buffer),
                            CancellationToken.None
                        );
                    }
                    catch (Exception ex)
                    {
                        receiveError = true;
                        errorMessage = ex.Message;
                    }
                    
                    if (receiveError)
                    {
                        Debug.LogWarning($"[DIAGNOSTICS] Receive attempt {attempts+1} failed: {errorMessage}");
                        yield return new WaitForSeconds(1f);
                        continue;
                    }
                    
                    startTime = DateTime.Now;
                    timeout = TimeSpan.FromSeconds(5);
                    
                    while (receiveTask != null && !receiveTask.IsCompleted && !receiveTask.IsFaulted && !receiveTask.IsCanceled)
                    {
                        if (DateTime.Now - startTime > timeout)
                        {
                            Debug.Log($"[DIAGNOSTICS] Receive attempt {attempts+1} timed out");
                            break; // Try again
                        }
                        yield return null;
                    }
                    
                    if (receiveTask != null && receiveTask.IsCompleted && !receiveTask.IsFaulted)
                    {
                        var receiveResult = receiveTask.Result;
                        if (receiveResult.MessageType == WebSocketMessageType.Text)
                        {
                            string response = Encoding.UTF8.GetString(buffer, 0, receiveResult.Count);
                            Debug.Log($"[DIAGNOSTICS] Received response: {response}");
                            
                            // Basic validation that it's a Nostr message
                            bool validResponse = false;
                            
                            try
                            {
                                var responseArray = JArray.Parse(response);
                                if (responseArray.Count > 0)
                                {
                                    string messageType = responseArray[0].ToString();
                                    result.ReceivedData = true;
                                    result.ResponseData = response;
                                    receivedResponse = true;
                                    validResponse = true;
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"[DIAGNOSTICS] Error parsing response: {ex.Message}");
                            }
                            
                            if (validResponse)
                            {
                                break;
                            }
                        }
                    }
                    
                    // Wait a bit before next attempt
                    yield return new WaitForSeconds(1f);
                }
                
                // Send a CLOSE message to clean up
                JArray closeMessage = new JArray();
                closeMessage.Add("CLOSE");
                closeMessage.Add(reqId);
                string closeJson = closeMessage.ToString(Formatting.None);
                byte[] closeBytes = Encoding.UTF8.GetBytes(closeJson);
                
                Debug.Log($"[DIAGNOSTICS] Sending CLOSE: {closeJson}");
                
                bool closeMessageError = false;
                string closeErrorMessage = "";
                sendTask = null;
                
                try
                {
                    sendTask = ws.SendAsync(
                        new ArraySegment<byte>(closeBytes),
                        WebSocketMessageType.Text,
                        true,
                        CancellationToken.None
                    );
                }
                catch (Exception ex)
                {
                    closeMessageError = true;
                    closeErrorMessage = ex.Message;
                }
                
                if (!closeMessageError && sendTask != null)
                {
                    while (!sendTask.IsCompleted && !sendTask.IsFaulted && !sendTask.IsCanceled)
                    {
                        yield return null;
                    }
                }
                
                if (closeMessageError)
                {
                    Debug.LogWarning($"[DIAGNOSTICS] Failed to send CLOSE: {closeErrorMessage}");
                }
                
                // Close the connection
                if (ws.State == WebSocketState.Open)
                {
                    Task closeTask = null;
                    bool closeError = false;
                    string closeTaskErrorMessage = "";
                    
                    try
                    {
                        closeTask = ws.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "Diagnostic test complete",
                            CancellationToken.None
                        );
                    }
                    catch (Exception ex)
                    {
                        closeError = true;
                        closeTaskErrorMessage = ex.Message;
                    }
                    
                    if (closeError)
                    {
                        Debug.LogWarning($"[DIAGNOSTICS] Error initiating close: {closeTaskErrorMessage}");
                    }
                    else if (closeTask != null)
                    {
                        while (closeTask != null && !closeTask.IsCompleted && !closeTask.IsFaulted && !closeTask.IsCanceled)
                        {
                            yield return null;
                        }
                        
                        if (closeTask.IsFaulted)
                        {
                            Debug.LogWarning($"[DIAGNOSTICS] Error during close: {closeTask.Exception?.InnerException?.Message ?? "Unknown error"}");
                        }
                    }
                }
                
                // Final result
                result.Success = true;
                result.EndTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                result.ConnectionEstablished = true;
                result.MessageSent = true;
                result.ReceivedData = receivedResponse;
                
                Debug.Log($"[DIAGNOSTICS] Test completed for {relayUrl}: Connection: {result.ConnectionEstablished}, " +
                    $"Send: {result.MessageSent}, Receive: {result.ReceivedData}");
            }
            
            onComplete?.Invoke(result);
        }
        
        /// <summary>
        /// Tests a complete event publication to verify relay acceptance
        /// </summary>
        /// <param name="relayUrl">The relay URL to test</param>
        /// <param name="testEvent">A valid, signed Nostr event to publish</param>
        /// <returns>A coroutine that publishes the event and reports the result</returns>
        public IEnumerator TestEventPublication(string relayUrl, NostrEvent testEvent, Action<DiagnosticResult> onComplete = null)
        {
            Debug.Log($"[DIAGNOSTICS] Testing event publication to {relayUrl}");
            
            DiagnosticResult result = new DiagnosticResult
            {
                RelayUrl = relayUrl,
                StartTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                EventId = testEvent.Id
            };
            
            using (var ws = new ClientWebSocket())
            {
                // Connect to the relay
                Debug.Log($"[DIAGNOSTICS] Connecting to {relayUrl}");
                Task connectTask = null;
                
                try
                {
                    connectTask = ws.ConnectAsync(new Uri(relayUrl), CancellationToken.None);
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Error = $"Connection failed: {ex.Message}";
                    result.EndTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    onComplete?.Invoke(result);
                    yield break;
                }
                
                // Wait for connection with timeout
                DateTime startTime = DateTime.Now;
                TimeSpan timeout = TimeSpan.FromSeconds(10);
                
                while (connectTask != null && !connectTask.IsCompleted && !connectTask.IsFaulted && !connectTask.IsCanceled)
                {
                    if (DateTime.Now - startTime > timeout)
                    {
                        result.Success = false;
                        result.Error = "Connection timeout after 10 seconds";
                        result.EndTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        onComplete?.Invoke(result);
                        yield break;
                    }
                    yield return null;
                }
                
                if (connectTask != null && connectTask.IsFaulted)
                {
                    result.Success = false;
                    result.Error = $"Connection failed: {connectTask.Exception?.InnerException?.Message ?? "Unknown error"}";
                    result.EndTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    onComplete?.Invoke(result);
                    yield break;
                }
                
                result.ConnectionEstablished = true;
                Debug.Log($"[DIAGNOSTICS] Connected to {relayUrl}");
                
                // Serialize the event
                string eventJson = testEvent.SerializeComplete();
                JArray message = new JArray();
                message.Add("EVENT");
                message.Add(JObject.Parse(eventJson));
                
                string messageJson = message.ToString(Formatting.None);
                byte[] messageBytes = Encoding.UTF8.GetBytes(messageJson);
                
                Debug.Log($"[DIAGNOSTICS] Publishing event: {messageJson}");
                
                // Send the EVENT message
                Task sendTask = null;
                
                try
                {
                    sendTask = ws.SendAsync(
                        new ArraySegment<byte>(messageBytes),
                        WebSocketMessageType.Text,
                        true,
                        CancellationToken.None
                    );
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Error = $"Failed to send event: {ex.Message}";
                    result.EndTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    onComplete?.Invoke(result);
                    yield break;
                }
                
                // Wait for send to complete
                while (sendTask != null && !sendTask.IsCompleted && !sendTask.IsFaulted && !sendTask.IsCanceled)
                {
                    yield return null;
                }
                
                if (sendTask != null && sendTask.IsFaulted)
                {
                    result.Success = false;
                    result.Error = $"Failed to send event: {sendTask.Exception?.InnerException?.Message ?? "Unknown error"}";
                    result.EndTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    onComplete?.Invoke(result);
                    yield break;
                }
                
                result.MessageSent = true;
                Debug.Log($"[DIAGNOSTICS] Event sent to {relayUrl}");
                
                // Wait for OK response
                var buffer = new byte[8192];
                bool receivedOk = false;
                bool success = false;
                string okMessage = "";
                
                // Wait for up to 10 seconds for an OK message
                for (int i = 0; i < 5; i++)
                {
                    Task<WebSocketReceiveResult> receiveTask = null;
                    bool receiveError = false;
                    string errorMessage = "";
                    
                    try
                    {
                        receiveTask = ws.ReceiveAsync(
                            new ArraySegment<byte>(buffer),
                            CancellationToken.None
                        );
                    }
                    catch (Exception ex)
                    {
                        receiveError = true;
                        errorMessage = ex.Message;
                    }
                    
                    if (receiveError)
                    {
                        Debug.LogWarning($"[DIAGNOSTICS] Receive attempt {i+1} failed: {errorMessage}");
                        yield return new WaitForSeconds(2f);
                        continue;
                    }
                    
                    startTime = DateTime.Now;
                    timeout = TimeSpan.FromSeconds(5);
                    
                    while (receiveTask != null && !receiveTask.IsCompleted && !receiveTask.IsFaulted && !receiveTask.IsCanceled)
                    {
                        if (DateTime.Now - startTime > timeout)
                        {
                            Debug.Log($"[DIAGNOSTICS] Receive attempt {i+1} timed out");
                            break;
                        }
                        yield return null;
                    }
                    
                    if (receiveTask != null && receiveTask.IsCompleted && !receiveTask.IsFaulted)
                    {
                        var receiveResult = receiveTask.Result;
                        if (receiveResult.MessageType == WebSocketMessageType.Text)
                        {
                            string response = Encoding.UTF8.GetString(buffer, 0, receiveResult.Count);
                            Debug.Log($"[DIAGNOSTICS] Received response: {response}");
                            result.ResponseData = response;
                            
                            bool parsedOk = false;
                            
                            try
                            {
                                var responseArray = JArray.Parse(response);
                                if (responseArray.Count >= 3 && responseArray[0].ToString() == "OK")
                                {
                                    receivedOk = true;
                                    string responseEventId = responseArray[1].ToString();
                                    bool accepted = responseArray[2].ToString() == "true" || responseArray[2].ToString() == "accepted";
                                    
                                    if (responseEventId == testEvent.Id)
                                    {
                                        if (accepted)
                                        {
                                            success = true;
                                            okMessage = "Event accepted by relay";
                                        }
                                        else
                                        {
                                            okMessage = $"Event rejected by relay: {responseArray[3]?.ToString() ?? "Unknown reason"}";
                                        }
                                        parsedOk = true;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"[DIAGNOSTICS] Error parsing response: {ex.Message}");
                            }
                            
                            if (parsedOk)
                            {
                                break;
                            }
                        }
                    }
                    
                    yield return new WaitForSeconds(2f);
                }
                
                // Close the connection
                if (ws.State == WebSocketState.Open)
                {
                    Task closeTask = null;
                    bool closeError = false;
                    string closeErrorMessage = "";
                    
                    try
                    {
                        closeTask = ws.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "Diagnostic test complete",
                            CancellationToken.None
                        );
                    }
                    catch (Exception ex)
                    {
                        closeError = true;
                        closeErrorMessage = ex.Message;
                    }
                    
                    if (closeError)
                    {
                        Debug.LogWarning($"[DIAGNOSTICS] Error during close: {closeErrorMessage}");
                    }
                    else if (closeTask != null)
                    {
                        while (closeTask != null && !closeTask.IsCompleted && !closeTask.IsFaulted && !closeTask.IsCanceled)
                        {
                            yield return null;
                        }
                        
                        if (closeTask.IsFaulted)
                        {
                            Debug.LogWarning($"[DIAGNOSTICS] Error during close: {closeTask.Exception?.InnerException?.Message ?? "Unknown error"}");
                        }
                    }
                }
                
                // Set final result
                result.Success = success;
                result.ReceivedData = receivedOk;
                result.EndTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                
                if (!receivedOk)
                {
                    result.Error = "No OK response received from relay";
                }
                else if (!success)
                {
                    result.Error = okMessage;
                }
                
                Debug.Log($"[DIAGNOSTICS] Publication test completed for {relayUrl}: Success={success}, ReceivedOK={receivedOk}");
                
                onComplete?.Invoke(result);
            }
        }
    }
    
    /// <summary>
    /// Results from a diagnostic test
    /// </summary>
    public class DiagnosticResult
    {
        /// <summary>
        /// The relay URL that was tested
        /// </summary>
        public string RelayUrl { get; set; }
        
        /// <summary>
        /// Whether the overall test was successful
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// Error message, if any
        /// </summary>
        public string Error { get; set; }
        
        /// <summary>
        /// Whether a connection was successfully established
        /// </summary>
        public bool ConnectionEstablished { get; set; }
        
        /// <summary>
        /// Whether a message was successfully sent
        /// </summary>
        public bool MessageSent { get; set; }
        
        /// <summary>
        /// Whether any data was received from the relay
        /// </summary>
        public bool ReceivedData { get; set; }
        
        /// <summary>
        /// Raw response data from the relay
        /// </summary>
        public string ResponseData { get; set; }
        
        /// <summary>
        /// When the test started (Unix timestamp in milliseconds)
        /// </summary>
        public long StartTime { get; set; }
        
        /// <summary>
        /// When the test ended (Unix timestamp in milliseconds)
        /// </summary>
        public long EndTime { get; set; }
        
        /// <summary>
        /// The event ID being tested, if applicable
        /// </summary>
        public string EventId { get; set; }
        
        /// <summary>
        /// Gets a detailed diagnostic report
        /// </summary>
        public string GetDetailedReport()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Diagnostic Report for {RelayUrl}");
            sb.AppendLine($"Success: {Success}");
            sb.AppendLine($"Connection Established: {ConnectionEstablished}");
            sb.AppendLine($"Message Sent: {MessageSent}");
            sb.AppendLine($"Response Received: {ReceivedData}");
            
            if (!string.IsNullOrEmpty(Error))
                sb.AppendLine($"Error: {Error}");
            
            if (!string.IsNullOrEmpty(EventId))
                sb.AppendLine($"Event ID: {EventId}");
            
            sb.AppendLine($"Duration: {EndTime - StartTime}ms");
            
            if (!string.IsNullOrEmpty(ResponseData))
            {
                sb.AppendLine("\nResponse Data:");
                sb.AppendLine(ResponseData);
            }
            
            return sb.ToString();
        }
    }
    
    /// <summary>
    /// Extension methods for Task to help with coroutines
    /// </summary>
    public static class TaskExtensions
    {
        /// <summary>
        /// Converts a Task to a Unity coroutine
        /// </summary>
        public static IEnumerator AsCoroutine(this Task task)
        {
            while (!task.IsCompleted && !task.IsFaulted && !task.IsCanceled)
            {
                yield return null;
            }
            
            if (task.IsFaulted)
                throw task.Exception;
        }
    }
} 