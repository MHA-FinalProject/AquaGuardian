using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/**
 * Predicts final oxygen levels from game parameters using Multiple Linear Regression
 * 
 * Trains on trial data to predict oxygen % based on 10 game parameters (speed, verticalSpeed, etc.).
 * Uses adaptive Ridge regularization and optional feature selection for small datasets (<10 trials).
 * 
 * Used by RegressionUtilities.OptimizeParameters which passes PredictOxygenUnclamped as a callback
 * to DifficultyParameterSolver for parameter optimization.
 * 
 * See also: FeatureExtractor, MultipleLinearRegression, DifficultyParameterSolver, TrialRegressionAlgorithm, RegressionUtilities
 */
public class OxygenPredictor
{
    private MultipleLinearRegression model;
    private string[] FeatureNames => FeatureExtractor.FeatureNames;
    private int[] selectedFeatureIndices;
    private string[] selectedFeatureNames;
    private bool useFeatureSelection = false;
    public int maxFeaturesForTraining = 10;
    
    /**
     *  Called from RegressionUtilities.PerformRegressionAnalysis - main training entry point
     *  Trains the regression model on trial data to predict oxygen levels from game parameters.
     * 
     * Process:
     * 1. Extracts features and targets from trials
     * 2. Calculates adaptive Ridge regularization (higher for smaller datasets)
     * 3. If dataset < 10 trials and feature selection enabled: selects top K features, retrains
     * 4. Otherwise: trains on all features
     * 5. Validates model quality (R^2, error thresholds)
     * 
     */
    public bool TrainModel(List<TrialDataModels.TrialData> trials, bool enableFeatureSelection = true)
    {
        if (trials == null || trials.Count < 3)
        {
            Debug.LogError($"Need at least 3 trials for training, got {trials?.Count ?? 0}");
            return false;
        }

        useFeatureSelection = false;
        selectedFeatureIndices = null;
        selectedFeatureNames = null;
        
        var (X_full, Y) = FeatureExtractor.ExtractFeaturesAndTargets(trials); 
        float adaptiveRidgeLambda = Mathf.Clamp(0.5f + (10f - trials.Count) * 0.2f, 0.5f, 2.0f);

        if (enableFeatureSelection && trials.Count < 10)
        {
            var tempModel = TrainModelWithFeatures(X_full, Y, FeatureNames, adaptiveRidgeLambda);
            SelectTopFeatures(tempModel, trials);
            
            float[][] X = ExtractSelectedFeatures(X_full);
            model = TrainModelWithFeatures(X, Y, selectedFeatureNames, adaptiveRidgeLambda);
            useFeatureSelection = true;
        }
        else
        {
            model = TrainModelWithFeatures(X_full, Y, FeatureNames, adaptiveRidgeLambda);
            useFeatureSelection = false;
        }

        return ValidateModel(trials);
    }
    
    //  Predicts oxygen level from game parameters, clamped to [0, 100]%.
    public float PredictOxygen(TrialDataModels.TrialData parameters)
    {
        float unclamped = PredictOxygenUnclamped(parameters);
        return Mathf.Clamp(unclamped, 0f, 100f);
    }

    //  Predicts oxygen level from game parameters without clamping (can be <0 or >100%).
    //  Used during optimization to allow gradient descent to explore full parameter space.
    public float PredictOxygenUnclamped(TrialDataModels.TrialData parameters)
    {
        if (model == null)
        {
            Debug.LogError("Model not trained! Call TrainModel() first.");
            return -1f;
        }
        
        float[] fullFeatures = FeatureExtractor.ExtractFeatures(parameters);
        float[] features = ExtractSelectedFeaturesFromVector(fullFeatures);
        return model.Predict(features);
    }

    //  Returns feature importance ranking from the trained model.
    //  Used to determine which features to optimize during parameter search.
    public (string feature, float importance)[] GetFeatureImportance()
    {
        if (model == null)
        {
            Debug.LogWarning("Model not trained - cannot get feature importance");
            return new (string, float)[0];
        }
        
        return model.GetFeatureImportance();
    }

    //  Returns the underlying MultipleLinearRegression model.
    //  Used to access model coefficients, R^2, and other internal properties.
    public MultipleLinearRegression GetModel(){
        return model; 
    }

    //  Trains a MultipleLinearRegression model with given features and Ridge regularization.
     private MultipleLinearRegression TrainModelWithFeatures(float[][] X, float[] Y, string[] featureNames, float ridgeLambda)
    {
        var m = new MultipleLinearRegression(normalize: true);
        m.ridgeLambda = ridgeLambda;
        m.Fit(X, Y, featureNames);
        return m;
    }

   
     // Selects top K most important features, excluding banned features (EffectiveDrainRate, factorForce if no Amadeo trials).
     // Stores selected feature indices and names for later use in prediction.
    private void SelectTopFeatures(MultipleLinearRegression tempModel, List<TrialDataModels.TrialData> trials)
    {
        bool anyAmadeoTrials = trials.Any(t => t.IsAmadeoMode > 0.5f);
        var allImportance = tempModel.GetFeatureImportance();

        var banned = new HashSet<string> { "EffectiveDrainRate" };
        if (!anyAmadeoTrials)
            banned.Add("factorForce");

        var filtered = allImportance.Where(item => !banned.Contains(item.feature)).ToArray();
        if (filtered.Length == 0)
            filtered = allImportance;

        int K = Mathf.Clamp(maxFeaturesForTraining, 2, filtered.Length);
        selectedFeatureIndices = new int[K];
        selectedFeatureNames = new string[K];

        for (int i = 0; i < K; i++)
        {
            string fname = filtered[i].feature;
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
    }

    // Extracts selected features from a single feature vector (for prediction). Returns full features if feature selection is disabled.
    private float[] ExtractSelectedFeaturesFromVector(float[] fullFeatures)
    {
        if (!useFeatureSelection || selectedFeatureIndices == null)
            return fullFeatures;

        float[] features = new float[selectedFeatureIndices.Length];
        for (int i = 0; i < selectedFeatureIndices.Length; i++)
            features[i] = fullFeatures[selectedFeatureIndices[i]];

        return features;
    }
    
    
    // Extracts selected features from full feature matrix (for training).Returns full matrix if feature selection is disabled.
    private float[][] ExtractSelectedFeatures(float[][] X_full)
    {
        if (!useFeatureSelection || selectedFeatureIndices == null)
            return X_full;
        
        float[][] X_selected = new float[X_full.Length][];
        for (int i = 0; i < X_full.Length; i++)
        {
            X_selected[i] = new float[selectedFeatureIndices.Length];
            for (int j = 0; j < selectedFeatureIndices.Length; j++)
                X_selected[i][j] = X_full[i][selectedFeatureIndices[j]];
        }
        
        return X_selected;
    }
    
    /**
     * Validates trained model quality against training data.
     * 
     * Checks:
     * 1. Variance in oxygen data (must be > 0.1%)
     * 2. Average prediction error (must be below threshold based on dataset size)
     * 3. R^2 score (must be above threshold based on dataset size and feature selection)
     * 
     * Thresholds are more lenient for small datasets (<10 trials) and when using feature selection.
     * 
     * Called from: TrainModel
     * Calls internally: PredictOxygen 
     */
    private bool ValidateModel(List<TrialDataModels.TrialData> trials)
    {
        float totalError = 0f;
        for (int i = 0; i < trials.Count; i++)
        {
            float actual = trials[i].finalOxygenRemaining;
            float predicted = PredictOxygen(trials[i]);
            totalError += Mathf.Abs(actual - predicted);
        }
        
        float avgError = totalError / trials.Count;
        float variance = trials.Max(t => t.finalOxygenRemaining) - trials.Min(t => t.finalOxygenRemaining);
        
        if (variance < 0.1f)
        {
            Debug.LogWarning($"No variance in oxygen data: all values ~{trials[0].finalOxygenRemaining:F1}%");
            return false;
        }
        
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
        
        float r2 = float.IsNaN(model.rSquared) ? -1f : model.rSquared;
        bool isValid = r2 > minR2 && avgError < maxError;
        
        if (!isValid)
        {
            Debug.LogWarning($"Validation failed: R2={r2:F3} (need >{minR2:F2}), Error={avgError:F2}% (need <{maxError:F1}%)");
        }
        
        return isValid;
    }
}
