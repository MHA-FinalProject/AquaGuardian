using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/**
 * Finds optimal game parameters to achieve target oxygen level
 * 
 * Called from RegressionUtilities.OptimizeParameters which tries methods in order:
 * First tries SolveForTargetOxygen (3-phase: minimal-change, then gradient refinement, then iterative if needed).
 * If that fails or error > 5%, tries RandomSweepOptimizer (random search).
 * If still fails or error > 5%, uses SolveForTargetDifficultyMulti (gradient descent fallback).
 * Returns the best solution found.
 */

public static class DifficultyParameterSolver
{
    #region Constants
    
    private const float DEFAULT_LEARNING_RATE = 0.2f;
    private const float MODERATE_LEARNING_RATE = 0.25f;
    private const float AGGRESSIVE_LEARNING_RATE = 0.3f;
    private const float CONVERGENCE_THRESHOLD = 0.1f;
    private const int DEFAULT_MAX_ITERATIONS = 150;
    private const int MODERATE_MAX_ITERATIONS = 200;
    private const int AGGRESSIVE_MAX_ITERATIONS = 250;
    
    #endregion
    
    #region Public API - Main Solvers
    
    /**
     * Fallback gradient descent method when SolveForTargetOxygen fails or error > 5%.
     * Updates top K features iteratively by moving in gradient direction (coefficient).
     * Each iteration predicts oxygen, calculates error, updates features proportionally to coefficients,
     * normalizes by sum of squared coefficients, and clamps to valid ranges. Stops when error < threshold.
     * Requires baseline to be pre-calculated (use FeatureExtractor.GetPatientBaseline).
     */
    public static TrialDataModels.TrialData SolveForTargetDifficultyMulti(
        MultipleLinearRegression model,
        float targetO2,
        int maxFeaturesToOptimize = 3,
        TrialDataModels.TrialData baseline = null,
        TrialDataModels.ParameterRanges ranges = null,
        string[] featureNames = null,
        List<TrialDataModels.TrialData> trials = null)
    {
        if (model == null || model.coefficients == null)
        {
            Debug.LogError("[DifficultyParameterSolver] Invalid model");
            return null;
        }

        if (baseline == null)
        {
            Debug.LogError("[DifficultyParameterSolver] Baseline parameter is required! Use FeatureExtractor.GetPatientBaseline() to calculate it.");
            return null;
        }

        ranges ??= new TrialDataModels.ParameterRanges();

        string[] fullFeatureNames = FeatureExtractor.FeatureNames;
        string[] modelFeatureNames = model.featureNames ?? fullFeatureNames;
        featureNames ??= fullFeatureNames;

        var importance = model.GetFeatureImportance();

        bool allowFactorForce = trials != null && trials.Any(t => t.IsAmadeoMode > 0.5f);
        var banned = CreateBannedFeatureSet(allowFactorForce);
        var topFeatures = BuildTopFeatureList(importance, maxFeaturesToOptimize, banned);

        // Fallback: if no valid features, use the most common oxygen-affecting parameters
        if (topFeatures.Length == 0)
        {
            Debug.LogWarning("[DifficultyParameterSolver] No valid features found, using fallback: RemoveHealthEveryLifeTime, removeHealthWithCollide");
            topFeatures = new[] { "RemoveHealthEveryLifeTime", "removeHealthWithCollide" };
        }

        if (topFeatures.Length > maxFeaturesToOptimize && maxFeaturesToOptimize > 0)
        {
            topFeatures = topFeatures.Take(maxFeaturesToOptimize).ToArray();
        }

        var mu = model.normalizer?.means;
        var sigma = model.normalizer?.stdDevs;
        bool hasNormalizer = (mu != null && sigma != null);
        
        // Use ONLY the provided baseline (no fallback calculation!)
        var bestParams = baseline;

        float initialO2 = model.Predict(FeatureExtractor.ExtractFeatures(bestParams));
        float bestError = Mathf.Abs(initialO2 - targetO2);

        float deltaFromTarget = Mathf.Abs(initialO2 - targetO2);
        float learningRate = GetAdaptiveLearningRate(deltaFromTarget);
        int maxIterations = GetAdaptiveMaxIterations(deltaFromTarget);
        
        for (int iter = 0; iter < maxIterations; iter++)
        {
            float currentO2 = model.Predict(FeatureExtractor.ExtractFeatures(bestParams));
            float error = currentO2 - targetO2;
            
            if (Mathf.Abs(error) < CONVERGENCE_THRESHOLD)
            {
                break;
            }
            
            float adaptiveLR = learningRate * Mathf.Min(1f, Mathf.Abs(error) / 5f);
            
            float sumBetaSq = 0f;
            foreach (var featureName in topFeatures)
            {
                int modelIdx = System.Array.IndexOf(modelFeatureNames, featureName);
                if (modelIdx >= 0 && modelIdx + 1 < model.coefficients.Length)
                {
                    float beta = model.coefficients[modelIdx + 1];
                    sumBetaSq += beta * beta;
                }
            }
            sumBetaSq = Mathf.Max(sumBetaSq, 1e-6f);

            bool anyAdjustment = false;
            
            foreach (var featureName in topFeatures)
            {
                int modelIdx = System.Array.IndexOf(modelFeatureNames, featureName);
                int fullIdx = System.Array.IndexOf(fullFeatureNames, featureName);

                if (modelIdx < 0 || fullIdx < 0 || modelIdx + 1 >= model.coefficients.Length)
                    continue;

                float coef = model.coefficients[modelIdx + 1];
                if (Mathf.Abs(coef) < 0.0001f) continue;
                
                float oldValueRaw = ParameterHelper.Get(bestParams, fullIdx);
               
                float oldValueNorm = oldValueRaw;
                if (hasNormalizer && modelIdx < mu.Length)
                {
                    oldValueNorm = (oldValueRaw - mu[modelIdx]) / Mathf.Max(1e-6f, sigma[modelIdx]);
                }
                
                float gradient = coef;  
                float step = -adaptiveLR * error * gradient / sumBetaSq;
                float newValueNorm = oldValueNorm + step;
                
                float newValueRaw = newValueNorm;
                if (hasNormalizer && modelIdx < mu.Length)
                {
                    newValueRaw = newValueNorm * Mathf.Max(1e-6f, sigma[modelIdx]) + mu[modelIdx];
                }
                
                (float min, float max) = ParameterHelper.Range(ranges, fullIdx);
                float clampedValue = Mathf.Clamp(newValueRaw, min, max);
                
                if (Mathf.Abs(clampedValue - oldValueRaw) > 0.001f)
                {
                    ParameterHelper.Set(ref bestParams, fullIdx, clampedValue);
                    anyAdjustment = true;
                }
            }

            if (!anyAdjustment)
            {
                break;
            }
        }
        
        float finalO2 = model.Predict(FeatureExtractor.ExtractFeatures(bestParams));
        float finalError = Mathf.Abs(finalO2 - targetO2);
        
        return bestParams;
    }

    /**
     * Main optimizer - most accurate method, called first by OptimizeParameters. Typically achieves <1% error.
     * Three phases: first finds minimal parameter changes analytically, then refines with gradient descent,
     * then does additional refinement if error > 0.5%. Requires baseParams to be pre-calculated.
     */
    public static TrialDataModels.TrialData SolveForTargetOxygen(
        MultipleLinearRegression model,
        TrialDataModels.TrialData baseParams,
        int[] topFeatureIndices,
        float targetO2,
        TrialDataModels.ParameterRanges ranges,
        System.Func<TrialDataModels.TrialData, float> predictO2,
        out float finalError)
    {
        string[] fullFeatureNames = FeatureExtractor.FeatureNames;
        string[] modelFeatureNames = model.featureNames ?? fullFeatureNames;

        // Base parameters only (9 parameters, excluding derived features like EffectiveDrainRate)
        Vector2[] bounds = new Vector2[]
        {
            ranges.speedRange,
            ranges.verticalSpeedRange,
            ranges.idleUpwardSpeedRange,
            ranges.lifeTimeRange,
            ranges.RemoveHealthEveryLifeTimeRange,
            ranges.removeHealthWithCollideRange,
            ranges.timeBetweenCollidesRange,
            ranges.healHealthPointRange,
            ranges.factorForceRange
        };

        int[] free = FilterFreeFeatures(topFeatureIndices, ranges, baseParams);

        if (free.Length < 2)
        {
            Debug.LogWarning($"[DifficultyParameterSolver] Only {free.Length} features with headroom, using fallback");
            free = new int[] { 4, 5 };
        }

        var x0 = SolveMinimalChange(model, baseParams, free, targetO2, bounds, fullFeatureNames, modelFeatureNames);

        float initialPrediction = predictO2(x0);
        float initialError = Mathf.Abs(initialPrediction - targetO2);

        int maxSteps = initialError > 40f ? 250 : (initialError > 20f ? 150 : (initialError > 10f ? 100 : 50));
        float learningRate = initialError > 40f ? 0.7f : (initialError > 20f ? 0.5f : (initialError > 10f ? 0.3f : 0.2f));
        float tolerance = 0.1f;  // Accept 0.1% error

        var x1 = RefineProjectedGradient(model, x0, free, targetO2, bounds, predictO2, maxSteps, learningRate, tolerance, fullFeatureNames, modelFeatureNames);
        
        float o1 = predictO2(x1);
        finalError = Mathf.Abs(o1 - targetO2);

        if (finalError > 0.5f)
        {
            int iterativeMaxIter = finalError > 40f ? 400 : (finalError > 20f ? 300 : (finalError > 10f ? 200 : 150));
            x1 = RefineProjectedGradientIterative(model, x1, free, targetO2, bounds, predictO2, iterativeMaxIter, fullFeatureNames, modelFeatureNames);
            o1 = predictO2(x1);
            finalError = Mathf.Abs(o1 - targetO2);
        }
        
        return x1;
    }
    
    #endregion
    
    #region Private Helpers - Core Optimization Algorithms

    /**
     * Finds minimal parameter changes analytically. Calculates what non-optimized features contribute,
     * then scales free features proportionally to their coefficients to reach target. Keeps changes minimal.
     */
    private static TrialDataModels.TrialData SolveMinimalChange(
        MultipleLinearRegression model,
        TrialDataModels.TrialData baseParams,
        int[] freeFeatures,
        float targetO2,
        Vector2[] bounds,
        string[] fullFeatureNames,
        string[] modelFeatureNames)
    {
        var work = baseParams;

        float fixedContribution = model.coefficients[0];
        bool optimizingDrainRateDeps = System.Array.IndexOf(freeFeatures, 3) >= 0 || System.Array.IndexOf(freeFeatures, 4) >= 0;

        for (int j = 0; j < fullFeatureNames.Length; j++)
        {
            if (System.Array.IndexOf(freeFeatures, j) >= 0) continue;

            if (optimizingDrainRateDeps && fullFeatureNames[j].StartsWith("Effective"))
                continue;

            string featureName = fullFeatureNames[j];
            int modelIdx = System.Array.IndexOf(modelFeatureNames, featureName);

            if (modelIdx < 0 || modelIdx + 1 >= model.coefficients.Length) continue;

            float xRaw = ParameterHelper.Get(work, j);
            float xHat = model.ToNormalized(modelIdx, xRaw);
            fixedContribution += model.coefficients[modelIdx + 1] * xHat;
        }

        int m = freeFeatures.Length;
        var a = new float[m];
        
        for (int k = 0; k < m; k++)
        {
            a[k] = RegressionMath.EffectiveBeta(model, freeFeatures[k], work, optimizingDrainRateDeps);
        }

        float rhs = targetO2 - fixedContribution;
        double denom = 1e-6;
        for (int k = 0; k < m; k++) denom += (double)a[k] * (double)a[k];
        float scale = (float)(rhs / denom);

        for (int k = 0; k < m; k++)
        {
            int j = freeFeatures[k];

            string featureName = fullFeatureNames[j];
            int modelIdx = System.Array.IndexOf(modelFeatureNames, featureName);
            if (modelIdx < 0) continue;

            float x0_raw = ParameterHelper.Get(work, j);
            float x0_hat = model.ToNormalized(modelIdx, x0_raw);
            float x1_hat = x0_hat + scale * a[k];
            float x1_raw = model.FromNormalized(modelIdx, x1_hat);
            x1_raw = ParameterHelper.Clamp(j, x1_raw, bounds);
            ParameterHelper.Set(ref work, j, x1_raw);
        }

        return work;
    }

    /**
     * Refines solution iteratively using gradient descent. Moves parameters in gradient direction to reduce error.
     * Learning rate adapts based on current error. Updates are clamped to valid parameter ranges.
     * Stops when error is small enough or max steps reached.
     */
    private static TrialDataModels.TrialData RefineProjectedGradient(
        MultipleLinearRegression model,
        TrialDataModels.TrialData start,
        int[] freeFeatures,
        float targetO2,
        Vector2[] bounds,
        System.Func<TrialDataModels.TrialData, float> predictO2,
        int maxSteps,
        float lr,
        float tol,
        string[] fullFeatureNames,
        string[] modelFeatureNames)
    {
        var cur = start;

        for (int step = 0; step < maxSteps; step++)
        {
            float y = predictO2(cur);
            float error = y - targetO2;
            if (Mathf.Abs(error) <= tol) break;

            float adaptiveLR = lr * Mathf.Min(1f, Mathf.Max(0.1f, Mathf.Abs(error) / 5f));

            float sumBetaSq = RegressionMath.SumBetaSq(model, freeFeatures, cur);
            bool optimizingLifeTimeOrDrop = System.Array.IndexOf(freeFeatures, 3) >= 0 || System.Array.IndexOf(freeFeatures, 4) >= 0;

            for (int k = 0; k < freeFeatures.Length; k++)
            {
                int j = freeFeatures[k];
                float beta = RegressionMath.EffectiveBeta(model, j, cur, optimizingLifeTimeOrDrop);

                float deltaHat = -adaptiveLR * error * beta / sumBetaSq;

                string featureName = fullFeatureNames[j];
                int modelIdx = System.Array.IndexOf(modelFeatureNames, featureName);
                if (modelIdx < 0) continue;

                float xRaw = ParameterHelper.Get(cur, j);
                float xHat = model.ToNormalized(modelIdx, xRaw);
                xHat += deltaHat;

                float newRaw = model.FromNormalized(modelIdx, xHat);
                newRaw = ParameterHelper.Clamp(j, newRaw, bounds);
                ParameterHelper.Set(ref cur, j, newRaw);
            }
        }

        return cur;
    }

    /**
     * Extended refinement that tracks the best solution found during iterations. Similar to RefineProjectedGradient
     * but keeps track of best error and returns that solution instead of the last one. Learning rate scales with error.
     * Continues until error is very small or no more improvements possible.
     */
    private static TrialDataModels.TrialData RefineProjectedGradientIterative(
        MultipleLinearRegression model,
        TrialDataModels.TrialData start,
        int[] freeFeatures,
        float targetO2,
        Vector2[] bounds,
        System.Func<TrialDataModels.TrialData, float> predictO2,
        int maxIterations,
        string[] fullFeatureNames,
        string[] modelFeatureNames)
    {
        var cur = start;
        float bestError = float.MaxValue;
        var bestParams = start;

        for (int iter = 0; iter < maxIterations; iter++)
        {
            float y = predictO2(cur);
            float error = y - targetO2;
            float absError = Mathf.Abs(error);

            if (absError < bestError)
            {
                bestError = absError;
                bestParams = ParameterHelper.Clone(cur);
            }

            if (absError <= 0.1f) break;

            float baseLR = absError > 30f ? 0.7f : 0.5f;
            float scale = Mathf.Max(0.05f, Mathf.Min(1f, absError / 10f));
            float adaptiveLR = baseLR * scale;

            float sumBetaSq = RegressionMath.SumBetaSq(model, freeFeatures, cur);
            bool optimizingLifeTimeOrDrop = System.Array.IndexOf(freeFeatures, 3) >= 0 || System.Array.IndexOf(freeFeatures, 4) >= 0;

            bool anyChange = false;
            for (int k = 0; k < freeFeatures.Length; k++)
            {
                int j = freeFeatures[k];
                float beta = RegressionMath.EffectiveBeta(model, j, cur, optimizingLifeTimeOrDrop);

                if (Mathf.Abs(beta) < 1e-6f) continue;

                float deltaHat = -adaptiveLR * error * beta / sumBetaSq;

                string featureName = fullFeatureNames[j];
                int modelIdx = System.Array.IndexOf(modelFeatureNames, featureName);
                if (modelIdx < 0) continue;

                float xRaw = ParameterHelper.Get(cur, j);
                float xHat = model.ToNormalized(modelIdx, xRaw);
                xHat += deltaHat;

                float newRaw = model.FromNormalized(modelIdx, xHat);
                float clampedRaw = ParameterHelper.Clamp(j, newRaw, bounds);

                if (Mathf.Abs(clampedRaw - xRaw) > 0.001f)
                {
                    ParameterHelper.Set(ref cur, j, clampedRaw);
                    anyChange = true;
                }
            }

            if (!anyChange) break;
        }

        return bestParams;
    }

    #endregion

    #region Private Helpers - Feature Selection

    private static string[] BuildTopFeatureList(
        (string feature, float importance)[] importance,
        int desiredCount,
        HashSet<string> banned)
    {
        if (importance == null || importance.Length == 0 || desiredCount <= 0)
            return System.Array.Empty<string>();

        var result = new List<string>(desiredCount);
        for (int i = 0; i < importance.Length && result.Count < desiredCount; i++)
        {
            string feature = importance[i].feature;
            if (banned != null && banned.Contains(feature))
                continue;

            if (!result.Contains(feature))
            {
                result.Add(feature);
            }
        }

        return result.ToArray();
    }

    private static int[] FilterFreeFeatures(
        int[] topFeatureIndices,
        TrialDataModels.ParameterRanges ranges,
        TrialDataModels.TrialData baseParams)
    {
        if (topFeatureIndices == null || topFeatureIndices.Length == 0)
            return System.Array.Empty<int>();

        string[] featureNames = FeatureExtractor.FeatureNames;
        var freeList = new List<int>(topFeatureIndices.Length);

        foreach (int featureIdx in topFeatureIndices)
        {
            if (featureIdx >= 0 && featureIdx < featureNames.Length && featureNames[featureIdx].StartsWith("Effective"))
                continue;

            if (!ParameterHelper.HasHeadroom(featureIdx, ranges, baseParams, threshold: 0.1f))
                continue;

            freeList.Add(featureIdx);
        }

        return freeList.ToArray();
    }

    private static HashSet<string> CreateBannedFeatureSet(bool allowFactorForce)
    {
        var banned = new HashSet<string> { "EffectiveDrainRate" };
        if (!allowFactorForce)
        {
            banned.Add("factorForce");
        }
        return banned;
    }
    
    #endregion
    
    #region Private Helpers - Adaptive Configuration
    
    /**
     * Returns learning rate based on how far we are from target. Far away = more aggressive, close = more conservative.
     */
    private static float GetAdaptiveLearningRate(float errorDistance)
    {
        if (errorDistance > 30f) return AGGRESSIVE_LEARNING_RATE;
        if (errorDistance > 15f) return MODERATE_LEARNING_RATE;
        return DEFAULT_LEARNING_RATE;
    }
    
    /**
     * Returns max iterations based on distance from target. Far away = more iterations, close = fewer iterations.
     */
    private static int GetAdaptiveMaxIterations(float errorDistance)
    {
        if (errorDistance > 30f) return AGGRESSIVE_MAX_ITERATIONS;
        if (errorDistance > 15f) return MODERATE_MAX_ITERATIONS;
        return DEFAULT_MAX_ITERATIONS;
    }
    
    /**
     * Random search fallback. Generates random parameter combinations, predicts oxygen for each,
     * returns the one with smallest error. Used when gradient methods fail or data is far from target.
     */
    public static (TrialDataModels.TrialData candidate, float error) RandomSweepOptimizer(
        System.Func<TrialDataModels.TrialData, float> predictFunc,
        TrialDataModels.ParameterRanges ranges,
        float targetOxygen,
        bool allowFactorForce,
        int samples = 150)
    {
        if (predictFunc == null || ranges == null || samples <= 0)
            return (null, float.MaxValue);

        var random = new System.Random(42);
        TrialDataModels.TrialData bestCandidate = null;
        float bestError = float.MaxValue;

        float Sample(Vector2 range) => UnityEngine.Mathf.Lerp(range.x, range.y, (float)random.NextDouble());

        for (int i = 0; i < samples; i++)
        {
            var candidate = new TrialDataModels.TrialData
            {
                speed = Sample(ranges.speedRange),
                verticalSpeed = Sample(ranges.verticalSpeedRange),
                idleUpwardSpeed = Sample(ranges.idleUpwardSpeedRange),
                lifeTime = Sample(ranges.lifeTimeRange),
                RemoveHealthEveryLifeTime = Sample(ranges.RemoveHealthEveryLifeTimeRange),
                removeHealthWithCollide = Sample(ranges.removeHealthWithCollideRange),
                timeBetweenCollides = Sample(ranges.timeBetweenCollidesRange),
                healHealthPoint = Sample(ranges.healHealthPointRange),
                factorForce = allowFactorForce ? Sample(ranges.factorForceRange) : 0f,
                IsAmadeoMode = allowFactorForce ? 1f : 0f
            };

            float predicted = predictFunc(candidate);
            float error = UnityEngine.Mathf.Abs(predicted - targetOxygen);
            
            if (error < bestError)
            {
                bestError = error;
                bestCandidate = candidate;
            }
        }

        return (bestCandidate, bestError);
    }

    #endregion
}
