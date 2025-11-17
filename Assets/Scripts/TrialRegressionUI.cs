using UnityEngine;
using UnityEngine.UI;
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

    [Header("Save Settings")]
    [SerializeField] private bool autoSaveResults = true;
    [SerializeField] private string saveFolder = "RegressionResults";

    [Header("Python Model Auto-Load")]
    [SerializeField] private bool autoLoadPythonModel = false;
    [SerializeField] private string pythonModelFileName = "regression_model_elasticnet.json";

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

        if (regressionPanel != null)
            regressionPanel.SetActive(false);

        // AUTO-LOAD Python model if enabled
        if (autoLoadPythonModel)
        {
            TrialRegressionAlgorithm.TryAutoLoadPythonModel(pythonModelFileName);
        }
    }

    public void CalculateRegression()
    {
        if (trialUIController == null)
            trialUIController = FindObjectOfType<TrialUIController>();
            
        bool useRandomParameters = trialUIController != null && trialUIController.IsRandomParametersMode();
        var trialData = TrialDataService.LoadAllTrials(useRandomParameters);
        
        int trialCount = trialData != null ? trialData.Count : 0;
        Debug.Log($"[TrialRegressionUI] Starting regression analysis - Mode: {(useRandomParameters ? "RANDOM" : "CONSTANT (CSV)")}, Trials: {trialCount}");

        if (trialData == null || trialData.Count < 2)
        {
            ShowError("Failed to load trial data! Need at least 2 trials.");
            return;
        }

        lastResult = TrialRegressionAlgorithm.PerformRegressionAnalysis(trialData);
        lastSavedReportHash = null; // Reset hash for new analysis
        ShowRegressionResults(lastResult.summaryText);

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

    public void CloseRegressionPanel()
    {
        if (regressionPanel != null)
            regressionPanel.SetActive(false);

        if (trialUIController == null)
            trialUIController = FindObjectOfType<TrialUIController>();
            
        if (trialUIController != null)
            trialUIController.OpenTrialControlPanel();

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
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
            Debug.LogWarning("No results to save!");
            return;
        }

        // Prevent duplicate saves: check if this is the same report we just saved
        string currentReportHash = lastResult.fullDetailsText?.GetHashCode().ToString();
        if (currentReportHash == lastSavedReportHash)
        {
            Debug.Log("[TrialRegressionUI] Skipping duplicate save - same report already saved");
            return;
        }

        bool saved = TrialRegressionAlgorithm.SaveRegressionResultsToFile(lastResult, saveFolder);
        if (saved)
        {
            lastSavedReportHash = currentReportHash;
        }
    }
}
