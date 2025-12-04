using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UI.TableUI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;
using System.IO;

/**
   * TrialRegressionUI
   * This class manages the user interface for performing regression analysis
   * on trial data. It provides methods to open/close the regression panel,
   * calculate regression, display results, and save them to a file.
   * Uses TrialRegressionAlgorithm for the core regression logic.
   */

public class TrialRegressionUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject regressionPanel;
    [SerializeField] private TMP_Text regressionResultsText;
    [SerializeField] private Button calculateRegressionButton;
    [SerializeField] private Button closeRegressionButton;
    [SerializeField] private Button multiTargetButton; // Button for multi-target analysis (10%-90%)

    [Header("Multi-Target Table Panel")]
    [SerializeField] private GameObject multiTargetPanel;
    [SerializeField] private TableUI multiTargetTable;
    [SerializeField] private Button closeMultiTargetButton;
    [SerializeField] private TMP_Text selectedRowFeedbackText; // Shows which row was selected

    // Remember time scale/state before opening the multi-target panel
    private float multiTargetPreviousTimeScale = 1f;
    
    // Store the last multi-target results for row click handling
    private MultiTargetOptimizer.MultiTargetResult lastMultiTargetResults;

    [Header("Save Settings")]
    [SerializeField] private bool autoSaveResults = true;
    [SerializeField] private string saveFolder = "RegressionResults";

    [Header("Python Server Settings")]
    [SerializeField] private bool usePythonServer = false;  // Default: Unity model

    private TrialDataModels.RegressionResult lastResult;
    private TrialUIController trialUIController;
    private string lastSavedReportHash; // Prevent duplicate saves of the same analysis

    void Start()
    {
        trialUIController = FindObjectOfType<TrialUIController>();
        
        if (calculateRegressionButton != null)
            calculateRegressionButton.onClick.AddListener(CalculateRegression);

        if (closeRegressionButton != null)
            closeRegressionButton.onClick.AddListener(CloseRegressionPanel);

        if (multiTargetButton != null)
            multiTargetButton.onClick.AddListener(CalculateMultiTargetAnalysis);

        if (closeMultiTargetButton != null)
            closeMultiTargetButton.onClick.AddListener(CloseMultiTargetPanel);

        if (regressionPanel != null)
            regressionPanel.SetActive(false);

        if (multiTargetPanel != null)
            multiTargetPanel.SetActive(false);

        // Hide MULTI button initially
        UpdateMultiButtonVisibility();

        // Check if Python server is enabled in editor
        if (usePythonServer)
        {
            CheckServerAvailability();
        }
    }

    void Update()
    {
        // Sync MULTI button visibility with ANALYSE button
        UpdateMultiButtonVisibility();
    }

    /// <summary>
    /// Shows MULTI button only when ANALYSE button is available (enough trials)
    /// </summary>
    private void UpdateMultiButtonVisibility()
    {
        if (multiTargetButton == null) return;

        // MULTI requires at least 3 trials (same as ANALYSE requires 2)
        bool canAnalyze = CanCalculateRegression();
        
        // Check if ANALYSE button is visible/active
        bool analyseVisible = calculateRegressionButton != null && 
                              calculateRegressionButton.gameObject.activeInHierarchy;

        // Show MULTI only if ANALYSE is visible and we have enough trials
        multiTargetButton.gameObject.SetActive(canAnalyze && analyseVisible);
    }
    
    private void CheckServerAvailability()
    {
        var serverClient = FindObjectOfType<PythonRegressionServerClient>();
        
        if (serverClient != null)
        {
            StartCoroutine(serverClient.CheckServerHealth());
        }
    }

    public void CalculateRegression()
    {
        if (trialUIController == null)
            trialUIController = FindObjectOfType<TrialUIController>();
            
        bool useRandomParameters = trialUIController != null && trialUIController.IsRandomParametersMode();
        var trialData = TrialDataService.LoadAllTrials(useRandomParameters);

        if (trialData == null || trialData.Count < 2)
        {
            ShowError("Failed to load trial data! Need at least 2 trials.");
            return;
        }

        // Check if Python server is enabled and available
        if (usePythonServer)
        {
            var serverClient = FindObjectOfType<PythonRegressionServerClient>();
            if (serverClient != null && serverClient.IsServerAvailable)
            {
                // Show panel immediately with loading message
                ShowLoadingMessage("Analyzing with Python server...\n\nTraining regression model on trial data...");
                StartCoroutine(serverClient.TrainAndAnalyze(trialData, 10f, OnServerAnalysisComplete));
                return;
            }
            else
            {
                // Server not available, will fall back to Unity model
            }
        }

        // Use Unity built-in model (patient-specific Ridge regression)
        lastResult = TrialRegressionAlgorithm.PerformRegressionAnalysis(trialData);
        lastSavedReportHash = null; // Reset hash for new analysis
        ShowRegressionResults(lastResult.summaryText);

        if (autoSaveResults)
            SaveRegressionResults();
    }
    
    private void OnServerAnalysisComplete(TrialDataModels.RegressionResult result)
    {
        lastResult = result;
        lastSavedReportHash = null;
        ShowRegressionResults(result.summaryText);
        
        if (autoSaveResults)
            SaveRegressionResults();
    }

    public void CalculateAndShowRegression() => CalculateRegression();

    private void ShowRegressionResults(string results)
    {
        if (regressionPanel != null)
        {
            regressionPanel.SetActive(true);
            regressionPanel.transform.SetAsLastSibling();
        }

        if (regressionResultsText != null)
            regressionResultsText.text = results;
    }

    private void ShowError(string errorMessage)
    {
        if (regressionPanel != null)
            regressionPanel.SetActive(true);

        if (regressionResultsText != null)
            regressionResultsText.text = $"ERROR:\n{errorMessage}";

        Debug.LogError(errorMessage);
    }

    private void ShowLoadingMessage(string message)
    {
        if (regressionPanel != null)
        {
            regressionPanel.SetActive(true);
            regressionPanel.transform.SetAsLastSibling();
        }

        if (regressionResultsText != null)
            regressionResultsText.text = message;
    }

    public void CloseRegressionPanel()
    {
        if (regressionPanel != null)
            regressionPanel.SetActive(false);

        // Return to trial control panel - it handles Time.timeScale = 0 and cursor
        if (trialUIController == null)
            trialUIController = FindObjectOfType<TrialUIController>();
            
        if (trialUIController != null)
            trialUIController.OpenTrialControlPanel();

        // Don't set Time.timeScale here! OpenTrialControlPanel() already sets it to 0
    }

    public void ForceCloseRegressionPanel()
    {
        if (regressionPanel != null && regressionPanel.activeSelf)
        {
            regressionPanel.SetActive(false);
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    public bool CanCalculateRegression()
    {
        if (trialUIController == null)
            trialUIController = FindObjectOfType<TrialUIController>();
            
        bool useRandomParameters = trialUIController != null && trialUIController.IsRandomParametersMode();
        var trialData = TrialDataService.LoadAllTrials(useRandomParameters);
        return trialData != null && trialData.Count >= 2;
    }

    public void SaveRegressionResults()
    {
        if (lastResult == null)
        {
            return;
        }

        // Prevent duplicate saves: check if this is the same report we just saved
        string currentReportHash = lastResult.fullDetailsText?.GetHashCode().ToString();
        if (currentReportHash == lastSavedReportHash)
        {
            return;
        }

        bool saved = TrialRegressionAlgorithm.SaveRegressionResultsToFile(lastResult, saveFolder);
        if (saved)
        {
            lastSavedReportHash = currentReportHash;
        }
    }

    /// <summary>
    /// Multi-target analysis - calculates optimal parameters for targets 10%-90%.
    /// Opens the multi-target panel and fills the table with results.
    /// </summary>
    public void CalculateMultiTargetAnalysis()
    {
        if (trialUIController == null)
            trialUIController = FindObjectOfType<TrialUIController>();

        bool useRandomParameters = trialUIController != null && trialUIController.IsRandomParametersMode();
        var trialData = TrialDataService.LoadAllTrials(useRandomParameters);

        if (trialData == null || trialData.Count < 3)
        {
            ShowError("Need at least 3 trials for multi-target analysis!");
            return;
        }

        // Run multi-target analysis and get results
        var results = RunMultiTargetAndGetResults(trialData);
        
        if (results != null && multiTargetPanel != null)
        {
            // Store results for row click handling
            lastMultiTargetResults = results;
            
            // Open the multi-target panel
            multiTargetPreviousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            multiTargetPanel.SetActive(true);
            multiTargetPanel.transform.SetAsLastSibling();
            
            // Fill the table with results
            FillMultiTargetTable(results);
            
            // DON'T setup click handlers - using buttons instead
            // SetupTableRowClickHandlers();
            
            // Clear feedback text
            if (selectedRowFeedbackText != null)
                selectedRowFeedbackText.text = "Click a button to select parameters";
        }
    }

    /// <summary>
    /// Runs multi-target analysis and returns the results object.
    /// </summary>
    private MultiTargetOptimizer.MultiTargetResult RunMultiTargetAndGetResults(List<TrialDataModels.TrialData> trialData)
    {
        try
        {
            int featureCount = FeatureExtractor.FeatureCount;
            var predictor = new OxygenPredictor { maxFeaturesForTraining = featureCount };
            bool trained = predictor.TrainModel(trialData, enableFeatureSelection: false);

            if (!trained)
            {
                Debug.LogError("Failed to train regression model");
                return null;
            }

            var model = predictor.GetModel();
            var results = MultiTargetOptimizer.OptimizeForAllTargets(trialData, predictor, model);

            if (results != null)
            {
                // Save to CSV
                MultiTargetOptimizer.SaveToTargetCSV(results);
                
                string timestamp = results.timestamp.ToString("yyyy-MM-dd_HH-mm-ss");
                MultiTargetOptimizer.SaveReportCSV(results, $"MultiTarget_Report_{timestamp}.csv");
            }

            return results;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Multi-target analysis failed: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Fills the TableUI with multi-target results.
    /// </summary>
    private void FillMultiTargetTable(MultiTargetOptimizer.MultiTargetResult results)
    {
        if (multiTargetTable == null)
        {
            Debug.LogError("[TrialRegressionUI] multiTargetTable is NULL! Please assign it in Inspector.");
            return;
        }
        
        if (results == null || results.results.Count == 0)
        {
            Debug.LogWarning("[TrialRegressionUI] Cannot fill table: results are null or empty");
            return;
        }

        Debug.Log($"[TrialRegressionUI] Filling table with {results.results.Count} results. Table has {multiTargetTable.Rows} rows, {multiTargetTable.Columns} columns");
        
        // Table should already have header row set up in Unity
        // We just need to fill the data rows (rows 1-9 for targets 10%-90%)
        
        for (int i = 0; i < results.results.Count && i < multiTargetTable.Rows - 1; i++)
        {
            var r = results.results[i];
            int row = i + 1; // Skip header row (row 0)
            
            // Column 0: Target Oxygen
            SetCellText(row, 0, $"{r.targetOxygen:F0}%");
            
            if (r.parameters != null)
            {
                var p = r.parameters;
                bool isAmadeo = p.IsAmadeoMode > 0.5f;
                
                // Column 1: Predicted Oxygen
                SetCellText(row, 1, $"{r.predictedOxygen:F1}%");
                
                // Column 2: Error
                SetCellText(row, 2, $"{r.error:F2}%");
                
                // Column 3: Speed
                SetCellText(row, 3, $"{p.speed:F2}");
                
                // Column 4: Vertical Speed
                SetCellText(row, 4, $"{p.verticalSpeed:F2}");
                
                // Column 5: Idle Upward Speed
                SetCellText(row, 5, $"{p.idleUpwardSpeed:F2}");
                
                // Column 6: Life Time
                SetCellText(row, 6, $"{p.lifeTime:F2}");
                
                // Column 7: Remove Health Every Life Time
                SetCellText(row, 7, $"{p.RemoveHealthEveryLifeTime:F2}");
                
                // Column 8: Remove Health With Collide
                SetCellText(row, 8, $"{p.removeHealthWithCollide:F2}");
                
                // Column 9: Time Between Collides
                SetCellText(row, 9, $"{p.timeBetweenCollides:F2}");
                
                // Column 10: Heal Health Point
                SetCellText(row, 10, $"{p.healHealthPoint:F2}");
                
                // Column 11: Factor Force
                SetCellText(row, 11, isAmadeo ? $"{p.factorForce:F2}" : "0");
            }
            else
            {
                // No solution found - clear the row
                for (int col = 1; col < 12; col++)
                {
                    SetCellText(row, col, "-");
                }
            }
        }
        
        Debug.Log($"[TrialRegressionUI] Filled table with {results.results.Count} results");
    }

    /// <summary>
    /// Helper to safely set cell text.
    /// </summary>
    private void SetCellText(int row, int col, string text)
    {
        if (multiTargetTable == null) return;
        
        try
        {
            if (row < multiTargetTable.Rows && col < multiTargetTable.Columns)
            {
                var cell = multiTargetTable.GetCell(row, col);
                if (cell != null)
                {
                    cell.text = text;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Failed to set cell [{row},{col}]: {e.Message}");
        }
    }

    /// <summary>
    /// Closes the multi-target panel and returns to trial control panel.
    /// OpenTrialControlPanel() handles Time.timeScale = 0 and cursor settings.
    /// </summary>
    public void CloseMultiTargetPanel()
    {
        if (multiTargetPanel != null)
            multiTargetPanel.SetActive(false);

        // Return to trial control panel - it handles Time.timeScale = 0
        if (trialUIController == null)
            trialUIController = FindObjectOfType<TrialUIController>();
            
        if (trialUIController != null)
            trialUIController.OpenTrialControlPanel();
        
        // Don't set Time.timeScale here! OpenTrialControlPanel() already sets it to 0
    }

    #region Row Click Handlers

    /// <summary>
    /// Sets up click handlers for each table row.
    /// Uses EventTrigger for more reliable click detection.
    /// </summary>
    private void SetupTableRowClickHandlers()
    {
        if (multiTargetTable == null) return;

        Debug.Log($"[TrialRegressionUI] Setting up click handlers for {multiTargetTable.Rows} rows, {multiTargetTable.Columns} columns");

        // Only setup first column of each row (Target Oxygen column) for clicking
        for (int row = 1; row < multiTargetTable.Rows; row++)
        {
            int rowIndex = row; // Capture for closure
            
            // Setup click on first column only (simpler and more reliable)
            try
            {
                var cell = multiTargetTable.GetCell(row, 0); // First column (Target Oxygen)
                if (cell != null)
                {
                    // Make sure raycast target is enabled
                    cell.raycastTarget = true;
                    
                    // Remove any existing EventTrigger
                    var existingTrigger = cell.gameObject.GetComponent<EventTrigger>();
                    if (existingTrigger != null)
                    {
                        Object.Destroy(existingTrigger);
                    }
                    
                    // Add EventTrigger component
                    var eventTrigger = cell.gameObject.AddComponent<EventTrigger>();
                    
                    // Create PointerClick entry
                    var clickEntry = new EventTrigger.Entry();
                    clickEntry.eventID = EventTriggerType.PointerClick;
                    clickEntry.callback.AddListener((data) => { OnTableRowClicked(rowIndex); });
                    eventTrigger.triggers.Add(clickEntry);
                    
                    // Create PointerEnter entry (for hover effect)
                    var enterEntry = new EventTrigger.Entry();
                    enterEntry.eventID = EventTriggerType.PointerEnter;
                    enterEntry.callback.AddListener((data) => { OnRowHoverEnter(rowIndex); });
                    eventTrigger.triggers.Add(enterEntry);
                    
                    // Create PointerExit entry
                    var exitEntry = new EventTrigger.Entry();
                    exitEntry.eventID = EventTriggerType.PointerExit;
                    exitEntry.callback.AddListener((data) => { OnRowHoverExit(rowIndex); });
                    eventTrigger.triggers.Add(exitEntry);
                    
                    Debug.Log($"[TrialRegressionUI] Added EventTrigger to row {row}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[TrialRegressionUI] Failed to add click handler to row {row}: {e.Message}");
            }
        }

        Debug.Log("[TrialRegressionUI] Row click handlers setup complete");
    }
    
    /// <summary>
    /// Called when mouse enters a row - shows hover effect
    /// </summary>
    private void OnRowHoverEnter(int rowIndex)
    {
        if (multiTargetTable == null) return;
        
        // Change background color for all cells in the row
        for (int col = 0; col < multiTargetTable.Columns; col++)
        {
            var cell = multiTargetTable.GetCell(rowIndex, col);
            if (cell != null)
            {
                cell.color = new Color(0.8f, 0.9f, 1f); // Light blue
            }
        }
    }
    
    /// <summary>
    /// Called when mouse exits a row - removes hover effect
    /// </summary>
    private void OnRowHoverExit(int rowIndex)
    {
        if (multiTargetTable == null) return;
        
        // Reset to normal color (white for text)
        for (int col = 0; col < multiTargetTable.Columns; col++)
        {
            var cell = multiTargetTable.GetCell(rowIndex, col);
            if (cell != null)
            {
                cell.color = Color.black; // Reset to black text
            }
        }
    }

    /// <summary>
    /// TEST method - call this first to verify buttons work at all
    /// </summary>
    public void TestButtonClick()
    {
        Debug.LogError("Test button clicked");
    }

    /// <summary>
    /// PUBLIC method to be called from Button OnClick in Inspector.
    /// Pass the target oxygen percentage (10, 20, 30... 90).
    /// </summary>
    public void OnTargetButtonClicked(int targetOxygenPercent)
    {
        Debug.LogError($"Button clicked for target {targetOxygenPercent}%");
        Debug.Log($"[TrialRegressionUI] Button clicked for target {targetOxygenPercent}%");
        
        // Convert target percent to row index (10% = row 1, 20% = row 2, etc.)
        int rowIndex = targetOxygenPercent / 10;
        Debug.Log($"[TrialRegressionUI] Row index: {rowIndex}");
        
        OnTableRowClicked(rowIndex);
    }

    /// <summary>
    /// Called when user clicks on a table row.
    /// Saves the parameters from that row as default for main game.
    /// </summary>
    private void OnTableRowClicked(int rowIndex)
    {
        Debug.Log($"[TrialRegressionUI] === ROW CLICKED: {rowIndex} ===");
        
        // Row index is 1-based (row 0 is header), so result index is row - 1
        int resultIndex = rowIndex - 1;

        if (lastMultiTargetResults == null || 
            resultIndex < 0 || 
            resultIndex >= lastMultiTargetResults.results.Count)
        {
            Debug.LogWarning($"[TrialRegressionUI] Invalid row click: row={rowIndex}, resultIndex={resultIndex}");
            return;
        }

        var result = lastMultiTargetResults.results[resultIndex];
        
        if (result.parameters == null)
        {
            if (selectedRowFeedbackText != null)
                selectedRowFeedbackText.text = $" No parameters available for target {result.targetOxygen:F0}%";
            return;
        }

        // Save the selected parameters
        bool saved = SelectedParametersService.SaveSelectedParameters(
            result.parameters, 
            result.targetOxygen, 
            result.predictedOxygen);

        if (saved)
        {
            if (selectedRowFeedbackText != null)
                selectedRowFeedbackText.text = $"Selected parameters for target {result.targetOxygen:F0}% (Predicted: {result.predictedOxygen:F1}%)";
            
            // Highlight the selected row
            HighlightRow(rowIndex);
            
            Debug.Log($"[TrialRegressionUI] Selected row {rowIndex} - Target: {result.targetOxygen}%, Predicted: {result.predictedOxygen:F1}%");
            
            // Close panel after 2 seconds
            StartCoroutine(CloseMultiTargetPanelAfterDelay(2f));
        }
        else
        {
            if (selectedRowFeedbackText != null)
                selectedRowFeedbackText.text = " Error saving parameters";
        }
    }

    /// <summary>
    /// Highlights the selected row in the table
    /// </summary>
    private void HighlightRow(int selectedRow)
    {
        if (multiTargetTable == null) return;

        Color normalColor = Color.black;                      // Normal black text
        Color selectedColor = new Color(0f, 0.5f, 0f);        // Dark green text for selected

        // Reset all rows to normal color, highlight selected
        for (int row = 1; row < multiTargetTable.Rows; row++)
        {
            Color color = (row == selectedRow) ? selectedColor : normalColor;
            
            for (int col = 0; col < multiTargetTable.Columns; col++)
            {
                try
                {
                    var cell = multiTargetTable.GetCell(row, col);
                    if (cell != null)
                    {
                        cell.color = color;
                    }
                }
                catch { }
            }
        }
    }

    /// <summary>
    /// Closes the multi-target panel after a delay
    /// </summary>
    private System.Collections.IEnumerator CloseMultiTargetPanelAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        CloseMultiTargetPanel();
    }

    #endregion
}
