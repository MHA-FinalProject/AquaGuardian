using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Predicts oxygen levels and finds optimal game parameters
/// Uses Multiple Linear Regression trained on 5 trial runs
/// Target: 2.5-5% oxygen remaining (perfect difficulty)
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
    
    /// <summary>
    /// Train model on trial data
    /// </summary>
    public bool TrainModel(List<TrialDataModels.TrialData> trials)
    {
        if (trials == null || trials.Count < 3)
        {
            Debug.LogError($"Need at least 3 trials for training, got {trials?.Count ?? 0}");
            return false;
        }
        
        Debug.Log($"=== TRAINING OXYGEN PREDICTOR ===");
        Debug.Log($"Training samples: {trials.Count}");
        
        // Prepare feature matrix X and target vector Y
        float[][] X = ExtractFeatures(trials);
        float[] Y = ExtractTargets(trials);
        
        // Create and train model
        model = new MultipleLinearRegression(normalize: true);
        model.Fit(X, Y, featureNames);
        
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
        
        float[] features = new float[]
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
        
        return model.Predict(features);
    }
    
    /// <summary>
    /// Find optimal parameters that target specific oxygen level
    /// Uses grid search over parameter ranges
    /// </summary>
    public TrialDataModels.TrialData FindOptimalParameters(
        float targetOxygen = 2.5f,
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
        Debug.Log($"R² Score: {model.rSquared:F4}");
        
        // Model is valid if R² > 0.5 and average error < 10%
        bool isValid = model.rSquared > 0.5f && avgError < 10f;
        
        if (!isValid)
        {
            Debug.LogWarning($"Model validation failed: R²={model.rSquared:F2}, AvgError={avgError:F1}%");
        }
        
        return isValid;
    }
    
    /// <summary>
    /// Print feature importance
    /// </summary>
    private void PrintFeatureImportance()
    {
        var importance = model.GetFeatureImportance();
        
        Debug.Log("\n=== FEATURE IMPORTANCE ===");
        Debug.Log("(Higher = more impact on oxygen)");
        
        foreach (var (feature, value) in importance)
        {
            string bar = new string('█', Mathf.RoundToInt(value * 10));
            Debug.Log($"{feature,-25} {value:F4} {bar}");
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
            downHealthPairSec = (ranges.oxygenDropPerSecRange.x + ranges.oxygenDropPerSecRange.y) / 2f,
            removeHealthWithCollide = (ranges.collisionDamageRange.x + ranges.collisionDamageRange.y) / 2f,
            timeBetweenCollides = (ranges.timeBetweenCollidesRange.x + ranges.timeBetweenCollidesRange.y) / 2f,
            healHealthPoint = (ranges.oxygenHealRange.x + ranges.oxygenHealRange.y) / 2f
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
                data.downHealthPairSec = Mathf.Lerp(ranges.oxygenDropPerSecRange.x, ranges.oxygenDropPerSecRange.y, t);
                break;
            case "removeHealthWithCollide":
                data.removeHealthWithCollide = Mathf.Lerp(ranges.collisionDamageRange.x, ranges.collisionDamageRange.y, t);
                break;
            case "timeBetweenCollides":
                data.timeBetweenCollides = Mathf.Lerp(ranges.timeBetweenCollidesRange.x, ranges.timeBetweenCollidesRange.y, t);
                break;
            case "healHealthPoint":
                data.healHealthPoint = Mathf.Lerp(ranges.oxygenHealRange.x, ranges.oxygenHealRange.y, t);
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

