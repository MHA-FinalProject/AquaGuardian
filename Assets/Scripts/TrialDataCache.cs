using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/** 
    * TrialDataCache
    * 
    * Caches oxygen values for trials across multiple runs to support regression analysis.
    * Stores data in memory and provides access to latest and historical run data.
    */
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
    
    private Dictionary<int, float> currentRunOxygenValues = new Dictionary<int, float>();
    
    private List<List<float>> allRunsHistory = new List<List<float>>();
    
    // Current run number (starts at 0)
    private int currentRunNumber = 0;

    // Maximum number of trials per run (configurable: 5 or 10)
    private const int MAX_TRIALS = 10;
    
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

    public void SaveTrialOxygen(int trialId, float oxygenRemaining)
    {
        if (trialId == 1)
        {
            if (currentRunOxygenValues.Count > 0)
            {
                var runData = new List<float>();
                for (int i = 1; i <= MAX_TRIALS; i++)
                {
                    runData.Add(currentRunOxygenValues.ContainsKey(i) ? currentRunOxygenValues[i] : 0f);
                }
                allRunsHistory.Add(runData);
            }
            
            currentRunOxygenValues.Clear();
            currentRunNumber++;
         
        }
        
        currentRunOxygenValues[trialId] = oxygenRemaining;
        Debug.Log($" Cached Trial {trialId}: Oxygen={oxygenRemaining:F1}%");
        
        if (trialId == MAX_TRIALS)
        {
            var runData = new List<float>();
            for (int i = 1; i <= MAX_TRIALS; i++)
            {
                runData.Add(currentRunOxygenValues.ContainsKey(i) ? currentRunOxygenValues[i] : 0f);
            }
            allRunsHistory.Add(runData);
        }
    }

    // Get the LATEST complete run's oxygen values (for regression)
    // Returns exactly 5 values (0 for missing trials to maintain index alignment)
    public List<float> GetLatestRunOxygenValues()
    {
        if (allRunsHistory.Count > 0)
        {
            return allRunsHistory[allRunsHistory.Count - 1];
        }
        
        var currentData = new List<float>();
        for (int i = 1; i <= 5; i++)
        {
            currentData.Add(currentRunOxygenValues.ContainsKey(i) ? currentRunOxygenValues[i] : 0f);
        }
        return currentData;
    }
    
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
    
    public List<List<float>> GetAllRunsHistory()
    {
        return new List<List<float>>(allRunsHistory);
    }
    
    public int GetTotalRuns()
    {
        return allRunsHistory.Count;
    }
    
    public void ClearCache()
    {
        currentRunOxygenValues.Clear();
        allRunsHistory.Clear();
        currentRunNumber = 0;
    }
    
    [ContextMenu("Print Cache State")]
    public void PrintCacheState()
    {
        foreach (var kvp in currentRunOxygenValues.OrderBy(x => x.Key))
        {
            // Debug.Log($"  Trial {kvp.Key}: {kvp.Value:F1}%");
        }
        
        for (int i = 0; i < allRunsHistory.Count; i++)
        {
            var run = allRunsHistory[i];
            Debug.Log($"  Run {i + 1}: {string.Join(", ", run.Select(v => v.ToString("F1") + "%"))} (Avg: {run.Average():F1}%)");
        }
    }
}
