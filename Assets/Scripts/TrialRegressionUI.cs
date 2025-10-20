using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

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
    [SerializeField] private Button saveResultsButton;

    [Header("Save Settings")]
    [SerializeField] private bool autoSaveResults = true;
    [SerializeField] private string saveFolder = "RegressionResults";

    private TrialRegressionAlgorithm.RegressionResult lastResult;

    void Start()
    {
        // Setup button click listeners
        if (calculateRegressionButton != null)
            calculateRegressionButton.onClick.AddListener(CalculateRegression);

        if (closeRegressionButton != null)
            closeRegressionButton.onClick.AddListener(CloseRegressionPanel);

        if (saveResultsButton != null)
            saveResultsButton.onClick.AddListener(SaveRegressionResults);

        // Hide panel on start
        if (regressionPanel != null)
            regressionPanel.SetActive(false);
    }

    public void CalculateRegression()
    {
        var trialData = TrialRegressionAlgorithm.LoadTrialDataFromCache();

        if (trialData == null || trialData.Count < 2)
        {
            ShowError("Failed to load trial data! Need at least 2 trials.");
            return;
        }

        // Run regression analysis
        lastResult = TrialRegressionAlgorithm.PerformRegressionAnalysis(trialData);

        // Display output in panel
        ShowRegressionResults(lastResult.summaryText);

        // Automatically save file if enabled
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

        Debug.Log("Results displayed");
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

        // Return to the trial control UI
        var trialUIController = FindObjectOfType<TrialUIController>();
        if (trialUIController != null)
            trialUIController.OpenTrialControlPanel();

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        
    }

    public void ForceCloseRegressionPanel()
    {
        if (regressionPanel != null && regressionPanel.activeSelf)
        {
            regressionPanel.SetActive(false);
            Debug.Log("Regression panel force closed");
        }
    }

    public bool CanCalculateRegression()
    {
        var trialData = TrialRegressionAlgorithm.LoadTrialDataFromCache();
        return trialData != null && trialData.Count >= 2;
    }

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
