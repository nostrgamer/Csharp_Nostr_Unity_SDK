using UnityEngine;
using UnityEditor;
using Nostr.Unity;

[CustomEditor(typeof(NostrManager))]
public class NostrManagerEditor : Editor
{
    private string _testMessage = "Hello from Unity Nostr SDK!";
    private string _customNsec = "";
    private bool _showPrivateKey = false;

    public override void OnInspectorGUI()
    {
        // Draw the default inspector
        DrawDefaultInspector();

        // Get the NostrManager instance
        NostrManager manager = (NostrManager)target;

        // Add the key management section
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Key Management", EditorStyles.boldLabel);

        // Display current keys
        if (Application.isPlaying)
        {
            // Toggle to show/hide private key
            _showPrivateKey = EditorGUILayout.Toggle("Show Private Key", _showPrivateKey);

            // Show key info
            EditorGUILayout.LabelField("Current Keys:");
            EditorGUI.indentLevel++;
            
            if (!string.IsNullOrEmpty(manager.PublicKeyBech32))
            {
                EditorGUILayout.TextField("Public Key (npub)", manager.PublicKeyBech32);
                
                if (_showPrivateKey && !string.IsNullOrEmpty(manager.PrivateKeyBech32))
                {
                    // Show warning about displaying private key
                    EditorGUILayout.HelpBox("CAUTION: Never share your private key with anyone!", MessageType.Warning);
                    EditorGUILayout.TextField("Private Key (nsec)", manager.PrivateKeyBech32);
                }
            }
            else
            {
                EditorGUILayout.LabelField("No keys loaded.");
            }
            
            EditorGUI.indentLevel--;
            
            // Custom nsec input field
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Import Existing Key:");
            EditorGUI.indentLevel++;
            
            _customNsec = EditorGUILayout.TextField("Enter nsec", _customNsec);
            
            if (GUILayout.Button("Import Key"))
            {
                if (!string.IsNullOrEmpty(_customNsec) && _customNsec.StartsWith("nsec"))
                {
                    manager.SetPrivateKey(_customNsec);
                    _customNsec = "";
                    EditorUtility.DisplayDialog("Success", "Key imported successfully!", "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog("Error", "Please enter a valid nsec key", "OK");
                }
            }
            
            EditorGUI.indentLevel--;
        }
        else
        {
            EditorGUILayout.HelpBox("Enter Play Mode to manage keys", MessageType.Info);
        }

        // Test message section
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Test Tools", EditorStyles.boldLabel);

        // Test message input field
        _testMessage = EditorGUILayout.TextField("Test Message", _testMessage);

        // Send test message button
        if (GUILayout.Button("Send Test Message"))
        {
            if (Application.isPlaying)
            {
                bool sent = manager.SendTestMessage(_testMessage);
                if (sent)
                {
                    Debug.Log("Test message sent! Check your Nostr client to confirm.");
                }
                else
                {
                    Debug.LogError("Failed to send test message. Check console for errors.");
                }
            }
            else
            {
                Debug.LogWarning("You need to be in Play Mode to send test messages.");
            }
        }

        // Connection status
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Connection Status", EditorStyles.boldLabel);
        if (Application.isPlaying)
        {
            bool isConnected = manager.Client != null && manager.Client.IsConnected;
            EditorGUILayout.LabelField("Connected to Relays:", isConnected ? "Yes" : "No");
            
            if (isConnected && manager.Client.ConnectedRelays.Count > 0)
            {
                EditorGUILayout.LabelField("Connected Relays:");
                EditorGUI.indentLevel++;
                foreach (var relay in manager.Client.ConnectedRelays)
                {
                    EditorGUILayout.LabelField(relay);
                }
                EditorGUI.indentLevel--;
            }
        }
        else
        {
            EditorGUILayout.LabelField("Start Play Mode to see connection status");
        }

        // Force refresh the inspector
        if (Application.isPlaying)
        {
            Repaint();
        }
    }
} 