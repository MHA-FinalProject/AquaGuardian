using UnityEngine;
using UnityEngine.UI;
using TMPro;

/**
 * TrialUIController - Manages all trial-related UI
 * Handles panel display, button states, and text updates
 * See also: TrialSystemManager, TrialParameterManager, TrialFishSpawner, GameSystemResetter, TrialRegressionUI
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
    [SerializeField] private GameObject trialResultsPanel;  // Panel for results (parent of trialResultsText)
    [SerializeField] private TMP_Text trialResultsText;  // Text for results only

    [Header("UI Buttons")]
    [SerializeField] private Button startTrialButton;
    [SerializeField] private Button continueTrialButton;
    [SerializeField] private Button multiTargetButton;
    [SerializeField] private Button closeTrialButton;

    [Header("Parameter Mode Toggle")]
    [SerializeField] private Toggle useRandomParametersToggle; // Toggle for random vs CSV (label is static in UI)

    [Header("References")]
    [SerializeField] private TrialSystemManager trialSystemManager;
    [SerializeField] private TrialRegressionUI regressionUI;

    void Start()
    {
        // Check references
        if (regressionUI == null)
            Debug.LogError("regressionUI is NULL! Please assign TrialRegressionUI in Inspector!");

        if (trialSystemManager == null)
            Debug.LogWarning("trialSystemManager is NULL!");

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

        // Hide results panel and clean text
        if (trialResultsPanel != null) trialResultsPanel.SetActive(false);
        if (trialResultsText != null) trialResultsText.text = "";
    }

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
            startTrialButton.onClick.AddListener(() =>
            {
                if (trialSystemManager != null)
                    trialSystemManager.StartTrials();
            });
        }

        // Continue button (also used for Restart when trial fails)
        if (continueTrialButton != null)
        {
            continueTrialButton.onClick.RemoveAllListeners();
            continueTrialButton.onClick.AddListener(() =>
            {
                if (trialSystemManager != null)
                {
                    // If trial failed and can retry, restart instead of continue
                    bool canRetry = trialSystemManager.CanRetryCurrentTrial;
                    var currentTrialData = trialSystemManager.CurrentTrialData;
                    bool trialFailed = currentTrialData != null && !currentTrialData.completed;

                    if (trialFailed && canRetry)
                    {
                        trialSystemManager.RestartCurrentTrial();
                    }
                    else
                    {
                        trialSystemManager.ContinueToNextTrial();
                    }
                }
            });
        }

        // Random Parameters Toggle
        if (useRandomParametersToggle != null)
        {
            useRandomParametersToggle.onValueChanged.RemoveAllListeners();
            useRandomParametersToggle.onValueChanged.AddListener(OnRandomModeToggled);
            // Label text is static in UI, only toggle checkbox changes
        }

        // Multi-Target button
        if (multiTargetButton != null)
        {
            multiTargetButton.onClick.RemoveAllListeners();
            multiTargetButton.onClick.AddListener(OpenMultiTargetAnalysis);
        }
        else
        {
            Debug.LogError("multiTargetButton is NULL! Not assigned in Inspector!");
        }

        // Close button - Restart game (same as ScenesManager.RestartGame)
        if (closeTrialButton != null)
        {
            closeTrialButton.onClick.RemoveAllListeners();
            closeTrialButton.onClick.AddListener(OnCloseButtonClicked);
        }

        UpdateTrialButtonsState(false, 0, 5);
    }


    public void OpenTrialControlPanel()
    {
        // Hide Main Panel when Trial Control Panel opens
        if (mainPanel != null)
        {
            mainPanel.SetActive(false);
        }

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

        // Determine if trial failed by checking CurrentTrialData
        bool trialFailed = false;
        if (trialSystemManager != null && trialSystemManager.CurrentTrialData != null)
        {
            trialFailed = !trialSystemManager.CurrentTrialData.completed;
        }
        
        UpdateTrialControlText(trialSystemManager?.TrialsMode ?? false,
                              trialSystemManager?.CurrentTrialNumber ?? 0,
                              trialSystemManager?.TotalTrials ?? 5,
                              trialFailed);
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
        // Check if we can retry the current trial
        bool canRetry = trialSystemManager != null && trialSystemManager.CanRetryCurrentTrial;
        
        // Switch between 3 status text objects based on state
        if (!trialsMode || currentTrial == 0)
        {
            // Show start status text
            if (startStatusText != null) startStatusText.SetActive(true);
            if (duringTrialStatusText != null) duringTrialStatusText.SetActive(false);
            if (endStatusText != null) endStatusText.SetActive(false);
        }
        else if (currentTrial >= totalTrials && !(trialFailed && canRetry))
        {
            // Show end status text only if:
            // - Last trial AND (completed OR no more retries)
            if (startStatusText != null) startStatusText.SetActive(false);
            if (duringTrialStatusText != null) duringTrialStatusText.SetActive(false);
            if (endStatusText != null) endStatusText.SetActive(true);
        }
        else
        {
            // Show during trial status text (also for last trial that failed but can retry)
            if (startStatusText != null) startStatusText.SetActive(false);
            if (duringTrialStatusText != null) duringTrialStatusText.SetActive(true);
            if (endStatusText != null) endStatusText.SetActive(false);
        }

        // Instructions handled by status text objects, not instructionsText

        UpdateTrialButtonsState(trialsMode, currentTrial, totalTrials, trialFailed);
    }


    public void UpdateTrialResults(float finalOxygen, bool completed, int currentTrial, int totalTrials)
    {
        // Check if we can retry the current trial
        bool canRetry = trialSystemManager != null && trialSystemManager.CanRetryCurrentTrial;
        bool trialFailed = !completed;
        
        // Switch status text objects
        // Show end status only if: last trial AND (completed OR no more retries)
        if (currentTrial >= totalTrials && !(trialFailed && canRetry))
        {
            // Show end status text
            if (startStatusText != null) startStatusText.SetActive(false);
            if (duringTrialStatusText != null) duringTrialStatusText.SetActive(false);
            if (endStatusText != null) endStatusText.SetActive(true);
        }
        else
        {
            // Show during trial status text (also for last trial that failed but can retry)
            if (startStatusText != null) startStatusText.SetActive(false);
            if (duringTrialStatusText != null) duringTrialStatusText.SetActive(true);
            if (endStatusText != null) endStatusText.SetActive(false);
        }

        // Results - don't touch instructions!
        if (trialResultsPanel != null) trialResultsPanel.SetActive(true);
        
        if (trialResultsText != null)
        {
            string resultText;
            if (!completed)
            {
                string message = canRetry ? "You have 1 more attempt" : "No more attempts";
                resultText = $"<b>Trial {currentTrial}/{totalTrials}</b> - {message}\n<mark=#FFFF0055>Oxygen: {finalOxygen:F1}%</mark>";
            }
            else
            {
                resultText = $"<b>Trial {currentTrial}/{totalTrials} completed</b>\n<mark=#FFFF0055>Oxygen remaining: {finalOxygen:F1}%</mark>";
              /* if (currentTrial >= totalTrials) resultText += "\nAll trials completed!";
              */
            }

            trialResultsText.text = resultText;
        }

        UpdateTrialButtonsState(true, currentTrial, totalTrials, trialFailed);
    }


    public void UpdateTrialUI(int currentTrial, int totalTrials)
    {
        // Show during trial status text
        if (startStatusText != null) startStatusText.SetActive(false);
        if (duringTrialStatusText != null) duringTrialStatusText.SetActive(true);
        if (endStatusText != null) endStatusText.SetActive(false);

        // Hide results panel during trial
        if (trialResultsPanel != null) trialResultsPanel.SetActive(false);

        UpdateTrialButtonsState(true, currentTrial, totalTrials);
    }


    private void UpdateTrialButtonsState(bool trialsMode, int currentTrial, int totalTrials, bool trialFailed = false)
    {
        // Check if we can retry the current trial
        bool canRetry = trialSystemManager != null && trialSystemManager.CanRetryCurrentTrial;
        
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
            // Show continue button if:
            // 1. Normal case: trial is not the last one (currentTrial < totalTrials)
            // 2. Special case: Last trial failed AND we can still retry
            bool isLastTrialWithRetryAvailable = currentTrial >= totalTrials && trialFailed && canRetry;
            bool showContinue = trialsMode && currentTrial > 0 && (currentTrial < totalTrials || isLastTrialWithRetryAvailable);
            continueTrialButton.gameObject.SetActive(showContinue);
        }

        if (multiTargetButton != null)
        {
            // Show multi-target button only when:
            // - We're on the last trial AND (trial completed OR no more retries available)
            bool isLastTrialCompleted = currentTrial >= totalTrials && !trialFailed;
            bool isLastTrialNoMoreRetries = currentTrial >= totalTrials && trialFailed && !canRetry;
            bool showMultiTarget = trialsMode && (isLastTrialCompleted || isLastTrialNoMoreRetries);
            multiTargetButton.gameObject.SetActive(showMultiTarget);
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


    public void OpenMultiTargetAnalysis()
    {
        // Close trial panel without showing main (multi-target panel will appear)
        CloseTrialControlPanel(false);

        // Keep game paused for analysis
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (regressionUI != null)
        {
            regressionUI.CalculateMultiTargetAnalysis();
        }
        else
        {
            Debug.LogError("ERROR: regressionUI is NULL! Not assigned in Inspector!");
        }
    }




    public void UpdateCompletionUI()
    {
        if (mainPanel != null)
            mainPanel.SetActive(true);

        // Notify GameStateManager that panel is opened (so it can close again later)
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.NotifyPanelOpened();
        }

        // Show end status text
        if (startStatusText != null) startStatusText.SetActive(false);
        if (duringTrialStatusText != null) duringTrialStatusText.SetActive(false);
        if (endStatusText != null) endStatusText.SetActive(true);

        int totalTrials = trialSystemManager?.TotalTrials ?? 5;

        if (trialResultsPanel != null) trialResultsPanel.SetActive(true);
        if (trialResultsText != null) trialResultsText.text = $"<b>All {totalTrials} trials completed!</b>";

        UpdateTrialButtonsState(true, totalTrials, totalTrials);
    }


    // Random Parameters Mode - Toggle only (label is static in UI)
    // Toggle logic: Checked (ON) = Constant/CSV, Unchecked (OFF) = Random
    private void OnRandomModeToggled(bool isOn)
    {
        // Mode changed - handled by UI

    }

    public bool IsRandomParametersMode()
    {
        return useRandomParametersToggle != null && !useRandomParametersToggle.isOn;
    }

    private void OnCloseButtonClicked()
    {
        // Find ScenesManager
        var scenesManager = FindObjectOfType<ScenesManager>();
        if (scenesManager != null)
        {
            scenesManager.RestartGame();
        }
        else
        {
            Debug.LogError("[TrialUI] ScenesManager not found! Creating fallback restart...");

            // Fallback: Manual restart
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
            );
        }
    }
}

