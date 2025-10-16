using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// UI Controller for trial regression analysis
/// Handles button clicks, panel visibility, and result display
/// Uses TrialRegressionAlgorithm for analysis logic
/// </summary>
public class TrialRegressionUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject regressionPanel;
    [SerializeField] private TMP_Text regressionResultsText;
    [SerializeField] private Button calculateRegressionButton;
    [SerializeField] private Button closeRegressionButton;
    [SerializeField] private Button saveResultsButton;
    
    [Header("Save Settings")]
    [SerializeField] private bool autoSaveResults = true;
    [SerializeField] private string saveFolder = "RegressionResults";
    
    private TrialRegressionAlgorithm.RegressionResult lastResult;
    
    void Start()
    {
        // Setup button listeners
        if (calculateRegressionButton != null)
            calculateRegressionButton.onClick.AddListener(CalculateRegression);
        
        if (closeRegressionButton != null)
            closeRegressionButton.onClick.AddListener(CloseRegressionPanel);
        
        if (saveResultsButton != null)
            saveResultsButton.onClick.AddListener(SaveRegressionResults);
        
        // Initially hide panel
        if (regressionPanel != null)
            regressionPanel.SetActive(false);
    }
    
    /// <summary>
    /// Calculate regression analysis and display results
    /// </summary>
    public void CalculateRegression()
    {
        Debug.Log("=== CALCULATE REGRESSION (UI) ===");
        
        // Load data using algorithm
        var trialData = TrialRegressionAlgorithm.LoadTrialDataFromCache();
        
        if (trialData == null || trialData.Count < 2)
        {
            ShowError("Failed to load trial data! Need at least 2 trials.");
            return;
        }
        
        // Perform analysis using algorithm
        lastResult = TrialRegressionAlgorithm.PerformRegressionAnalysis(trialData);
        
        // Display results in UI
        ShowRegressionResults(lastResult.summaryText);
        
        // Auto-save if enabled
        if (autoSaveResults)
            SaveRegressionResults();
    }
    
    /// <summary>
    /// Alternative method name for compatibility
    /// </summary>
    public void CalculateAndShowRegression() => CalculateRegression();
    
    /// <summary>
    /// Display regression results in UI panel
    /// </summary>
    private void ShowRegressionResults(string results)
    {
        if (regressionPanel != null)
        {
            regressionPanel.SetActive(true);
            // Ensure regression panel is on top
            regressionPanel.transform.SetAsLastSibling();
        }
        
        if (regressionResultsText != null)
            regressionResultsText.text = results;
        
        Debug.Log("Results displayed in UI");
    }
    
    /// <summary>
    /// Display error message in UI panel
    /// </summary>
    private void ShowError(string errorMessage)
    {
        if (regressionPanel != null)
            regressionPanel.SetActive(true);
        
        if (regressionResultsText != null)
            regressionResultsText.text = $"ERROR:\n{errorMessage}";
        
        Debug.LogError(errorMessage);
    }
    
    /// <summary>
    /// Close regression panel and return to trial control panel
    /// </summary>
    public void CloseRegressionPanel()
    {
        if (regressionPanel != null)
            regressionPanel.SetActive(false);
        
        // Reopen trial control panel
        var trialUIController = FindObjectOfType<TrialUIController>();
        if (trialUIController != null)
            trialUIController.OpenTrialControlPanel();
        
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        Debug.Log("Regression panel closed");
    }
    
    /// <summary>
    /// Force close regression panel without opening trial panel
    /// Used when starting new game/trial
    /// </summary>
    public void ForceCloseRegressionPanel()
    {
        if (regressionPanel != null && regressionPanel.activeSelf)
        {
            regressionPanel.SetActive(false);
            Debug.Log("Regression panel force closed");
        }
    }
    
    /// <summary>
    /// Check if regression can be calculated (enough trial data available)
    /// </summary>
    public bool CanCalculateRegression()
    {
        var trialData = TrialRegressionAlgorithm.LoadTrialDataFromCache();
        return trialData != null && trialData.Count >= 2;
    }
    
    /// <summary>
    /// Save regression results to file
    /// </summary>
    public void SaveRegressionResults()
    {
        if (lastResult == null)
        {
            Debug.LogWarning("No results to save!");
            return;
        }
        
        bool success = TrialRegressionAlgorithm.SaveRegressionResultsToFile(lastResult, saveFolder);
        
        if (success && regressionResultsText != null)
        {
            string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            regressionResultsText.text += $"\n\nSaved: RegressionAnalysis_{timestamp}.txt";
        }
    }
}

/* ============================================================================
 * COMMENTED OUT: BACK BUTTON FUNCTIONALITY
 * ============================================================================
 * This section contains the Back Button system that allows returning to the
 * main panel after regression analysis is complete.
 * 
 * To enable this feature:
 * 1. Uncomment the backButton field declaration below
 * 2. Uncomment the button listener setup in Start()
 * 3. Uncomment the BackToTrialPanel() method below
 * 4. Assign the Back Button reference in Unity Inspector
 * 
 * Features:
 * - Initially hidden, appears only after analysis completes
 * - Returns to main panel for new game/trial
 * - Properly resets game state (Time.timeScale, PlayerIntro, etc.)
 * - Closes both regression and trial panels
 * ============================================================================
 *
 * // 1. Add this field at the top of the class (after saveResultsButton):
 * // [SerializeField] private Button backButton; // Back to trial panel after analysis
 *
 * // 2. Add this to Start() method (after saveResultsButton listener):
 * // if (backButton != null)
 * // {
 * //     backButton.onClick.AddListener(BackToTrialPanel);
 * //     backButton.gameObject.SetActive(false); // Initially hidden
 * // }
 *
 * // 3. Add this to CalculateRegression() method (at beginning and end):
 * // if (backButton != null) backButton.gameObject.SetActive(false); // Hide during analysis
 * // ... analysis code ...
 * // if (backButton != null) backButton.gameObject.SetActive(true); // Show after analysis
 *
 * // 4. Add this method to the class:
 * 
 * /// <summary>
 * /// Back button - Return to trial panel and reset for new game
 * /// </summary>
 * public void BackToTrialPanel()
 * {
 *     Debug.Log("=== BACK TO TRIAL PANEL ===");
 *     
 *     // Close regression panel
 *     if (regressionPanel != null)
 *     {
 *         regressionPanel.SetActive(false);
 *         Debug.Log("Regression panel closed");
 *     }
 *     
 *     // Hide back button
 *     if (backButton != null)
 *     {
 *         backButton.gameObject.SetActive(false);
 *         Debug.Log("Back button hidden");
 *     }
 *     
 *     // Close trial control panel explicitly
 *     var trialUIController = FindObjectOfType<TrialUIController>();
 *     if (trialUIController != null)
 *     {
 *         trialUIController.CloseTrialControlPanel();
 *         Debug.Log("Trial control panel closed (SetActive = false)");
 *     }
 *     
 *     // Reopen main panel for new game
 *     var panelOpenUp = FindObjectOfType<PanelOpenUp>();
 *     if (panelOpenUp != null && panelOpenUp.Panel != null)
 *     {
 *         panelOpenUp.Panel.SetActive(true);
 *         Debug.Log("Main panel reopened for new game");
 *     }
 *     
 *     // Allow time to flow
 *     Time.timeScale = 1f;
 *     Cursor.lockState = CursorLockMode.None;
 *     Cursor.visible = true;
 *     
 *     // Reset player intro
 *     var playerIntro = FindObjectOfType<PlayerIntro>();
 *     if (playerIntro != null)
 *     {
 *         playerIntro.ResetIntro();
 *         Debug.Log("Player intro reset");
 *     }
 *     
 *     Debug.Log("Back completed - ready for new game");
 * }
 *
 * // 5. Update CloseRegressionPanel() to hide back button:
 * // Add this at the beginning of CloseRegressionPanel():
 * // if (backButton != null) backButton.gameObject.SetActive(false);
 *
 * // 6. Update ForceCloseRegressionPanel() to hide back button:
 * // Add this inside ForceCloseRegressionPanel():
 * // if (backButton != null) backButton.gameObject.SetActive(false);
 *
 * ============================================================================
 */

/* ============================================================================
 * COMMENTED OUT: ACTIVE LEARNING & OPTIMAL PARAMETERS SYSTEM
 * ============================================================================
 * This section contains the Active Learning system that uses the 5 trial runs
 * as training data to generate optimal parameters for the main game.
 * 
 * To enable this feature, implement the methods in TrialRegressionAlgorithm.cs
 * and add UI buttons/fields here to call them.
 * 
 * Based on: Personalized Difficulty Adaptation via Active Learning
 * ============================================================================
 */


