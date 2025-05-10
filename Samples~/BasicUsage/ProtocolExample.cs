using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NostrUnity.Models;
using NostrUnity.Crypto;
using NostrUnity.Protocol;

namespace NostrUnity.Examples
{
    /// <summary>
    /// Demonstrates the use of Nostr protocol components
    /// </summary>
    public class ProtocolExample : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private TMP_InputField _messageInput;
        [SerializeField] private Button _createEventButton;
        [SerializeField] private Button _validateEventButton;
        [SerializeField] private Button _serializeEventButton;
        [SerializeField] private Button _signEventButton;
        [SerializeField] private Button _verifySignatureButton;
        [SerializeField] private TextMeshProUGUI _outputText;
        
        private KeyPair _keyPair;
        private NostrEvent _currentEvent;
        
        private void Start()
        {
            // Generate a new keypair for testing
            _keyPair = new KeyPair();
            AppendOutput($"Generated a new key pair: {_keyPair.Npub}");
            
            // Set up button listeners
            _createEventButton.onClick.AddListener(CreateEvent);
            _validateEventButton.onClick.AddListener(ValidateEvent);
            _serializeEventButton.onClick.AddListener(SerializeEvent);
            _signEventButton.onClick.AddListener(SignEvent);
            _verifySignatureButton.onClick.AddListener(VerifySignature);
        }
        
        /// <summary>
        /// Creates a new Nostr event from the input field
        /// </summary>
        private void CreateEvent()
        {
            string content = _messageInput.text;
            if (string.IsNullOrEmpty(content))
            {
                AppendOutput("Error: Please enter a message");
                return;
            }
            
            try
            {
                // Create a new text note event
                _currentEvent = new NostrEvent(
                    publicKey: _keyPair.PublicKeyHex,
                    kind: 1, // Text note
                    content: content
                );
                
                AppendOutput($"Created new event with ID: {_currentEvent.Id}");
            }
            catch (Exception ex)
            {
                AppendOutput($"Error creating event: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Validates the current event
        /// </summary>
        private void ValidateEvent()
        {
            if (_currentEvent == null)
            {
                AppendOutput("Error: No event to validate. Create an event first.");
                return;
            }
            
            try
            {
                // Validate the event structure
                ValidationResult result = NostrValidator.ValidateEvent(_currentEvent);
                AppendOutput($"Validation result: {result}");
                
                // Validate with the NostrSerializer for ID computation
                string serialized = NostrSerializer.SerializeForId(_currentEvent);
                string computedId = NostrSerializer.ComputeId(serialized);
                bool idMatches = string.Equals(computedId, _currentEvent.Id, StringComparison.OrdinalIgnoreCase);
                
                AppendOutput($"ID verification: {(idMatches ? "Valid" : "Invalid")}");
                AppendOutput($"  Expected: {_currentEvent.Id}");
                AppendOutput($"  Computed: {computedId}");
            }
            catch (Exception ex)
            {
                AppendOutput($"Error validating event: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Serializes the current event
        /// </summary>
        private void SerializeEvent()
        {
            if (_currentEvent == null)
            {
                AppendOutput("Error: No event to serialize. Create an event first.");
                return;
            }
            
            try
            {
                // Serialize using the protocol serializer
                string serializedForId = NostrSerializer.SerializeForId(_currentEvent);
                AppendOutput($"Serialized for ID: {serializedForId}");
                
                string serializedComplete = NostrSerializer.SerializeComplete(_currentEvent);
                AppendOutput($"Serialized complete: {serializedComplete}");
                
                // Example relay message for publishing
                string publishMessage = RelayMessageHandler.CreateEventMessage(_currentEvent);
                AppendOutput($"Publish message: {publishMessage}");
            }
            catch (Exception ex)
            {
                AppendOutput($"Error serializing event: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Signs the current event
        /// </summary>
        private void SignEvent()
        {
            if (_currentEvent == null)
            {
                AppendOutput("Error: No event to sign. Create an event first.");
                return;
            }
            
            try
            {
                // Sign the event using the private key
                _currentEvent.Sign(_keyPair.PrivateKeyHex);
                AppendOutput($"Signed event with signature: {_currentEvent.Sig}");
            }
            catch (Exception ex)
            {
                AppendOutput($"Error signing event: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Verifies the signature of the current event
        /// </summary>
        private void VerifySignature()
        {
            if (_currentEvent == null)
            {
                AppendOutput("Error: No event to verify. Create an event first.");
                return;
            }
            
            if (string.IsNullOrEmpty(_currentEvent.Sig))
            {
                AppendOutput("Error: Event is not signed. Sign the event first.");
                return;
            }
            
            try
            {
                // Verify the signature using the protocol validator
                ValidationResult result = NostrValidator.VerifySignature(_currentEvent);
                AppendOutput($"Signature verification: {result}");
                
                // Perform a complete validation with both structure and signature
                ValidationResult completeResult = NostrValidator.ValidateEventComplete(_currentEvent);
                AppendOutput($"Complete validation: {completeResult}");
            }
            catch (Exception ex)
            {
                AppendOutput($"Error verifying signature: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Appends text to the output field
        /// </summary>
        private void AppendOutput(string text)
        {
            _outputText.text = $"{text}\n{_outputText.text}";
            Debug.Log(text);
        }
    }
} 