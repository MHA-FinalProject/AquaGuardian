using System;
using System.Linq;
using UnityEngine;

/**
 * Utilities for regression optimization and matrix operations
 * Handles chain rule derivatives, linear algebra, metrics calculation, and data cleaning
 */
public static class RegressionMath
{
    #region Constants
    
    private const float MIN_CHOLESKY_DIAGONAL = 1e-12f;
    private const float MIN_VARIANCE_THRESHOLD = 1e-12f;
    private const float NAN_REPLACEMENT = 0f;
    
    // Feature indices (matching TrialDataModels.FeatureNames)
    private const int IDX_LIFETIME = 3;
    private const int IDX_REMOVE_HEALTH = 4;
    private const int IDX_EFFECTIVE_DRAIN_RATE = 9;
    
    #endregion

    #region Gradient Descent Optimization

    // Calculate effective coefficient including chain rule for derived features
    public static float EffectiveBeta(
        MultipleLinearRegression model,
        int featureIndex,
        TrialDataModels.TrialData currentParams,
        bool optimizingDerivedDependencies)
    {
        string[] fullFeatureNames = FeatureExtractor.FeatureNames;
        string[] modelFeatureNames = model.featureNames ?? fullFeatureNames;

        if (featureIndex < 0 || featureIndex >= fullFeatureNames.Length)
        {
            Debug.LogWarning($"[RegressionMath] Invalid feature index: {featureIndex}");
            return 0f;
        }

        string featureName = fullFeatureNames[featureIndex];
        int modelIdx = System.Array.IndexOf(modelFeatureNames, featureName);

        if (modelIdx < 0 || modelIdx + 1 >= model.coefficients.Length)
            return 0f;  // Feature not in model

        // Get direct coefficient
        float beta = model.coefficients[modelIdx + 1];

        // If not optimizing derived dependencies, return direct beta only
        if (!optimizingDerivedDependencies)
            return beta;

        // Add EffectiveDrainRate contribution via chain rule
        int drainRateModelIdx = System.Array.IndexOf(modelFeatureNames, "EffectiveDrainRate");
        float effectiveDrainRateBeta = (drainRateModelIdx >= 0 && drainRateModelIdx + 1 < model.coefficients.Length)
            ? model.coefficients[drainRateModelIdx + 1] : 0f;

        // Chain rule for lifeTime: dEDR/d lifeTime = -RemoveHealthEveryLifeTime / (lifeTime^2)
        if (featureIndex == IDX_LIFETIME)
        {
            float currentDrop = ParameterHelper.Get(currentParams, IDX_REMOVE_HEALTH);
            float currentLife = ParameterHelper.Get(currentParams, IDX_LIFETIME);
            if (currentLife > 0.1f)
            {
                float dEDR_dLife = -currentDrop / (currentLife * currentLife);
                float stdDevLife = model.Stds?.ElementAtOrDefault(IDX_LIFETIME) ?? 1.0f;
                float stdDevEDR = model.Stds?.ElementAtOrDefault(IDX_EFFECTIVE_DRAIN_RATE) ?? 1.0f;
                float normalizedDerivative = dEDR_dLife * stdDevLife / Mathf.Max(1e-6f, stdDevEDR);
                beta += effectiveDrainRateBeta * normalizedDerivative;
            }
        }
        // Chain rule for RemoveHealthEveryLifeTime: dEDR/d drop = 1 / lifeTime
        else if (featureIndex == IDX_REMOVE_HEALTH)
        {
            float currentLife = ParameterHelper.Get(currentParams, IDX_LIFETIME);
            if (currentLife > 0.1f)
            {
                float dEDR_dDrop = 1.0f / currentLife;
                float stdDevDrop = model.Stds?.ElementAtOrDefault(IDX_REMOVE_HEALTH) ?? 1.0f;
                float stdDevEDR = model.Stds?.ElementAtOrDefault(IDX_EFFECTIVE_DRAIN_RATE) ?? 1.0f;
                float normalizedDerivative = dEDR_dDrop * stdDevDrop / Mathf.Max(1e-6f, stdDevEDR);
                beta += effectiveDrainRateBeta * normalizedDerivative;
            }
        }

        return beta;
    }

    // Calculate sum of squared betas for global normalization in gradient descent
    public static float SumBetaSq(
        MultipleLinearRegression model,
        int[] freeFeatures,
        TrialDataModels.TrialData currentParams)
    {
        bool optimizingDerivedDependencies = freeFeatures.Contains(IDX_LIFETIME) || freeFeatures.Contains(IDX_REMOVE_HEALTH);
        
        float sumBetaSq = 0f;
        foreach (int j in freeFeatures)
        {
            float beta = EffectiveBeta(model, j, currentParams, optimizingDerivedDependencies);
            sumBetaSq += beta * beta;
        }
        
        return Mathf.Max(sumBetaSq, 1e-6f);
    }
    
    #endregion
    
    #region NaN/Inf Protection
    
  
    public static bool IsBad(float v) => float.IsNaN(v) || float.IsInfinity(v);
    
  
    public static void CleanVector(float[] v, float replacement = NAN_REPLACEMENT)
    {
        if (v == null) return;
        int badCount = 0;
        for (int i = 0; i < v.Length; i++)
        {
            if (IsBad(v[i]))
            {
                v[i] = replacement;
                badCount++;
            }
        }
        if (badCount > 0)
        {
            Debug.LogWarning($"[RegressionMath] Replaced {badCount} NaN/Inf value(s) with {replacement}");
        }
    }
    
    // Clean NaN/Inf values in a matrix
    public static void CleanMatrix(float[][] X, float replacement = NAN_REPLACEMENT)
    {
        if (X == null) return;
        int totalBad = 0;
        for (int i = 0; i < X.Length; i++)
        {
            if (X[i] == null) continue;
            int rowBad = 0;
            for (int j = 0; j < X[i].Length; j++)
            {
                if (IsBad(X[i][j]))
                {
                    X[i][j] = replacement;
                    rowBad++;
                    totalBad++;
                }
            }
        }
        if (totalBad > 0)
        {
            Debug.LogWarning($"[RegressionMath] Replaced {totalBad} NaN/Inf value(s) in matrix with {replacement}");
        }
    }
    
    #endregion
    
    #region Input Validation
    
    /**
     * Validate regression inputs
     */
    public static bool ValidateInputs(float[][] X, float[] Y, out string errorMessage)
    {
        if (X == null)
        {
            errorMessage = "Feature matrix X is null";
            return false;
        }
        if (Y == null)
        {
            errorMessage = "Target vector Y is null";
            return false;
        }
        if (X.Length == 0)
        {
            errorMessage = "Feature matrix X is empty";
            return false;
        }
        if (X.Length != Y.Length)
        {
            errorMessage = $"Size mismatch: X has {X.Length} samples, Y has {Y.Length}";
            return false;
        }
        if (X[0] == null || X[0].Length == 0)
        {
            errorMessage = "First row of X is null or empty";
            return false;
        }
        
        errorMessage = null;
        return true;
    }
    
    #endregion
    
    #region Metrics Calculation
    
    /**
     * Calculate RMSE, MAE, and R2 metrics
     */
    public static RegressionMetrics ComputeMetrics(float[] y, float[] yhat)
    {
        if (y == null || yhat == null)
        {
            Debug.LogError("[RegressionMath] ComputeMetrics: y or yhat is null");
            return new RegressionMetrics(float.NaN, float.NaN, float.NaN);
        }
        
        if (y.Length == 0 || yhat.Length == 0 || y.Length != yhat.Length)
        {
            Debug.LogError($"[RegressionMath] Invalid y/yhat lengths: y={y?.Length}, yhat={yhat?.Length}");
            return new RegressionMetrics(float.NaN, float.NaN, float.NaN);
        }
        
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

        double rmse = Math.Sqrt(mse / n);
        double r2 = (ssTot <= MIN_VARIANCE_THRESHOLD)
            ? ((ssRes <= MIN_VARIANCE_THRESHOLD) ? 1.0 : 0.0)
            : (1.0 - ssRes / ssTot);

        return new RegressionMetrics((float)rmse, (float)(mae / n), (float)r2);
    }
    
    #endregion
    
    #region Matrix Operations
    
    /**
     * Extract subset of feature matrix rows
     */
    public static float[][] SubsetX(float[][] X, int[] idx)
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

    /**
     * Extract subset of target vector elements
     */
    public static float[] SubsetY(float[] y, int[] idx)
    {
        var result = new float[idx.Length];
        for (int r = 0; r < idx.Length; r++)
        {
            result[r] = y[idx[r]];
        }
        return result;
    }

    /**
     * Solve symmetric positive definite system using Cholesky decomposition
     * A x = b where A is SPD (from normal equations with Ridge regularization)
     */
    public static double[] SolveSPDByCholesky(double[,] A, double[] b)
    {
        int n = A.GetLength(0);
        var L = new double[n, n];
        int fixCount = 0;

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
                    if (s <= MIN_CHOLESKY_DIAGONAL)
                    {
                        fixCount++;
                        if (fixCount == 1)
                        {
                            Debug.LogWarning($"[Cholesky] Matrix not positive definite: diagonal element {i} = {s:E3}");
                        }
                        s = MIN_CHOLESKY_DIAGONAL;
                    }
                    L[i, j] = Math.Sqrt(s);
                }
                else
                {
                    L[i, j] = s / L[j, j];
                }
            }
        }
        
        if (fixCount > n / 2)
        {
            Debug.LogError($"[Cholesky] Critical: Fixed {fixCount}/{n} diagonal elements. Results unreliable!");
            return null;
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
    
    #endregion
}

