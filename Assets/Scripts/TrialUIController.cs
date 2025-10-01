using UnityEngine;
using UnityEngine.UI;
using TMPro;

/**
 * TrialUIController - Manages all trial-related UI
 * Handles panel display, button states, and text updates

 */
public class TrialUIController : MonoBehaviour
{
    [Header("UI Panels")]
    [SerializeField] private GameObject trialButton;
    [SerializeField] private GameObject trialControlPanel;
    [SerializeField] private GameObject mainPanel;
    
    [Header("UI Text")]
    [SerializeField] private TMP_Text trialStatusText;
    [SerializeField] private TMP_Text trialResultsText;
    
    [Header("UI Buttons")]
    [SerializeField] private Button startTrialButton;
    [SerializeField] private Button continueTrialButton;
    [SerializeField] private Button analyzeTrialsButton;
    [SerializeField] private Button closeTrialButton;
    
    [Header("References")]
    [SerializeField] private TrialSystemManager trialSystemManager;
    [SerializeField] private TrialRegressionAnalyzer regressionAnalyzer;
    
    void Start()
    {
        InitializeTrialUI();
        SetupTrialButtons();
        
        if (trialControlPanel != null)
        {
            trialControlPanel.SetActive(false);
        }
    }
    
    /// <summary>
    /// Initialize trial UI elements
    /// </summary>
    private void InitializeTrialUI()
    {
        if (trialStatusText != null)
        {
            trialStatusText.text = "Ready to start trials";
        }
        
        if (trialResultsText != null)
        {
            trialResultsText.text = "Click Start to begin 5 trials\nReach the fish in each trial\nSystem will record oxygen levels";
        }
    }
    
    /// <summary>
    /// Setup all trial button listeners
    /// </summary>
    private void SetupTrialButtons()
    {
        // Main Trial button
        if (trialButton != null)
        {
            var button = trialButton.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(OpenTrialControlPanel);
            }
        }
        
        // Start button
        if (startTrialButton != null)
        {
            startTrialButton.onClick.RemoveAllListeners();
            startTrialButton.onClick.AddListener(() => {
                if (trialSystemManager != null)
                    trialSystemManager.StartTrials();
            });
        }
        
        // Continue button
        if (continueTrialButton != null)
        {
            continueTrialButton.onClick.RemoveAllListeners();
            continueTrialButton.onClick.AddListener(() => {
                if (trialSystemManager != null)
                    trialSystemManager.ContinueToNextTrial();
            });
        }
        
        // Analyze button
        if (analyzeTrialsButton != null)
        {
            analyzeTrialsButton.onClick.RemoveAllListeners();
            analyzeTrialsButton.onClick.AddListener(AnalyzeTrialResults);
        }
        
        // Close button
        if (closeTrialButton != null)
        {
            closeTrialButton.onClick.RemoveAllListeners();
            closeTrialButton.onClick.AddListener(() => {
                if (trialSystemManager != null && trialSystemManager.CurrentTrialNumber >= trialSystemManager.TotalTrials)
                {
                    trialSystemManager.CompleteAllTrials();
                }
                else
                {
                    CloseTrialControlPanel();
                }
            });
        }
        
        UpdateTrialButtonsState(false, 0, 5);
    }
    
    /// <summary>
    /// Open trial control panel
    /// </summary>
    public void OpenTrialControlPanel()
    {
        Debug.Log("Opening trial control panel");
        
        if (trialControlPanel != null)
        {
            trialControlPanel.SetActive(true);
            
            var cg = trialControlPanel.GetComponent<CanvasGroup>();
            if (cg) 
            { 
                cg.interactable = true; 
                cg.blocksRaycasts = true; 
            }
            
            // Freeze time while panel is open
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            
            trialControlPanel.transform.SetAsLastSibling();
        }
        else
        {
            Debug.LogError("Trial control panel NOT ASSIGNED in Inspector!");
        }
        
        UpdateTrialControlText(trialSystemManager?.TrialsMode ?? false, 
                              trialSystemManager?.CurrentTrialNumber ?? 0, 
                              trialSystemManager?.TotalTrials ?? 5);
    }
    
    /// <summary>
    /// Close trial control panel
    /// </summary>
    public void CloseTrialControlPanel()
    {
        if (trialControlPanel != null)
        {
            trialControlPanel.SetActive(false);
            
            var cg = trialControlPanel.GetComponent<CanvasGroup>();
            if (cg) 
            { 
                cg.interactable = false; 
                cg.blocksRaycasts = false; 
            }
        }
        
        // Restore time
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    
    /// <summary>
    /// Update trial control text
    /// </summary>
    public void UpdateTrialControlText(bool trialsMode, int currentTrial, int totalTrials)
    {
        if (trialStatusText != null)
        {
            string statusText = "";
            
            if (!trialsMode)
            {
                statusText = "Ready to start trials";
            }
            else if (currentTrial < totalTrials)
            {
                statusText = $"Trial {currentTrial}/{totalTrials} completed";
            }
            else
            {
                statusText = $"All {totalTrials} trials completed";
            }
            
            trialStatusText.text = statusText;
        }
        
        if (trialResultsText != null)
        {
            string instructionsText = "";
            
            if (!trialsMode)
            {
                instructionsText = "Click Start to begin 5 trials\nReach the fish in each trial\nSystem will record oxygen levels";
            }
            else if (currentTrial < totalTrials)
            {
                instructionsText = $"{totalTrials - currentTrial} remaining";
            }
            else
            {
                instructionsText = "All trials completed!\nClick Analyze for regression\nor Close to finish";
            }
            
            trialResultsText.text = instructionsText;
        }
        
        UpdateTrialButtonsState(trialsMode, currentTrial, totalTrials);
    }
    
    /// <summary>
    /// Update trial results after trial end
    /// </summary>
    public void UpdateTrialResults(float finalOxygen, bool completed, int currentTrial, int totalTrials)
    {
        if (trialStatusText != null)
        {
            trialStatusText.text = $"{currentTrial}/{totalTrials}";
        }
        
        if (trialResultsText != null)
        {
            if (currentTrial < totalTrials)
            {
                trialResultsText.text = $"Remaining: {totalTrials - currentTrial}";
            }
            else
            {
                trialResultsText.text = "Completed";
            }
        }
        
        UpdateTrialButtonsState(true, currentTrial, totalTrials);
    }
    
    /// <summary>
    /// Update trial UI during trial
    /// </summary>
    public void UpdateTrialUI(int currentTrial, int totalTrials)
    {
        if (trialStatusText != null)
        {
            trialStatusText.text = $"TRIAL {currentTrial}/{totalTrials} - Reach the fish!";
        }
        
        UpdateTrialButtonsState(true, currentTrial, totalTrials);
    }
    
    /// <summary>
    /// Update button states based on trial progress
    /// </summary>
    private void UpdateTrialButtonsState(bool trialsMode, int currentTrial, int totalTrials)
    {
        if (startTrialButton != null)
        {
            bool showStart = !trialsMode || currentTrial == 0;
            SetButtonVisible(startTrialButton, showStart);
            
            if (showStart)
            {
                var label = startTrialButton.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = "Start";
            }
        }
        
        if (continueTrialButton != null)
        {
            bool showContinue = trialsMode && currentTrial > 0 && currentTrial < totalTrials;
            continueTrialButton.gameObject.SetActive(showContinue);
        }
        
        if (analyzeTrialsButton != null)
        {
            bool showAnalyze = trialsMode && currentTrial >= totalTrials;
            analyzeTrialsButton.gameObject.SetActive(showAnalyze);
        }
    }
    
    /// <summary>
    /// Set button visibility safely
    /// </summary>
    private void SetButtonVisible(Button button, bool visible)
    {
        if (button == null) return;
        
        var cg = button.GetComponent<CanvasGroup>();
        if (cg == null) cg = button.gameObject.AddComponent<CanvasGroup>();
        
        cg.alpha = visible ? 1f : 0f;
        cg.blocksRaycasts = visible;
        cg.interactable = visible;
    }
    
    /// <summary>
    /// Analyze trial results
    /// </summary>
    public void AnalyzeTrialResults()
    {
        Debug.Log("Analyzing trial results...");
        
        CloseTrialControlPanel();
        
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        if (regressionAnalyzer != null)
        {
            Debug.Log("Calling regression analyzer");
            regressionAnalyzer.CalculateAndShowRegression();
        }
        else
        {
            Debug.LogError("Regression analyzer not assigned in Inspector");
        }
    }
    
    /// <summary>
    /// Update completion UI
    /// </summary>
    public void UpdateCompletionUI()
    {
        Debug.Log("Updating completion UI...");
        
        if (mainPanel != null)
        {
            mainPanel.SetActive(true);
            Debug.Log("Main panel re-enabled");
        }
        
        if (trialStatusText != null)
        {
            int totalTrials = trialSystemManager?.TotalTrials ?? 5;
            trialStatusText.text = $"All {totalTrials} trials completed!";
        }
        
        if (trialResultsText != null)
        {
            int totalTrials = trialSystemManager?.TotalTrials ?? 5;
            trialResultsText.text = $"All {totalTrials} Trials Complete!\nOxygen data recorded for each trial\nReady for regression analysis";
        }
        
        UpdateTrialButtonsState(false, 0, trialSystemManager?.TotalTrials ?? 5);
        
        Debug.Log("Completion UI updated");
    }
}

