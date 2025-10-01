using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Globalization;

/// <summary>
/// Automatic Playtesting for Game Parameter Tuning via Active Learning
/// Based on research by Zook et al. (2019): https://arxiv.org/abs/1908.01417
/// 
/// This system implements automated parameter tuning for AquaGuardian game:
/// - Runs 5 trial games with varying parameters
/// - Collects performance data (final oxygen remaining)
/// - Uses linear regression to identify parameter correlations
/// - Optimizes for target: oxygen > 0 but close to 0 (barely winning)
/// - Reduces the need for extensive manual playtesting
/// </summary>
public class TrialRegressionAnalyzer : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject regressionPanel;  // Panel to show regression results
    [SerializeField] private TMP_Text regressionResultsText;  // Text to display regression results
    [SerializeField] private Button calculateRegressionButton;  // Button to start regression calculation
    [SerializeField] private Button closeRegressionButton;  // Button to close regression panel
    
    [Header("Data Files")]
    [SerializeField] private TextAsset trialDataCSV;  // DEPRECATED: No longer used - reads directly from file
    
    [System.Serializable]
    public class TrialData
    {
        public int trialId;
        public float speed;
        public float verticalSpeed;
        public float idleUpwardSpeed;
        public float lifeTime;
        public float downHealthPairSec;
        public float removeHealthWithCollide;
        public float timeBetweenCollides;
        public float healHealthPoint;
        public float factorForce;
        public float finalOxygenRemaining;
        public bool completed;                 // Added to match PanelOpenUp
    }
    
    private List<TrialData> allTrialData = new List<TrialData>();
    
    void Start()
    {
        // Setup button events
        if (calculateRegressionButton != null)
        {
            calculateRegressionButton.onClick.AddListener(CalculateRegression);
        }
        
        if (closeRegressionButton != null)
        {
            closeRegressionButton.onClick.AddListener(CloseRegressionPanel);
        }
        
        // Hide regression panel initially
        if (regressionPanel != null)
        {
            regressionPanel.SetActive(false);
        }
    }
    
    /// <summary>
    /// Load trial data from CSV file and calculate regression
    /// </summary>
    public void CalculateRegression()
    {
        Debug.Log("=== STARTING REGRESSION ANALYSIS ===");
        
        if (LoadTrialDataFromCSV())
        {
            string regressionResults = PerformRegressionAnalysis();
            ShowRegressionResults(regressionResults);
        }
        else
        {
            ShowError("Failed to load trial data from CSV file!");
        }
    }
    
    /// <summary>
    /// Public method to calculate and show regression results (alias for compatibility)
    /// </summary>
    public void CalculateAndShowRegression()
    {
        CalculateRegression();
    }
    
    /// <summary>
    /// Load trial data from Trial_5_runs_.csv
    /// </summary>
    private bool LoadTrialDataFromCSV()
    {
        try
        {
            allTrialData.Clear();
            
            // Read directly from file to get updated data
            string csvPath = System.IO.Path.Combine(UnityEngine.Application.dataPath, "Data", "Trial_5_runs_.csv");
            Debug.Log($"TrialRegressionAnalyzer: Attempting to load CSV from: {csvPath}");
            
            if (!System.IO.File.Exists(csvPath))
            {
                Debug.LogError($"Trial CSV file not found at: {csvPath}");
                Debug.LogError("Please ensure Trial_5_runs_.csv exists in Assets/Data/ folder");
                return false;
            }
            
            Debug.Log("CSV file found, reading contents...");
            
            string csvText = System.IO.File.ReadAllText(csvPath);
            string[] lines = csvText.Split(new[] { "\r\n", "\r", "\n" }, System.StringSplitOptions.RemoveEmptyEntries);
            
            Debug.Log($"Loading trial data from: {csvPath}");
            
            if (lines.Length <= 1)
            {
                Debug.LogError("CSV file is empty or has no data rows!");
                return false;
            }
            
            // Parse data lines (skip header at index 0)
            for (int i = 1; i < lines.Length; i++)
            {
                string[] fields = lines[i].Split(',');
                
                if (fields.Length < 11)
                {
                    Debug.LogWarning($"CSV line {i} doesn't have enough fields, skipping...");
                    continue;
                }
                
                // Check if final oxygen is filled
                if (string.IsNullOrEmpty(fields[10]) || !float.TryParse(fields[10], out float finalOxygen))
                {
                    Debug.LogWarning($"Trial {i} missing final oxygen data, skipping...");
                    continue;
                }
                
                var trialData = new TrialData
                {
                    trialId = int.Parse(fields[0]),
                    speed = float.Parse(fields[1]),
                    verticalSpeed = float.Parse(fields[2]),
                    idleUpwardSpeed = float.Parse(fields[3]),
                    lifeTime = float.Parse(fields[4]),
                    downHealthPairSec = float.Parse(fields[5]),
                    removeHealthWithCollide = float.Parse(fields[6]),
                    timeBetweenCollides = float.Parse(fields[7]),
                    healHealthPoint = float.Parse(fields[8]),
                    factorForce = float.Parse(fields[9]),
                    finalOxygenRemaining = finalOxygen,
                    completed = finalOxygen > 0  // Assume completed if oxygen > 0
                };
                
                Debug.Log($"Parsed CSV row {i}: Trial {trialData.trialId}");
                Debug.Log($"  Speed: {trialData.speed}, VertSpeed: {trialData.verticalSpeed}, IdleUp: {trialData.idleUpwardSpeed}");
                Debug.Log($"  DropPerSec: {trialData.downHealthPairSec}, LifeTime: {trialData.lifeTime}");
                Debug.Log($"  CollDamage: {trialData.removeHealthWithCollide}, HealPoints: {trialData.healHealthPoint}");
                
                allTrialData.Add(trialData);
                Debug.Log($"Loaded trial {trialData.trialId}: Final O2 = {trialData.finalOxygenRemaining:F1}%, Speed={trialData.speed:F1}");
            }
            
            Debug.Log($"Successfully loaded {allTrialData.Count} trials for regression analysis");
            
            if (allTrialData.Count == 0)
            {
                Debug.LogError("No trial data loaded! All trials may be missing final oxygen values.");
                Debug.LogError("Make sure to complete at least 2 trials before running regression analysis.");
            }
            else if (allTrialData.Count < 2)
            {
                Debug.LogWarning($"Only {allTrialData.Count} trial loaded. Need at least 2 trials for meaningful regression analysis.");
            }
            else
            {
                Debug.Log("Sufficient trial data loaded for regression analysis.");
                
                // Show summary of loaded trials
                Debug.Log("=== LOADED TRIAL SUMMARY ===");
                foreach (var trial in allTrialData)
                {
                    Debug.Log($"Trial {trial.trialId}: Speed={trial.speed}, DropPerSec={trial.downHealthPairSec}, FinalO2={trial.finalOxygenRemaining}%");
                }
                Debug.Log("=============================");
            }
            
            return allTrialData.Count >= 2; // Need at least 2 trials for regression
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error loading trial data: {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Perform linear regression analysis
    /// </summary>
    private string PerformRegressionAnalysis()
    {
        if (allTrialData.Count < 2)
        {
            return "Need at least 2 completed trials for regression analysis!";
        }
        
        // Prepare output values
        float[] outputs = allTrialData.Select(t => t.finalOxygenRemaining).ToArray();
        
        // Calculate correlations for each feature
        var correlations = new Dictionary<string, float>();
        
        correlations["Speed"] = CalculateCorrelation(
            allTrialData.Select(t => t.speed).ToArray(), outputs);
            
        correlations["Vertical Speed"] = CalculateCorrelation(
            allTrialData.Select(t => t.verticalSpeed).ToArray(), outputs);
            
        correlations["Idle Upward Speed"] = CalculateCorrelation(
            allTrialData.Select(t => t.idleUpwardSpeed).ToArray(), outputs);
            
        correlations["Life Time"] = CalculateCorrelation(
            allTrialData.Select(t => t.lifeTime).ToArray(), outputs);
            
        correlations["Oxygen Drop Per Sec"] = CalculateCorrelation(
            allTrialData.Select(t => t.downHealthPairSec).ToArray(), outputs);
            
        correlations["Collision Damage"] = CalculateCorrelation(
            allTrialData.Select(t => t.removeHealthWithCollide).ToArray(), outputs);
            
        correlations["Time Between Collides"] = CalculateCorrelation(
            allTrialData.Select(t => t.timeBetweenCollides).ToArray(), outputs);
            
        correlations["Oxygen Heal Points"] = CalculateCorrelation(
            allTrialData.Select(t => t.healHealthPoint).ToArray(), outputs);
            
        correlations["Factor Force"] = CalculateCorrelation(
            allTrialData.Select(t => t.factorForce).ToArray(), outputs);
        
        // Build results string
        string results = "ACTIVE LEARNING REGRESSION ANALYSIS\n";
      
        results += $"Number of completed trials: {allTrialData.Count}\n\n";
        
        results += "TRIAL OXYGEN RESULTS:\n";
        results += "--------------------\n";
        float totalOxygen = 0f;
        int perfectTrials = 0;
        int failedTrials = 0;
        
        foreach (var trial in allTrialData)
        {
            string quality = GetOxygenQuality(trial.finalOxygenRemaining);
            results += $"Trial {trial.trialId}: {trial.finalOxygenRemaining:F1}% {quality}\n";
            totalOxygen += trial.finalOxygenRemaining;
            
            if (trial.finalOxygenRemaining <= 0) failedTrials++;
            else if (trial.finalOxygenRemaining <= 5) perfectTrials++;
        }
        
        float avgOxygen = totalOxygen / allTrialData.Count;
        results += $"\nAverage Final Oxygen: {avgOxygen:F1}%\n";
        results += $"Perfect Trials (≤5%): {perfectTrials}/{allTrialData.Count}\n";
        results += $"Failed Trials (0%): {failedTrials}/{allTrialData.Count}\n\n";
        
        results += "FEATURE CORRELATIONS:\n";
        results += "--------------------\n";
        foreach (var corr in correlations.OrderByDescending(x => x.Value))
        {
            string impact = corr.Value > 0 ? "HELPFUL" : "HARMFUL";
            string strength = Mathf.Abs(corr.Value) > 0.7f ? "STRONG" : 
                             Mathf.Abs(corr.Value) > 0.3f ? "MODERATE" : "WEAK";
            results += $"{corr.Key}: {corr.Value:F3} ({strength} {impact})\n";
        }
        
        results += "\nINTERPRETATION:\n";
        results += "---------------\n";
        results += "• Positive values = Parameter INCREASES final oxygen\n";
        results += "• Negative values = Parameter DECREASES final oxygen\n";
        results += "• Values closer to ±1 = STRONGER relationship\n\n";
        
        // Find most important parameters
        var mostPositive = correlations.OrderByDescending(x => x.Value).First();
        var mostNegative = correlations.OrderBy(x => x.Value).First();
        
      
        results += $"Most helpful: {mostPositive.Key} ({mostPositive.Value:F3})\n";
        results += $"Most harmful: {mostNegative.Key} ({mostNegative.Value:F3})\n\n";
        
       
     
        
        if (mostPositive.Value > 0.3f)
        {
            results += $"INCREASE {mostPositive.Key}\n   → Helps preserve more oxygen\n\n";
        }
        if (mostNegative.Value < -0.3f)
        {
            results += $" DECREASE {mostNegative.Key}\n   → Reduces oxygen waste\n\n";
        }
        
       
        if (mostPositive.Value > 0.3f)
        {
            results += $"INCREASE {mostPositive.Key} to preserve more oxygen\n\n";
        }
        if (mostNegative.Value < -0.3f)
        {
            results += $"DECREASE {mostNegative.Key} to reduce oxygen waste\n\n";
        }
        
        // Target optimization analysis
        results += "TARGET ANALYSIS:\n";
        results += "----------------\n";
        results += "GOAL: Final oxygen between 1-5% (close to zero but > 0)\n\n";
        
        if (failedTrials > 0)
        {
            results += $"{failedTrials} trials failed (0% oxygen)\n";
            results += "REDUCE difficulty (decrease drain, increase heal)\n\n";
        }
        
        if (avgOxygen > 15)
        {
            results += "Average oxygen too high - wasted efficiency\n";
            results += "INCREASE difficulty (increase drain, reduce heal)\n\n";
        }
        else if (avgOxygen >= 1 && avgOxygen <= 10)
        {
            results += "Excellent balance - near optimal difficulty\n\n";
        }
        else if (avgOxygen < 1 && failedTrials == 0)
        {
            results += "PERFECT! Very close to target zone\n\n";
        }
        
        return results;
    }
    
    /// <summary>
    /// Calculate Pearson correlation coefficient
    /// </summary>
    private float CalculateCorrelation(float[] x, float[] y)
    {
        if (x.Length != y.Length || x.Length == 0) return 0f;
        
        int n = x.Length;
        float sumX = x.Sum();
        float sumY = y.Sum();
        float sumXY = 0f;
        float sumX2 = 0f;
        float sumY2 = 0f;
        
        for (int i = 0; i < n; i++)
        {
            sumXY += x[i] * y[i];
            sumX2 += x[i] * x[i];
            sumY2 += y[i] * y[i];
        }
        
        float numerator = n * sumXY - sumX * sumY;
        float denominator = Mathf.Sqrt((n * sumX2 - sumX * sumX) * (n * sumY2 - sumY * sumY));
        
        return denominator == 0f ? 0f : numerator / denominator;
    }
    
    /// <summary>
    /// Show regression results in panel
    /// </summary>
    private void ShowRegressionResults(string results)
    {
        if (regressionPanel != null)
        {
            regressionPanel.SetActive(true);
        }
        
        if (regressionResultsText != null)
        {
            regressionResultsText.text = results;
        }
        
        Debug.Log("=== REGRESSION ANALYSIS COMPLETE ===");
        Debug.Log(results);
    }
    
    /// <summary>
    /// Show error message
    /// </summary>
    private void ShowError(string errorMessage)
    {
        if (regressionPanel != null)
        {
            regressionPanel.SetActive(true);
        }
        
        if (regressionResultsText != null)
        {
            regressionResultsText.text = $"ERROR:\n{errorMessage}\n\nMake sure all 5 trials are completed with final oxygen values!";
        }
        
        Debug.LogError(errorMessage);
    }
    
    /// <summary>
    /// Close regression panel
    /// </summary>
    public void CloseRegressionPanel()
    {
        if (regressionPanel != null)
        {
            regressionPanel.SetActive(false);
        }

        // Restore gameplay time and keep cursor always accessible
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    
    /// <summary>
    /// Get oxygen quality indicator for display
    /// </summary>
    private string GetOxygenQuality(float oxygen)
    {
        if (oxygen <= 0) return "FAILED";
       
        else if (oxygen <= 15) return "GOOD";
        else if (oxygen <= 30) return "OK";
        else return "TOO HIGH";
    }
    
    /// <summary>
    /// Public method to check if regression can be calculated
    /// </summary>
    public bool CanCalculateRegression()
    {
        return LoadTrialDataFromCSV() && allTrialData.Count >= 2;
    }
}

