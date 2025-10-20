using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/**
 * TrialSystemManager - Manages the trial system lifecycle
 * Handles trial flow: Start - Run - End - Continue - Complete
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
        
        RestoreOriginalGameState();
        
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
            systemResetter.CleanupSpawned();
        if (panelOpenUp != null)
            panelOpenUp.caveInfos.Clear();

        if (systemResetter != null)
            systemResetter.ResetPlayerToStartPosition();

        if (systemResetter != null)
            systemResetter.ResetGameSystemsForTrial();
        
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
                currentTrialData = parameterManager.LoadAndApplyTrialParameters(currentTrialNumber);
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
            systemResetter.CleanupSpawned();
        if (panelOpenUp != null)
            panelOpenUp.caveInfos.Clear();

        // Increment trial number only if previous completed or first trial
        if (currentTrialNumber == 0 || (currentTrialData != null && currentTrialData.completed))
            currentTrialNumber++;

        if (systemResetter != null)
            systemResetter.ResetPlayerToStartPosition();

        if (systemResetter != null)
            systemResetter.ResetGameSystemsForTrial();

        try
        {
            if (panelOpenUp != null)
                panelOpenUp.LoadCaveFileForTrial(currentTrialNumber);
            if (parameterManager != null)
                currentTrialData = parameterManager.LoadAndApplyTrialParameters(currentTrialNumber);
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
        _startingNext = false;
    }
    
 
    private void EndTrialAndShowPanel(float finalOxygen, bool completed)
    {
        currentTrialData.finalOxygenRemaining = finalOxygen;
        currentTrialData.completed = completed;
        
        if (completed)
            PlayTrialCompletionSound();
        
        if (completed && parameterManager != null)
        {
            bool csvSaved = parameterManager.SaveTrialResultToCSV(currentTrialData);
            if (!csvSaved)
                Debug.LogError($"Failed to save trial {currentTrialData.trialId} results to CSV!");
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
        
        // Close trial panel without showing main (UpdateCompletionUI will show it)
        if (uiController != null)
            uiController.CloseTrialControlPanel(false);
        
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    
    private void RestoreOriginalGameState()
    {
        ReenableFinishers();
        
        if (panelOpenUp != null)
            panelOpenUp.RestoreOriginalCaveFile();
        
        currentTrialNumber = 0;
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
