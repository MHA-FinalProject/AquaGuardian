using UnityEngine;
using System.Linq;

/// <summary>
/// Normalize features for better regression performance
/// Stores normalization parameters for inverse transform
/// </summary>
public class FeatureNormalizer
{
    public float[] means;
    public float[] stdDevs;
    public int numFeatures;
    
    /// <summary>
    /// Fit normalizer to data (calculate mean and std for each feature)
    /// </summary>
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
        
        // Calculate mean for each feature
        for (int j = 0; j < numFeatures; j++)
        {
            float sum = 0f;
            for (int i = 0; i < m; i++)
            {
                sum += X[i][j];
            }
            means[j] = sum / m;
        }
        
        // Calculate standard deviation for each feature
        for (int j = 0; j < numFeatures; j++)
        {
            float sumSquares = 0f;
            for (int i = 0; i < m; i++)
            {
                float diff = X[i][j] - means[j];
                sumSquares += diff * diff;
            }
            stdDevs[j] = Mathf.Sqrt(sumSquares / m);
            
            // Avoid division by zero
            if (stdDevs[j] < 1e-10f)
            {
                stdDevs[j] = 1f;
                Debug.LogWarning($"Feature {j} has zero variance, using stdDev=1");
            }
        }
        
        Debug.Log($"Normalizer fitted: {numFeatures} features");
    }
    
    /// <summary>
    /// Normalize data: Z = (X - mean) / std
    /// </summary>
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
        
        float[][] normalized = new float[m][];
        for (int i = 0; i < m; i++)
        {
            normalized[i] = new float[n];
            for (int j = 0; j < n; j++)
            {
                normalized[i][j] = (X[i][j] - means[j]) / stdDevs[j];
            }
        }
        
        return normalized;
    }
    
    /// <summary>
    /// Normalize single sample
    /// </summary>
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
        
        float[] normalized = new float[x.Length];
        for (int i = 0; i < x.Length; i++)
        {
            normalized[i] = (x[i] - means[i]) / stdDevs[i];
        }
        
        return normalized;
    }
    
    /// <summary>
    /// Denormalize data: X = Z * std + mean
    /// </summary>
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
        
        float[][] denormalized = new float[m][];
        for (int i = 0; i < m; i++)
        {
            denormalized[i] = new float[n];
            for (int j = 0; j < n; j++)
            {
                denormalized[i][j] = Z[i][j] * stdDevs[j] + means[j];
            }
        }
        
        return denormalized;
    }
    
    /// <summary>
    /// Denormalize single sample
    /// </summary>
    public float[] InverseTransformSample(float[] z)
    {
        if (z == null || z.Length == 0) return null;
        if (means == null || stdDevs == null)
        {
            Debug.LogError("Normalizer not fitted!");
            return z;
        }
        
        float[] denormalized = new float[z.Length];
        for (int i = 0; i < z.Length; i++)
        {
            denormalized[i] = z[i] * stdDevs[i] + means[i];
        }
        
        return denormalized;
    }
    
    /// <summary>
    /// Fit and transform in one step
    /// </summary>
    public float[][] FitTransform(float[][] X)
    {
        Fit(X);
        return Transform(X);
    }
}

