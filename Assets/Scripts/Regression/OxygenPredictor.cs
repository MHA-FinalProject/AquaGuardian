using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/**
 * Predicts final oxygen levels from game parameters using Multiple Linear Regression
 * 
 * Trains on trial data to predict oxygen % based on 10 game parameters (speed, verticalSpeed, etc.).
 * Uses adaptive Ridge regularization and optional feature selection for small datasets (<10 trials).
 * Used by DifficultyParameterSolver for parameter optimization.
 * 
 * See also: FeatureExtractor, MultipleLinearRegression, DifficultyParameterSolver, TrialRegressionAlgorithm
 */
public class OxygenPredictor
{
    private MultipleLinearRegression model;
    private string[] FeatureNames => FeatureExtractor.FeatureNames;
    
    private int[] selectedFeatureIndices;
    private string[] selectedFeatureNames;
    private bool useFeatureSelection = false;

    public int maxFeaturesForTraining = 10;
    
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
    
    public float PredictOxygen(TrialDataModels.TrialData parameters)
    {
        float unclamped = PredictOxygenUnclamped(parameters);
        return Mathf.Clamp(unclamped, 0f, 100f);
    }

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

    public (string feature, float importance)[] GetFeatureImportance()
    {
        if (model == null)
        {
            Debug.LogWarning("Model not trained - cannot get feature importance");
            return new (string, float)[0];
        }
        
        return model.GetFeatureImportance();
    }

    public MultipleLinearRegression GetModel()
        {
        return model;
    }

    // Train model with given features and regularization
    private MultipleLinearRegression TrainModelWithFeatures(float[][] X, float[] Y, string[] featureNames, float ridgeLambda)
    {
        var m = new MultipleLinearRegression(normalize: true);
        m.ridgeLambda = ridgeLambda;
        m.Fit(X, Y, featureNames);
        return m;
    }

    // Select top K features based on importance
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

    // Extract selected features from full feature vector
    private float[] ExtractSelectedFeaturesFromVector(float[] fullFeatures)
    {
        if (!useFeatureSelection || selectedFeatureIndices == null)
            return fullFeatures;

        float[] features = new float[selectedFeatureIndices.Length];
        for (int i = 0; i < selectedFeatureIndices.Length; i++)
            features[i] = fullFeatures[selectedFeatureIndices[i]];

        return features;
    }
    
    // Extract selected features from full feature matrix
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
    
    // Validate trained model against training data
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
