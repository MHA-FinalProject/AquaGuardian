using UnityEngine;
using System.Collections.Generic;
using System.Linq;


/**
* Handler for Python-trained regression models
* Manages loading, switching, and using Python-trained models (ElasticNet, Ridge, etc.)
* alongside Unity's built-in model. Provides auto-loading, prediction, optimization,
* and reporting capabilities for hybrid ML workflows.
* See also: PythonRegressionModel, TrialRegressionAlgorithm, OxygenPredictor
*/
public static class PythonRegressionHandler
{
    private static PythonRegressionModel pythonModel = null;
    
    // Flag to control whether to use Python model or Unity model
  
    public static bool usePythonModel = false;

    // Check if Python model is loaded and active
    public static bool IsModelReady => usePythonModel && pythonModel != null && pythonModel.IsLoaded;

    // Load Python-trained model from JSON
    // Enables hybrid mode - can use either Unity or Python model
    public static bool LoadPythonModel(string jsonPath)
    {
        pythonModel = new PythonRegressionModel();
        bool success = pythonModel.LoadFromJSON(jsonPath);
        if (success)
        {
            usePythonModel = true;
           // Debug.Log($"[PythonHandler] Python model loaded and activated");
           // Debug.Log($"   {pythonModel.GetModelInfo()}");
        }
        else
        {
            usePythonModel = false;
            pythonModel = null;
        }
        return success;
    }

    // Switch to Unity built-in model
    public static void UseUnityModel()
    {
        usePythonModel = false;
        Debug.Log("[PythonHandler] Switched to Unity built-in model");
    }

    // Switch to Python model (if loaded)
    public static bool UsePythonModel()
    {
        if (pythonModel == null || !pythonModel.IsLoaded)
        {
            Debug.LogWarning("[PythonHandler] Python model not loaded! Call LoadPythonModel() first.");
            return false;
        }
        usePythonModel = true;
        Debug.Log("[PythonHandler] Switched to Python model");
        return true;
    }

    // Get current model type description
    public static string GetCurrentModelType()
    {
        if (usePythonModel && pythonModel != null && pythonModel.IsLoaded)
            return $"Python ({pythonModel.Model.model_type})";
        return "Unity (Ridge)";
    }

    // AUTO-LOAD: Try to automatically load Python model from common locations
    // Searches in: StreamingAssets/RegressionModels/, Data/RegressionModels/, and root
    public static bool TryAutoLoadPythonModel(string preferredModelName = "regression_model_elasticnet.json")
    {
        if (pythonModel != null && pythonModel.IsLoaded)
        {
            Debug.Log("[PythonHandler] Python model already loaded");
            return true;
        }

        // List of paths to search (in order of preference)
        string[] searchPaths = new string[]
        {
            System.IO.Path.Combine(Application.streamingAssetsPath, "RegressionModels", preferredModelName),
            System.IO.Path.Combine(Application.dataPath, "Data", "RegressionModels", preferredModelName),
            System.IO.Path.Combine(Application.streamingAssetsPath, preferredModelName),
            System.IO.Path.Combine(Application.dataPath, "Data", preferredModelName),
            System.IO.Path.Combine(Application.dataPath, "..", preferredModelName)
        };

        foreach (string path in searchPaths)
        {
            if (System.IO.File.Exists(path))
            {
                Debug.Log($"[PythonHandler] Found Python model at: {path}");
                if (LoadPythonModel(path))
                {
                    return true;
                }
            }
        }

        // If preferred name not found, try to find any JSON file (but skip example files)
        string[] fallbackPaths = new string[]
        {
            System.IO.Path.Combine(Application.streamingAssetsPath, "RegressionModels"),
            System.IO.Path.Combine(Application.dataPath, "Data", "RegressionModels"),
            System.IO.Path.Combine(Application.streamingAssetsPath, "")
        };

        foreach (string dir in fallbackPaths)
        {
            if (System.IO.Directory.Exists(dir))
            {
                string[] jsonFiles = System.IO.Directory.GetFiles(dir, "regression_model_*.json");
                jsonFiles = jsonFiles.Where(f => !f.Contains("example") && !f.Contains("_example")).ToArray();
                if (jsonFiles.Length > 0)
                {
                    string foundPath = jsonFiles[0];
                    Debug.Log($"[PythonHandler] Auto-found Python model: {foundPath}");
                    if (LoadPythonModel(foundPath))
                    {
                        return true;
                    }
                }
            }
        }

        Debug.Log("[PythonHandler] No valid Python model found - using Unity model");
        Debug.Log("   To use Python model: Train with 'python PythonScripts/train_regression_model.py <csv> ElasticNet'");
        return false;
    }

 

    // Perform regression analysis using Python-trained model
    public static TrialDataModels.RegressionResult PerformPythonRegressionAnalysis(
        List<TrialDataModels.TrialData> allTrialData,
        float targetOxygen)
    {
        if (!IsModelReady)
        {
            Debug.LogError("[PythonHandler] Cannot perform analysis - model not ready!");
            return new TrialDataModels.RegressionResult
            {
                summaryText = "ERROR: Python model not loaded",
                fullDetailsText = "ERROR: Python model not loaded",
                correlations = new Dictionary<string, float>()
            };
        }

        var result = new TrialDataModels.RegressionResult
        {
            correlations = new Dictionary<string, float>()
        };

        RegressionUtilities.CalculateTrialStatistics(allTrialData, result);

        // Calculate prediction error on training data
        float avgError = allTrialData.Average(t => 
            Mathf.Abs(t.finalOxygenRemaining - pythonModel.PredictOxygen(t)));

        // Get feature importance from Python model
        var allImportance = pythonModel.GetFeatureImportance();
        
        // Store top 5 in correlations
        foreach (var item in allImportance.Take(5))
        {
            result.correlations[item.feature] = item.importance;
        }

        // Optimization using Python model
        bool anyAmadeoTrials = allTrialData.Any(t => t.IsAmadeoMode > 0.5f);
        
        var bannedFeatures = new HashSet<string> { "EffectiveDrainRate" };
        if (!anyAmadeoTrials)
            bannedFeatures.Add("factorForce");

        var filteredImportance = allImportance
            .Where(t => !bannedFeatures.Contains(t.feature))
            .ToArray();

        int[] topIndices = RegressionUtilities.BuildOptimizationIndices(
            filteredImportance, TrialDataModels.FeatureNames);

        var (ranges, baseline) = PrepareOptimizationBaseline(allTrialData, targetOxygen);

        // Use DifficultyParameterSolver with Python predictor
        // Try improved optimization first (using Python model coefficients)
        var solution = OptimizeWithPythonModel(baseline, topIndices, targetOxygen, ranges, anyAmadeoTrials);
        float error = solution != null 
            ? Mathf.Abs(pythonModel.PredictOxygen(solution) - targetOxygen)
            : float.MaxValue;

        // Fallback to RandomSweepOptimizer if needed
        if (solution == null || float.IsNaN(error) || error > 5.0f)
        {
            var (sweepCandidate, sweepError) = DifficultyParameterSolver.RandomSweepOptimizer(
                d => pythonModel.PredictOxygenUnclamped(d),
                ranges, targetOxygen, anyAmadeoTrials, samples: 300); // Increased samples

            if (sweepCandidate != null && sweepError < (float.IsNaN(error) ? float.MaxValue : error))
            {
                solution = sweepCandidate;
                error = sweepError;
            }
        }

        result.optimizedSolution = solution;
        result.optimizedSolutionError = error;

        // Generate reports using TrialReportGenerator
        result.summaryText = TrialReportGenerator.GeneratePythonModelSummary(
            allTrialData, avgError, pythonModel, solution, error, targetOxygen, allImportance);
        result.fullDetailsText = TrialReportGenerator.GeneratePythonModelFullReport(
            allTrialData, avgError, result.averageOxygen, pythonModel,
            allImportance, solution, error, targetOxygen);

        return result;
    }

   

    // Optimize parameters using Python model coefficients (gradient-based)
    private static TrialDataModels.TrialData OptimizeWithPythonModel(
        TrialDataModels.TrialData baseline,
        int[] topIndices,
        float targetOxygen,
        TrialDataModels.ParameterRanges ranges,
        bool anyAmadeoTrials)
    {
        if (pythonModel == null || !pythonModel.IsLoaded)
            return null;

        var model = pythonModel.Model;
        if (model.betas == null || model.betas.Length == 0)
            return null;

        // Use gradient descent based on Python model coefficients
        var solution = baseline;
        float currentO2 = pythonModel.PredictOxygenUnclamped(solution);
        float error = Mathf.Abs(currentO2 - targetOxygen);
        
        if (error < 0.5f)
            return solution; // Already close enough

        // Adaptive learning rate based on error
        float learningRate = error > 40f ? 0.5f : (error > 20f ? 0.3f : (error > 10f ? 0.2f : 0.1f));
        int maxIterations = error > 40f ? 200 : (error > 20f ? 150 : (error > 10f ? 100 : 50));

        string[] featureNames = TrialDataModels.FeatureNames;
        
        for (int iter = 0; iter < maxIterations && error > 0.1f; iter++)
        {
            float delta = currentO2 - targetOxygen;
            if (Mathf.Abs(delta) < 0.1f)
                break;

            // Update top features based on their coefficients
            float sumSquaredCoeffs = 0f;
            foreach (int idx in topIndices)
            {
                if (idx >= 0 && idx < model.betas.Length)
                {
                    float coeff = model.betas[idx];
                    sumSquaredCoeffs += coeff * coeff;
                }
            }

            if (sumSquaredCoeffs < 1e-9f)
                break; // No meaningful coefficients

            // Update each top feature
            foreach (int idx in topIndices)
            {
                if (idx < 0 || idx >= featureNames.Length || idx >= model.betas.Length)
                    continue;

                float coeff = model.betas[idx];
                if (Mathf.Abs(coeff) < 1e-9f)
                    continue;

                // Get current value and range
                float currentVal = ParameterHelper.Get(solution, idx);
                Vector2 range = GetRangeForIndex(ranges, idx);
                
                // Calculate normalized coefficient contribution
                float normalizedCoeff = coeff / Mathf.Sqrt(sumSquaredCoeffs);
                
                // Update: move opposite to error direction, scaled by coefficient
                float update = -delta * normalizedCoeff * learningRate;
                float newVal = Mathf.Clamp(currentVal + update, range.x, range.y);
                
                ParameterHelper.Set(ref solution, idx, newVal);
            }

            currentO2 = pythonModel.PredictOxygenUnclamped(solution);
            error = Mathf.Abs(currentO2 - targetOxygen);
        }

        return solution;
    }

    // Get parameter range for feature index
    private static Vector2 GetRangeForIndex(TrialDataModels.ParameterRanges ranges, int idx)
    {
        switch (idx)
        {
            case 0: return ranges.speedRange;
            case 1: return ranges.verticalSpeedRange;
            case 2: return ranges.idleUpwardSpeedRange;
            case 3: return ranges.lifeTimeRange;
            case 4: return ranges.RemoveHealthEveryLifeTimeRange;
            case 5: return ranges.removeHealthWithCollideRange;
            case 6: return ranges.timeBetweenCollidesRange;
            case 7: return ranges.healHealthPointRange;
            case 8: return ranges.factorForceRange;
            default: return new Vector2(0f, 100f);
        }
    }

    // Prepare optimization baseline from trial data
    private static (TrialDataModels.ParameterRanges ranges, TrialDataModels.TrialData baseline)
        PrepareOptimizationBaseline(List<TrialDataModels.TrialData> trials, float targetOxygen)
    {
        var ranges = new TrialDataModels.ParameterRanges();
        ranges = RegressionUtilities.ConstrainRangesToObserved(ranges, trials, targetOxygen);
        var baseline = FeatureExtractor.GetPatientBaseline(trials, ranges, useMedian: true);
        baseline.IsAmadeoMode = 0f;
        return (ranges, baseline);
    }

  
}

