using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System;
using NostrUnity.Utils;

namespace NostrUnity.UI
{
    /// <summary>
    /// Displays Nostr errors in the UI
    /// </summary>
    public class NostrErrorDisplay : MonoBehaviour
    {
        [SerializeField] private GameObject errorPanel;
        
        [SerializeField] private Text errorText;
        
        [SerializeField] private float displayDuration = 5f;
        [SerializeField] private bool showInfoMessages = false;
        
        private Queue<string> errorQueue = new Queue<string>();
        private float currentDisplayTime;
        private bool isDisplaying;
        
        private void Awake()
        {
            // Validate components
            if (errorPanel == null)
            {
                Debug.LogWarning("NostrErrorDisplay: Error panel is not assigned!");
            }
            
            if (errorText == null)
            {
                Debug.LogError("NostrErrorDisplay: No text component assigned! Assign a UI Text component.");
            }
        }
        
        private void OnEnable()
        {
            NostrErrorHandler.OnError += HandleError;
            if (errorPanel != null) errorPanel.SetActive(false);
        }
        
        private void OnDisable()
        {
            NostrErrorHandler.OnError -= HandleError;
        }
        
        private void HandleError(string message, NostrErrorHandler.NostrErrorSeverity severity)
        {
            if (!enabled) return;
            
            // Optionally filter out info messages
            if (severity == NostrErrorHandler.NostrErrorSeverity.Info && !showInfoMessages)
                return;
                
            // Add error to queue
            errorQueue.Enqueue($"[{severity}] {message}");
            
            // If not currently displaying, start displaying
            if (!isDisplaying)
            {
                DisplayNextError();
            }
        }
        
        private void Update()
        {
            if (!enabled || !isDisplaying) return;
            
            currentDisplayTime -= Time.deltaTime;
            
            if (currentDisplayTime <= 0)
            {
                if (errorQueue.Count > 0)
                {
                    DisplayNextError();
                }
                else
                {
                    isDisplaying = false;
                    if (errorPanel != null) errorPanel.SetActive(false);
                }
            }
        }
        
        private void DisplayNextError()
        {
            if (errorQueue.Count == 0) return;
            
            string error = errorQueue.Dequeue();
            
            // Update text component
            if (errorText != null)
            {
                errorText.text = error;
            }
            
            if (errorPanel != null) errorPanel.SetActive(true);
            
            currentDisplayTime = displayDuration;
            isDisplaying = true;
        }
        
        /// <summary>
        /// Clear all pending error messages
        /// </summary>
        public void ClearErrors()
        {
            errorQueue.Clear();
            isDisplaying = false;
            if (errorPanel != null) errorPanel.SetActive(false);
        }
    }
} 