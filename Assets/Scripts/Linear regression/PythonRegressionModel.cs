using UnityEngine;
using System;
using System.IO;
using System.Linq;

/**
* Python-trained regression model loader and predictor  
* Loads model JSON exported from scikit-learn (Ridge/ElasticNet/Huber/PLS)
* Performs lightweight z-score normalization + dot product prediction
* Compatible with IL2CPP, Android, iOS - no external dependencies
*/

[Serializable]
public class PythonRegressionModel
{
    [Serializable]
    public class ModelData
    {
        public string[] feature_names;
        public float intercept;
        public float[] betas;
        public float[] means;
        public float[] stds;
        public string model_type;
        public int n_samples;
        public int n_features;
        public float train_mae;
        public float train_r2;
        public float train_rmse;
        
        // Optional model-specific parameters
        public float alpha;
        public float l1_ratio;
        public float epsilon;
        public int n_components;
    }

    private ModelData model;
    private bool isLoaded = false;

    /// <summary>
    /// Load model from JSON file
    /// </summary>
    public bool LoadFromJSON(string jsonPath)
    {
        try
        {
            if (!File.Exists(jsonPath))
            {
                Debug.LogError($"[PythonRegressionModel] JSON file not found: {jsonPath}");
                return false;
            }

            string json = File.ReadAllText(jsonPath);
            model = JsonUtility.FromJson<ModelData>(json);

            if (model == null || model.betas == null || model.means == null || model.stds == null)
            {
                Debug.LogError("[PythonRegressionModel] Invalid JSON structure");
                return false;
            }

            if (model.betas.Length != model.means.Length || model.betas.Length != model.stds.Length)
            {
                Debug.LogError($"[PythonRegressionModel] Dimension mismatch: betas={model.betas.Length}, means={model.means.Length}, stds={model.stds.Length}");
                return false;
            }

            // Validate that model is actually trained (not example/dummy)
            bool allBetasZero = model.betas.All(b => Mathf.Abs(b) < 1e-9f);
            if (model.n_samples == 0 || allBetasZero)
            {
                Debug.LogWarning($"[PythonRegressionModel] Model appears to be untrained (n_samples={model.n_samples}, all betas zero). Rejecting model.");
                Debug.LogWarning("   This might be the example file. Train a real model with Python first!");
                model = null;
                return false;
            }

            isLoaded = true;
            Debug.Log($"[PythonModel] Loaded {model.model_type}: {model.n_features} features, {model.n_samples} samples");
            Debug.Log($"   Train MAE={model.train_mae:F2}%, RMSE={model.train_rmse:F2}%, R^2={model.train_r2:F3}");
            
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[PythonRegressionModel] Failed to load JSON: {e.Message}\n{e.StackTrace}");
            return false;
        }
    }

    /// <summary>
    /// Predict oxygen level from trial data (clamped to [0, 100])
    /// </summary>
    public float PredictOxygen(TrialDataModels.TrialData trial)
    {
        float unclamped = PredictOxygenUnclamped(trial);
        return Mathf.Clamp(unclamped, 0f, 100f);
    }

    /// <summary>
    /// Predict oxygen level (unclamped, for optimization)
    /// </summary>
    public float PredictOxygenUnclamped(TrialDataModels.TrialData trial)
    {
        if (!isLoaded)
        {
            Debug.LogError("[PythonRegressionModel] Model not loaded!");
            return -1f;
        }

        // Extract features (same order as Python FEATURE_NAMES)
        float[] features = FeatureExtractor.ExtractFeatures(trial);

        return PredictFromFeatures(features);
    }

    /// <summary>
    /// Predict from raw feature array
    /// Formula: y_hat = intercept + Sum(beta_i * ((x_i - mu_i) / sigma_i))
    /// </summary>
    public float PredictFromFeatures(float[] features)
    {
        if (!isLoaded)
        {
            Debug.LogError("[PythonRegressionModel] Model not loaded!");
            return -1f;
        }

        if (features.Length != model.n_features)
        {
            Debug.LogError($"[PythonRegressionModel] Feature count mismatch: expected {model.n_features}, got {features.Length}");
            return -1f;
        }

        // 1. Z-score normalization: x_hat = (x - mu) / sigma
        float[] normalized = new float[features.Length];
        for (int i = 0; i < features.Length; i++)
        {
            float std = model.stds[i];
            // Avoid division by zero for constant features
            if (std < 1e-9f)
                std = 1.0f;
            
            normalized[i] = (features[i] - model.means[i]) / std;
        }

        // 2. Linear prediction: y_hat = intercept + Sum(beta_i * x_hat_i)
        float prediction = model.intercept;
        for (int i = 0; i < normalized.Length; i++)
        {
            prediction += model.betas[i] * normalized[i];
        }

        return prediction;
    }

    /// <summary>
    /// Get model info for debugging/UI
    /// </summary>
    public string GetModelInfo()
    {
        if (!isLoaded)
            return "Model not loaded";

        return $"Model Type: {model.model_type}\n" +
               $"Features: {model.n_features}\n" +
               $"Training Samples: {model.n_samples}\n" +
               $"Train MAE: {model.train_mae:F2}%\n" +
               $"Train RMSE: {model.train_rmse:F2}%\n" +
               $"Train R^2: {model.train_r2:F3}";
    }

    /// <summary>
    /// Get feature importance (sorted by |beta|)
    /// </summary>
    public (string feature, float importance)[] GetFeatureImportance()
    {
        if (!isLoaded || model.feature_names == null)
            return new (string, float)[0];

        var importance = new (string, float)[model.n_features];
        for (int i = 0; i < model.n_features; i++)
        {
            string featureName = (i < model.feature_names.Length) 
                ? model.feature_names[i] 
                : $"Feature_{i}";
            importance[i] = (featureName, Mathf.Abs(model.betas[i]));
        }

        return importance.OrderByDescending(x => x.Item2).ToArray();
    }

    public bool IsLoaded => isLoaded;
    public ModelData Model => model;
}