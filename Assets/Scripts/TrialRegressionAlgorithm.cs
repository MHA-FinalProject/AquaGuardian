using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Globalization;
using System;

/// <summary>
/// Regression analysis algorithm for trial data
/// Performs correlation analysis and generates recommendations
/// Separated from UI logic for better code organization
/// </summary>
public class TrialRegressionAlgorithm
{
    /// <summary>
    /// Result of regression analysis
    /// </summary>
    public class RegressionResult
    {
        public string summaryText;
        public Dictionary<string, float> correlations;
        public float averageOxygen;
        public int perfectTrials;
        public int failedTrials;
        public int totalTrials;
        public List<TrialDataModels.TrialData> analyzedTrials;
    }
    
    /// <summary>
    /// Load trial data from TrialDataCache (primary method)
    /// Falls back to CSV if cache is empty
    /// </summary>
    public static List<TrialDataModels.TrialData> LoadTrialDataFromCache()
    {
        var trialDataList = new List<TrialDataModels.TrialData>();
        
        try
        {
            // Get latest run oxygen values from cache
            var latestOxygenValues = TrialDataCache.Instance.GetLatestRunOxygenValues();
            
            if (latestOxygenValues == null || latestOxygenValues.Count() < 5)
            {
                Debug.LogWarning($"Incomplete cached trial data ({latestOxygenValues?.Count() ?? 0}/5 trials) - trying CSV fallback...");
                return LoadTrialDataFromCSV();
            }
            
            Debug.Log($"Loading data from CACHE (no CSV reading!)");
            Debug.Log($"Found {latestOxygenValues.Count()} cached oxygen values");
            
            // We still need parameters from CSV (speed, lifeTime, etc.)
            // But we use CACHED oxygen values!
            string csvPath = Path.Combine(Application.dataPath, "Data", "Trial_5_runs_.csv");
            
            if (!File.Exists(csvPath))
            {
                Debug.LogError($"Trial_5_runs_.csv not found at: {csvPath}");
                return trialDataList;
            }
            
            string csvText = File.ReadAllText(csvPath);
            string[] lines = csvText.Split(new[] { "\r\n", "\r", "\n" }, System.StringSplitOptions.RemoveEmptyEntries);
            
            if (lines.Length <= 1) return trialDataList;
            
            // Load parameters from CSV but use CACHED oxygen values
            for (int i = 1; i < lines.Length && i <= latestOxygenValues.Count(); i++)
            {
                string[] fields = lines[i].Split(',');
                if (fields.Length < 10) continue;
                
                float cachedOxygen = latestOxygenValues[i - 1];
                
                var trialData = new TrialDataModels.TrialData
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
                    finalOxygenRemaining = cachedOxygen, // FROM CACHE!
                    completed = cachedOxygen > 0
                };
                
                trialDataList.Add(trialData);
                Debug.Log($"  Trial {trialData.trialId}: Oxygen={cachedOxygen:F1}% (from cache)");
            }
            
            Debug.Log($"Loaded {trialDataList.Count} trials from cache");
            return trialDataList;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error loading from cache: {e.Message}");
            return trialDataList;
        }
    }
    
    /// <summary>
    /// Load trial data directly from CSV (fallback method)
    /// Reads the last non-empty o2_runX column for each trial
    /// </summary>
    public static List<TrialDataModels.TrialData> LoadTrialDataFromCSV()
    {
        var trialDataList = new List<TrialDataModels.TrialData>();
        
        try
        {
            string csvPath = Path.Combine(Application.dataPath, "Data", "Trial_5_runs_.csv");
            
            if (!File.Exists(csvPath))
            {
                Debug.LogError($"Trial_5_runs_.csv not found at: {csvPath}");
                return trialDataList;
            }
            
            string csvText = File.ReadAllText(csvPath);
            Debug.Log($"Reading FRESH data from disk: {csvPath}");
            
            string[] lines = csvText.Split(new[] { "\r\n", "\r", "\n" }, System.StringSplitOptions.RemoveEmptyEntries);
            
            if (lines.Length <= 1) return trialDataList;
            
            for (int i = 1; i < lines.Length; i++)
            {
                string[] fields = lines[i].Split(',');
                if (fields.Length < 12) continue; // Need at least o2_run1 (column 11)
                
                float finalOxygen = 0f;
                bool foundValue = false;
                
                // Search backwards from last column (o2_run10) to first (o2_run1)
                for (int col = 20; col >= 11; col--) // Columns 11-20 are o2_run1 to o2_run10
                {
                    if (col < fields.Length)
                    {
                        string val = fields[col].Trim();
                        if (!string.IsNullOrEmpty(val) && float.TryParse(val, out finalOxygen))
                        {
                            foundValue = true;
                            Debug.Log($"Trial {i}: Using column {col} (o2_run{col-10}) = {finalOxygen}%");
                            break; // Found the last non-empty value
                        }
                    }
                }
                
                if (!foundValue) continue; // Skip this trial if no oxygen value found
                
                var trialData = new TrialDataModels.TrialData
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
                    completed = finalOxygen > 0
                };
                
                trialDataList.Add(trialData);
            }
            
            Debug.Log($"Loaded {trialDataList.Count} trials from CSV");
            return trialDataList;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error loading CSV: {e.Message}");
            return trialDataList;
        }
    }
    
    /// <summary>
    /// Perform regression analysis on trial data
    /// Returns formatted text results and correlation data
    /// </summary>
    public static RegressionResult PerformRegressionAnalysis(List<TrialDataModels.TrialData> allTrialData)
    {
        if (allTrialData == null || allTrialData.Count == 0)
        {
            return new RegressionResult
            {
                summaryText = "ERROR: No trial data available",
                correlations = new Dictionary<string, float>(),
                totalTrials = 0
            };
        }
        
        var result = new RegressionResult
        {
            correlations = new Dictionary<string, float>(),
            analyzedTrials = new List<TrialDataModels.TrialData>(allTrialData)
        };
        
        // Calculate correlations
        float[] outputs = allTrialData.Select(t => t.finalOxygenRemaining).ToArray();
        
        result.correlations["Speed"] = CalculateCorrelation(
            allTrialData.Select(t => t.speed).ToArray(), outputs);
        result.correlations["VerticalSpeed"] = CalculateCorrelation(
            allTrialData.Select(t => t.verticalSpeed).ToArray(), outputs);
        result.correlations["IdleUpwardSpeed"] = CalculateCorrelation(
            allTrialData.Select(t => t.idleUpwardSpeed).ToArray(), outputs);
        result.correlations["LifeTime"] = CalculateCorrelation(
            allTrialData.Select(t => t.lifeTime).ToArray(), outputs);
        result.correlations["O2DropPerSec"] = CalculateCorrelation(
            allTrialData.Select(t => t.downHealthPairSec).ToArray(), outputs);
        result.correlations["CollisionDamage"] = CalculateCorrelation(
            allTrialData.Select(t => t.removeHealthWithCollide).ToArray(), outputs);
        result.correlations["TimeBetweenCollides"] = CalculateCorrelation(
            allTrialData.Select(t => t.timeBetweenCollides).ToArray(), outputs);
        result.correlations["HealPoints"] = CalculateCorrelation(
            allTrialData.Select(t => t.healHealthPoint).ToArray(), outputs);
        result.correlations["FactorForce"] = CalculateCorrelation(
            allTrialData.Select(t => t.factorForce).ToArray(), outputs);
        
        // Calculate statistics
        float totalOxygen = 0f;
        int perfectTrials = 0;
        int failedTrials = 0;
        
        foreach (var trial in allTrialData)
        {
            totalOxygen += trial.finalOxygenRemaining;
            if (trial.finalOxygenRemaining <= 5f && trial.finalOxygenRemaining > 0f)
                perfectTrials++;
            if (trial.finalOxygenRemaining <= 0f)
                failedTrials++;
        }
        
        result.totalTrials = allTrialData.Count;
        result.averageOxygen = totalOxygen / allTrialData.Count;
        result.perfectTrials = perfectTrials;
        result.failedTrials = failedTrials;
        
        // Generate summary text
        string summaryText = "REGRESSION ANALYSIS\n";
        summaryText += $"Trials:{result.totalTrials} Avg:{result.averageOxygen:F1}% Perfect:{perfectTrials} Failed:{failedTrials}\n";
        summaryText += "TOP CORRELATIONS:\n";
        
        var top3 = result.correlations.OrderByDescending(x => Mathf.Abs(x.Value)).Take(3);
        foreach (var corr in top3)
        {
            string sign = corr.Value > 0 ? "+" : "";
            summaryText += $"{sign}{corr.Value:F2} {corr.Key}\n";
        }
        
        summaryText += "\nRECOMMENDATIONS:\n";
        
        var mostPositive = result.correlations.OrderByDescending(x => x.Value).First();
        var mostNegative = result.correlations.OrderBy(x => x.Value).First();
        
        if (Mathf.Abs(mostPositive.Value) > 0.3f)
        {
            summaryText += $"INCREASE {mostPositive.Key}\n";
        }
        if (Mathf.Abs(mostNegative.Value) > 0.3f)
        {
            summaryText += $"DECREASE {mostNegative.Key}\n";
        }
        
        result.summaryText = summaryText;
        
        Debug.Log(summaryText);
        return result;
    }
    
    /// <summary>
    /// Calculate Pearson correlation coefficient between two arrays
    /// Returns value between -1 (negative correlation) and +1 (positive correlation)
    /// </summary>
    public static float CalculateCorrelation(float[] x, float[] y)
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
    /// Save regression results to text file
    /// </summary>
    public static bool SaveRegressionResultsToFile(RegressionResult result, string saveFolder = "RegressionResults")
    {
        if (result == null || string.IsNullOrEmpty(result.summaryText))
        {
            Debug.LogWarning("No results to save!");
            return false;
        }
        
        try
        {
            string dataPath = Path.Combine(Application.dataPath, "Data");
            string savePath = Path.Combine(dataPath, saveFolder);
            
            if (!Directory.Exists(savePath))
                Directory.CreateDirectory(savePath);
            
            string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string fileName = $"RegressionAnalysis_{timestamp}.txt";
            string fullPath = Path.Combine(savePath, fileName);
            
            string fileContent = "=====================================\n";
            fileContent += "REGRESSION ANALYSIS\n";
            fileContent += "=====================================\n";
            fileContent += $"Generated: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\n";
            fileContent += $"Trials analyzed: {result.totalTrials}\n";
            fileContent += "=====================================\n\n";
            fileContent += result.summaryText;
            fileContent += "\n\n=====================================\n";
            fileContent += "RAW TRIAL DATA:\n";
            
            foreach (var trial in result.analyzedTrials)
            {
                fileContent += $"\nTrial {trial.trialId}:\n";
                fileContent += $"  Speed: {trial.speed:F2}\n";
                fileContent += $"  VerticalSpeed: {trial.verticalSpeed:F2}\n";
                fileContent += $"  IdleUpwardSpeed: {trial.idleUpwardSpeed:F2}\n";
                fileContent += $"  LifeTime: {trial.lifeTime:F2}\n";
                fileContent += $"  O2DropPerSec: {trial.downHealthPairSec:F2}\n";
                fileContent += $"  CollisionDamage: {trial.removeHealthWithCollide:F2}\n";
                fileContent += $"  TimeBetweenCollides: {trial.timeBetweenCollides:F2}\n";
                fileContent += $"  HealPoints: {trial.healHealthPoint:F2}\n";
                fileContent += $"  FactorForce: {trial.factorForce:F2}\n";
                fileContent += $"  FinalO2: {trial.finalOxygenRemaining:F1}%\n";
            }
            
            File.WriteAllText(fullPath, fileContent);
          //  Debug.Log($"Results saved: {fullPath}");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Save failed: {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Train ML model and predict optimal parameters
    /// Uses Multiple Linear Regression on trial data
    /// </summary>
    public static TrialDataModels.TrialData PredictOptimalParameters(
        List<TrialDataModels.TrialData> trials,
        float targetOxygen = 2.5f)
    {
        if (trials == null || trials.Count < 3)
        {
            Debug.LogError("Need at least 3 trials for ML prediction");
            return null;
        }
        
        Debug.Log("=== MACHINE LEARNING PREDICTION ===");
        
        // Create and train predictor
        var predictor = new OxygenPredictor();
        bool trained = predictor.TrainModel(trials);
        
        if (!trained)
        {
            Debug.LogError("Failed to train ML model");
            return null;
        }
        
        // Find optimal parameters
        var optimalParams = predictor.FindOptimalParameters(targetOxygen);
        
        if (optimalParams != null)
        {
           
            Debug.Log($"Target oxygen: {targetOxygen}%");
            Debug.Log($"Predicted oxygen: {predictor.PredictOxygen(optimalParams):F2}%");
            Debug.Log($"Speed: {optimalParams.speed:F2}");
            Debug.Log($"Vertical Speed: {optimalParams.verticalSpeed:F2}");
            Debug.Log($"O2 Drop/sec: {optimalParams.downHealthPairSec:F2}");
        }
        
        return optimalParams;
    }
}


