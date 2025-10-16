using UnityEngine;
using System.Linq;

/// <summary>
/// Multiple Linear Regression using Ordinary Least Squares (OLS)
/// Y = β₀ + β₁X₁ + β₂X₂ + ... + βₙXₙ
/// Solves: β = (XᵀX)⁻¹XᵀY
/// </summary>
public class MultipleLinearRegression
{
    // Model parameters
    public float[] coefficients;      // β₀, β₁, ..., βₙ (includes intercept)
    public float rSquared;             // R² score (0-1, higher is better)
    public float adjustedRSquared;     // Adjusted R² (accounts for features)
    public float meanSquaredError;     // MSE
    public float rootMeanSquaredError; // RMSE
    
    // Feature info
    public int numFeatures;
    public int numSamples;
    public string[] featureNames;
    
    // Normalizer (optional but recommended)
    private FeatureNormalizer normalizer;
    private bool useNormalization;
    
    /// <summary>
    /// Constructor
    /// </summary>
    public MultipleLinearRegression(bool normalize = true)
    {
        useNormalization = normalize;
        if (normalize)
        {
            normalizer = new FeatureNormalizer();
        }
    }
    
    /// <summary>
    /// Fit model to training data
    /// X: [m x n] feature matrix (m samples, n features)
    /// Y: [m] target vector
    /// </summary>
    public void Fit(float[][] X, float[] Y, string[] featureNames = null)
    {
        if (X == null || Y == null || X.Length != Y.Length)
        {
            Debug.LogError("Invalid input data for regression");
            return;
        }
        
        numSamples = X.Length;
        numFeatures = X[0].Length;
        this.featureNames = featureNames;
        
        Debug.Log($"=== FITTING MULTIPLE LINEAR REGRESSION ===");
        Debug.Log($"Samples: {numSamples}, Features: {numFeatures}");
        
        // Normalize features if enabled
        float[][] X_processed = X;
        if (useNormalization)
        {
            normalizer.Fit(X);
            X_processed = normalizer.Transform(X);
            Debug.Log("Features normalized");
        }
        
        // Add intercept column (all 1s)
        float[][] X_with_intercept = MatrixHelper.AddInterceptColumn(X_processed);
        
        // Calculate β = (XᵀX)⁻¹XᵀY
        // Step 1: Xᵀ
        float[][] XT = MatrixHelper.Transpose(X_with_intercept);
        
        // Step 2: XᵀX
        float[][] XTX = MatrixHelper.Multiply(XT, X_with_intercept);
        
        // Step 3: (XᵀX)⁻¹
        float[][] XTX_inv = MatrixHelper.Inverse(XTX);
        if (XTX_inv == null)
        {
            Debug.LogError("Failed to invert XᵀX matrix - data may be collinear");
            return;
        }
        
        // Step 4: (XᵀX)⁻¹Xᵀ
        float[][] XTX_inv_XT = MatrixHelper.Multiply(XTX_inv, XT);
        
        // Step 5: β = (XᵀX)⁻¹XᵀY
        coefficients = MatrixHelper.MultiplyVector(XTX_inv_XT, Y);
        
        Debug.Log($"Coefficients calculated: {coefficients.Length}");
        
        // Calculate performance metrics
        CalculateMetrics(X, Y);
        
        // Print results
        PrintModelSummary();
    }
    
    /// <summary>
    /// Predict target value for new features
    /// </summary>
    public float Predict(float[] features)
    {
        if (coefficients == null)
        {
            Debug.LogError("Model not fitted! Call Fit() first.");
            return 0f;
        }
        
        if (features.Length != numFeatures)
        {
            Debug.LogError($"Feature count mismatch: expected {numFeatures}, got {features.Length}");
            return 0f;
        }
        
        // Normalize if needed
        float[] features_processed = features;
        if (useNormalization)
        {
            features_processed = normalizer.TransformSample(features);
        }
        
        // Y = β₀ + β₁X₁ + β₂X₂ + ... + βₙXₙ
        float prediction = coefficients[0]; // Intercept
        for (int i = 0; i < features_processed.Length; i++)
        {
            prediction += coefficients[i + 1] * features_processed[i];
        }
        
        return prediction;
    }
    
    /// <summary>
    /// Predict for multiple samples
    /// </summary>
    public float[] PredictBatch(float[][] X)
    {
        if (X == null || X.Length == 0) return null;
        
        float[] predictions = new float[X.Length];
        for (int i = 0; i < X.Length; i++)
        {
            predictions[i] = Predict(X[i]);
        }
        
        return predictions;
    }
    
    /// <summary>
    /// Calculate model performance metrics
    /// </summary>
    private void CalculateMetrics(float[][] X, float[] Y)
    {
        // Make predictions
        float[] predictions = PredictBatch(X);
        
        // Calculate mean of Y
        float meanY = Y.Average();
        
        // Calculate SS_total and SS_residual
        float ssTotal = 0f;
        float ssResidual = 0f;
        
        for (int i = 0; i < Y.Length; i++)
        {
            float residual = Y[i] - predictions[i];
            ssResidual += residual * residual;
            
            float deviation = Y[i] - meanY;
            ssTotal += deviation * deviation;
        }
        
        // R² = 1 - (SS_residual / SS_total)
        rSquared = 1f - (ssResidual / ssTotal);
        
        // Adjusted R² = 1 - [(1-R²)(n-1)/(n-k-1)]
        int n = numSamples;
        int k = numFeatures;
        adjustedRSquared = 1f - ((1f - rSquared) * (n - 1) / (n - k - 1));
        
        // MSE and RMSE
        meanSquaredError = ssResidual / n;
        rootMeanSquaredError = Mathf.Sqrt(meanSquaredError);
    }
    
    /// <summary>
    /// Print model summary
    /// </summary>
    public void PrintModelSummary()
    {
        Debug.Log("=== MODEL SUMMARY ===");
        Debug.Log($"R² Score: {rSquared:F4} ({(rSquared * 100):F1}% variance explained)");
        Debug.Log($"Adjusted R²: {adjustedRSquared:F4}");
        Debug.Log($"RMSE: {rootMeanSquaredError:F3}");
        Debug.Log($"MSE: {meanSquaredError:F3}");
        
        Debug.Log("\nCOEFFICIENTS:");
        Debug.Log($"  Intercept (β₀): {coefficients[0]:F4}");
        
        for (int i = 1; i < coefficients.Length; i++)
        {
            string name = (featureNames != null && i - 1 < featureNames.Length) 
                ? featureNames[i - 1] 
                : $"X{i}";
            Debug.Log($"  {name} (β{i}): {coefficients[i]:F4}");
        }
    }
    
    /// <summary>
    /// Get feature importance (absolute coefficient values)
    /// </summary>
    public (string feature, float importance)[] GetFeatureImportance()
    {
        if (coefficients == null || coefficients.Length < 2) return null;
        
        var importance = new (string, float)[numFeatures];
        
        for (int i = 0; i < numFeatures; i++)
        {
            string name = (featureNames != null && i < featureNames.Length) 
                ? featureNames[i] 
                : $"Feature_{i}";
            
            float value = Mathf.Abs(coefficients[i + 1]);
            importance[i] = (name, value);
        }
        
        // Sort by importance (descending)
        return importance.OrderByDescending(x => x.Item2).ToArray();
    }
}

