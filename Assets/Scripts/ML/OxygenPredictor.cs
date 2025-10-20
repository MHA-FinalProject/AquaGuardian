using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Predicts oxygen levels and finds optimal game parameters
/// Uses Multiple Linear Regression trained on 5 trial runs
/// Target: 5% oxygen remaining (perfect difficulty)
/// </summary>
public class OxygenPredictor
{
    private MultipleLinearRegression model;
    private string[] featureNames = new string[]
    {
        "speed",
        "verticalSpeed",
        "idleUpwardSpeed",
        "lifeTime",
        "downHealthPairSec",
        "removeHealthWithCollide",
        "timeBetweenCollides",
        "healHealthPoint"
    };
    
    // Feature selection
    private int[] selectedFeatureIndices; // Indices of selected features
    private string[] selectedFeatureNames; // Names of selected features
    private bool useFeatureSelection = false;
    public int topKFeatures = 4; // Use top 4 most important features
    
    /// <summary>
    /// Train model on trial data with automatic feature selection
    /// </summary>
    public bool TrainModel(List<TrialDataModels.TrialData> trials, bool enableFeatureSelection = true)
    {
        if (trials == null || trials.Count < 3)
        {
            Debug.LogError($"Need at least 3 trials for training, got {trials?.Count ?? 0}");
            return false;
        }
        
        // Reset feature selection state
        useFeatureSelection = false;
        selectedFeatureIndices = null;
        selectedFeatureNames = null;
        
        Debug.Log($"=== TRAINING OXYGEN PREDICTOR ===");
        Debug.Log($"Training samples: {trials.Count}");
        
        // Prepare full feature matrix X and target vector Y
        float[][] X_full = ExtractFeatures(trials);
        float[] Y = ExtractTargets(trials);
        
        // Feature selection: use only top K features if enabled and few samples
        if (enableFeatureSelection && trials.Count < 10)
        {
            Debug.Log($"\n=== FEATURE SELECTION (Small Dataset) ===");
            Debug.Log($"Samples: {trials.Count}, Features: {featureNames.Length}");
            
            // Step 1: Train initial model with all features
            var tempModel = new MultipleLinearRegression(normalize: true);
            tempModel.ridgeLambda = 0.5f;
            tempModel.Fit(X_full, Y, featureNames);
            
            // Step 2: Get feature importance and select top K
            var importance = tempModel.GetFeatureImportance();
            int K = Mathf.Min(topKFeatures, Mathf.Max(2, trials.Count - 1)); // At least 2, at most trials-1
            
            selectedFeatureIndices = new int[K];
            selectedFeatureNames = new string[K];
            
            Debug.Log($"Selecting Top {K} features:");
            for (int i = 0; i < K; i++)
            {
                // Find index of this feature in original array
                string fname = importance[i].feature;
                for (int j = 0; j < featureNames.Length; j++)
                {
                    if (featureNames[j] == fname)
                    {
                        selectedFeatureIndices[i] = j;
                        selectedFeatureNames[i] = fname;
                        Debug.Log($"  {i + 1}. {fname} (importance: {importance[i].importance:F4})");
                        break;
                    }
                }
            }
            
            // IMPORTANT: Set flag BEFORE extracting features or training
            useFeatureSelection = true;
            
            // Step 3: Extract only selected features
            float[][] X = ExtractSelectedFeatures(X_full);
            
            // Step 4: Train final model with selected features only
            model = new MultipleLinearRegression(normalize: true);
            model.ridgeLambda = 0.5f;
            model.Fit(X, Y, selectedFeatureNames);
            
            Debug.Log($"Model trained with {K} selected features");
        }
        else
        {
            // Use all features
            useFeatureSelection = false;
            selectedFeatureIndices = null;
            selectedFeatureNames = null;
            
            model = new MultipleLinearRegression(normalize: true);
            model.ridgeLambda = 0.5f;
            model.Fit(X_full, Y, featureNames);
            
            Debug.Log(" Model trained with all features");
        }
        
        // Validate model
        bool isValid = ValidateModel(trials);
        
        if (isValid)
        {
            Debug.Log("Model trained successfully!");
            PrintFeatureImportance();
        }
        else
        {
            Debug.LogWarning("Model trained but validation shows poor fit");
        }
        
        return isValid;
    }
    
    /// <summary>
    /// Predict oxygen level for given parameters
    /// </summary>
    public float PredictOxygen(TrialDataModels.TrialData parameters)
    {
        if (model == null)
        {
            Debug.LogError("Model not trained! Call TrainModel() first.");
            return -1f;
        }
        
        float[] fullFeatures = new float[]
        {
            parameters.speed,
            parameters.verticalSpeed,
            parameters.idleUpwardSpeed,
            parameters.lifeTime,
            parameters.downHealthPairSec,
            parameters.removeHealthWithCollide,
            parameters.timeBetweenCollides,
            parameters.healHealthPoint
        };
        
        // If using feature selection, extract only selected features
        float[] features;
        if (useFeatureSelection && selectedFeatureIndices != null)
        {
            features = new float[selectedFeatureIndices.Length];
            for (int i = 0; i < selectedFeatureIndices.Length; i++)
            {
                features[i] = fullFeatures[selectedFeatureIndices[i]];
            }
        }
        else
        {
            features = fullFeatures;
        }
        
        return model.Predict(features);
    }
    
    /// <summary>
    /// Find optimal parameters that target specific oxygen level
    /// Uses grid search over parameter ranges
    /// </summary>
    public TrialDataModels.TrialData FindOptimalParameters(
        float targetOxygen = 5.0f,
        TrialDataModels.ParameterRanges ranges = null,
        int gridResolution = 5)
    {
        if (model == null)
        {
            Debug.LogError("Model not trained!");
            return null;
        }
        
        if (ranges == null)
        {
            ranges = new TrialDataModels.ParameterRanges();
        }
        
        Debug.Log($"=== FINDING OPTIMAL PARAMETERS ===");
        Debug.Log($"Target oxygen: {targetOxygen}%");
        Debug.Log($"Grid resolution: {gridResolution} points per parameter");
        
        TrialDataModels.TrialData bestParams = null;
        float bestError = float.MaxValue;
        int evaluations = 0;
        
        // Grid search over top 3 most important features
        var importance = model.GetFeatureImportance();
        var topFeatures = importance.Take(3).ToArray();
        
        Debug.Log($"Optimizing top 3 features:");
        foreach (var (feature, imp) in topFeatures)
        {
            Debug.Log($"  - {feature}: importance {imp:F4}");
        }
        
        // Generate grid for top 3 features
        var grid = GenerateParameterGrid(ranges, topFeatures, gridResolution);
        
        foreach (var candidate in grid)
        {
            float predicted = PredictOxygen(candidate);
            float error = Mathf.Abs(predicted - targetOxygen);
            
            evaluations++;
            
            if (error < bestError)
            {
                bestError = error;
                bestParams = candidate;
            }
        }
        
        Debug.Log($"Evaluated {evaluations} parameter combinations");
        Debug.Log($"Best match: {bestError:F2}% error");
        Debug.Log($"Predicted oxygen: {PredictOxygen(bestParams):F2}%");
        Debug.Log($"Target oxygen: {targetOxygen}%");
        
        return bestParams;
    }
    
    /// <summary>
    /// Extract feature matrix from trials
    /// </summary>
    private float[][] ExtractFeatures(List<TrialDataModels.TrialData> trials)
    {
        float[][] X = new float[trials.Count][];
        
        for (int i = 0; i < trials.Count; i++)
        {
            X[i] = new float[]
            {
                trials[i].speed,
                trials[i].verticalSpeed,
                trials[i].idleUpwardSpeed,
                trials[i].lifeTime,
                trials[i].downHealthPairSec,
                trials[i].removeHealthWithCollide,
                trials[i].timeBetweenCollides,
                trials[i].healHealthPoint
            };
        }
        
        return X;
    }
    
    /// <summary>
    /// Extract target vector (oxygen values) from trials
    /// </summary>
    private float[] ExtractTargets(List<TrialDataModels.TrialData> trials)
    {
        return trials.Select(t => t.finalOxygenRemaining).ToArray();
    }
    
    /// <summary>
    /// Extract only selected features from full feature matrix
    /// </summary>
    private float[][] ExtractSelectedFeatures(float[][] X_full)
    {
        if (!useFeatureSelection || selectedFeatureIndices == null)
            return X_full;
        
        int m = X_full.Length;
        int k = selectedFeatureIndices.Length;
        
        float[][] X_selected = new float[m][];
        for (int i = 0; i < m; i++)
        {
            X_selected[i] = new float[k];
            for (int j = 0; j < k; j++)
            {
                X_selected[i][j] = X_full[i][selectedFeatureIndices[j]];
            }
        }
        
        return X_selected;
    }
    
    /// <summary>
    /// Validate model by checking predictions vs actual
    /// </summary>
    private bool ValidateModel(List<TrialDataModels.TrialData> trials)
    {
        Debug.Log("\n=== MODEL VALIDATION ===");
        
        float totalError = 0f;
        for (int i = 0; i < trials.Count; i++)
        {
            float actual = trials[i].finalOxygenRemaining;
            float predicted = PredictOxygen(trials[i]);
            float error = Mathf.Abs(actual - predicted);
            
            totalError += error;
            
            Debug.Log($"Trial {trials[i].trialId}: Actual={actual:F1}%, Predicted={predicted:F1}%, Error={error:F1}%");
        }
        
        float avgError = totalError / trials.Count;
        Debug.Log($"\nAverage Error: {avgError:F2}%");
        Debug.Log($"R2 Score: {model.rSquared:F4}");
        
        // Check variance
        float maxO2 = trials.Max(t => t.finalOxygenRemaining);
        float minO2 = trials.Min(t => t.finalOxygenRemaining);
        float variance = maxO2 - minO2;
        
        if (variance < 0.1f)
        {
            Debug.LogWarning($"No variance in oxygen data: all values ~{trials[0].finalOxygenRemaining:F1}%");
            return false;
        }
        
        // Adaptive validation based on dataset size and feature count
        bool isSmallDataset = trials.Count < 10;
        int numFeatures = (model.numFeatures > 0) ? model.numFeatures : featureNames.Length;
        bool usingFeatureSelection = numFeatures <= 4; // If using 4 or fewer features
        
        float minR2, maxError;
        
        if (usingFeatureSelection && isSmallDataset)
        {
            // With feature selection + small dataset - very lenient
            minR2 = -0.5f; // Allow negative R2 (Ridge regularization helps)
            maxError = 30f;
            Debug.Log($"Using lenient criteria (Feature Selection: {numFeatures} features, {trials.Count} samples)");
        }
        else if (isSmallDataset)
        {
            // Small dataset without feature selection
            minR2 = 0.2f;
            maxError = 25f;
            Debug.Log($"Using moderate criteria ({numFeatures} features, {trials.Count} samples)");
        }
        else
        {
            // Large dataset - stricter
            minR2 = 0.5f;
            maxError = 15f;
            Debug.Log($"Using strict criteria ({numFeatures} features, {trials.Count} samples)");
        }
        
        bool isValid = model.rSquared > minR2 && avgError < maxError;
        
        if (!isValid)
        {
            Debug.LogWarning($"Validation failed: R2={model.rSquared:F3} (need >{minR2:F2}), Error={avgError:F2}% (need <{maxError:F1}%)");
        }
        else
        {
            Debug.Log($"Validated: R2={model.rSquared:F3}, Error={avgError:F2}%");
        }
        
        return isValid;
    }
    
    /// <summary>
    /// Get feature importance (public access)
    /// </summary>
    public (string feature, float importance)[] GetFeatureImportance()
    {
        if (model == null)
        {
            Debug.LogWarning("Model not trained - cannot get feature importance");
            return new (string, float)[0];
        }
        
        return model.GetFeatureImportance();
    }
    
    /// <summary>
    /// Get the trained model (for K-Fold CV or other analysis)
    /// </summary>
    public MultipleLinearRegression GetModel()
    {
        return model;
    }
    
    /// <summary>
    /// Print feature importance
    /// </summary>
    private void PrintFeatureImportance()
    {
        var importance = GetFeatureImportance();
        
        Debug.Log("\n=== FEATURE IMPORTANCE ===");
        if (useFeatureSelection && selectedFeatureNames != null)
        {
            Debug.Log($"Using {selectedFeatureNames.Length} selected features:");
        }
        Debug.Log("(Higher = more impact on oxygen)");
        
        foreach (var (feature, value) in importance)
        {
            string bar = new string('#', Mathf.RoundToInt(value * 10));
            string marker = (useFeatureSelection && selectedFeatureNames != null && selectedFeatureNames.Contains(feature)) ? " [SELECTED]" : "";
            Debug.Log($"{feature,-25} {value:F4} {bar}{marker}");
        }
    }
    
    /// <summary>
    /// Generate parameter grid for optimization
    /// </summary>
    private List<TrialDataModels.TrialData> GenerateParameterGrid(
        TrialDataModels.ParameterRanges ranges,
        (string feature, float importance)[] topFeatures,
        int resolution)
    {
        var grid = new List<TrialDataModels.TrialData>();
        
        // Base parameters (average values)
        var baseParams = new TrialDataModels.TrialData
        {
            speed = (ranges.speedRange.x + ranges.speedRange.y) / 2f,
            verticalSpeed = (ranges.verticalSpeedRange.x + ranges.verticalSpeedRange.y) / 2f,
            idleUpwardSpeed = (ranges.idleUpwardSpeedRange.x + ranges.idleUpwardSpeedRange.y) / 2f,
            lifeTime = (ranges.lifeTimeRange.x + ranges.lifeTimeRange.y) / 2f,
            downHealthPairSec = (ranges.downHealthPairSecRange.x + ranges.downHealthPairSecRange.y) / 2f,
            removeHealthWithCollide = (ranges.removeHealthWithCollideRange.x + ranges.removeHealthWithCollideRange.y) / 2f,
            timeBetweenCollides = (ranges.timeBetweenCollidesRange.x + ranges.timeBetweenCollidesRange.y) / 2f,
            healHealthPoint = (ranges.healHealthPointRange.x + ranges.healHealthPointRange.y) / 2f
        };
        
        // Generate grid only for top 3 features
        for (int i = 0; i < resolution; i++)
        {
            for (int j = 0; j < resolution; j++)
            {
                for (int k = 0; k < resolution; k++)
                {
                    var candidate = CloneParams(baseParams);
                    
                    // Vary top 3 features
                    SetFeatureValue(candidate, topFeatures[0].feature, ranges, i, resolution);
                    SetFeatureValue(candidate, topFeatures[1].feature, ranges, j, resolution);
                    SetFeatureValue(candidate, topFeatures[2].feature, ranges, k, resolution);
                    
                    grid.Add(candidate);
                }
            }
        }
        
        return grid;
    }
    
    private void SetFeatureValue(TrialDataModels.TrialData data, string feature, 
        TrialDataModels.ParameterRanges ranges, int index, int resolution)
    {
        float t = index / (float)(resolution - 1); // 0 to 1
        
        switch (feature)
        {
            case "speed":
                data.speed = Mathf.Lerp(ranges.speedRange.x, ranges.speedRange.y, t);
                break;
            case "verticalSpeed":
                data.verticalSpeed = Mathf.Lerp(ranges.verticalSpeedRange.x, ranges.verticalSpeedRange.y, t);
                break;
            case "idleUpwardSpeed":
                data.idleUpwardSpeed = Mathf.Lerp(ranges.idleUpwardSpeedRange.x, ranges.idleUpwardSpeedRange.y, t);
                break;
            case "lifeTime":
                data.lifeTime = Mathf.Lerp(ranges.lifeTimeRange.x, ranges.lifeTimeRange.y, t);
                break;
            case "downHealthPairSec":
                data.downHealthPairSec = Mathf.Lerp(ranges.downHealthPairSecRange.x, ranges.downHealthPairSecRange.y, t);
                break;
            case "removeHealthWithCollide":
                data.removeHealthWithCollide = Mathf.Lerp(ranges.removeHealthWithCollideRange.x, ranges.removeHealthWithCollideRange.y, t);
                break;
            case "timeBetweenCollides":
                data.timeBetweenCollides = Mathf.Lerp(ranges.timeBetweenCollidesRange.x, ranges.timeBetweenCollidesRange.y, t);
                break;
            case "healHealthPoint":
                data.healHealthPoint = Mathf.Lerp(ranges.healHealthPointRange.x, ranges.healHealthPointRange.y, t);
                break;
        }
    }
    
    private TrialDataModels.TrialData CloneParams(TrialDataModels.TrialData source)
    {
        return new TrialDataModels.TrialData
        {
            speed = source.speed,
            verticalSpeed = source.verticalSpeed,
            idleUpwardSpeed = source.idleUpwardSpeed,
            lifeTime = source.lifeTime,
            downHealthPairSec = source.downHealthPairSec,
            removeHealthWithCollide = source.removeHealthWithCollide,
            timeBetweenCollides = source.timeBetweenCollides,
            healHealthPoint = source.healHealthPoint
        };
    }
}

