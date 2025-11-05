using UnityEngine;
using System;

/**
 * Feature Normalizer - Standardizes features to mean=0, std=1
 * Uses sample standard deviation (n-1) for better estimation with small datasets
 */
public class FeatureNormalizer
{
    private const float MIN_STD_DEV = 1e-10f; // Minimum std dev to prevent division by zero
    
    public float[] means;
    public float[] stdDevs;
    public int numFeatures;

    public void Fit(float[][] X)
    {
        if (X == null || X.Length == 0)
        {
            Debug.LogError("Cannot fit normalizer on empty data");
            return;
        }

        int m = X.Length;
        numFeatures = X[0].Length;
        means = new float[numFeatures];
        stdDevs = new float[numFeatures];

        // Calculate means
        for (int j = 0; j < numFeatures; j++)
        {
            double sum = 0.0;
            for (int i = 0; i < m; i++)
                sum += X[i][j];
            means[j] = (float)(sum / m);
        }

        // Calculate sample standard deviations (n-1)
        for (int j = 0; j < numFeatures; j++)
        {
            double sumSquares = 0.0;
            for (int i = 0; i < m; i++)
            {
                double diff = X[i][j] - means[j];
                sumSquares += diff * diff;
            }
            double denom = Mathf.Max(1, m - 1);
            double sd = System.Math.Sqrt(sumSquares / denom);
            if (sd < MIN_STD_DEV)
            {
                sd = 1.0;
                Debug.LogWarning($"Feature {j} has ~zero variance, using stdDev=1");
            }
            stdDevs[j] = (float)sd;
        }
        Debug.Log($"Normalizer fitted: {numFeatures} features (sample std, n-1)");
    }

    public float[][] Transform(float[][] X)
    {
        if (X == null || X.Length == 0) return null;
        if (means == null || stdDevs == null)
        {
            Debug.LogError("Normalizer not fitted! Call Fit() first.");
            return X;
        }
        int m = X.Length;
        int n = X[0].Length;
        if (n != numFeatures)
        {
            Debug.LogError($"Feature count mismatch: expected {numFeatures}, got {n}");
            return X;
        }
        var Z = new float[m][];
        for (int i = 0; i < m; i++)
        {
            Z[i] = new float[n];
            for (int j = 0; j < n; j++)
                Z[i][j] = (X[i][j] - means[j]) / stdDevs[j];
        }
        return Z;
    }

    public float[] TransformSample(float[] x)
    {
        if (x == null || x.Length == 0) return null;
        if (means == null || stdDevs == null)
        {
            Debug.LogError("Normalizer not fitted! Call Fit() first.");
            return x;
        }
        if (x.Length != numFeatures)
        {
            Debug.LogError($"Feature count mismatch: expected {numFeatures}, got {x.Length}");
            return x;
        }
        var z = new float[x.Length];
        for (int i = 0; i < x.Length; i++)
            z[i] = (x[i] - means[i]) / stdDevs[i];
        return z;
    }

    public float[][] InverseTransform(float[][] Z)
    {
        if (Z == null || Z.Length == 0) return null;
        if (means == null || stdDevs == null)
        {
            Debug.LogError("Normalizer not fitted!");
            return Z;
        }
        int m = Z.Length;
        int n = Z[0].Length;
        var X = new float[m][];
        for (int i = 0; i < m; i++)
        {
            X[i] = new float[n];
            for (int j = 0; j < n; j++)
                X[i][j] = Z[i][j] * stdDevs[j] + means[j];
        }
        return X;
    }

    public float[] InverseTransformSample(float[] z)
    {
        if (z == null || z.Length == 0) return null;
        if (means == null || stdDevs == null)
        {
            Debug.LogError("Normalizer not fitted!");
            return z;
        }
        var x = new float[z.Length];
        for (int i = 0; i < z.Length; i++)
            x[i] = z[i] * stdDevs[i] + means[i];
        return x;
    }

    public float[][] FitTransform(float[][] X)
    {
        Fit(X);
        return Transform(X);
    }
}

