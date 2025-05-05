using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using Nostr.Unity;

/// <summary>
/// Editor UI for Nostr diagnostic tools
/// </summary>
[CustomEditor(typeof(DiagnosticTools))]
public class NostrDiagnosticToolsEditor : Editor
{
    private string _relayUrl = "wss://relay.damus.io";
    private string _testMessage = "Test message from Unity Nostr SDK";
    private string _resultText = "";
    private bool _showFullResponse = false;
    private Vector2 _scrollPosition;
    private bool _isRunningTest = false;
    private int _selectedTest = 0;
    private string[] _testOptions = new string[] { "Relay Connection", "Event Publication" };
    
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Nostr Relay Diagnostics", EditorStyles.boldLabel);
        
        DiagnosticTools diagnosticTools = (DiagnosticTools)target;
        
        EditorGUILayout.Space();
        
        // Test selection
        _selectedTest = EditorGUILayout.Popup("Test Type:", _selectedTest, _testOptions);
        
        // Relay URL field
        _relayUrl = EditorGUILayout.TextField("Relay URL:", _relayUrl);
        
        if (_selectedTest == 1) // Event Publication
        {
            _testMessage = EditorGUILayout.TextField("Test Message:", _testMessage);
        }
        
        EditorGUILayout.Space();
        
        // Run test button
        EditorGUI.BeginDisabledGroup(_isRunningTest);
        
        if (GUILayout.Button(_isRunningTest ? "Testing..." : $"Run {_testOptions[_selectedTest]} Test"))
        {
            _resultText = "Running test...";
            _isRunningTest = true;
            
            if (_selectedTest == 0) // Connection Test
            {
                EditorCoroutine.Start(RunConnectionTest(diagnosticTools));
            }
            else // Publication Test
            {
                EditorCoroutine.Start(RunPublicationTest(diagnosticTools));
            }
        }
        
        EditorGUI.EndDisabledGroup();
        
        // Test results
        if (!string.IsNullOrEmpty(_resultText))
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Test Results", EditorStyles.boldLabel);
            
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(300));
            EditorStyles.textArea.wordWrap = true;
            
            EditorGUILayout.TextArea(_resultText, EditorStyles.textArea, 
                GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
            
            EditorGUILayout.EndScrollView();
            
            _showFullResponse = EditorGUILayout.Foldout(_showFullResponse, "Show JSON Response");
        }
    }
    
    private IEnumerator RunConnectionTest(DiagnosticTools diagnosticTools)
    {
        yield return diagnosticTools.TestRelayConnection(_relayUrl, OnTestComplete);
    }
    
    private IEnumerator RunPublicationTest(DiagnosticTools diagnosticTools)
    {
        // Create a test event
        var keyManager = new NostrKeyManager();
        string privateKey = keyManager.GeneratePrivateKey();
        string publicKey = keyManager.GetPublicKey(privateKey, true);
        
        var testEvent = new NostrEvent(
            publicKey,
            1, // kind 1 = text note
            _testMessage,
            new string[0][]
        );
        
        testEvent.Sign(privateKey);
        
        yield return diagnosticTools.TestEventPublication(_relayUrl, testEvent, OnTestComplete);
    }
    
    private void OnTestComplete(DiagnosticResult result)
    {
        _isRunningTest = false;
        _resultText = result.GetDetailedReport();
        Repaint();
    }
    
    /// <summary>
    /// Simple coroutine starter for Editor code
    /// </summary>
    public class EditorCoroutine
    {
        public static EditorCoroutine Start(IEnumerator routine)
        {
            EditorCoroutine coroutine = new EditorCoroutine(routine);
            coroutine.Start();
            return coroutine;
        }
        
        private readonly IEnumerator _routine;
        
        EditorCoroutine(IEnumerator routine)
        {
            _routine = routine;
        }
        
        void Start()
        {
            EditorApplication.update += Update;
        }
        
        void Stop()
        {
            EditorApplication.update -= Update;
        }
        
        void Update()
        {
            if (!_routine.MoveNext())
            {
                Stop();
            }
        }
    }
} 