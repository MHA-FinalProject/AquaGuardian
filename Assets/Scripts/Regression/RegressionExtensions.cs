using UnityEngine;

/**
 * Extension methods for MultipleLinearRegression
 * Provides convenient access to normalization operations
 */
public static class RegressionExtensions
{
    private const float MIN_DENOMINATOR = 1e-6f;
    
    public static float ToNormalized(this MultipleLinearRegression model, int featureIndex, float rawValue)
    {
        if (model?.Means == null || model.Stds == null || featureIndex >= model.Means.Length)
            return rawValue;
        
        float mean = model.Means[featureIndex];
        float std = Mathf.Max(MIN_DENOMINATOR, model.Stds[featureIndex]);
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

