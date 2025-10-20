using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Singleton cache for trial oxygen data - NO CSV READING NEEDED!
/// Data is stored directly when trials complete
/// </summary>
public class TrialDataCache : MonoBehaviour
{
    private static TrialDataCache _instance;
    
    public static TrialDataCache Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("TrialDataCache");
                _instance = go.AddComponent<TrialDataCache>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }
    
    /// <summary>
    /// Current run's oxygen values: [trialId] = oxygen%
    /// </summary>
    private Dictionary<int, float> currentRunOxygenValues = new Dictionary<int, float>();
    
    /// <summary>
    /// All historical runs: each list contains 5 oxygen values (one per trial)
    /// </summary>
    private List<List<float>> allRunsHistory = new List<List<float>>();
    
    /// <summary>
    /// Current run number (increments when trial 1 starts)
    /// </summary>
    private int currentRunNumber = 0;
    
    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }
    
    /// <summary>
    /// Called when a trial completes - stores oxygen value
    /// </summary>
    public void SaveTrialOxygen(int trialId, float oxygenRemaining)
    {
        // If this is trial 1, we're starting a new run
        if (trialId == 1)
        {
            // Save previous run to history if it had data
            if (currentRunOxygenValues.Count > 0)
            {
                // CRITICAL: Always save exactly 5 values to maintain index alignment
                var runData = new List<float>();
                for (int i = 1; i <= 5; i++)
                {
                    runData.Add(currentRunOxygenValues.ContainsKey(i) ? currentRunOxygenValues[i] : 0f);
                }
                allRunsHistory.Add(runData);
                Debug.Log($" Saved Run {allRunsHistory.Count} to history: {string.Join(", ", runData.Select(v => v.ToString("F1")))}");
            }
            
            // Clear for new run
            currentRunOxygenValues.Clear();
            currentRunNumber++;
            Debug.Log($"=== STARTING RUN {currentRunNumber} ===");
        }
        
        // Store this trial's oxygen
        currentRunOxygenValues[trialId] = oxygenRemaining;
        Debug.Log($" Cached Trial {trialId}: Oxygen={oxygenRemaining:F1}%");
        
        // If this is trial 5, finalize the run
        if (trialId == 5)
        {
            // CRITICAL: Always save exactly 5 values to maintain index alignment
            var runData = new List<float>();
            for (int i = 1; i <= 5; i++)
            {
                runData.Add(currentRunOxygenValues.ContainsKey(i) ? currentRunOxygenValues[i] : 0f);
            }
            
            allRunsHistory.Add(runData);
           
            Debug.Log($"    Values: {string.Join(", ", runData.Select(v => v.ToString("F1") + "%"))}");
            Debug.Log($"    Average: {runData.Average():F1}%");
        }
    }
    
    /// <summary>
    /// Get the LATEST complete run's oxygen values (for regression)
    /// Returns exactly 5 values (0 for missing trials to maintain index alignment)
    /// </summary>
    public List<float> GetLatestRunOxygenValues()
    {
        if (allRunsHistory.Count > 0)
        {
            return allRunsHistory[allRunsHistory.Count - 1];
        }
        
        // If no complete run yet, return current run data (even if incomplete)
        // CRITICAL: Always return 5 values to match trial IDs (1-5)
        var currentData = new List<float>();
        for (int i = 1; i <= 5; i++)
        {
            // Add 0 for missing trials to maintain index alignment
            currentData.Add(currentRunOxygenValues.ContainsKey(i) ? currentRunOxygenValues[i] : 0f);
        }
        return currentData;
    }
    
    /// <summary>
    /// Get the latest oxygen value for a specific trial
    /// </summary>
    public float GetLatestTrialOxygen(int trialId)
    {
        if (allRunsHistory.Count > 0)
        {
            var lastRun = allRunsHistory[allRunsHistory.Count - 1];
            if (trialId > 0 && trialId <= lastRun.Count)
                return lastRun[trialId - 1];
        }
        
        if (currentRunOxygenValues.ContainsKey(trialId))
            return currentRunOxygenValues[trialId];
        
        return 0f;
    }
    
    /// <summary>
    /// Get ALL historical run data (for O2_Wide_AllSets.csv)
    /// </summary>
    public List<List<float>> GetAllRunsHistory()
    {
        return new List<List<float>>(allRunsHistory);
    }
    
    /// <summary>
    /// Get total number of complete runs
    /// </summary>
    public int GetTotalRuns()
    {
        return allRunsHistory.Count;
    }
    
    /// <summary>
    /// Clear all cached data (for testing/reset)
    /// </summary>
    public void ClearCache()
    {
        currentRunOxygenValues.Clear();
        allRunsHistory.Clear();
        currentRunNumber = 0;
        
    }
    
    /// <summary>
    /// Debug: Print current cache state
    /// </summary>
    [ContextMenu("Print Cache State")]
    public void PrintCacheState()
    {
        
        Debug.Log($"Current Run: {currentRunNumber}");
        Debug.Log($"Total Complete Runs: {allRunsHistory.Count}");
        
       
        foreach (var kvp in currentRunOxygenValues.OrderBy(x => x.Key))
        {
            Debug.Log($"  Trial {kvp.Key}: {kvp.Value:F1}%");
        }
        
       
        for (int i = 0; i < allRunsHistory.Count; i++)
        {
            var run = allRunsHistory[i];
            Debug.Log($"  Run {i + 1}: {string.Join(", ", run.Select(v => v.ToString("F1") + "%"))} (Avg: {run.Average():F1}%)");
        }
        
        //Debug.Log("==============================");
    }
}


