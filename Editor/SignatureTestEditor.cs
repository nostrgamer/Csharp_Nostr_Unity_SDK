using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor script for running signature tests
/// </summary>
[CustomEditor(typeof(SignatureTestComponent))]
public class SignatureTestEditor : Editor
{
    private SerializedProperty runTestsOnStartProp;
    private SerializedProperty sendEventToRelayProp;
    private SerializedProperty relayUrlProp;
    private SerializedProperty testPrivateKeyProp;
    private SerializedProperty testResultsProp;
    
    void OnEnable()
    {
        runTestsOnStartProp = serializedObject.FindProperty("runTestsOnStart");
        sendEventToRelayProp = serializedObject.FindProperty("sendEventToRelay");
        relayUrlProp = serializedObject.FindProperty("relayUrl");
        testPrivateKeyProp = serializedObject.FindProperty("testPrivateKey");
        testResultsProp = serializedObject.FindProperty("testResults");
    }
    
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        EditorGUILayout.LabelField("Nostr Signature Test Tool", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        
        EditorGUILayout.PropertyField(runTestsOnStartProp);
        EditorGUILayout.PropertyField(sendEventToRelayProp);
        
        if (sendEventToRelayProp.boolValue)
        {
            EditorGUILayout.PropertyField(relayUrlProp);
        }
        
        EditorGUILayout.Space();
        
        EditorGUILayout.LabelField("Test Private Key (leave empty to generate)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(testPrivateKeyProp);
        
        EditorGUILayout.Space();
        
        if (GUILayout.Button("Run Signature Tests"))
        {
            SignatureTestComponent testComponent = (SignatureTestComponent)target;
            testComponent.RunTests();
        }
        
        EditorGUILayout.Space();
        
        EditorGUILayout.LabelField("Test Results", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(testResultsProp, GUIContent.none, GUILayout.Height(200));
        
        if (!string.IsNullOrEmpty(testResultsProp.stringValue))
        {
            if (GUILayout.Button("Copy Results to Clipboard"))
            {
                GUIUtility.systemCopyBuffer = testResultsProp.stringValue;
                Debug.Log("Test results copied to clipboard");
            }
        }
        
        serializedObject.ApplyModifiedProperties();
    }
} 