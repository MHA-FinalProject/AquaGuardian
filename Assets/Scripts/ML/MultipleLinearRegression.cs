using UnityEngine;
using System;
using System.Linq;

/// <summary>
/// Multiple Linear Regression with Ridge regularization + Cholesky solver
/// Uses double precision internally for numerical stability
/// Ridge prevents overfitting when N is small (e.g., 5 samples)
/// Cholesky decomposition is more stable than Gaussian elimination
/// </summary>
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

    // Normalizer
    private FeatureNormalizer normalizer;
    private bool useNormalization;

    // Ridge regularization strength (0.1-1.0 for small N)
    public float ridgeLambda = 0.5f;

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
    /// Fit model to training data using Ridge regression with Cholesky solver
    /// </summary>
    /// <param name="X">Feature matrix [m x n] (without intercept)</param>
    /// <param name="Y">Target vector [m]</param>
    /// <param name="featureNames">Optional feature names</param>
    public void Fit(float[][] X, float[] Y, string[] featureNames = null)
    {
        if (X == null || Y == null || X.Length == 0 || X.Length != Y.Length)
        {
            Debug.LogError("Invalid input data for regression");
            return;
        }

        numSamples = X.Length;
        numFeatures = X[0].Length;
        this.featureNames = featureNames;

        Debug.Log($"=== FITTING MULTIPLE LINEAR REGRESSION (Ridge + Cholesky) ===");
        Debug.Log($"Samples: {numSamples}, Features: {numFeatures}, Lambda: {ridgeLambda:F3}");

        // 1) Normalize features
        float[][] Xprocessed = X;
        if (useNormalization)
        {
            normalizer.Fit(X);
            Xprocessed = normalizer.Transform(X);
            Debug.Log("Features normalized (z-score, sample std)");
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
        double lambda = ridgeLambda;
        for (int a = 1; a < d; a++)  // Start from 1 to skip intercept
        {
            A[a, a] += lambda;
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

        Debug.Log($"Coefficients calculated: {coefficients.Length}");

        // 7) Calculate metrics on original X, Y
        CalculateMetrics(X, Y);
        
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
        float[] x = useNormalization ? normalizer.TransformSample(features) : features;

        // y = β0 + β1*x1 + β2*x2 + ... + βn*xn
        double yhat = coefficients[0];  // intercept
        for (int j = 0; j < x.Length; j++)
        {
            yhat += coefficients[j + 1] * x[j];
        }

        return (float)yhat;
    }

    /// <summary>
    /// Predict for multiple samples
    /// </summary>
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

    /// <summary>
    /// K-Fold Cross Validation
    /// Returns average RMSE, MAE, R2 across folds
    /// Trains a fresh model on each fold
    /// </summary>
    public (float rmse, float mae, float r2) KFoldCV(float[][] X, float[] Y, int kFolds = 5, int? seed = null)
    {
        int n = X.Length;
        int k = Mathf.Clamp(kFolds, 2, Mathf.Min(10, n));

        Debug.Log($"\n=== K-FOLD CROSS VALIDATION ({k} folds) ===");

        // Shuffle indices
        var idx = Enumerable.Range(0, n).ToArray();
        var rand = seed.HasValue ? new System.Random(seed.Value) : new System.Random();

        for (int i = n - 1; i > 0; i--)
        {
            int j = rand.Next(i + 1);
            (idx[i], idx[j]) = (idx[j], idx[i]);
        }

        int foldSize = Math.Max(1, n / k);
        double rmseSum = 0;
        double maeSum = 0;
        double r2Sum = 0;
        int foldsCounted = 0;

        for (int f = 0; f < k; f++)
        {
            int start = f * foldSize;
            int end = Math.Min(n, start + foldSize);
            var testIdx = idx.Skip(start).Take(end - start).ToArray();
            var trainIdx = idx.Where(ii => ii < start || ii >= end).ToArray();

            // Need enough training samples
            if (trainIdx.Length < numFeatures + 1)
            {
                Debug.LogWarning($"Fold {f + 1}: Not enough training samples, skipping");
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
            
            Debug.Log($"Fold {f + 1}: RMSE={rmse:F3}, MAE={mae:F3}, R2={r2:F3}");
            
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

        Debug.Log($"\n=== CV AVERAGE ===");
        Debug.Log($"RMSE: {avgRMSE:F3}, MAE: {avgMAE:F3}, R2: {avgR2:F3}");

        return (avgRMSE, avgMAE, avgR2);
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

            float value = Mathf.Abs(coefficients[i + 1]);  // Skip intercept at index 0
            importance[i] = (name, value);
        }

        // Sort by importance (descending)
        return importance.OrderByDescending(x => x.Item2).ToArray();
    }

    // ========== PRIVATE HELPER METHODS ==========

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

    /// <summary>
    /// Print model summary
    /// </summary>
    public void PrintModelSummary()
    {
        Debug.Log("\n=== RIDGE MODEL SUMMARY ===");
        Debug.Log($"Lambda (regularization): {ridgeLambda:F3}");
        Debug.Log($"R2 Score: {rSquared:F4} ({(rSquared * 100f):F1}% variance explained)");
        
        if (!float.IsNaN(adjustedRSquared))
        {
            Debug.Log($"Adjusted R2: {adjustedRSquared:F4}");
        }
        
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

        double rmse = Math.Sqrt(mse / Math.Max(1, n));
        double r2 = (ssTot <= 1e-12)
            ? ((ssRes <= 1e-12) ? 1.0 : 0.0)
            : (1.0 - ssRes / ssTot);

        return ((float)rmse, (float)(mae / n), (float)r2);
    }

    /// <summary>
    /// Solve SPD system using Cholesky decomposition
    /// More stable than Gaussian elimination with partial pivoting
    /// </summary>
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
