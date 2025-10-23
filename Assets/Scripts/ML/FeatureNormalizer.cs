using UnityEngine;

/**
* Normalize features for better regression performance
* Stores normalization parameters for inverse transform
* Uses sample standard deviation (n-1) for better estimation with small datasets
*/

public class FeatureNormalizer
{
    public float[] means;
    public float[] stdDevs;
    public int numFeatures;

   // Fit normalizer to data (calculate mean and sample std for each feature)
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
            double sum = 0.0;
            for (int i = 0; i < m; i++)
            {
                sum += X[i][j];
            }
            means[j] = (float)(sum / m);
        }

        // Calculate sample standard deviation (n-1) for each feature
        for (int j = 0; j < numFeatures; j++)
        {
            double sumSquares = 0.0;
            for (int i = 0; i < m; i++)
            {
                double diff = X[i][j] - means[j];
                sumSquares += diff * diff;
            }
            
            // Use sample std (n-1) for better estimation with small datasets
            double denom = Mathf.Max(1, m - 1);
            double sd = System.Math.Sqrt(sumSquares / denom);
            
            // Avoid division by zero
            if (sd < 1e-10)
            {
                sd = 1.0;
                Debug.LogWarning($"Feature {j} has ~zero variance, using stdDev=1");
            }
            
            stdDevs[j] = (float)sd;
        }

        Debug.Log($"Normalizer fitted: {numFeatures} features (sample std, n-1)");
    }

  
    // Normalize data: Z = (X - mean) / std
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
            {
                Z[i][j] = (X[i][j] - means[j]) / stdDevs[j];
            }
        }

        return Z;
    }

    // Normalize single sample
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
        {
            z[i] = (x[i] - means[i]) / stdDevs[i];
        }

        return z;
    }

    // Denormalize data: X = Z * std + mean
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
            {
                X[i][j] = Z[i][j] * stdDevs[j] + means[j];
            }
        }

        return X;
    }

// Denormalize single sample
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
        {
            x[i] = z[i] * stdDevs[i] + means[i];
        }

        return x;
    }

   // Fit and transform in one step
    public float[][] FitTransform(float[][] X)
    {
        Fit(X);
        return Transform(X);
    }
}
