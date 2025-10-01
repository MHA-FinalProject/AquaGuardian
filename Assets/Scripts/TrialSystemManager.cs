using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/**
 * TrialSystemManager - Manages the trial system lifecycle
 * Handles trial flow: Start → Run → End → Continue → Complete
 * Extracted from PanelOpenUp.cs for better code organization
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
    
    // Trial state
    private bool trialsMode = false;
    private int currentTrialNumber = 0;
    private PanelOpenUp.TrialData currentTrialData;
    private float trialStartTime;
    private bool _startingNext = false;
    private List<Collider> _disabledFinishers = new List<Collider>();
    
    // Public getters
    public bool TrialsMode => trialsMode;
    public int CurrentTrialNumber => currentTrialNumber;
    public int TotalTrials => totalTrials;
    public PanelOpenUp.TrialData CurrentTrialData => currentTrialData;
    
    void Start()
    {
        Debug.Log("TrialSystemManager initialized");
        
        // Subscribe to game events
        GameStateManager.OnGameEnded += OnGameEnded;
    }
    
    void OnDestroy()
    {
        GameStateManager.OnGameEnded -= OnGameEnded;
    }
    
    /// <summary>
    /// Start the trial system
    /// </summary>
    public void StartTrials()
    {
        Debug.Log("=== STARTING TRIAL SYSTEM ===");
        
        Debug.Log($"BEFORE: trialsMode = {trialsMode}");
        trialsMode = true;
        Debug.Log($"AFTER: trialsMode = {trialsMode}, TrialsMode property = {TrialsMode}");
        currentTrialNumber = 0;
        
        // Set trials mode in GameStateManager
        Debug.Log("Setting GameStateManager.SetTrialsActive(true)");
        GameStateManager.SetTrialsActive(true);
        Debug.Log($"GameStateManager.AreTrialsActive is now: {GameStateManager.AreTrialsActive}");
        
        // Disable all existing finishers to prevent scene transitions during trials
        DisableAllFinishers();
        
        // Close trial control panel and start first trial
        if (uiController != null)
        {
            uiController.CloseTrialControlPanel();
        }
        
        // Use CoroutineHost to ensure coroutine runs even if this GameObject becomes inactive
        if (CoroutineHost.Instance != null)
        {
            Debug.Log("Using CoroutineHost to start first trial");
            CoroutineHost.Instance.StartCoroutine(StartNextTrialCoroutine());
        }
        else
        {
            Debug.Log("CoroutineHost not available, using this GameObject for first trial");
            StartCoroutine(StartNextTrialCoroutine());
        }
    }
    
    /// <summary>
    /// Continue to next trial
    /// </summary>
    public void ContinueToNextTrial()
    {
        Debug.Log($"=== CONTINUE TO NEXT TRIAL ===");
        Debug.Log($"Current: {currentTrialNumber}/{totalTrials}");
        Debug.Log($"trialsMode = {trialsMode}, TrialsMode property = {TrialsMode}");
        
        if (_startingNext) 
        {
            Debug.LogWarning("Continue already in progress, ignoring click");
            return;
        }

        if (currentTrialNumber >= totalTrials)
        {
            Debug.Log("All trials completed - finishing");
            if (uiController != null)
            {
                uiController.CloseTrialControlPanel();
            }
            CompleteAllTrials();
            return;
        }

        StartNextTrialExplicit();
    }
    
    /// <summary>
    /// Called when player reaches the trial fish
    /// </summary>
    public void OnTrialFishReached(float finalOxygen, bool completed)
    {
        Debug.Log("Player reached trial fish!");
        
        if (!trialsMode) 
        {
            Debug.LogError("NOT IN TRIAL MODE - ignoring fish reach. Click Trial → Start first.");
            return;
        }
        
        if (currentTrialData == null)
            currentTrialData = new PanelOpenUp.TrialData { trialId = currentTrialNumber };

        EndTrialAndShowPanel(finalOxygen, completed);
    }
    
    /// <summary>
    /// Complete all trials and restore normal game state
    /// </summary>
    public void CompleteAllTrials()
    {
        Debug.Log($"=== COMPLETING ALL TRIALS ===");
        Debug.Log($"Trials completed: {currentTrialNumber}/{totalTrials}");
        
        // Exit trials mode
        ExitTrialsMode();
        
        // Clean up trial objects
        if (systemResetter != null)
        {
            systemResetter.CleanupAllTrialObjects();
        }
        
        // Restore original game state
        RestoreOriginalGameState();
        
        // Update UI
        if (uiController != null)
        {
            uiController.UpdateCompletionUI();
        }
        
        Debug.Log("=== ALL TRIALS COMPLETED SUCCESSFULLY ===");
    }
    
    // ========== PRIVATE METHODS ==========
    
    /// <summary>
    /// Called when a game ends during trials
    /// </summary>
    private void OnGameEnded(float finalOxygen, bool completed)
    {
        Debug.Log($"Game ended: oxygen={finalOxygen:F1}%, completed={completed}");
        
        if (!trialsMode) return;
        
        Debug.Log("TRIAL MODE: Handling game end in OnGameEnded");
        
        if (currentTrialData == null)
            currentTrialData = new PanelOpenUp.TrialData { trialId = currentTrialNumber };
        
        EndTrialAndShowPanel(finalOxygen, completed);
    }
    
    /// <summary>
    /// Start next trial with explicit flow
    /// </summary>
    private void StartNextTrialExplicit()
    {
        if (CoroutineHost.Instance != null)
        {
            CoroutineHost.Instance.StartCoroutine(StartNextTrialCoroutine());
        }
        else
        {
            StartCoroutine(StartNextTrialCoroutine());
        }
    }
    
    /// <summary>
    /// Trial coroutine with step-by-step flow
    /// </summary>
    private IEnumerator StartNextTrialCoroutine()
    {
        _startingNext = true;
        Debug.Log($"=== STARTING TRIAL COROUTINE ===");
        Debug.Log($"TrialSystemManager.trialsMode = {trialsMode}");

        // STEP 1: Close trial control panel
        Debug.Log("STEP 1: Closing trial control panel");
        if (uiController != null)
        {
            uiController.CloseTrialControlPanel();
        }

        // STEP 2: Unfreeze time and disable finishers
        Debug.Log("STEP 2: Unfreezing time and disabling finishers");
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        DisableAllFinishers();

        // STEP 3: Clean up previous trial
        Debug.Log("STEP 3: Cleaning up previous trial");
        if (systemResetter != null)
        {
            systemResetter.CleanupSpawned();
        }
        if (panelOpenUp != null)
        {
            panelOpenUp.caveInfos.Clear();
        }

        // STEP 4: Increment trial number
        currentTrialNumber++;
        Debug.Log($"STEP 4: Starting trial {currentTrialNumber}/{totalTrials}");

        // STEP 5: Reset player
        Debug.Log("STEP 5: Resetting player to start position");
        if (panelOpenUp != null)
        {
            panelOpenUp.ResetPlayerToStartPosition();
        }

        // STEP 6: Reset game systems
        Debug.Log("STEP 6: Resetting all game systems");
        if (panelOpenUp != null)
        {
            panelOpenUp.ResetGameSystemsForTrial();
        }

        // STEP 7: Load cave file and parameters
        try
        {
            if (panelOpenUp != null)
            {
                panelOpenUp.LoadCaveFileForTrial(currentTrialNumber);
            }
            if (parameterManager != null)
            {
                currentTrialData = parameterManager.LoadAndApplyTrialParameters(currentTrialNumber);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error loading trial data: {e.Message}");
            _startingNext = false;
            yield break;
        }

        yield return null;

        // STEP 8: Prepare components
        if (systemResetter != null)
        {
            systemResetter.PrepareComponentsForClosePanel();
        }

        // STEP 9: Rebuild caves and create fish
        try
        {
            Debug.Log($"STEP 9: About to call ClosePanel - trialsMode={trialsMode}, TrialsMode property={TrialsMode}");
            if (panelOpenUp != null)
            {
                panelOpenUp.ClosePanel();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error rebuilding caves: {e.Message}");
            _startingNext = false;
            yield break;
        }

        yield return new WaitForFixedUpdate();

        // STEP 10: Update UI
        if (uiController != null)
        {
            uiController.UpdateTrialUI(currentTrialNumber, totalTrials);
        }

        // STEP 11: Wait for setup to complete
        yield return new WaitForSeconds(2f);
        
        // STEP 12: Notify GameStateManager
        GameStateManager.Instance?.NotifyPanelClosed();

        Debug.Log($"=== TRIAL {currentTrialNumber} SETUP COMPLETE ===");
        Debug.Log($"END OF COROUTINE: trialsMode = {trialsMode}, TrialsMode property = {TrialsMode}");
        _startingNext = false;
    }
    
 
    private void EndTrialAndShowPanel(float finalOxygen, bool completed)
    {
        currentTrialData.finalOxygenRemaining = finalOxygen;
        currentTrialData.completed = completed;
        
        // Save result
        if (parameterManager != null)
        {
            bool csvSaved = parameterManager.SaveTrialResultToCSV(currentTrialData);
            if (!csvSaved)
            {
                Debug.LogError($"Failed to save trial {currentTrialData.trialId} results to CSV!");
            }
        }

        // Prepare for next trial
        PrepareForNextTrial();

        // Update UI
        if (uiController != null)
        {
            uiController.UpdateTrialResults(finalOxygen, completed, currentTrialNumber, totalTrials);
            uiController.UpdateTrialControlText(trialsMode, currentTrialNumber, totalTrials);
            uiController.OpenTrialControlPanel();
        }
    }
    

    private void PrepareForNextTrial()
    {
        Debug.Log("=== PREPARING FOR NEXT TRIAL ===");
        
        if (systemResetter != null)
        {
            systemResetter.PrepareForNextTrial();
        }

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        

    }
    
    private void ExitTrialsMode()
    {
      
        
   
        trialsMode = false;
        Debug.Log($"AFTER EXIT: trialsMode = {trialsMode}, TrialsMode property = {TrialsMode}");
        GameStateManager.SetTrialsActive(false);
        
        if (uiController != null)
        {
            uiController.CloseTrialControlPanel();
        }
        
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
       
    }
    

    private void RestoreOriginalGameState()
    {
       
        // Re-enable finishers
        ReenableFinishers();
        
        // Restore original cave file
        if (panelOpenUp != null)
        {
            panelOpenUp.RestoreOriginalCaveFile();
        }
        
        currentTrialNumber = 0;
        
        Debug.Log("Original game state restored");
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
                Debug.Log($"Disabled finisher: {finisher.name}");
            }
        }
    }
    
   
    private void ReenableFinishers()
    {
        Debug.Log($"Re-enabling {_disabledFinishers.Count} finishers");
        foreach (var collider in _disabledFinishers)
        {
            if (collider != null)
            {
                collider.enabled = true;
                Debug.Log($"Re-enabled finisher: {collider.name}");
            }
        }
        _disabledFinishers.Clear();
    }
}
