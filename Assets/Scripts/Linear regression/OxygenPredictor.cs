using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/**
 * Predicts oxygen levels and finds optimal game parameters
 * Uses Multiple Linear Regression trained on trial runs
 * Target: 10% oxygen remaining (perfect difficulty)
 * Used for difficulty parameter solver and to find optimal parameters for a given oxygen level
 * It is not used for training the model, only for prediction and finding optimal parameters
 * It is not used for training the model, only for prediction and finding optimal parameters
 */
public class OxygenPredictor
{
    private MultipleLinearRegression model;
    
    // Feature names - use centralized definition from FeatureExtractor
    private string[] FeatureNames => FeatureExtractor.FeatureNames;
    
    // Feature selection
    private int[] selectedFeatureIndices; // Indices of selected features
    private string[] selectedFeatureNames; // Names of selected features
    private bool useFeatureSelection = false;
    public int topKFeatures = 10; // Default: use all 10 features for best accuracy (can be set to 4 for small datasets)
    
    public bool TrainModel(List<TrialDataModels.TrialData> trials, bool enableFeatureSelection = true)
    {
        if (trials == null || trials.Count < 3)
        {
            Debug.LogError($"Need at least 3 trials for training, got {trials?.Count ?? 0}");
            return false;
        }

        //Debug.Log($"[OxygenPredictor] Training with {FeatureExtractor.FeatureCount} effective features (calculated per-sample)");

        // Reset feature selection state
        useFeatureSelection = false;
        selectedFeatureIndices = null;
        selectedFeatureNames = null;

        // Prepare full feature matrix X and target vector Y using centralized extractor
        var (X_full, Y) = FeatureExtractor.ExtractFeaturesAndTargets(trials);

        // Feature selection: use only top K features if enabled and few samples
        if (enableFeatureSelection && trials.Count < 10)
        {
            // Step 1: Train initial model with all features
            var tempModel = new MultipleLinearRegression(normalize: true);
            tempModel.ridgeLambda = 0.5f;
            tempModel.Fit(X_full, Y, FeatureNames);

            // Step 2: Get feature importance and select top K
            var importance = tempModel.GetFeatureImportance();
            int K = Mathf.Min(topKFeatures, Mathf.Max(2, trials.Count - 1)); // At least 2, at most trials-1

            selectedFeatureIndices = new int[K];
            selectedFeatureNames = new string[K];

            for (int i = 0; i < K; i++)
            {
                // Find index of this feature in original array
                string fname = importance[i].feature;
                for (int j = 0; j < FeatureNames.Length; j++)
                {
                    if (FeatureNames[j] == fname)
                    {
                        selectedFeatureIndices[i] = j;
                        selectedFeatureNames[i] = fname;
                        break;
                    }
                }
            }

           
            useFeatureSelection = true;

            // Step 3: Extract only selected features
            float[][] X = ExtractSelectedFeatures(X_full);

            // Step 4: Train final model with selected features only
            model = new MultipleLinearRegression(normalize: true);
            model.ridgeLambda = 0.5f;
            model.Fit(X, Y, selectedFeatureNames);
        }
        else
        {
            // Use all features
            useFeatureSelection = false;
            selectedFeatureIndices = null;
            selectedFeatureNames = null;

            model = new MultipleLinearRegression(normalize: true);
            model.ridgeLambda = 0.5f;
            model.Fit(X_full, Y, FeatureNames);
        }

        // Validate model
        return ValidateModel(trials);
    }
    
    public float PredictOxygen(TrialDataModels.TrialData parameters)
    {
        if (model == null)
        {
            Debug.LogError("Model not trained! Call TrainModel() first.");
            return -1f;
        }
        
        // Use centralized feature extraction
        float[] fullFeatures = FeatureExtractor.ExtractFeatures(parameters);
        
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
        
        float predicted = model.Predict(features);
        
        // Clamp prediction to valid oxygen range [0, 100]%
        // Linear regression can predict values outside this range, especially with extreme parameters
        predicted = Mathf.Clamp(predicted, 0f, 100f);
        
        return predicted;
    }
    
    public TrialDataModels.TrialData FindOptimalParameters(
        float targetOxygen = 10.0f,
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
        
        
        TrialDataModels.TrialData bestParams = null;
        float bestError = float.MaxValue;
        int evaluations = 0;
        
        // Grid search over top 3 most important features
        var importance = model.GetFeatureImportance();
        var topFeatures = importance.Take(3).ToArray();
        
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
        
        return bestParams;
    }
    
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
    
    private bool ValidateModel(List<TrialDataModels.TrialData> trials)
    {
        
        float totalError = 0f;
        for (int i = 0; i < trials.Count; i++)
        {
            float actual = trials[i].finalOxygenRemaining;
            float predicted = PredictOxygen(trials[i]);
            float error = Mathf.Abs(actual - predicted);
            totalError += error;
        }
        
        float avgError = totalError / trials.Count;
        
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
        int numFeatures = (model.numFeatures > 0) ? model.numFeatures : FeatureNames.Length;
        bool usingFeatureSelection = numFeatures <= 4;
        
        float minR2, maxError;
        
        if (usingFeatureSelection && isSmallDataset)
        {
            minR2 = -0.5f;
            maxError = 30f;
        }
        else if (isSmallDataset)
        {
            minR2 = 0.2f;
            maxError = 25f;
        }
        else
        {
            minR2 = 0.5f;
            maxError = 15f;
        }
        
        bool isValid = model.rSquared > minR2 && avgError < maxError;
        
        if (!isValid)
        {
            Debug.LogWarning($"Validation failed: R2={model.rSquared:F3} (need >{minR2:F2}), Error={avgError:F2}% (need <{maxError:F1}%)");
        }
        
        return isValid;
    }
    
    public (string feature, float importance)[] GetFeatureImportance()
    {
        if (model == null)
        {
            Debug.LogWarning("Model not trained - cannot get feature importance");
            return new (string, float)[0];
        }
        
        return model.GetFeatureImportance();
    }
    
   
    // Get the trained model (for K-Fold CV or other analysis)
    public MultipleLinearRegression GetModel()
    {
        return model;
    }
    
    
    private void PrintFeatureImportance()
    {
        // Logging disabled - only critical errors are logged
    }
    

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
            RemoveHealthEveryLifeTime = (ranges.RemoveHealthEveryLifeTimeRange.x + ranges.RemoveHealthEveryLifeTimeRange.y) / 2f,
            removeHealthWithCollide = (ranges.removeHealthWithCollideRange.x + ranges.removeHealthWithCollideRange.y) / 2f,
            timeBetweenCollides = (ranges.timeBetweenCollidesRange.x + ranges.timeBetweenCollidesRange.y) / 2f,
            healHealthPoint = (ranges.healHealthPointRange.x + ranges.healHealthPointRange.y) / 2f,
            factorForce = (ranges.factorForceRange.x + ranges.factorForceRange.y) / 2f  // FIXED: Added factorForce
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

    // Set the value of a feature in the parameter ranges for the grid search
    // data is the candidate parameter values 
    
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
            case "RemoveHealthEveryLifeTime":
                data.RemoveHealthEveryLifeTime = Mathf.Lerp(ranges.RemoveHealthEveryLifeTimeRange.x, ranges.RemoveHealthEveryLifeTimeRange.y, t);
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
            case "factorForce":  // FIXED: Added factorForce case
                data.factorForce = Mathf.Lerp(ranges.factorForceRange.x, ranges.factorForceRange.y, t);
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
            RemoveHealthEveryLifeTime = source.RemoveHealthEveryLifeTime,
            removeHealthWithCollide = source.removeHealthWithCollide,
            timeBetweenCollides = source.timeBetweenCollides,
            healHealthPoint = source.healHealthPoint,
            factorForce = source.factorForce,
            IsAmadeoMode = source.IsAmadeoMode  // Input mode indicator
        };
    }
}

