using UnityEngine;
using System.Linq;

/// <summary>
/// Mathematical calculations for regression coefficients (beta)
/// Handles chain rule derivatives for derived features (EffectiveDrainRate)
/// </summary>
public static class RegressionMath
{
    /// <summary>
    /// Calculate effective beta including chain rule for derived features
    /// Used when optimizing lifeTime or RemoveHealthEveryLifeTime (affects EffectiveDrainRate)
    /// </summary>
    public static float EffectiveBeta(
        MultipleLinearRegression model,
        int featureIndex,
        TrialDataModels.TrialData currentParams,
        bool optimizingLifeTimeOrDrop)
    {
        float beta = model.coefficients[featureIndex + 1];
        
        if (!optimizingLifeTimeOrDrop)
            return beta;
        
        float effectiveDrainRateBeta = model.coefficients[10]; // coefficient for EffectiveDrainRate (index 9)
        
        // Add EffectiveDrainRate contribution via chain rule
        if (featureIndex == 3) // lifeTime
        {
            float currentDrop = ParameterHelper.Get(currentParams, 4);
            float currentLife = ParameterHelper.Get(currentParams, 3);
            if (currentLife > 0.1f)
            {
                // d(EDR)/d(lifeTime) = -drop / life²
                float dEDR_dLife = -currentDrop / (currentLife * currentLife);
                float stdDevLife = model.Stds?.ElementAtOrDefault(3) ?? 1.0f;
                float stdDevEDR = model.Stds?.ElementAtOrDefault(9) ?? 1.0f;
                float normalizedDerivative = dEDR_dLife * stdDevLife / Mathf.Max(1e-6f, stdDevEDR);
                beta += effectiveDrainRateBeta * normalizedDerivative;
            }
        }
        else if (featureIndex == 4) // RemoveHealthEveryLifeTime
        {
            float currentLife = ParameterHelper.Get(currentParams, 3);
            if (currentLife > 0.1f)
            {
                // d(EDR)/d(drop) = 1 / life
                float dEDR_dDrop = 1.0f / currentLife;
                float stdDevDrop = model.Stds?.ElementAtOrDefault(4) ?? 1.0f;
                float stdDevEDR = model.Stds?.ElementAtOrDefault(9) ?? 1.0f;
                float normalizedDerivative = dEDR_dDrop * stdDevDrop / Mathf.Max(1e-6f, stdDevEDR);
                beta += effectiveDrainRateBeta * normalizedDerivative;
            }
        }
        
        return beta;
    }

    /// <summary>
    /// Calculate sum of squared betas for global normalization in gradient descent
    /// Prevents instability from dividing by individual beta²
    /// </summary>
    public static float SumBetaSq(
        MultipleLinearRegression model,
        int[] freeFeatures,
        TrialDataModels.TrialData currentParams)
    {
        bool optimizingLifeTimeOrDrop = freeFeatures.Contains(3) || freeFeatures.Contains(4);
        
        float sumBetaSq = 0f;
        foreach (int j in freeFeatures)
        {
            float beta = EffectiveBeta(model, j, currentParams, optimizingLifeTimeOrDrop);
            sumBetaSq += beta * beta;
        }
        
        return Mathf.Max(sumBetaSq, 1e-6f); // Prevent division by zero
    }
}

