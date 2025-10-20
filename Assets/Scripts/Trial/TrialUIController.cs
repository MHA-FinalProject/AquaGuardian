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
    
    [Header("UI Status Text - 3 Separate Objects")]
    [SerializeField] private GameObject startStatusText;      // Status text for start
    [SerializeField] private GameObject duringTrialStatusText; // Status text during trials
    [SerializeField] private GameObject endStatusText;         // Status text when complete
    
    [Header("UI Texts")]
    [SerializeField] private TMP_Text instructionsText;  // Text for instructions only
    [SerializeField] private TMP_Text trialResultsText;  // Text for results only
    
    [Header("UI Buttons")]
    [SerializeField] private Button startTrialButton;
    [SerializeField] private Button continueTrialButton;
    
    [SerializeField] private Button analyzeTrialsButton;
    [SerializeField] private Button closeTrialButton;
    
    [Header("References")]
    [SerializeField] private TrialSystemManager trialSystemManager;
    [SerializeField] private TrialRegressionUI regressionUI;
    
    void Start()
    {
        Debug.Log("=== TrialUIController Start ===");
        
        // Check references
        if (regressionUI == null)
            Debug.LogError("regressionUI is NULL! Please assign TrialRegressionUI in Inspector!");
        else
            Debug.Log("regressionUI assigned correctly");
            
        if (trialSystemManager == null)
            Debug.LogWarning("trialSystemManager is NULL!");
        else
            Debug.Log("trialSystemManager assigned correctly");
        
        InitializeTrialUI();
        SetupTrialButtons();
        
        if (trialControlPanel != null)
        {
            trialControlPanel.SetActive(false);
        }
    }
    
   
    private void InitializeTrialUI()
    {
        // Show start status text, hide others
        if (startStatusText != null) startStatusText.SetActive(true);
        if (duringTrialStatusText != null) duringTrialStatusText.SetActive(false);
        if (endStatusText != null) endStatusText.SetActive(false);
        
        if (instructionsText != null) instructionsText.text = "Press start to begin";
        if (trialResultsText != null) trialResultsText.text = ""; // Clean results, no mutual overwriting
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
            Debug.Log("Analyze button listener added successfully");
        }
        else
        {
            Debug.LogError("analyzeTrialsButton is NULL! Not assigned in Inspector!");
        }
        
       // Close button
        if (closeTrialButton != null)
        {
            closeTrialButton.onClick.RemoveAllListeners();
            closeTrialButton.onClick.AddListener(() => {
                Debug.Log("Close button clicked!");
                CloseTrialControlPanel();
            });
        }
        
        UpdateTrialButtonsState(false, 0, 5);
    }
    
    
    public void OpenTrialControlPanel()
    {
        if (trialControlPanel != null)
        {
            trialControlPanel.SetActive(true);
            
            var cg = trialControlPanel.GetComponent<CanvasGroup>();
            if (cg) 
            { 
                cg.interactable = true; 
                cg.blocksRaycasts = true; 
            }
            
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
    

    public void CloseTrialControlPanel(bool showMain = true)
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
        
        if (showMain)
        {
            // Show main panel when closing trial panel
            if (mainPanel != null)
            {
                mainPanel.SetActive(true);
                // Ensure main panel is on top
                mainPanel.transform.SetAsLastSibling();
            }
            
            // Inform GameStateManager the settings/main panel is opened again
            GameStateManager.Instance?.NotifyPanelOpened();
            
            // Resume game time only when returning to main menu
            Time.timeScale = 1f;
        }
        
        // Cursor always available
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    
  
    public void UpdateTrialControlText(bool trialsMode, int currentTrial, int totalTrials, bool trialFailed = false)
    {
        // Switch between 3 status text objects based on state
        if (!trialsMode || currentTrial == 0)
        {
            // Show start status text
            if (startStatusText != null) startStatusText.SetActive(true);
            if (duringTrialStatusText != null) duringTrialStatusText.SetActive(false);
            if (endStatusText != null) endStatusText.SetActive(false);
        }
        else if (currentTrial >= totalTrials)
        {
            // Show end status text
            if (startStatusText != null) startStatusText.SetActive(false);
            if (duringTrialStatusText != null) duringTrialStatusText.SetActive(false);
            if (endStatusText != null) endStatusText.SetActive(true);
        }
        else
        {
            // Show during trial status text
            if (startStatusText != null) startStatusText.SetActive(false);
            if (duringTrialStatusText != null) duringTrialStatusText.SetActive(true);
            if (endStatusText != null) endStatusText.SetActive(false);
        }
        
        // Instructions now go ONLY to instructionsText
        if (instructionsText != null)
        {
            if (!trialsMode || currentTrial == 0)
            {
                instructionsText.text = "Press start to begin";
            }
            else if (currentTrial < totalTrials)
            {
                instructionsText.text = trialFailed 
                    ? "Restart to retry or Continue to skip"
                    : "Press Continue to proceed";
            }
            else
            {
                instructionsText.text = "Click Analyze to see results";
            }
        }
        
        UpdateTrialButtonsState(trialsMode, currentTrial, totalTrials, trialFailed);
    }
    
 
    public void UpdateTrialResults(float finalOxygen, bool completed, int currentTrial, int totalTrials)
    {
        Debug.Log($"UpdateTrialResults: Trial {currentTrial}/{totalTrials}, O2={finalOxygen:F1}%, Completed={completed}");
        
        // Switch status text objects
        if (currentTrial >= totalTrials)
        {
            // Show end status text
            if (startStatusText != null) startStatusText.SetActive(false);
            if (duringTrialStatusText != null) duringTrialStatusText.SetActive(false);
            if (endStatusText != null) endStatusText.SetActive(true);
        }
        else
        {
            // Show during trial status text
            if (startStatusText != null) startStatusText.SetActive(false);
            if (duringTrialStatusText != null) duringTrialStatusText.SetActive(true);
            if (endStatusText != null) endStatusText.SetActive(false);
        }
        
        // Results - don't touch instructions!
        if (trialResultsText != null)
        {
            string resultText;
            if (!completed)
            {
                resultText = $"Trial {currentTrial}/{totalTrials} - Try again\nOxygen: {finalOxygen:F1}%";
            }
            else
            {
                resultText = $"Trial {currentTrial}/{totalTrials} completed\nOxygen remaining: {finalOxygen:F1}%";
                if (currentTrial >= totalTrials) resultText += "\nAll trials completed!";
            }
            
            trialResultsText.text = resultText;
        }
        
        UpdateTrialButtonsState(true, currentTrial, totalTrials, !completed);
    }
    
  
    public void UpdateTrialUI(int currentTrial, int totalTrials)
    {
        // Show during trial status text
        if (startStatusText != null) startStatusText.SetActive(false);
        if (duringTrialStatusText != null) duringTrialStatusText.SetActive(true);
        if (endStatusText != null) endStatusText.SetActive(false);
        
        UpdateTrialButtonsState(true, currentTrial, totalTrials);
    }
    
   
    private void UpdateTrialButtonsState(bool trialsMode, int currentTrial, int totalTrials, bool trialFailed = false)
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
            
            // Update continue button text based on trial status
            if (showContinue)
            {
                var label = continueTrialButton.GetComponentInChildren<TMP_Text>();
                if (label != null)
                    label.text = trialFailed ? "Skip to Next" : "Continue";
            }
        }
        
      
        if (analyzeTrialsButton != null)
        {
            bool showAnalyze = trialsMode && currentTrial >= totalTrials;
            analyzeTrialsButton.gameObject.SetActive(showAnalyze);
        }
    }
    
  
    private void SetButtonVisible(Button button, bool visible)
    {
        if (button == null) return;
        
        var cg = button.GetComponent<CanvasGroup>();
        if (cg == null) cg = button.gameObject.AddComponent<CanvasGroup>();
        
        cg.alpha = visible ? 1f : 0f;
        cg.blocksRaycasts = visible;
        cg.interactable = visible;
    }
    
   
    public void AnalyzeTrialResults()
    {
        Debug.Log("=== ANALYZE TRIAL RESULTS BUTTON CLICKED ===");
        
        // Close trial panel without showing main (regression panel will appear)
        CloseTrialControlPanel(false);
        
        // Keep game paused for analysis
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        if (regressionUI != null)
        {
            Debug.Log("Calling regressionUI.CalculateAndShowRegression()...");
            regressionUI.CalculateAndShowRegression();
        }
        else
        {
            Debug.LogError("ERROR: regressionUI is NULL! Not assigned in Inspector!");
            Debug.LogError("Please assign TrialRegressionUI in the Inspector for TrialUIController");
        }
    }
    
   
    public void UpdateCompletionUI()
    {
        if (mainPanel != null)
            mainPanel.SetActive(true);
        
        // Show end status text
        if (startStatusText != null) startStatusText.SetActive(false);
        if (duringTrialStatusText != null) duringTrialStatusText.SetActive(false);
        if (endStatusText != null) endStatusText.SetActive(true);
        
        int totalTrials = trialSystemManager?.TotalTrials ?? 5;
        
        if (instructionsText != null) instructionsText.text = "Click Analyze to see results";
        if (trialResultsText != null) trialResultsText.text = $"All {totalTrials} trials completed!";
        
        UpdateTrialButtonsState(true, totalTrials, totalTrials);
    }
}

