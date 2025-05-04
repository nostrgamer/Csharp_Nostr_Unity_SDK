using UnityEngine;
using UnityEngine.UI;
using Nostr.Unity;

namespace Nostr.Unity.Examples
{
    /// <summary>
    /// Debug component to display Nostr keys and allow inputting custom keys
    /// Attach to a GameObject with Text components to display the keys
    /// </summary>
    public class NostrKeyDebugger : MonoBehaviour
    {
        [SerializeField]
        private NostrManager nostrManager;

        [SerializeField]
        private Text publicKeyText;

        [SerializeField]
        private Text privateKeyText;

        [SerializeField]
        private InputField nsecInputField;

        [SerializeField]
        private Button importButton;

        [SerializeField]
        private Toggle showPrivateKeyToggle;

        private void Start()
        {
            // Find NostrManager if not assigned
            if (nostrManager == null)
            {
                nostrManager = FindAnyObjectByType<NostrManager>();
                if (nostrManager == null)
                {
                    Debug.LogError("NostrManager not found in scene. Please add it to a GameObject first.");
                    return;
                }
            }

            // Set up UI elements
            if (showPrivateKeyToggle != null)
            {
                showPrivateKeyToggle.onValueChanged.AddListener(OnTogglePrivateKey);
            }

            if (importButton != null)
            {
                importButton.onClick.AddListener(ImportKey);
            }

            // Initial update
            UpdateKeyDisplay();
        }

        private void Update()
        {
            // Continuously update the key display in case keys change
            UpdateKeyDisplay();
        }

        /// <summary>
        /// Updates the key display in the UI
        /// </summary>
        private void UpdateKeyDisplay()
        {
            if (publicKeyText != null)
            {
                publicKeyText.text = "Public Key (npub):\n" + (nostrManager.PublicKeyBech32 ?? "None");
            }

            if (privateKeyText != null)
            {
                bool showPrivate = showPrivateKeyToggle != null && showPrivateKeyToggle.isOn;
                
                if (showPrivate && !string.IsNullOrEmpty(nostrManager.PrivateKeyBech32))
                {
                    privateKeyText.text = "Private Key (nsec):\n" + nostrManager.PrivateKeyBech32;
                }
                else
                {
                    privateKeyText.text = "Private Key: [Hidden]";
                }
            }
        }

        /// <summary>
        /// Imports a key from the input field
        /// </summary>
        public void ImportKey()
        {
            if (nsecInputField == null || string.IsNullOrEmpty(nsecInputField.text))
            {
                Debug.LogWarning("No nsec entered");
                return;
            }

            string nsec = nsecInputField.text.Trim();
            if (!nsec.StartsWith("nsec"))
            {
                Debug.LogError("Invalid nsec format. Must start with 'nsec'");
                return;
            }

            // Set the private key
            nostrManager.SetPrivateKey(nsec);
            Debug.Log("Imported key: " + nostrManager.PublicKeyBech32);

            // Clear the input field for security
            nsecInputField.text = "";
        }

        /// <summary>
        /// Toggles the display of the private key
        /// </summary>
        private void OnTogglePrivateKey(bool show)
        {
            UpdateKeyDisplay();
        }
    }
} 