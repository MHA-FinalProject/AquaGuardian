using UnityEngine;
using System;
using System.Linq;

/**
 * Multiple Linear Regression with Ridge regularization + Cholesky solver
 * Uses double precision internally for numerical stability
 * Ridge prevents overfitting when N is small (e.g., 5 samples)
 * Cholesky decomposition is more stable than Gaussian elimination
 */
public class MultipleLinearRegression
{
    // Model parameters (public API in float)
    public float[] coefficients;
    public float rSquared;
    public float adjustedRSquared;
    public float meanSquaredError;
    public float rootMeanSquaredError;

    // Feature info
    public int numFeatures;
    public int numSamples;
    public string[] featureNames;

    // Normalizer (public access for DifficultyParameterSolver)
    public FeatureNormalizer normalizer;  // FIXED: Made public for normalized-space optimization
    private bool useNormalization;
    
    // ADDED: Helper properties for cleaner access to normalization parameters
    public float[] Means => normalizer?.means;
    public float[] Stds => normalizer?.stdDevs;

    // Ridge regularization strength (0.1-1.0 for small N)
    public float ridgeLambda = 0.5f;


    public MultipleLinearRegression(bool normalize = true)
    {
        useNormalization = normalize;
        if (normalize)
        {
            normalizer = new FeatureNormalizer();
        }
    }

    // ========== NaN/Inf Protection Helpers ==========
    
    private static bool IsBad(float v) => float.IsNaN(v) || float.IsInfinity(v);
    
    private static void CleanVector(float[] v, float replacement = 0f)
    {
        if (v == null) return;
        for (int i = 0; i < v.Length; i++)
            if (IsBad(v[i])) v[i] = replacement;
    }
    
    private static void CleanMatrix(float[][] X, float replacement = 0f)
    {
        if (X == null) return;
        for (int i = 0; i < X.Length; i++)
            CleanVector(X[i], replacement);
    }
    
    // ==================================================

    public void Fit(float[][] X, float[] Y, string[] featureNames = null)
    {
        if (X == null || Y == null || X.Length == 0 || X.Length != Y.Length)
        {
            Debug.LogError("Invalid input data for regression");
            return;
        }

        // Clean NaN/Inf values before processing
        CleanMatrix(X);
        CleanVector(Y);

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
        double lambdaEff = Math.Max(1e-9, ridgeLambda); // Ensure lambda is never zero
        for (int a = 1; a < d; a++)  // Start from 1 to skip intercept
        {
            A[a, a] += lambdaEff;
        }

        // 5) Solve using Cholesky decomposition (A is SPD due to Ridge)
        double[] beta = SolveSPDByCholesky(A, b);

        if (beta == null)
        {
            Debug.LogError("Cholesky decomposition failed (matrix not positive definite)");
            return;
        }

        // 6) Store as float for public API
        coefficients = beta.Select(x => (float)x).ToArray();

        // 7) Calculate metrics on original X, Y
        CalculateMetrics(X, Y);

        PrintModelSummary();
    }

    
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
        CleanVector(features);

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

    /*
    K-Fold Cross Validation - improved to cover ALL samples
    Returns average RMSE, MAE, R2 across folds
    Trains a fresh model on each fold
    */
    public (float rmse, float mae, float r2) KFoldCV(float[][] X, float[] Y, int kFolds = 5, int? seed = null)
    {
        if (X == null || Y == null || X.Length == 0 || X.Length != Y.Length)
            return (float.NaN, float.NaN, float.NaN);

        // Clean NaN/Inf values before CV
        CleanMatrix(X);
        CleanVector(Y);

        int n = X.Length;
        int k = Mathf.Clamp(kFolds, 2, Mathf.Min(10, n));

       // Debug.Log($"\n=== K-FOLD CROSS VALIDATION ({k} folds) ===");

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

            // FIXED: Use fold-specific feature count from X, not object's numFeatures
            int foldNumFeatures = (X != null && X.Length > 0) ? X[0].Length : numFeatures;
            
            // Need enough training samples: at least (features + 1)
            if (trainIdx.Length < Math.Max(2, foldNumFeatures + 1))
            {
                Debug.LogWarning($"Fold {f + 1}: Not enough training samples ({trainIdx.Length}), skipping");
                continue;
            }

            // Build train/test sets
            var Xtr = SubsetX(X, trainIdx);
            var Ytr = SubsetY(Y, trainIdx);
            var Xte = SubsetX(X, testIdx);
            var Yte = SubsetY(Y, testIdx);

            // Train model on this fold
            var model = new MultipleLinearRegression(normalize: useNormalization)
            {
                ridgeLambda = this.ridgeLambda
            };
            model.Fit(Xtr, Ytr, featureNames);

            // Predict on test fold
            var preds = model.PredictBatch(Xte);

            // Calculate metrics for this fold
            var (rmse, mae, r2) = ComputeMetrics(Yte, preds);
            
            // Check for NaN/Inf before adding to sum
            if (float.IsNaN(rmse) || float.IsNaN(r2) || float.IsInfinity(rmse) || float.IsInfinity(r2))
            {
                Debug.LogWarning($"Fold {f + 1}: NaN/Inf metrics detected, skipping");
                continue;
            }

            rmseSum += rmse;
            maeSum += mae;
            r2Sum += r2;
            foldsCounted++;
        }

        if (foldsCounted == 0)
        {
            Debug.LogWarning("K-Fold CV: No valid folds");
            return (float.NaN, float.NaN, float.NaN);
        }

        float avgRMSE = (float)(rmseSum / foldsCounted);
        float avgMAE = (float)(maeSum / foldsCounted);
        float avgR2 = (float)(r2Sum / foldsCounted);

      //  Debug.Log($"\n=== CV AVERAGE ({foldsCounted} folds) ===");
      //  Debug.Log($"RMSE: {avgRMSE:F3}, MAE: {avgMAE:F3}, R2: {avgR2:F3}");

        return (avgRMSE, avgMAE, avgR2);
    }

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


    private void CalculateMetrics(float[][] X, float[] Y)
    {
        var predictions = PredictBatch(X);
        var (rmse, mae, r2) = ComputeMetrics(Y, predictions);

        rSquared = r2;

        int n = Y.Length;
        int k = numFeatures;

        // Adjusted R2 (accounts for number of features)
        adjustedRSquared = (n - k - 1 > 0)
            ? (1f - ((1f - rSquared) * (n - 1) / (n - k - 1)))
            : float.NaN;

        meanSquaredError = rmse * rmse;
        rootMeanSquaredError = rmse;
    }

    // Print model summary (disabled - only critical errors are logged)
    public void PrintModelSummary()
    {
        // Logging disabled to reduce console clutter
        // Only errors and warnings are logged
    }

    // ========== MATRIX OPERATIONS ==========

    private static float[][] SubsetX(float[][] X, int[] idx)
    {
        var result = new float[idx.Length][];
        for (int r = 0; r < idx.Length; r++)
        {
            int i = idx[r];
            result[r] = new float[X[i].Length];
            Array.Copy(X[i], result[r], X[i].Length);
        }
        return result;
    }

    private static float[] SubsetY(float[] y, int[] idx)
    {
        var result = new float[idx.Length];
        for (int r = 0; r < idx.Length; r++)
        {
            result[r] = y[idx[r]];
        }
        return result;
    }

    private static (float rmse, float mae, float r2) ComputeMetrics(float[] y, float[] yhat)
    {
        int n = y.Length;
        
        // Handle empty arrays
        if (n == 0)
        {
            return (float.NaN, float.NaN, float.NaN);
        }
        
        double mse = 0;
        double mae = 0;
        double mean = y.Average();
        double ssTot = 0;
        double ssRes = 0;

        for (int i = 0; i < n; i++)
        {
            double error = yhat[i] - y[i];
            mse += error * error;
            mae += Math.Abs(error);
            ssRes += error * error;

            double deviation = y[i] - mean;
            ssTot += deviation * deviation;
        }

        double rmse = Math.Sqrt(mse / n);
        double r2 = (ssTot <= 1e-12)
            ? ((ssRes <= 1e-12) ? 1.0 : 0.0)
            : (1.0 - ssRes / ssTot);

        return ((float)rmse, (float)(mae / n), (float)r2);
    }

    /*
    Solve SPD system using Cholesky decomposition
    More stable than Gaussian elimination with partial pivoting
    */
    private static double[] SolveSPDByCholesky(double[,] A, double[] b)
    {
        int n = A.GetLength(0);
        var L = new double[n, n];

        // Cholesky decomposition: A = L L^T
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j <= i; j++)
            {
                double s = A[i, j];
                for (int k = 0; k < j; k++)
                {
                    s -= L[i, k] * L[j, k];
                }

                if (i == j)
                {
                    if (s <= 1e-12)
                    {
                        Debug.LogWarning($"Diagonal element {i} too small ({s:E3}), using fallback");
                        s = 1e-12;
                    }
                    L[i, j] = Math.Sqrt(s);
                }
                else
                {
                    L[i, j] = s / L[j, j];
                }
            }
        }

        // Forward substitution: L y = b
        var y = new double[n];
        for (int i = 0; i < n; i++)
        {
            double s = b[i];
            for (int k = 0; k < i; k++)
            {
                s -= L[i, k] * y[k];
            }
            y[i] = s / L[i, i];
        }

        // Backward substitution: L^T x = y
        var x = new double[n];
        for (int i = n - 1; i >= 0; i--)
        {
            double s = y[i];
            for (int k = i + 1; k < n; k++)
            {
                s -= L[k, i] * x[k];
            }
            x[i] = s / L[i, i];
        }

        return x;
    }
}

public static class RegressionNormalizationExtensions
{
    public static float ToNormalized(this MultipleLinearRegression model, int featureIndex, float rawValue)
    {
        if (model?.Means == null || model.Stds == null || featureIndex >= model.Means.Length)
            return rawValue;
        
        float mean = model.Means[featureIndex];
        float std = Mathf.Max(1e-6f, model.Stds[featureIndex]);
        return (rawValue - mean) / std;
    }
    
    public static float FromNormalized(this MultipleLinearRegression model, int featureIndex, float normalizedValue)
    {
        if (model?.Means == null || model.Stds == null || featureIndex >= model.Means.Length)
            return normalizedValue;
        
        float mean = model.Means[featureIndex];
        float std = model.Stds[featureIndex];
        return normalizedValue * std + mean;
    }
}
