using System;
using System.Collections.Generic;
using UnityEngine;

namespace NostrUnity.Utils
{
    /// <summary>
    /// Utility for implementing rate limiting on outgoing messages
    /// </summary>
    public class RateLimiter
    {
        private readonly int _maxMessagesPerInterval;
        private readonly float _intervalSeconds;
        private readonly Queue<QueuedMessage> _messageQueue = new Queue<QueuedMessage>();
        
        private int _messagesSentInCurrentInterval = 0;
        private float _intervalStartTime;
        
        /// <summary>
        /// Gets a value indicating whether there are messages in the queue
        /// </summary>
        public bool HasQueuedMessages => _messageQueue.Count > 0;
        
        /// <summary>
        /// Gets the number of messages currently in the queue
        /// </summary>
        public int QueuedMessageCount => _messageQueue.Count;
        
        /// <summary>
        /// Gets the number of messages sent in the current interval
        /// </summary>
        public int MessagesSentInCurrentInterval => _messagesSentInCurrentInterval;
        
        /// <summary>
        /// Creates a new rate limiter
        /// </summary>
        /// <param name="maxMessagesPerInterval">Maximum number of messages allowed per interval</param>
        /// <param name="intervalSeconds">The interval length in seconds</param>
        public RateLimiter(int maxMessagesPerInterval, float intervalSeconds)
        {
            if (maxMessagesPerInterval <= 0)
                throw new ArgumentException("Maximum messages per interval must be positive", nameof(maxMessagesPerInterval));
            
            if (intervalSeconds <= 0)
                throw new ArgumentException("Interval seconds must be positive", nameof(intervalSeconds));
            
            _maxMessagesPerInterval = maxMessagesPerInterval;
            _intervalSeconds = intervalSeconds;
            _intervalStartTime = Time.time;
        }
        
        /// <summary>
        /// Attempts to send a message, queuing it if it would exceed the rate limit
        /// </summary>
        /// <param name="message">The message to send</param>
        /// <param name="sendAction">The action to invoke to send the message</param>
        /// <returns>True if the message was sent immediately, false if it was queued</returns>
        public bool TrySend(string message, Action<string> sendAction)
        {
            UpdateInterval();
            
            if (_messagesSentInCurrentInterval < _maxMessagesPerInterval)
            {
                // We're under the rate limit, send immediately
                sendAction(message);
                _messagesSentInCurrentInterval++;
                return true;
            }
            else
            {
                // Queue the message for later
                _messageQueue.Enqueue(new QueuedMessage(message, sendAction));
                return false;
            }
        }
        
        /// <summary>
        /// Updates the current time interval and processes any queued messages if the rate limit allows
        /// </summary>
        public void Update()
        {
            UpdateInterval();
            ProcessQueue();
        }
        
        /// <summary>
        /// Updates the current time interval
        /// </summary>
        private void UpdateInterval()
        {
            float currentTime = Time.time;
            float elapsedTime = currentTime - _intervalStartTime;
            
            if (elapsedTime >= _intervalSeconds)
            {
                // Reset interval
                _intervalStartTime = currentTime;
                _messagesSentInCurrentInterval = 0;
            }
        }
        
        /// <summary>
        /// Processes queued messages if the rate limit allows
        /// </summary>
        private void ProcessQueue()
        {
            // Process as many queued messages as possible without exceeding the rate limit
            while (_messageQueue.Count > 0 && _messagesSentInCurrentInterval < _maxMessagesPerInterval)
            {
                QueuedMessage queuedMessage = _messageQueue.Dequeue();
                queuedMessage.SendAction(queuedMessage.Message);
                _messagesSentInCurrentInterval++;
            }
        }
        
        /// <summary>
        /// Clear all queued messages
        /// </summary>
        public void ClearQueue()
        {
            _messageQueue.Clear();
        }
        
        /// <summary>
        /// Represents a message that has been queued for later sending
        /// </summary>
        private class QueuedMessage
        {
            public string Message { get; }
            public Action<string> SendAction { get; }
            
            public QueuedMessage(string message, Action<string> sendAction)
            {
                Message = message;
                SendAction = sendAction;
            }
        }
    }
} 