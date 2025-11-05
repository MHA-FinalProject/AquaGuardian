using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/**
 * TrialSystemManager - Manages the trial system lifecycle
 * Handles the trial system lifecycle: Start - Run - End - Continue - Complete
 * See also: PanelOpenUp, TrialParameterManager, TrialFishSpawner, TrialUIController, GameSystemResetter 
 */
public class TrialSystemManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PanelOpenUp panelOpenUp;
    [SerializeField] private TrialParameterManager parameterManager;
    [SerializeField] private TrialFishSpawner fishSpawner;
    [SerializeField] private TrialUIController uiController;
    [SerializeField] private GameSystemResetter systemResetter;

    [Header("Trial Settings")]
    [SerializeField] private int totalTrials = 5;

    [Header("Audio Settings")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip trialCompletionSound;

    // Trial state
    private bool trialsMode = false;
    private int currentTrialNumber = 0;
    private TrialDataModels.TrialData currentTrialData;
    private float trialStartTime;
    private bool _startingNext = false;
    private List<Collider> _disabledFinishers = new List<Collider>();

    // Public getters
    public bool TrialsMode => trialsMode;
    public int CurrentTrialNumber => currentTrialNumber;
    public int TotalTrials => totalTrials;
    public TrialDataModels.TrialData CurrentTrialData => currentTrialData;

    void Start()
    {
        GameStateManager.OnGameEnded += OnGameEnded;
    }

    void OnDestroy()
    {
        GameStateManager.OnGameEnded -= OnGameEnded;
    }


    public void StartTrials()
    {
        // Ensure GameStateManager exists
        if (GameStateManager.Instance == null)
        {
            var gsm = FindObjectOfType<GameStateManager>();
            if (gsm == null)
            {
                GameObject gsmObj = new GameObject("GameStateManager");
                gsmObj.AddComponent<GameStateManager>();

            }
        }

        trialsMode = true;
        currentTrialNumber = 0;
        GameStateManager.SetTrialsActive(true);
        DisableAllFinishers();


        if (uiController != null)
            uiController.CloseTrialControlPanel(false);

        if (CoroutineHost.Instance != null)
            CoroutineHost.Instance.StartCoroutine(StartNextTrialCoroutine());
        else
            StartCoroutine(StartNextTrialCoroutine());
    }


    public void ContinueToNextTrial()
    {
        if (_startingNext) return;

        if (currentTrialNumber >= totalTrials)
        {
            if (uiController != null)
                uiController.CloseTrialControlPanel(false);
            CompleteAllTrials();
            return;
        }

        StartNextTrialExplicit();
    }

    public void OnTrialFishReached(float finalOxygen, bool completed)
    {
        if (!trialsMode) return;

        if (currentTrialData == null)
            currentTrialData = new TrialDataModels.TrialData { trialId = currentTrialNumber };

        EndTrialAndShowPanel(finalOxygen, completed);
    }

    public void RestartCurrentTrial()
    {
        if (_startingNext) return;

        if (CoroutineHost.Instance != null)
            CoroutineHost.Instance.StartCoroutine(RestartCurrentTrialCoroutine());
        else
            StartCoroutine(RestartCurrentTrialCoroutine());
    }

    public void CompleteAllTrials()
    {
        ExitTrialsMode();

        if (systemResetter != null)
            systemResetter.CleanupAllTrialObjects();

        // Show completion UI first
        if (uiController != null)
            uiController.UpdateCompletionUI();
    }


    private void OnGameEnded(float finalOxygen, bool completed)
    {
        if (!trialsMode) return;

        if (currentTrialData == null)
            currentTrialData = new TrialDataModels.TrialData { trialId = currentTrialNumber };

        EndTrialAndShowPanel(finalOxygen, completed);
    }

    private void StartNextTrialExplicit()
    {
        if (CoroutineHost.Instance != null)
            CoroutineHost.Instance.StartCoroutine(StartNextTrialCoroutine());
        else
            StartCoroutine(StartNextTrialCoroutine());
    }

    private IEnumerator RestartCurrentTrialCoroutine()
    {
        _startingNext = true;

        // Close any UI panels before restarting
        if (uiController != null)
            uiController.CloseTrialControlPanel(false);

        var regressionUI = FindObjectOfType<TrialRegressionUI>();
        if (regressionUI != null)
            regressionUI.ForceCloseRegressionPanel();

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        DisableAllFinishers();

        if (systemResetter != null)
        {
            systemResetter.CleanupSpawned();
            systemResetter.ResetPlayerToStartPosition();
            systemResetter.ResetGameSystemsForTrial();
        }
        if (panelOpenUp != null)
            panelOpenUp.caveInfos.Clear();

        if (!GameStateManager.AreTrialsActive)
        {
            // Debug.LogError("CRITICAL: Trials became inactive during restart!");
            GameStateManager.SetTrialsActive(true);
        }

        try
        {
            if (panelOpenUp != null)
                panelOpenUp.LoadCaveFileForTrial(currentTrialNumber);
            if (parameterManager != null)
            {
                bool useRandom = uiController != null && uiController.IsRandomParametersMode();
                currentTrialData = parameterManager.LoadAndApplyTrialParameters(currentTrialNumber, useRandom);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error loading trial data: {e.Message}");
            _startingNext = false;
            yield break;
        }

        yield return null;

        if (systemResetter != null)
            systemResetter.PrepareComponentsForClosePanel();

        try
        {
            if (panelOpenUp != null)
                panelOpenUp.ClosePanel();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error rebuilding caves: {e.Message}");
            _startingNext = false;
            yield break;
        }

        yield return new WaitForFixedUpdate();

        if (uiController != null)
            uiController.UpdateTrialUI(currentTrialNumber, totalTrials);

        GameStateManager.Instance?.NotifyPanelClosed();

        // Start timing the trial
        trialStartTime = Time.time;
        Debug.Log($"[TrialSystem] Trial {currentTrialNumber} started");

        _startingNext = false;
    }

    private IEnumerator StartNextTrialCoroutine()
    {
        _startingNext = true;

        // Close any UI panels before starting next trial
        if (uiController != null)
            uiController.CloseTrialControlPanel(false);

        var regressionUI = FindObjectOfType<TrialRegressionUI>();
        if (regressionUI != null)
            regressionUI.ForceCloseRegressionPanel();

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        DisableAllFinishers();

        if (systemResetter != null)
        {
            systemResetter.CleanupSpawned();
            systemResetter.ResetPlayerToStartPosition();
            systemResetter.ResetGameSystemsForTrial();
        }
        if (panelOpenUp != null)
            panelOpenUp.caveInfos.Clear();

        if (currentTrialNumber == 0 || (currentTrialData != null && currentTrialData.completed))
            currentTrialNumber++;

        try
        {
            if (panelOpenUp != null)
                panelOpenUp.LoadCaveFileForTrial(currentTrialNumber);
            if (parameterManager != null)
            {
                bool useRandom = uiController != null && uiController.IsRandomParametersMode();
                currentTrialData = parameterManager.LoadAndApplyTrialParameters(currentTrialNumber, useRandom);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error loading trial data: {e.Message}");
            _startingNext = false;
            yield break;
        }

        yield return null;

        if (systemResetter != null)
            systemResetter.PrepareComponentsForClosePanel();

        try
        {
            if (panelOpenUp != null)
                panelOpenUp.ClosePanel();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error rebuilding caves: {e.Message}");
            _startingNext = false;
            yield break;
        }

        yield return new WaitForFixedUpdate();

        if (uiController != null)
            uiController.UpdateTrialUI(currentTrialNumber, totalTrials);

        GameStateManager.Instance?.NotifyPanelClosed();

        // Start timing the trial
        trialStartTime = Time.time;
        Debug.Log($"[TrialSystem] Trial {currentTrialNumber} started");

        _startingNext = false;
    }


    private void EndTrialAndShowPanel(float finalOxygen, bool completed)
    {
        // Calculate trial duration
        float trialEndTime = Time.time;
        float duration = trialEndTime - trialStartTime;

        // Determine if Amadeo/Emulation was ACTUALLY used during this trial
        // Check both: 1) Configuration (InputType), AND 2) Actual input source during trial
        AmadeoClient amadeoClient = FindObjectOfType<AmadeoClient>();
        PlayerMovement playerMovement = FindObjectOfType<PlayerMovement>();
        float isAmadeoMode = 0f;

        if (amadeoClient != null && playerMovement != null)
        {
            // Check configuration
            InputType inputType = amadeoClient.CurrentInputType;
            bool isConfiguredForAmadeo = (inputType == InputType.Amadeo || inputType == InputType.EmulationMode);

            // Check actual input usage during trial
            bool actuallyUsedAmadeo = playerMovement.ActuallyUsedAmadeoInput;

            // Set IsAmadeoMode only if BOTH conditions are met:
            // 1. Configured for Amadeo/Emulation mode
            // 2. Movement actually came from Amadeo (not keyboard fallback)
            if (isConfiguredForAmadeo && actuallyUsedAmadeo)
            {
                isAmadeoMode = 1f;
                Debug.Log($"[TrialSystemManager] Trial {currentTrialData.trialId}: Amadeo mode - Configured={isConfiguredForAmadeo}, ActuallyUsed={actuallyUsedAmadeo}, AmadeoCount={playerMovement.amadeoInputCount}, KeyboardCount={playerMovement.keyboardInputCount}");
            }
            else
            {
                isAmadeoMode = 0f;
                if (isConfiguredForAmadeo && !actuallyUsedAmadeo)
                {
                    Debug.Log($"[TrialSystemManager] Trial {currentTrialData.trialId}: Configured for Amadeo but used keyboard (fallback) - AmadeoCount={playerMovement.amadeoInputCount}, KeyboardCount={playerMovement.keyboardInputCount}");
                }
                else
                {
                    Debug.Log($"[TrialSystemManager] Trial {currentTrialData.trialId}: Keyboard mode - AmadeoCount={playerMovement.amadeoInputCount}, KeyboardCount={playerMovement.keyboardInputCount}");
                }
            }
        }
        else if (amadeoClient != null)
        {
            // Fallback: check configuration only if PlayerMovement not found
            InputType inputType = amadeoClient.CurrentInputType;
            if (inputType == InputType.Amadeo || inputType == InputType.EmulationMode)
            {
                isAmadeoMode = 1f;
                Debug.LogWarning($"[TrialSystemManager] Trial {currentTrialData.trialId}: PlayerMovement not found, using configuration only (InputType={inputType})");
            }
        }

        currentTrialData.finalOxygenRemaining = finalOxygen;
        currentTrialData.completed = completed;
        currentTrialData.trialDuration = duration;
        currentTrialData.IsAmadeoMode = isAmadeoMode;

        Debug.Log($" Trial {currentTrialData.trialId} ended - O2: {finalOxygen:F1}%, Duration: {duration:F1}s, Completed: {completed}, IsAmadeoMode: {isAmadeoMode}");

        if (completed)
            PlayTrialCompletionSound();

        if (completed && parameterManager != null)
        {
            bool csvSaved = parameterManager.SaveTrialResultToCSV(currentTrialData);
            if (!csvSaved)
            {
                Debug.LogError($"[TrialSystem] Failed to save trial {currentTrialData.trialId} results to CSV!");
            }
        }

        PrepareForNextTrial();

        if (uiController != null)
        {
            uiController.UpdateTrialResults(finalOxygen, completed, currentTrialNumber, totalTrials);
            uiController.UpdateTrialControlText(trialsMode, currentTrialNumber, totalTrials, !completed);
            uiController.OpenTrialControlPanel();
        }
    }

    private void PlayTrialCompletionSound()
    {
        if (audioSource != null && trialCompletionSound != null)
            audioSource.PlayOneShot(trialCompletionSound);
    }


    private void PrepareForNextTrial()
    {
        if (systemResetter != null)
            systemResetter.PrepareForNextTrial();

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void ExitTrialsMode()
    {
        trialsMode = false;
        GameStateManager.SetTrialsActive(false);

        if (uiController != null)
            uiController.CloseTrialControlPanel(false);

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void DisableAllFinishers()
    {
        _disabledFinishers.Clear();
        var finishers = FindObjectsOfType<GoToEndGame>();

        foreach (var finisher in finishers)
        {
            var collider = finisher.GetComponent<Collider>();
            if (collider != null && collider.enabled)
            {
                collider.enabled = false;
                _disabledFinishers.Add(collider);
            }
        }
    }

    private void ReenableFinishers()
    {
        foreach (var collider in _disabledFinishers)
        {
            if (collider != null)
                collider.enabled = true;
        }
        _disabledFinishers.Clear();
    }
}
