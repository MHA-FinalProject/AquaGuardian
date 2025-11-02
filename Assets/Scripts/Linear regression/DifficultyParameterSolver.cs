using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public static class DifficultyParameterSolver
{

    public static TrialDataModels.TrialData SolveForTargetDifficulty(
        MultipleLinearRegression model,
        float targetO2,
        TrialDataModels.ParameterRanges ranges = null,
        string[] featureNames = null)
    {
        if (model == null || model.coefficients == null || model.coefficients.Length < 2)
        {
            Debug.LogError("Invalid model for solving");
            return null;
        }

        if (ranges == null)
            ranges = new TrialDataModels.ParameterRanges();
        
        featureNames ??= FeatureExtractor.FeatureNames;

        // Create base parameters with default values
        var baseParams = FeatureExtractor.GetMidRangeDefaults(ranges);
        
        // Get feature importance (absolute coefficient values)
        var importance = model.GetFeatureImportance();
        
        // Create banned list: features that cannot be directly optimized
        var banned = new System.Collections.Generic.HashSet<string>(new[] { "EffectiveDrainRate" }); // Always banned (derived feature)
        bool amadeo = baseParams.IsAmadeoMode > 0.5f;
        if (!amadeo) banned.Add("factorForce"); // Ban factorForce if not Amadeo mode
        
        // Select the first non-banned feature
        var topFeature = importance.First(t => !banned.Contains(t.Item1));
        int topFeatureIdx = System.Array.IndexOf(featureNames, topFeature.Item1);
        
        // Extra guard: if somehow EDR (index 9) was selected, pick next valid feature
        if (topFeatureIdx == 9)
        {
            Debug.LogWarning("[Solver] EffectiveDrainRate (index 9) was selected - this is a derived feature! Selecting next valid feature.");
            topFeature = importance.Skip(1).First(t => !banned.Contains(t.Item1));
            topFeatureIdx = System.Array.IndexOf(featureNames, topFeature.Item1);
        }
        
        // Work in NORMALIZED space (the coefficients are in normalized scale!)
        var mu = model.normalizer?.means;
        var sigma = model.normalizer?.stdDevs;
        bool hasNormalizer = (mu != null && sigma != null);
        
        if (!hasNormalizer)
        {
            Debug.LogWarning("Model has no normalizer - analytical solution may be inaccurate");
        }
        
        // Calculate sum of fixed features' contribution
        float fixedContribution = model.coefficients[0]; // intercept
        
        for (int i = 1; i < model.coefficients.Length; i++)
        {
            int featureIdx = i - 1;
            if (featureIdx == topFeatureIdx)
                continue; // Skip the feature we're solving for
                
            float featureValueRaw = ParameterHelper.Get(baseParams, featureIdx);
            
            // Normalize the feature value
            float featureValueNorm = featureValueRaw;
            if (hasNormalizer && featureIdx < mu.Length)
            {
                featureValueNorm = (featureValueRaw - mu[featureIdx]) / Mathf.Max(1e-6f, sigma[featureIdx]);
            }
            
            float contribution = model.coefficients[i] * featureValueNorm;
            fixedContribution += contribution;
        }
        
        // Solve for top feature in NORMALIZED space
        float topCoefficient = model.coefficients[topFeatureIdx + 1];
        
        if (Mathf.Abs(topCoefficient) < 0.0001f)
        {
            Debug.LogWarning($"Coefficient for {topFeature.Item1} is too small, cannot solve reliably");
            return baseParams;
        }
        
        float solvedValueNorm = (targetO2 - fixedContribution) / topCoefficient;
        
        // Convert back to RAW space
        float solvedValue = solvedValueNorm;
        if (hasNormalizer && topFeatureIdx < mu.Length)
        {
            solvedValue = solvedValueNorm * Mathf.Max(1e-6f, sigma[topFeatureIdx]) + mu[topFeatureIdx];
        }
        
        // Apply constraints
        (float min, float max) = ParameterHelper.Range(ranges, topFeatureIdx);
        float clampedValue = Mathf.Clamp(solvedValue, min, max);
        
        if (Mathf.Abs(solvedValue - clampedValue) > 0.01f)
        {
            Debug.LogWarning($"Value {solvedValue:F2} outside valid range [{min:F2}, {max:F2}], clamping to {clampedValue:F2}");
        }
        
        // Apply solved value to base parameters
        ParameterHelper.Set(ref baseParams, topFeatureIdx, clampedValue);
        
        return baseParams;
    }

    public class OptimizationReport
    {
        public List<string> iterationLog = new List<string>();
        public string finalReport = "";
    }

    public static TrialDataModels.TrialData SolveForTargetDifficultyMulti(
        MultipleLinearRegression model,
        float targetO2,
        int numParametersToSolve = 3,
        TrialDataModels.ParameterRanges ranges = null,
        string[] featureNames = null,
        OptimizationReport report = null,
        List<TrialDataModels.TrialData> trials = null)
    {
        if (model == null || model.coefficients == null)
        {
            Debug.LogError("Invalid model");
            return null;
        }

        if (ranges == null)
            ranges = new TrialDataModels.ParameterRanges();

        featureNames ??= FeatureExtractor.FeatureNames;

        var importance = model.GetFeatureImportance();
        var topFeatures = importance.Take(numParametersToSolve).ToArray();
        
        // Report initialization
        if (report != null)
        {
            report.iterationLog.Add("GRADIENT DESCENT OPTIMIZATION\n");
            report.iterationLog.Add($"Target O2: {targetO2:F2}%\n");
            report.iterationLog.Add($"Optimizing {numParametersToSolve} most important features:\n");
            foreach (var (feature, imp) in topFeatures)
            {
                report.iterationLog.Add($"  - {feature}: coefficient = {model.coefficients[System.Array.IndexOf(featureNames, feature) + 1]:F4}\n");
            }
            report.iterationLog.Add("\n");
        }

        // Get normalizer for working in normalized space
        var mu = model.normalizer?.means;
        var sigma = model.normalizer?.stdDevs;
        bool hasNormalizer = (mu != null && sigma != null);
        
        if (!hasNormalizer)
        {
            Debug.LogWarning("Model has no normalizer - gradient descent may be inaccurate");
        }
        
        // FIXED: Start with personalized parameters based on patient's trial median
        var bestParams = FeatureExtractor.GetPatientBaseline(trials, ranges, useMedian: true);
        
        // Calculate initial prediction
        float initialO2 = model.Predict(FeatureExtractor.ExtractFeatures(bestParams));
        float bestError = Mathf.Abs(initialO2 - targetO2);
        
        
        if (report != null)
        {
            report.iterationLog.Add("INITIAL STATE:\n");
            foreach (var (featureName, _) in topFeatures)
            {
                int idx = System.Array.IndexOf(featureNames, featureName);
                float val = ParameterHelper.Get(bestParams, idx);
                report.iterationLog.Add($"  {featureName,-25} = {val:F2}\n");
            }
            report.iterationLog.Add($"\nPredicted O2: {initialO2:F2}%\n");
            report.iterationLog.Add($"Error: {bestError:F2}%\n\n");
        }
        
        // Gradient Descent parameters (OPTIMIZED for better convergence)
        float learningRate = 0.2f;          // Reduced from 0.5 to 0.2 for smaller, more stable steps
        int maxIterations = 150;            // Increased from 100 to allow more time to converge
        float convergenceThreshold = 0.05f; // Stop when error < 0.05%
        
      
        if (report != null)
        {
            report.iterationLog.Add("\n");
        }
        
        for (int iter = 0; iter < maxIterations; iter++)
        {
            float currentO2 = model.Predict(FeatureExtractor.ExtractFeatures(bestParams));
            float error = currentO2 - targetO2;
            
            // Check convergence
            if (Mathf.Abs(error) < convergenceThreshold)
            {
                if (report != null)
                {
                    report.iterationLog.Add($"\nCONVERGED at iteration {iter + 1}!\n");
                    report.iterationLog.Add($"Final error: {Mathf.Abs(error):F3}%\n");
                }
                break;
            }
            
            // Adaptive learning rate (reduce as we get closer)
            float adaptiveLR = learningRate * Mathf.Min(1f, Mathf.Abs(error) / 5f);
            
            // Log iteration start (only significant iterations)
            bool shouldLog = (iter < 5) || ((iter + 1) % 10 == 0) || (Mathf.Abs(error) < 1.0f);
            
            if (report != null && shouldLog)
            {
                report.iterationLog.Add($"\nIteration {iter + 1}:\n");
                report.iterationLog.Add($"  Current O2: {currentO2:F2}% | Target: {targetO2:F2}% | Error: {error:F3}%\n");
                report.iterationLog.Add($"  Learning Rate: {adaptiveLR:F3}\n");
            }
            
            // FIXED: Calculate global beta normalization (sum of squared betas)
            // This prevents instability from dividing by individual beta^2
            float sumBetaSq = 0f;
            foreach (var (featureName, _) in topFeatures)
            {
                int featureIdx = System.Array.IndexOf(featureNames, featureName);
                float beta = model.coefficients[featureIdx + 1];
                sumBetaSq += beta * beta;
            }
            sumBetaSq = Mathf.Max(sumBetaSq, 1e-6f); // Prevent division by zero
            
            // Adjust each top feature based on its coefficient (gradient)
            bool anyAdjustment = false;
            
            foreach (var (featureName, importance_val) in topFeatures)
            {
                int featureIdx = System.Array.IndexOf(featureNames, featureName);
                float coef = model.coefficients[featureIdx + 1];
                
                if (Mathf.Abs(coef) < 0.0001f) continue;
                
                float oldValueRaw = ParameterHelper.Get(bestParams, featureIdx);
                
               
                float oldValueNorm = oldValueRaw;
                if (hasNormalizer && featureIdx < mu.Length)
                {
                    oldValueNorm = (oldValueRaw - mu[featureIdx]) / Mathf.Max(1e-6f, sigma[featureIdx]);
                }
                
                // FIXED: Use global normalization instead of individual beta^2
                // This ensures all features move proportionally to their impact
                float gradient = coef;  
                float step = -adaptiveLR * error * gradient / sumBetaSq;  // Global normalized step
                
                float newValueNorm = oldValueNorm + step;
                
                // FIXED: Convert back to RAW space BEFORE clamping
                float newValueRaw = newValueNorm;
                if (hasNormalizer && featureIdx < mu.Length)
                {
                    newValueRaw = newValueNorm * Mathf.Max(1e-6f, sigma[featureIdx]) + mu[featureIdx];
                }
                
                // Clamp to valid range IN RAW SPACE
                (float min, float max) = ParameterHelper.Range(ranges, featureIdx);
                float clampedValue = Mathf.Clamp(newValueRaw, min, max);
                
                if (Mathf.Abs(clampedValue - oldValueRaw) > 0.001f)
                {
                    ParameterHelper.Set(ref bestParams, featureIdx, clampedValue);
                    anyAdjustment = true;
                    
                    // Log the change
                    if (report != null && shouldLog)
                    {
                        string direction = clampedValue > oldValueRaw ? "increased" : "decreased";
                        float change = clampedValue - oldValueRaw;
                        report.iterationLog.Add($"    {featureName,-23} {direction,-10} {oldValueRaw:F2} to {clampedValue:F2} (change: {change:+F2})\n");
                    }
                }
            }
            
            
            // If no adjustments were made (all parameters at bounds), break
            if (!anyAdjustment)
            {
                if (report != null)
                {
                    report.iterationLog.Add($"\nStopped at iteration {iter + 1}: All parameters at bounds\n");
                }
                break;
            }
        }
        
        // Final verification
        float finalO2 = model.Predict(FeatureExtractor.ExtractFeatures(bestParams));
        float finalError = Mathf.Abs(finalO2 - targetO2);
        
        // Generate final report
        if (report != null)
        {
            report.iterationLog.Add("\n\nFINAL OPTIMIZED PARAMETERS:\n");
            
            for (int i = 0; i < featureNames.Length; i++)
            {
                float val = ParameterHelper.Get(bestParams, i);
                report.iterationLog.Add($"  {featureNames[i],-25} = {val:F2}\n");
            }
            
            report.iterationLog.Add($"\nRESULT:\n");
            report.iterationLog.Add($"    Predicted O2: {finalO2:F2}%\n");
            report.iterationLog.Add($"    Target O2:    {targetO2:F2}%\n");
            report.iterationLog.Add($"    Error:        {finalError:F3}%\n");
            
            // Build complete report string
            report.finalReport = string.Join("", report.iterationLog);
        }
        
        return bestParams;
    }

    // Advanced solver that works strictly in normalized space for precise O2 targeting
    // Uses minimal-change closed-form solution + projected gradient refinement
    public static TrialDataModels.TrialData SolveForTargetOxygen(
        MultipleLinearRegression model,
        TrialDataModels.TrialData baseParams,
        int[] topFeatureIndices,
        float targetO2,
        TrialDataModels.ParameterRanges ranges,
        System.Func<TrialDataModels.TrialData, float> predictO2,
        out float finalError)
    {
        // Build bounds array
        Vector2[] bounds = new Vector2[9] {
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

        // Smart feature selection: use ALL provided features that have headroom and significant beta
        // Filter features that:
        // 1. Have headroom (not clamped at boundaries)
        // 2. Have non-negligible coefficient (|beta| > 1e-6)
        // 3. Are NOT derived features (EffectiveDrainRate is index 9, derived from RemoveHealthEveryLifeTime/lifeTime)
        int[] free = topFeatureIndices
            .Where(j => j != 9)  // Exclude EffectiveDrainRate (index 9) - it's derived, will be updated automatically
            .Where(j => ParameterHelper.HasHeadroom(j, ranges, baseParams, threshold: 0.1f))  // Not stuck at boundary
            .Where(j => Mathf.Abs(model.coefficients[j + 1]) > 1e-6f)         // Beta not negligible
            .ToArray();
        
        // Fallback: if after filtering we have too few features, use defaults (O2drop, CollisionDamage)
        if (free.Length < 2)
        {
            Debug.LogWarning($"[Solver] Only {free.Length} features with headroom found. Using fallback: RemoveHealthEveryLifeTime, removeHealthWithCollide");
            free = new int[]{4, 5}; // RemoveHealthEveryLifeTime, removeHealthWithCollide
        }
        else
        {
            Debug.Log($"[Solver] Using {free.Length} features for optimization: {string.Join(", ", free)}");
        }

        // 1) closed-form minimal-change
        var x0 = SolveMinimalChange(model, baseParams, free, targetO2, bounds);

        // 2) projected-gradient refine (more steps for better convergence)
        var x1 = RefineProjectedGradient(model, x0, free, targetO2, bounds, predictO2, maxSteps: 50, lr: 0.3f, tol: 0.01f);
        
        float o1 = predictO2(x1);
        finalError = Mathf.Abs(o1 - targetO2);
        
        // If still not close enough, try iterative refinement with adaptive learning rate
        if (finalError > 1.0f)
        {
            Debug.LogWarning($"[Solver] Initial optimization reached {o1:F2}% (target: {targetO2:F2}%), error: {finalError:F2}%. Attempting iterative refinement...");
            x1 = RefineProjectedGradientIterative(model, x1, free, targetO2, bounds, predictO2, maxIterations: 100);
            o1 = predictO2(x1);
            finalError = Mathf.Abs(o1 - targetO2);
        }
        
        return x1;
    }

    private static TrialDataModels.TrialData SolveMinimalChange(
        MultipleLinearRegression model,
        TrialDataModels.TrialData baseParams,
        int[] freeFeatures,
        float targetO2,
        Vector2[] bounds)
    {
        // Copy working params
        var work = baseParams;

        // 1) Compute fixed contribution from locked features (normalized inputs)
        // Note: EffectiveDrainRate (index 9) is derived from RemoveHealthEveryLifeTime / lifeTime
        // If we're optimizing RemoveHealthEveryLifeTime (index 4) or lifeTime (index 3), EffectiveDrainRate will change
        // So we need to handle it specially - include it in fixed contribution only if neither 3 nor 4 are in freeFeatures
        float fixedContribution = model.coefficients[0]; // intercept
        bool optimizingLifeTimeOrDrop = System.Array.IndexOf(freeFeatures, 3) >= 0 || System.Array.IndexOf(freeFeatures, 4) >= 0;
        
        for (int j = 0; j < 10; j++)  // Fixed: Changed from 9 to 10 to include EffectiveDrainRate
        {
            if (System.Array.IndexOf(freeFeatures, j) >= 0) continue; // skip free ones
            
            // Skip EffectiveDrainRate (index 9) if we're optimizing its dependencies (lifeTime or RemoveHealthEveryLifeTime)
            if (j == 9 && optimizingLifeTimeOrDrop) continue;
            
            float xRaw  = ParameterHelper.Get(work, j);
            float xHat  = model.ToNormalized(j, xRaw);
            fixedContribution += model.coefficients[j + 1] * xHat;
        }
        
        // If we're optimizing lifeTime or RemoveHealthEveryLifeTime, we need to account for EffectiveDrainRate change
        // But since EffectiveDrainRate is a derived feature, we can't optimize it directly
        // Instead, we'll include its contribution in the gradient calculation for lifeTime and RemoveHealthEveryLifeTime

        // 2) Build 'a' vector for the free features (their betas in normalized space)
        // Using centralized GetEffectiveBeta to handle chain rule for derived features
        int m = freeFeatures.Length;
        var a = new float[m];
        
        for (int k = 0; k < m; k++)
        {
            a[k] = RegressionMath.EffectiveBeta(model, freeFeatures[k], work, optimizingLifeTimeOrDrop);
        }

        // 3) RHS for minimal-change solution: a·Δx̂ = target - fixed
        float rhs = targetO2 - fixedContribution;

        // 4) Minimal-norm Δx̂ solution:
        
        double denom = 1e-6;
        for (int k = 0; k < m; k++) denom += (double)a[k] * (double)a[k];
        float scale = (float)(rhs / denom);

        // 5) Apply Δx̂, convert to RAW, clamp
        for (int k = 0; k < m; k++)
        {
            int j = freeFeatures[k];
            float x0_raw = ParameterHelper.Get(work, j);
            float x0_hat = model.ToNormalized(j, x0_raw);
            float x1_hat = x0_hat + scale * a[k];
            float x1_raw = model.FromNormalized(j, x1_hat);
            x1_raw = ParameterHelper.Clamp(j, x1_raw, bounds); // enforce RAW bounds
            ParameterHelper.Set(ref work, j, x1_raw);
        }

        // 6) Update EffectiveDrainRate (index 9) - it's derived from RemoveHealthEveryLifeTime / lifeTime
        // This is automatically computed as a property, but we need to ensure it's included in fixed contribution
        // Note: EffectiveDrainRate will be recalculated automatically when work is used, but we need to include it in the model calculation

        return work;
    }

    private static TrialDataModels.TrialData RefineProjectedGradient(
        MultipleLinearRegression model,
        TrialDataModels.TrialData start,
        int[] freeFeatures,
        float targetO2,
        Vector2[] bounds,
        System.Func<TrialDataModels.TrialData, float> predictO2,
        int maxSteps = 5,
        float lr = 0.5f,
        float tol = 0.1f)
    {
        var cur = start;

        for (int step = 0; step < maxSteps; step++)
        {
            float y = predictO2(cur);             // uses model.PredictOxygen(data) or your predictor
            float error = y - targetO2;           // positive if we overshoot
            if (Mathf.Abs(error) <= tol) break;   // good enough

            // Adaptive learning rate: reduce as we get closer
            float adaptiveLR = lr * Mathf.Min(1f, Mathf.Max(0.1f, Mathf.Abs(error) / 5f));

            // FIXED: Use centralized functions for beta calculation and normalization
            float sumBetaSq = RegressionMath.SumBetaSq(model, freeFeatures, cur);
            bool optimizingLifeTimeOrDrop = System.Array.IndexOf(freeFeatures, 3) >= 0 || System.Array.IndexOf(freeFeatures, 4) >= 0;

            // Gradient in normalized space: d(y)/d(x̂_j) = β_j (with chain rule for derived features)
            for (int k = 0; k < freeFeatures.Length; k++)
            {
                int j = freeFeatures[k];
                float beta = RegressionMath.EffectiveBeta(model, j, cur, optimizingLifeTimeOrDrop);
                
                // FIXED: Use global normalization for stable gradient updates
                float deltaHat = -adaptiveLR * error * beta / sumBetaSq;

                float xRaw = ParameterHelper.Get(cur, j);
                float xHat = model.ToNormalized(j, xRaw);
                xHat += deltaHat;

                // back to RAW + clamp
                float newRaw = model.FromNormalized(j, xHat);
                newRaw = ParameterHelper.Clamp(j, newRaw, bounds);
                ParameterHelper.Set(ref cur, j, newRaw);
            }
        }

        return cur;
    }

    private static TrialDataModels.TrialData RefineProjectedGradientIterative(
        MultipleLinearRegression model,
        TrialDataModels.TrialData start,
        int[] freeFeatures,
        float targetO2,
        Vector2[] bounds,
        System.Func<TrialDataModels.TrialData, float> predictO2,
        int maxIterations = 100)
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
            
            if (absError <= 0.1f) break; // Good enough

            // Adaptive learning rate: start high, reduce as we get closer
            float baseLR = 0.5f;
            float adaptiveLR = baseLR * Mathf.Max(0.05f, Mathf.Min(1f, absError / 10f));

            // FIXED: Use centralized functions for beta calculation and normalization
            float sumBetaSq = RegressionMath.SumBetaSq(model, freeFeatures, cur);
            bool optimizingLifeTimeOrDrop = System.Array.IndexOf(freeFeatures, 3) >= 0 || System.Array.IndexOf(freeFeatures, 4) >= 0;

            // Update each free feature
            bool anyChange = false;
            for (int k = 0; k < freeFeatures.Length; k++)
            {
                int j = freeFeatures[k];
                float beta = RegressionMath.EffectiveBeta(model, j, cur, optimizingLifeTimeOrDrop);
                
                if (Mathf.Abs(beta) < 1e-6f) continue; // Skip features with negligible impact
                
                // FIXED: Use global normalization for stable gradient updates
                float deltaHat = -adaptiveLR * error * beta / sumBetaSq;

                float xRaw = ParameterHelper.Get(cur, j);
                float xHat = model.ToNormalized(j, xRaw);
                xHat += deltaHat;

                // Convert back to raw space and clamp
                float newRaw = model.FromNormalized(j, xHat);
                float clampedRaw = ParameterHelper.Clamp(j, newRaw, bounds);
                
                if (Mathf.Abs(clampedRaw - xRaw) > 0.001f)
                {
                    ParameterHelper.Set(ref cur, j, clampedRaw);
                    anyChange = true;
                }
            }
            
            if (!anyChange) break; // All parameters at bounds
        }

        return bestParams; // Return best solution found
    }
}

