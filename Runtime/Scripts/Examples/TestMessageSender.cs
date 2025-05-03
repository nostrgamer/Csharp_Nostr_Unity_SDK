using UnityEngine;
using UnityEngine.UI;

namespace Nostr.Unity.Examples
{
    /// <summary>
    /// Example component for sending test messages via the Nostr network
    /// Attach this to a GameObject with a Button component to easily send test messages
    /// </summary>
    public class TestMessageSender : MonoBehaviour
    {
        [SerializeField]
        private NostrManager nostrManager;

        [SerializeField]
        private InputField messageInput;

        [SerializeField]
        private Button sendButton;

        [SerializeField]
        private Text statusText;

        private void Start()
        {
            // Find NostrManager if not assigned
            if (nostrManager == null)
            {
                nostrManager = FindObjectOfType<NostrManager>();
                if (nostrManager == null)
                {
                    Debug.LogError("NostrManager not found in scene. Please add it to a GameObject first.");
                    
                    if (statusText != null)
                        statusText.text = "Error: NostrManager not found";
                        
                    if (sendButton != null)
                        sendButton.interactable = false;
                        
                    return;
                }
            }

            // Set up button listener
            if (sendButton != null)
            {
                sendButton.onClick.AddListener(SendMessage);
            }
            
            // Update status
            if (statusText != null)
            {
                statusText.text = "Ready to send test messages";
            }
        }

        /// <summary>
        /// Sends a test message using the NostrManager
        /// </summary>
        public void SendMessage()
        {
            if (nostrManager == null)
            {
                Debug.LogError("NostrManager not found");
                return;
            }

            // Get custom message if available
            string message = null;
            if (messageInput != null && !string.IsNullOrWhiteSpace(messageInput.text))
            {
                message = messageInput.text;
            }

            // Send the message
            bool sent = nostrManager.SendTestMessage(message);
            
            // Update status
            if (statusText != null)
            {
                statusText.text = sent ? 
                    "Message sent! Check relay to confirm." : 
                    "Failed to send message. Check console for errors.";
            }
            
            // Clear input field
            if (messageInput != null)
            {
                messageInput.text = "";
            }
        }
    }
} 