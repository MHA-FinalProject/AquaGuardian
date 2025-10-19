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
        public string summaryText; // Short version for UI display
        public string fullDetailsText; // Full version for file export
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
    /// Randomly selects 5 different trials instead of taking first 5
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
            
            // Count available trials (lines with valid data)
            int availableTrials = Mathf.Min(lines.Length - 1, latestOxygenValues.Count());
            int trialsToSelect = Mathf.Min(5, availableTrials);
            
            // Select 5 random DIFFERENT trial indices
            List<int> selectedIndices = SelectRandomIndices(availableTrials, trialsToSelect);
            Debug.Log($"Randomly selected {trialsToSelect} trials: [{string.Join(", ", selectedIndices.Select(x => x + 1))}]");
            
            // Load parameters from CSV but use CACHED oxygen values
            foreach (int idx in selectedIndices)
            {
                int lineIndex = idx + 1; // +1 to skip header
                string[] fields = lines[lineIndex].Split(',');
                if (fields.Length < 10) continue;
                
                float cachedOxygen = latestOxygenValues[idx];
                
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
            
            Debug.Log($"Loaded {trialDataList.Count} random trials from cache");
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
    /// Randomly selects 5 different trials instead of taking all
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
            
            // First pass: collect all valid trials
            List<(int lineIndex, TrialDataModels.TrialData data)> allValidTrials = new List<(int, TrialDataModels.TrialData)>();
            
            for (int i = 1; i < lines.Length; i++)
            {
                string[] fields = lines[i].Split(',');
                if (fields.Length < 12) continue; // Need at least o2_run1 (column 11)
                
                float finalOxygen = 0f;
                bool foundValue = false;
                
                // Search backwards from last column (o2_run30) to first (o2_run1)
                for (int col = Mathf.Min(40, fields.Length - 1); col >= 10; col--) // o2_run1 starts at column 10
                {
                    if (col < fields.Length)
                    {
                        string val = fields[col].Trim();
                        if (!string.IsNullOrEmpty(val) && float.TryParse(val, out finalOxygen))
                        {
                            foundValue = true;
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
                
                allValidTrials.Add((i, trialData));
            }
            
            // Second pass: randomly select 5 different trials
            int availableTrials = allValidTrials.Count;
            int trialsToSelect = Mathf.Min(5, availableTrials);
            
            List<int> selectedIndices = SelectRandomIndices(availableTrials, trialsToSelect);
            Debug.Log($"Randomly selected {trialsToSelect} trials from CSV: [{string.Join(", ", selectedIndices.Select(i => allValidTrials[i].data.trialId))}]");
            
            foreach (int idx in selectedIndices)
            {
                var selectedTrial = allValidTrials[idx].data;
                trialDataList.Add(selectedTrial);
                Debug.Log($"  Trial {selectedTrial.trialId}: Oxygen={selectedTrial.finalOxygenRemaining:F1}%");
            }
            
            Debug.Log($"Loaded {trialDataList.Count} random trials from CSV");
            return trialDataList;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error loading CSV: {e.Message}");
            return trialDataList;
        }
    }
    
    /// <summary>
    /// Select N random different indices from range [0, maxIndex)
    /// Ensures no duplicates
    /// </summary>
    private static List<int> SelectRandomIndices(int maxIndex, int count)
    {
        if (count > maxIndex)
            count = maxIndex;
        
        // Create list of all possible indices
        List<int> allIndices = new List<int>();
        for (int i = 0; i < maxIndex; i++)
            allIndices.Add(i);
        
        // Shuffle using Fisher-Yates algorithm
        System.Random rng = new System.Random();
        for (int i = allIndices.Count - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            int temp = allIndices[i];
            allIndices[i] = allIndices[j];
            allIndices[j] = temp;
        }
        
        // Take first N indices (they're already shuffled)
        List<int> selected = allIndices.Take(count).ToList();
        selected.Sort(); // Sort for cleaner output
        
        return selected;
    }
    
    /// <summary>
    /// Perform ML regression analysis using Multiple Linear Regression
    /// Returns formatted text results with predictions and model metrics
    /// </summary>
    public static RegressionResult PerformRegressionAnalysis(List<TrialDataModels.TrialData> allTrialData)
    {
        if (allTrialData == null || allTrialData.Count < 3)
        {
            string errorMsg = $"ERROR: Need at least 3 trials for ML analysis\nFound: {allTrialData?.Count ?? 0} trials";
            return new RegressionResult
            {
                summaryText = errorMsg,
                fullDetailsText = errorMsg,
                correlations = new Dictionary<string, float>(),
                totalTrials = allTrialData?.Count ?? 0
            };
        }
        
        Debug.Log("=== MULTIPLE LINEAR REGRESSION ANALYSIS ===");
        
        var result = new RegressionResult
        {
            correlations = new Dictionary<string, float>(),
            analyzedTrials = new List<TrialDataModels.TrialData>(allTrialData),
            totalTrials = allTrialData.Count
        };
        
        // Calculate basic statistics
        float totalOxygen = 0f;
        int perfectTrials = 0;
        int failedTrials = 0;
        
        foreach (var trial in allTrialData)
        {
            totalOxygen += trial.finalOxygenRemaining;
            // Perfect = within ±2.5% of target (5% ± 2.5% = 2.5% - 7.5%)
            if (trial.finalOxygenRemaining >= 2.5f && trial.finalOxygenRemaining <= 7.5f)
                perfectTrials++;
            if (trial.finalOxygenRemaining <= 0f)
                failedTrials++;
        }
        
        result.averageOxygen = totalOxygen / allTrialData.Count;
        result.perfectTrials = perfectTrials;
        result.failedTrials = failedTrials;
        
        // Create and train ML predictor
        var predictor = new OxygenPredictor();
        predictor.topKFeatures = 4; // Use top 4 features for small datasets
        bool trained = predictor.TrainModel(allTrialData, enableFeatureSelection: true);
        
        if (!trained)
        {
            string errorMsg = "ERROR: Failed to train ML model\nNot enough variance in data";
            result.summaryText = errorMsg;
            result.fullDetailsText = errorMsg;
            return result;
        }
        
        // Get model (trained by predictor)
        var model = predictor.GetModel();
        
        // Perform K-Fold CV for validation
        var (X, y) = BuildFeatureMatrix(allTrialData);
        int kFolds = Mathf.Min(5, Mathf.Max(2, allTrialData.Count));
        var (cvRmse, cvMae, cvR2) = model.KFoldCV(X, y, kFolds);
        
        // Interpret R2
        string quality = cvR2 > 0.7f ? "Excellent!" :
                        cvR2 > 0.5f ? "Good" :
                        cvR2 > 0.3f ? "Fair" : "Poor";
        
        // Find optimal parameters first (for both UI and file)
        var optimal = predictor.FindOptimalParameters(targetOxygen: 5.0f);
        
        // Generate SHORT summary for UI display
        string summaryText = "=== REGRESSION ANALYSIS COMPLETE ===\n";
        summaryText += $"Trials Analyzed: {result.totalTrials}\n";
        summaryText += $"Average Oxygen: {result.averageOxygen:F1}%\n";
        summaryText += $"Perfect Trials (2.5-7.5%): {perfectTrials}\n";
        summaryText += $"Failed Trials (0%): {failedTrials}\n";
        summaryText += $"Model Quality : {cvR2:F3} ({quality})\n\n";
        
        // Prediction errors (compact format)
        summaryText += "=== PREDICTION ACCURACY ===\n";
        float totalError = 0f;
        for (int i = 0; i < allTrialData.Count; i++)
        {
            float actual = allTrialData[i].finalOxygenRemaining;
            float predicted = predictor.PredictOxygen(allTrialData[i]);
            float error = Mathf.Abs(actual - predicted);
            totalError += error;
            
            summaryText += $"Trial {allTrialData[i].trialId}: Actual={actual:F1}%, Predicted={predicted:F1}% -> Error={error:F1}%\n";
        }
        float avgError = totalError / allTrialData.Count;
        summaryText += $"Average Error = {avgError:F2}%\n";
        
        if (optimal != null)
        {
            float predictedOptimal = predictor.PredictOxygen(optimal);
            summaryText += "=== RECOMMENDED PARAMETERS ===\n";
            summaryText += $"Target: 5.0% -> Predicted: {predictedOptimal:F1}%\n";
            summaryText += $"Speed: {optimal.speed:F2}\n";
            summaryText += $"Vertical Speed: {optimal.verticalSpeed:F2}\n";
            summaryText += $"Idle Upward Speed: {optimal.idleUpwardSpeed:F3}\n";
            summaryText += $"Life Time: {optimal.lifeTime:F2}\n";
            summaryText += $"O2 Drop/sec: {optimal.downHealthPairSec:F2}\n";
            summaryText += $"Collision Damage: {optimal.removeHealthWithCollide:F2}\n";
            summaryText += $"Time Between Collides: {optimal.timeBetweenCollides:F2}\n";
            summaryText += $"Heal Points: {optimal.healHealthPoint:F2}\n";
        }
        
        summaryText += "Full details saved to:\n";
        summaryText += "Assets/Data/RegressionResults/\n";
        summaryText += "RegressionAnalysis_[timestamp].txt\n";
        
        // Generate FULL detailed text for file export
        string fullDetailsText = "=== MULTIPLE LINEAR REGRESSION (Ridge) ===\n\n";
        fullDetailsText += $"Trials Analyzed: {result.totalTrials}\n";
        fullDetailsText += $"Average Oxygen: {result.averageOxygen:F1}%\n";
        fullDetailsText += $"Perfect Trials (2.5-7.5%): {perfectTrials}\n";
        fullDetailsText += $"Failed Trials (0%): {failedTrials}\n\n";
        
        // K-Fold CV results
        fullDetailsText += "=== MODEL VALIDATION (K-Fold CV) ===\n";
        fullDetailsText += $"Folds: {kFolds}\n";
        fullDetailsText += $"Cross-Val RMSE: {cvRmse:F2}%\n";
        fullDetailsText += $"Cross-Val MAE: {cvMae:F2}%\n";
        fullDetailsText += $"Cross-Val R2: {cvR2:F3}\n";
        fullDetailsText += $"Model Quality: {quality}\n\n";
        
        fullDetailsText += "=== MODEL PREDICTIONS ===\n";
        fullDetailsText += "(Actual vs Predicted Oxygen)\n\n";
        
        for (int i = 0; i < allTrialData.Count; i++)
        {
            float actual = allTrialData[i].finalOxygenRemaining;
            float predicted = predictor.PredictOxygen(allTrialData[i]);
            float error = Mathf.Abs(actual - predicted);
            
            fullDetailsText += $"Trial {allTrialData[i].trialId}:\n";
            fullDetailsText += $"  Actual: {actual:F1}%  Predicted: {predicted:F1}%\n";
            fullDetailsText += $"  Error: {error:F1}%\n\n";
        }
        
        fullDetailsText += $"Average Prediction Error: {avgError:F2}%\n\n";
        
        // Feature importance
        fullDetailsText += "=== FEATURE IMPORTANCE ===\n";
        fullDetailsText += "(Impact on oxygen level)\n\n";
        
        var importance = predictor.GetFeatureImportance();
        foreach (var (feature, value) in importance.Take(5))
        {
            string bar = new string('█', Mathf.RoundToInt(value * 20));
            fullDetailsText += $"{feature}:\n  {value:F4} {bar}\n";
            
            // Store in correlations dict for compatibility
            result.correlations[feature] = value;
        }
        
        fullDetailsText += "\n=== OPTIMAL PARAMETERS ===\n";
        fullDetailsText += "Target: 5.0% oxygen remaining\n\n";
        
        // Use optimal parameters calculated earlier
        if (optimal != null)
        {
            float predictedOptimal = predictor.PredictOxygen(optimal);
            
            fullDetailsText += $"Predicted Oxygen: {predictedOptimal:F2}%\n\n";
            fullDetailsText += "Recommended Parameters:\n";
            fullDetailsText += $"  Speed: {optimal.speed:F2}\n";
            fullDetailsText += $"  Vertical Speed: {optimal.verticalSpeed:F2}\n";
            fullDetailsText += $"  Idle Upward Speed: {optimal.idleUpwardSpeed:F3}\n";
            fullDetailsText += $"  Life Time: {optimal.lifeTime:F2}\n";
            fullDetailsText += $"  O2 Drop/sec: {optimal.downHealthPairSec:F2}\n";
            fullDetailsText += $"  Collision Damage: {optimal.removeHealthWithCollide:F2}\n";
            fullDetailsText += $"  Time Between Collides: {optimal.timeBetweenCollides:F2}\n";
            fullDetailsText += $"  Heal Points: {optimal.healHealthPoint:F2}\n";
        }
        else
        {
            fullDetailsText += "Could not find optimal parameters\n";
        }
        
        result.summaryText = summaryText; // SHORT version for UI
        result.fullDetailsText = fullDetailsText; // FULL version for file
        
        Debug.Log(summaryText);
        return result;
    }
    
    /// <summary>
    /// Build feature matrix and target vector from trial data
    /// </summary>
    private static (float[][], float[]) BuildFeatureMatrix(List<TrialDataModels.TrialData> trials)
    {
        int n = trials.Count;
        int k = 8; // 8 features
        
        float[][] X = new float[n][];
        float[] y = new float[n];
        
        for (int i = 0; i < n; i++)
        {
            X[i] = new float[k];
            X[i][0] = trials[i].speed;
            X[i][1] = trials[i].verticalSpeed;
            X[i][2] = trials[i].idleUpwardSpeed;
            X[i][3] = trials[i].lifeTime;
            X[i][4] = trials[i].downHealthPairSec;
            X[i][5] = trials[i].removeHealthWithCollide;
            X[i][6] = trials[i].timeBetweenCollides;
            X[i][7] = trials[i].healHealthPoint;
            
            y[i] = trials[i].finalOxygenRemaining;
        }
        
        return (X, y);
    }
    
    /// <summary>
    /// Calculate Pearson correlation coefficient between two arrays
    /// Returns value between -1 (negative correlation) and +1 (positive correlation)
    /// NOTE: This function is no longer used in main regression analysis
    /// The ML model (Multiple Linear Regression) is used instead
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
        if (result == null || string.IsNullOrEmpty(result.fullDetailsText))
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
            fileContent += "REGRESSION ANALYSIS - FULL REPORT\n";
            fileContent += "=====================================\n";
            fileContent += $"Generated: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\n";
            fileContent += $"Trials analyzed: {result.totalTrials}\n";
            fileContent += "=====================================\n\n";
            fileContent += result.fullDetailsText;
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
        float targetOxygen = 5.0f)
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


