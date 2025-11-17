using UnityEngine;
using System;
using System.Linq;

/**
 * Multiple Linear Regression Model for Oxygen Prediction
 * Uses regularization, normalization, and cross-validation
 */
 
public class MultipleLinearRegression
{
    #region Constants
    
    private const float MIN_LAMBDA = 1e-9f;
    
    #endregion
    
    #region Public API - Model Parameters and Metrics
    
    public float[] coefficients;
    public float rSquared;
    public float adjustedRSquared;
    public float meanSquaredError;
    public float rootMeanSquaredError;

    #endregion

    #region Feature Information
    
    public int numFeatures;
    public int numSamples;
    public string[] featureNames;

    #endregion

    #region Normalization
    
    public FeatureNormalizer normalizer;  // Public for optimizer access
    private bool useNormalization;
    
    // Helper properties for cleaner access to normalization parameters
    public float[] Means => normalizer?.means;
    public float[] Stds => normalizer?.stdDevs;

    #endregion

    #region Regularization

    public float ridgeLambda = 0.5f;      // Ridge regularization strength (0.1-1.0 for small n)
    
    #endregion

    public MultipleLinearRegression(bool normalize = true)
    {
        useNormalization = normalize;
        if (normalize)
        {
            normalizer = new FeatureNormalizer();
        }
    }

    
    #region Model Training

    public void Fit(float[][] X, float[] Y, string[] featureNames = null)
    {
        if (!RegressionMath.ValidateInputs(X, Y, out string error))
        {
            Debug.LogError($"[MultipleLinearRegression] Invalid input: {error}");
            return;
        }

        // Clean NaN/Inf values before processing
        RegressionMath.CleanMatrix(X);
        RegressionMath.CleanVector(Y);

        numSamples = X.Length;
        numFeatures = X[0].Length;
        this.featureNames = featureNames;

        // 1) Normalize features
        float[][] Xprocessed = X;
        if (useNormalization)
        {
            normalizer.Fit(X);
            Xprocessed = normalizer.Transform(X);
        }

        // 2) Build design matrix Phi with intercept
        // Order: [1, x1, x2, ..., xk] (intercept at index 0)
        int m = numSamples;
        int d = numFeatures + 1;  // +1 for intercept
        double[,] Phi = new double[m, d];

        for (int i = 0; i < m; i++)
        {
            Phi[i, 0] = 1.0;  // intercept
            for (int j = 0; j < numFeatures; j++)
            {
                Phi[i, j + 1] = Xprocessed[i][j];
            }
        }

        // 3) Build normal equations: A = Phi^T Phi, b = Phi^T y
        double[,] A = new double[d, d];
        double[] b = new double[d];

        for (int i = 0; i < m; i++)
        {
            double yi = Y[i];
            for (int a = 0; a < d; a++)
            {
                double va = Phi[i, a];
                b[a] += va * yi;
                for (int c = 0; c < d; c++)
                {
                    A[a, c] += va * Phi[i, c];
                }
            }
        }

        // 4) Add Ridge regularization (do NOT penalize intercept at index 0)
        double lambdaEff = Math.Max(MIN_LAMBDA, ridgeLambda);
        for (int a = 1; a < d; a++)  // Start from 1 to skip intercept
        {
            A[a, a] += lambdaEff;
        }

        // 5) Solve using Cholesky decomposition (A is SPD due to Ridge)
        double[] beta = RegressionMath.SolveSPDByCholesky(A, b);

        if (beta == null)
        {
            Debug.LogError("Cholesky decomposition failed (matrix not positive definite)");
            return;
        }

        // 6) Store as float for public API
        coefficients = beta.Select(x => (float)x).ToArray();

        // 7) Calculate metrics on original X, Y
        CalculateMetrics(X, Y);
    }

    #endregion

    #region Prediction
    
    public float Predict(float[] features)
    {
        if (coefficients == null)
        {
            Debug.LogError("Model not fitted! Call Fit() first.");
            return 0f;
        }

        if (features == null || features.Length != numFeatures)
        {
            Debug.LogError($"Feature count mismatch: expected {numFeatures}, got {features?.Length ?? 0}");
            return 0f;
        }

        // Clean NaN/Inf values in input features
        RegressionMath.CleanVector(features);

        // Normalize if needed
        float[] x = useNormalization ? normalizer.TransformSample(features) : features;

      
        double yhat = coefficients[0];  // intercept
        for (int j = 0; j < x.Length; j++)
        {
            yhat += coefficients[j + 1] * x[j];
        }

        // Check for NaN/Inf in result
        if (double.IsNaN(yhat) || double.IsInfinity(yhat))
        {
            Debug.LogWarning("Prediction resulted in NaN/Inf, returning 0");
            return 0f;
        }

        return (float)yhat;
    }

    
    public float[] PredictBatch(float[][] X)
    {
        if (X == null || X.Length == 0) return null;

        var predictions = new float[X.Length];
        for (int i = 0; i < X.Length; i++)
        {
            predictions[i] = Predict(X[i]);
        }

        return predictions;
    }

    #endregion

    #region Cross-Validation
    
    public RegressionMetrics KFoldCV(float[][] X, float[] Y, int kFolds = 5, int? seed = null)
    {
        if (!RegressionMath.ValidateInputs(X, Y, out string error))
        {
            Debug.LogError($"[MultipleLinearRegression] K-Fold CV failed: {error}");
            return new RegressionMetrics(float.NaN, float.NaN, float.NaN);
        }

        // Clean NaN/Inf values before CV
        RegressionMath.CleanMatrix(X);
        RegressionMath.CleanVector(Y);

        int n = X.Length;
        int k = Mathf.Clamp(kFolds, 2, Mathf.Min(10, n));

        // Shuffle indices
        var idx = Enumerable.Range(0, n).ToArray();
        var rand = seed.HasValue ? new System.Random(seed.Value) : new System.Random();

        for (int i = n - 1; i > 0; i--)
        {
            int j = rand.Next(i + 1);
            (idx[i], idx[j]) = (idx[j], idx[i]);
        }

        // Build fold sizes that cover ALL n samples
        int baseSize = n / k;
        int remainder = n % k;

        int start = 0;
        var folds = new (int start, int len)[k];
        for (int f = 0; f < k; f++)
        {
            int len = baseSize + (f < remainder ? 1 : 0);
            folds[f] = (start, len);
            start += len;
        }

        double rmseSum = 0;
        double maeSum = 0;
        double r2Sum = 0;
        int foldsCounted = 0;

        for (int f = 0; f < k; f++)
        {
            var (s, len) = folds[f];
            if (len <= 0) continue;
            
            var testIdx = idx.Skip(s).Take(len).ToArray();
            var trainIdx = idx.Where(ii => ii < s || ii >= s + len).ToArray();

            int foldNumFeatures = (X != null && X.Length > 0) ? X[0].Length : numFeatures;
            
            // Need enough training samples: at least (features + 1)
            if (trainIdx.Length < Math.Max(2, foldNumFeatures + 1))
            {
                Debug.LogWarning($"Fold {f + 1}: Not enough training samples ({trainIdx.Length}), skipping");
                continue;
            }

            // Build train/test sets
            var Xtr = RegressionMath.SubsetX(X, trainIdx);
            var Ytr = RegressionMath.SubsetY(Y, trainIdx);
            var Xte = RegressionMath.SubsetX(X, testIdx);
            var Yte = RegressionMath.SubsetY(Y, testIdx);

            // Train model on this fold
            var model = new MultipleLinearRegression(normalize: useNormalization)
            {
                ridgeLambda = this.ridgeLambda
            };
            model.Fit(Xtr, Ytr, featureNames);

            // Predict on test fold
            var preds = model.PredictBatch(Xte);

            // Calculate metrics for this fold
            var metrics = RegressionMath.ComputeMetrics(Yte, preds);
            
            // Check for NaN/Inf before adding to sum
            if (float.IsNaN(metrics.RMSE) || float.IsNaN(metrics.R2) || float.IsInfinity(metrics.RMSE) || float.IsInfinity(metrics.R2))
            {
                Debug.LogWarning($"Fold {f + 1}: NaN/Inf metrics detected, skipping");
                continue;
            }

            rmseSum += metrics.RMSE;
            maeSum += metrics.MAE;
            r2Sum += metrics.R2;
            foldsCounted++;
        }

        if (foldsCounted == 0)
        {
            Debug.LogWarning("K-Fold CV: No valid folds");
            return new RegressionMetrics(float.NaN, float.NaN, float.NaN);
        }

        float avgRMSE = (float)(rmseSum / foldsCounted);
        float avgMAE = (float)(maeSum / foldsCounted);
        float avgR2 = (float)(r2Sum / foldsCounted);

        return new RegressionMetrics(avgRMSE, avgMAE, avgR2);
    }
    
    #endregion

    #region Feature Importance

    public (string feature, float importance)[] GetFeatureImportance()
    {
        if (coefficients == null || coefficients.Length < 2) return null;

        var importance = new (string, float)[numFeatures];

        for (int i = 0; i < numFeatures; i++)
        {
            string name = (featureNames != null && i < featureNames.Length)
                ? featureNames[i]
                : $"Feature_{i}";

            float value = Mathf.Abs(coefficients[i + 1]);  // Skip intercept at index 0
            importance[i] = (name, value);
        }

        // Sort by importance (descending)
        return importance.OrderByDescending(x => x.Item2).ToArray();
    }

    #endregion

    #region Metrics Calculation

    private void CalculateMetrics(float[][] X, float[] Y)
    {
        var predictions = PredictBatch(X);
        var metrics = RegressionMath.ComputeMetrics(Y, predictions);

        rSquared = metrics.R2;

        int n = Y.Length;
        int k = numFeatures;

        // Adjusted R2 (accounts for number of features)
        adjustedRSquared = (n - k - 1 > 0)
            ? (1f - ((1f - rSquared) * (n - 1) / (n - k - 1)))
            : float.NaN;

        meanSquaredError = metrics.RMSE * metrics.RMSE;
        rootMeanSquaredError = metrics.RMSE;
    }
    
    #endregion
}
