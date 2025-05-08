using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Threading.Tasks;

namespace NostrUnity.Examples
{
    public class NostrExample : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_InputField messageInput;
        [SerializeField] private Button sendButton;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI publicKeyText;

        private NostrManager _nostrManager;

        private void Start()
        {
            // Initialize NostrManager
            _nostrManager = gameObject.AddComponent<NostrManager>();
            _nostrManager.Initialize(); // This will generate a new key pair

            // Display the public key
            publicKeyText.text = $"Your Public Key: {_nostrManager.GetNpub()}";

            // Setup button listener
            sendButton.onClick.AddListener(SendMessage);

            // Subscribe to events
            _nostrManager.OnConnected += (relay) => UpdateStatus($"Connected to {relay}");
            _nostrManager.OnDisconnected += (relay) => UpdateStatus($"Disconnected from {relay}");
            _nostrManager.OnError += (error) => UpdateStatus($"Error: {error}");
            _nostrManager.OnEventReceived += (ev) => UpdateStatus($"Received event: {ev.Content}");
        }

        private async void SendMessage()
        {
            if (string.IsNullOrEmpty(messageInput.text))
            {
                UpdateStatus("Please enter a message");
                return;
            }

            try
            {
                UpdateStatus("Sending message...");
                await _nostrManager.PostTextNote(messageInput.text, "wss://relay.damus.io");
                UpdateStatus("Message sent successfully!");
                messageInput.text = "";
            }
            catch (System.Exception ex)
            {
                UpdateStatus($"Error sending message: {ex.Message}");
            }
        }

        private void UpdateStatus(string message)
        {
            statusText.text = message;
            Debug.Log(message);
        }

        private void OnDestroy()
        {
            if (_nostrManager != null)
            {
                _nostrManager.OnConnected -= (relay) => UpdateStatus($"Connected to {relay}");
                _nostrManager.OnDisconnected -= (relay) => UpdateStatus($"Disconnected from {relay}");
                _nostrManager.OnError -= (error) => UpdateStatus($"Error: {error}");
                _nostrManager.OnEventReceived -= (ev) => UpdateStatus($"Received event: {ev.Content}");
            }
        }
    }
} 