using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/**
 * Finds optimal game parameters to achieve target oxygen level.
 * 
 * Solves the "inverse regression" problem: given a model that predicts oxygen from parameters,
 * we need to find parameters that produce a specific target oxygen level (for example 10%).
 * The regression equation is: oxygen = b0 + b1*speed + b2*damage + ...
 * We solve for speed, damage, etc. when oxygen is fixed.
 * 
 * Called from RegressionUtilities.OptimizeParameters which tries methods in order:
 * 1. SolveForTargetOxygen - 3-phase optimizer (analytical, gradient descent, extended refinement).
 *    Primary method, called first.
 * 2. RandomSweepOptimizer - random search fallback if solution is null, error is NaN, or error > 5%.
 * 3. SolveForTargetDifficultyMulti - gradient descent fallback if solution is null or error is NaN.
 * 
 * Returns the best solution found across all attempted methods.
 */

public static class DifficultyParameterSolver
{
    // Learning rates control how big each optimization step is
    // Higher = faster convergence but might overshoot
    // Lower = more precise but slower
    private const float DEFAULT_LEARNING_RATE = 0.2f;      // For errors < 15%
    private const float MODERATE_LEARNING_RATE = 0.25f;    // For errors 15-30%
    private const float AGGRESSIVE_LEARNING_RATE = 0.3f;   // For errors > 30%

    // Convergence threshold: stop when error is below this value
    private const float CONVERGENCE_THRESHOLD = 0.2f;

    // Maximum iterations to prevent infinite loops - more iterations for larger initial errors
    private const int DEFAULT_MAX_ITERATIONS = 150;        // For errors < 15%
    private const int MODERATE_MAX_ITERATIONS = 200;       // For errors 15-30%
    private const int AGGRESSIVE_MAX_ITERATIONS = 250;     // For errors > 30%



    /**
     * Main optimizer - called first by RegressionUtilities.OptimizeParameters.
     * 
     * This is the PRIMARY solver that runs 3 PHASES internally:
     * 
     * Phase 1: SolveMinimalChange() - Analytical solution (minimal change using effective betas)
     * Phase 2: RefineProjectedGradient() - Gradient refinement (projected gradient descent with clamping)
     * Phase 3: RefineProjectedGradientIterative() - Extended refinement (further iterative search that tracks best found)
     * 
     * @param model Trained regression model (coefficients + optional normalization data)
     * @param baseParams Patient baseline TrialData derived from previous trials
     * @param topFeatureIndices Indices into FeatureExtractor.FeatureNames defining desired freedom
     * @param targetO2 Target oxygen percentage to hit
     * @param ranges Parameter range container used for clamping and headroom checks
     * @param predictO2 Function that maps TrialData to predicted oxygen (usually model.Predict ∘ ExtractFeatures)
     * @param finalError Absolute prediction error achieved by the returned parameters
     * @return TrialData containing the best parameter set produced across the three phases
     */

     
    public static TrialDataModels.TrialData SolveForTargetOxygen(MultipleLinearRegression model, TrialDataModels.TrialData baseParams,
        int[] topFeatureIndices, float targetO2, TrialDataModels.ParameterRanges ranges, System.Func<TrialDataModels.TrialData, float> predictO2, out float finalError)
    {
        string[] fullFeatureNames = FeatureExtractor.FeatureNames; 
        string[] modelFeatureNames = model.featureNames ?? fullFeatureNames;

       
        // Define bounds for base parameters (9 parameters only, excluding derived features like EffectiveDrainRate).
        // Each parameter is constrained to its valid range as defined in ranges.
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
        // Determine which features have headroom (can be adjusted)
        int[] free = FilterFreeFeatures(topFeatureIndices, ranges, baseParams);

        if (free.Length < 2) // If there are less than 2 features with headroom, use fallback
        {
            Debug.LogWarning($" Only {free.Length} features with headroom, using fallback"); // Log warning if there are less than 2 features with headroom
            free = new int[] { 4, 5 }; // Use fallback features (RemoveHealthEveryLifeTime and removeHealthWithCollide)
        }
        // ========== PHASE 1: Analytical Solution ==========
        // Minimal-change analytic solution using closed-form math
        var x0 = SolveMinimalChange(model, baseParams, free, targetO2, bounds, fullFeatureNames, modelFeatureNames);

        // Calculate initial error and determine adaptive parameters for Phase 2
        float initialPrediction = predictO2(x0);
        float initialError = Mathf.Abs(initialPrediction - targetO2);
        
        // Determine adaptive optimization parameters for Phase 2:
        // - Error > 40: 250 steps, learning rate 0.7
        // - Error > 20: 150 steps, learning rate 0.5
        // - Error > 10: 100 steps, learning rate 0.3
        // - Otherwise: 50 steps, learning rate 0.2
        int maxSteps = initialError > 40f ? 250 : (initialError > 20f ? 150 : (initialError > 10f ? 100 : 50));
        float learningRate = initialError > 40f ? 0.7f : (initialError > 20f ? 0.5f : (initialError > 10f ? 0.3f : 0.2f));
        float tolerance = 0.2f;  // Accept 0.2% error (balance: accuracy + variability)

        // ========== PHASE 2: Gradient Descent Refinement ==========
        // Projected gradient refinement - iteratively adjusts parameters
        var x1 = RefineProjectedGradient(model, x0, free, targetO2, bounds, predictO2, maxSteps, learningRate, tolerance, fullFeatureNames, modelFeatureNames);

        // ========== PHASE 3: Extended Iterative Refinement ==========
        // Only runs if Phase 2 error > 0.5% - tracks best solution found
        float o1 = predictO2(x1);
        finalError = Mathf.Abs(o1 - targetO2);

        if (finalError > 0.5f) // If error is greater than 0.5%, use iterative refinement  
        {
            int iterativeMaxIter = finalError > 40f ? 400 : (finalError > 20f ? 300 : (finalError > 10f ? 200 : 150));
            x1 = RefineProjectedGradientIterative(model, x1, free, targetO2, bounds, predictO2, iterativeMaxIter, fullFeatureNames, modelFeatureNames);
            o1 = predictO2(x1);
            finalError = Mathf.Abs(o1 - targetO2);
        }

        return x1;
    }


    /**
     * RandomSweepOptimizer - FALLBACK SOLVER (not a phase, this is a separate solver).
     * 
     * This is one of the THREE SOLVERS, used when SolveForTargetOxygen fails.
     * Generates random parameter combinations, predicts oxygen for each, returns the one with smallest error.
     * Used when gradient methods fail or data is far from target.
     * 
     * Uses biased sampling strategy: for low oxygen targets (<30%), biases towards harder parameters
     * (high speed, high damage, low healing). For high oxygen targets (>70%), biases towards easier parameters
     * (low speed, low damage, high healing). Uses power function to bias sampling towards range boundaries.
     * 
     * Called from:
     * - RegressionUtilities.OptimizeParameters - if SolveForTargetOxygen fails or error > 5%
     * - PythonRegressionHandler.PerformPythonRegressionAnalysis (line 124) - always runs for comparison
     */
    public static (TrialDataModels.TrialData candidate, float error) RandomSweepOptimizer(System.Func<TrialDataModels.TrialData, float> predictFunc,
        TrialDataModels.ParameterRanges ranges, float targetOxygen, bool allowFactorForce, int samples = 150)
    {
        if (predictFunc == null || ranges == null || samples <= 0)
            return (null, float.MaxValue);

        // Seed based on target oxygen only
        // var random = new System.Random((int)System.DateTime.Now.Ticks);
        // Results are PERSONALIZED because:
        // - Model is trained on patient's specific trial data
        // - Different patients → different model -> different predictions -> different "best" selection
        // Same patient clicking multiple times -> same results (deterministic)
        int seed = (int)(targetOxygen * 1000);
        var random = new System.Random(seed);
        TrialDataModels.TrialData bestCandidate = null;
        float bestError = float.MaxValue;

        // Bias sampling towards extremes for low/high targets
        bool biasToLow = targetOxygen < 30f;
        bool biasToHigh = targetOxygen > 70f;

        float Sample(Vector2 range, bool preferMin = false, bool preferMax = false)
        {
            float t = (float)random.NextDouble();
            // Bias towards extremes using power function
            if (preferMin) t = t * t; // Bias towards 0 (min)
            if (preferMax) t = 1f - (1f - t) * (1f - t); // Bias towards 1 (max)
            return UnityEngine.Mathf.Lerp(range.x, range.y, t);
        }

        for (int i = 0; i < samples; i++)
        {
            var candidate = new TrialDataModels.TrialData
            {
                // For low oxygen: bias to high speed, high damage, min healing
                speed = Sample(ranges.speedRange, preferMin: biasToHigh, preferMax: biasToLow),
                verticalSpeed = Sample(ranges.verticalSpeedRange), // Vertical speed is not biased
                idleUpwardSpeed = Sample(ranges.idleUpwardSpeedRange, preferMin: biasToHigh, preferMax: biasToLow), // Idle upward speed is biased to high if oxygen is low
                lifeTime = Sample(ranges.lifeTimeRange, preferMin: biasToLow, preferMax: biasToHigh), // Life time is biased to high if oxygen is high
                RemoveHealthEveryLifeTime = Sample(ranges.RemoveHealthEveryLifeTimeRange, preferMin: biasToHigh, preferMax: biasToLow), // Remove health every life time is biased to high if oxygen is low
                removeHealthWithCollide = Sample(ranges.removeHealthWithCollideRange, preferMin: biasToHigh, preferMax: biasToLow), // Remove health with collide is biased to high if oxygen is low
                timeBetweenCollides = Sample(ranges.timeBetweenCollidesRange, preferMin: biasToLow, preferMax: biasToHigh), // Time between collides is biased to high if oxygen is high
                healHealthPoint = Sample(ranges.healHealthPointRange, preferMin: biasToLow, preferMax: biasToHigh), // Heal health point is biased to high if oxygen is high
                factorForce = allowFactorForce ? Sample(ranges.factorForceRange) : 0f,
                IsAmadeoMode = allowFactorForce ? 1f : 0f
            };

            float predicted = predictFunc(candidate); // Predict oxygen for the candidate
            float error = UnityEngine.Mathf.Abs(predicted - targetOxygen); // Calculate error between predicted and target oxygen

            if (error < bestError)
            {
                bestError = error;
                bestCandidate = candidate;
            }
        }

        return (bestCandidate, bestError);
    }


    /**
     * SolveForTargetDifficultyMulti - FALLBACK SOLVER (not a phase, this is a separate solver).
     * 
     * This is one of the THREE SOLVERS, used when both SolveForTargetOxygen and RandomSweepOptimizer fail.
     * Gradient-descent optimizer that updates the top-K most important features.
     * Called when SolveForTargetOxygen and RandomSweepOptimizer both fail (solution is null or error is NaN).
     * 
     * Each iteration:  1. Predicts oxygen level, 2. Calculates error, 3. Updates features proportionally to regression coefficients, 4. Parameters are normalized by sum of squared coefficients, 5. Parameters are clamped to valid ranges, 6. Stops when error falls below convergence threshold.
     * 
     * @param model Trained regression model with coefficients
     * @param targetO2 Target oxygen percentage         
     * @param maxFeaturesToOptimize How many features to adjust (default 3)
     * @param baseline Starting point - patient's median parameters from previous trials
     * @param ranges Valid min/max for each parameter
     * @param featureNames Feature name ordering used by model (optional).
     * @param trials Optional historical trials; used to decide whether factorForce is allowed.
     * @return Optimized parameters, or null if inputs invalid
     */
    public static TrialDataModels.TrialData SolveForTargetDifficultyMulti(MultipleLinearRegression model, float targetO2, int maxFeaturesToOptimize = 3,
        TrialDataModels.TrialData baseline = null, TrialDataModels.ParameterRanges ranges = null, string[] featureNames = null, List<TrialDataModels.TrialData> trials = null)
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
        // Ensure ranges is not null to avoid null checks later
        ranges ??= new TrialDataModels.ParameterRanges();
        string[] fullFeatureNames = FeatureExtractor.FeatureNames; // Get all feature names
        string[] modelFeatureNames = model.featureNames ?? fullFeatureNames;
        featureNames ??= fullFeatureNames; // Use all feature names if no feature names are provided
        var importance = model.GetFeatureImportance(); // Get importance ranking from model (feature, importance) tuples
        bool allowFactorForce = trials != null && trials.Any(t => t.IsAmadeoMode > 0.5f); //If any trial is in Amadeo mode, allow factorForce to be tuned
        var banned = CreateBannedFeatureSet(allowFactorForce); //Create set of features to exclude from optimization (derived features, Amadeo-only)
        var topFeatures = BuildTopFeatureList(importance, maxFeaturesToOptimize, banned); //Build list of top features ignoring banned ones

        // Fallback features if model importance returned nothing usable - if no valid features, use known oxygen-affecting parameters
        if (topFeatures.Length == 0)
        {
            Debug.LogWarning("[DifficultyParameterSolver] No valid features found, using fallback: RemoveHealthEveryLifeTime, removeHealthWithCollide");
            topFeatures = new[] { "RemoveHealthEveryLifeTime", "removeHealthWithCollide" };
        }

        if (topFeatures.Length > maxFeaturesToOptimize && maxFeaturesToOptimize > 0)
        {
            topFeatures = topFeatures.Take(maxFeaturesToOptimize).ToArray();
        }
        // Get means and stdDevs from model (if they exist) - used for normalization
        var mu = model.normalizer?.means;
        var sigma = model.normalizer?.stdDevs;

        // Use only the provided baseline (no fallback calculation!)
        var bestParams = baseline;
        // Initial prediction and adaptive step control - calculate initial oxygen and error
        float initialO2 = model.Predict(FeatureExtractor.ExtractFeatures(bestParams));
        float deltaFromTarget = Mathf.Abs(initialO2 - targetO2);
        float learningRate = GetAdaptiveLearningRate(deltaFromTarget);
        int maxIterations = GetAdaptiveMaxIterations(deltaFromTarget);

        for (int iter = 0; iter < maxIterations; iter++)
        {
            float currentO2 = model.Predict(FeatureExtractor.ExtractFeatures(bestParams));
            float error = currentO2 - targetO2; // Calculate error between current oxygen and target oxygen

            if (Mathf.Abs(error) < CONVERGENCE_THRESHOLD) break; // Stop if error is below convergence threshold

            float adaptiveLR = learningRate * Mathf.Min(1f, Mathf.Abs(error) / 5f); // Scale down learning rate when error is small to avoid oscillations

            // Calculate sum of squared coefficients - used to normalize multi-feature steps
            float sumBetaSq = 0f;
            foreach (var featureName in topFeatures) // Update each feature proportionally to its coefficient (gradient direction)
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

            // Update each chosen feature proportionally to its coefficient (gradient direction)
            foreach (var featureName in topFeatures)
            {
                int modelIdx = System.Array.IndexOf(modelFeatureNames, featureName); // Get model index of feature
                int fullIdx = System.Array.IndexOf(fullFeatureNames, featureName); // Get full index of feature (including derived features)
                if (modelIdx < 0 || fullIdx < 0 || modelIdx + 1 >= model.coefficients.Length) continue;

                float coef = model.coefficients[modelIdx + 1];
                if (Mathf.Abs(coef) < 0.0001f) continue; // ignore near-zero coefficients

                float oldValueRaw = ParameterHelper.Get(bestParams, fullIdx); // Get current value of feature
                float oldValueNorm = ApplyNormalization(oldValueRaw, modelIdx, mu, sigma); // Convert to normalized value

                float gradient = coef;
                // negative sign: reduce oxygen if prediction > target, increase otherwise
                float step = -adaptiveLR * error * gradient / sumBetaSq;
                float newValueNorm = oldValueNorm + step; // Calculate new normalized value

                float newValueRaw = ReverseNormalization(newValueNorm, modelIdx, mu, sigma); // Convert back to raw value
                // clamp to allowed range for parameter index - ensure the new value is within the allowed range
                (float min, float max) = ParameterHelper.Range(ranges, fullIdx);
                float clampedValue = Mathf.Clamp(newValueRaw, min, max);

                if (Mathf.Abs(clampedValue - oldValueRaw) > 0.001f)
                {
                    ParameterHelper.Set(ref bestParams, fullIdx, clampedValue);
                    anyAdjustment = true; // Set to true if any adjustment was made
                }
            }

            if (!anyAdjustment)
                break;
        }

        float finalO2 = model.Predict(FeatureExtractor.ExtractFeatures(bestParams));
        float finalError = Mathf.Abs(finalO2 - targetO2);
        return bestParams;
    }

    // *********** Three-Phase Optimization Helpers (for SolveForTargetOxygen only) ***********
    
    /**
     * Phase 1 of SolveForTargetOxygen: Analytical solution.
     * 
     * This is ONE of the THREE PHASES inside SolveForTargetOxygen (not a separate solver).
     * Calculates the minimal parameter changes needed to reach target oxygen using closed-form math.
     * 
     * Algorithm:
     * 1. Calculate fixed contribution: intercept + all non-optimized features
     * 2. Calculate needed change: rhs = target - fixed_contribution
     * 3. Scale free features: delta_x = rhs * beta / sum(beta^2)
     * 
     * Formula: x_new = x_old + (target - fixed_contribution) * beta / sum(beta^2)
     * This distributes the needed change proportionally across free features.
     * 
     * Called from: SolveForTargetOxygen (line 89) - Phase 1
     */
    private static TrialDataModels.TrialData SolveMinimalChange( MultipleLinearRegression model,TrialDataModels.TrialData baseParams,
        int[] freeFeatures, // Indices of features to optimize
        float targetO2, // Target oxygen percentage
        Vector2[] bounds, // Allowed parameter ranges
        string[] fullFeatureNames, // All feature names
        string[] modelFeatureNames) // Feature names used by the model
    {
        var work = baseParams;

        // Step 1: Calculate fixed contribution (intercept + non-optimized features)
        // Start with intercept (constant term of regression model)
        float fixedContribution = model.coefficients[0];
        
        // Check if we're optimizing lifeTime (index 3) or RemoveHealthEveryLifeTime (index 4)
        // These affect EffectiveDrainRate, so we need special handling
        bool optimizingDrainRateDeps = System.Array.IndexOf(freeFeatures, 3) >= 0 || System.Array.IndexOf(freeFeatures, 4) >= 0;
   
        // Add contribution from all features that we're not optimizing
        for (int j = 0; j < fullFeatureNames.Length; j++)
        {
            // Skip features we're optimizing
            if (System.Array.IndexOf(freeFeatures, j) >= 0) continue;

            // Skip EffectiveDrainRate if we're changing its dependencie (it will be recalculated automatically)
            if (optimizingDrainRateDeps && fullFeatureNames[j].StartsWith("Effective")) continue;

            string featureName = fullFeatureNames[j];
            int modelIdx = System.Array.IndexOf(modelFeatureNames, featureName);

            if (modelIdx < 0 || modelIdx + 1 >= model.coefficients.Length) continue;

            // Get current value, normalize it, and add its contribution
            float xRaw = ParameterHelper.Get(work, j);
            float xHat = model.ToNormalized(modelIdx, xRaw);
            fixedContribution += model.coefficients[modelIdx + 1] * xHat;
        }

        // Step 2: Get effective coefficients for free features
        // EffectiveBeta accounts for indirect effects (e.g., lifeTime affects EffectiveDrainRate)
        int m = freeFeatures.Length;
        var a = new float[m];

        for (int k = 0; k < m; k++)
        {
            a[k] = RegressionMath.EffectiveBeta(model, freeFeatures[k], work, optimizingDrainRateDeps);
        }

        // Step 3: Calculate how much we need to change
        // rhs = right-hand side = target - what fixed features already contribute
        float rhs = targetO2 - fixedContribution;
        
        // Step 4: Calculate normalization factor (sum of squared coefficients)
        // This ensures features with large coefficients don't dominate
        double denom = 1e-6; // Small epsilon to prevent division by zero
        for (int k = 0; k < m; k++) denom += (double)a[k] * (double)a[k];
        float scale = (float)(rhs / denom);

        // Step 5: Update each free feature proportionally to its coefficient
        for (int k = 0; k < m; k++)
        {
            int j = freeFeatures[k];

            string featureName = fullFeatureNames[j];
            int modelIdx = System.Array.IndexOf(modelFeatureNames, featureName);
            if (modelIdx < 0) continue;

            // Get current normalized value
            float x0_raw = ParameterHelper.Get(work, j);
            float x0_hat = model.ToNormalized(modelIdx, x0_raw);
            
            // Apply the change: new = old + scale * coefficient
            float x1_hat = x0_hat + scale * a[k];
            
            // Convert back to raw scale and clamp to valid range
            float x1_raw = model.FromNormalized(modelIdx, x1_hat);
            x1_raw = ParameterHelper.Clamp(j, x1_raw, bounds);
            ParameterHelper.Set(ref work, j, x1_raw);
        }

        return work;
    }

    /**
     * Phase 2 of SolveForTargetOxygen: Gradient descent refinement.
     * 
     * This is ONE of the THREE PHASES inside SolveForTargetOxygen (not a separate solver).
     * Iteratively adjusts parameters to minimize prediction error.
     * 
     * Algorithm:
     * 1. Predict oxygen with current parameters
     * 2. Calculate error = prediction - target
     * 3. For each feature: update = -learning_rate * error * coefficient / sum(coefficients^2)
     * 4. Clamp to valid ranges and repeat until error < tolerance
     * 
     * Learning rate adapts based on error magnitude:
     * - Large error (>5%): full learning rate
     * - Small error (<5%): reduced learning rate (0.1x to 1x)
     * This prevents overshooting when close to target.
     * 
     * Called from: SolveForTargetOxygen (line 105) - Phase 2
     */
    private static TrialDataModels.TrialData RefineProjectedGradient(
        MultipleLinearRegression model,
        TrialDataModels.TrialData start, // Starting point from Phase 1
        int[] freeFeatures, // Features to optimize
        float targetO2, // Target oxygen level
        Vector2[] bounds, // Valid parameter ranges
        System.Func<TrialDataModels.TrialData, float> predictO2, // Prediction function
        int maxSteps, // Maximum iterations
        float lr, // Base learning rate
        float tol, // Convergence tolerance
        string[] fullFeatureNames,
        string[] modelFeatureNames)
    {
        var cur = start;

        // Main optimization loop
        for (int step = 0; step < maxSteps; step++)
        {
            // Step 1: Predict oxygen with current parameters
            float y = predictO2(cur);
            float error = y - targetO2; // Positive = too high, negative = too low
            
            // Check convergence: stop if error is small enough
            if (Mathf.Abs(error) <= tol) break;

            // Step 2: Calculate adaptive learning rate
            // When error is large (>5%), use full learning rate
            // When error is small (<5%), scale down proportionally (min 0.1x)
            // This prevents overshooting when we're close to target
            float adaptiveLR = lr * Mathf.Min(1f, Mathf.Max(0.1f, Mathf.Abs(error) / 5f));

            // Step 3: Calculate normalization factor (sum of squared coefficients)
            // This ensures all features contribute equally regardless of coefficient magnitude
            float sumBetaSq = RegressionMath.SumBetaSq(model, freeFeatures, cur);
            
            // Check if we're optimizing lifeTime or RemoveHealthEveryLifeTime
            // These affect EffectiveDrainRate, so we need EffectiveBeta instead of regular beta
            bool optimizingLifeTimeOrDrop = System.Array.IndexOf(freeFeatures, 3) >= 0 || System.Array.IndexOf(freeFeatures, 4) >= 0;

            // Step 4: Update each free feature
            for (int k = 0; k < freeFeatures.Length; k++)
            {
                int j = freeFeatures[k];
                
                // Get effective coefficient (accounts for indirect effects via EffectiveDrainRate)
                float beta = RegressionMath.EffectiveBeta(model, j, cur, optimizingLifeTimeOrDrop);

                // Calculate update step in normalized space
                // Negative sign: if error > 0 (too high), move opposite to gradient (decrease)
                // Divide by sumBetaSq to normalize across features
                float deltaHat = -adaptiveLR * error * beta / sumBetaSq;

                string featureName = fullFeatureNames[j];
                int modelIdx = System.Array.IndexOf(modelFeatureNames, featureName);
                if (modelIdx < 0) continue;

                // Get current value and normalize it
                float xRaw = ParameterHelper.Get(cur, j);
                float xHat = model.ToNormalized(modelIdx, xRaw);
                
                // Apply the update in normalized space
                xHat += deltaHat;

                // Convert back to raw scale and clamp to valid range
                float newRaw = model.FromNormalized(modelIdx, xHat);
                newRaw = ParameterHelper.Clamp(j, newRaw, bounds);
                ParameterHelper.Set(ref cur, j, newRaw);
            }
        }

        return cur;
    }

    /**
     * Phase 3 of SolveForTargetOxygen: Extended refinement with best-solution tracking.
     * 
     * This is ONE of the THREE PHASES inside SolveForTargetOxygen (not a separate solver).
     * Similar to Phase 2, but tracks the best solution found during all iterations.
     * Prevents returning a worse solution if gradient descent overshoots the optimal point.
     * 
     * Key differences from Phase 2:
     * - Remembers best error/parameters found (not just last iteration)
     * - Learning rate: baseLR (0.5 or 0.7) scaled by error/10 (0.05x to 1x)
     * - Stops when error <= 0.2% or no parameters can be adjusted
     * 
     * Used only when Phase 2 error > 0.5%.
     * Called from: SolveForTargetOxygen (line 114) - Phase 3
     */
    private static TrialDataModels.TrialData RefineProjectedGradientIterative(
        MultipleLinearRegression model,
        TrialDataModels.TrialData start, // Starting point from Phase 2
        int[] freeFeatures, // Features to optimize
        float targetO2, // Target oxygen level
        Vector2[] bounds, // Valid parameter ranges
        System.Func<TrialDataModels.TrialData, float> predictO2, // Prediction function
        int maxIterations, // Maximum iterations (adaptive based on error from Phase 2)
        string[] fullFeatureNames,
        string[] modelFeatureNames)
    {
        var cur = start;
        float bestError = float.MaxValue; // Track the best error found so far
        var bestParams = start; // Track the best parameters found so far

        for (int iter = 0; iter < maxIterations; iter++)
        {
            float y = predictO2(cur);
            float error = y - targetO2;
            float absError = Mathf.Abs(error);

            // Update best solution if current is better
            // This ensures we return the best point even if later iterations overshoot
            if (absError < bestError)
            {
                bestError = absError;
                bestParams = ParameterHelper.Clone(cur);
            }

            if (absError <= 0.2f) break;

            // Adaptive learning rate: baseLR (0.5 or 0.7) scaled by error/10
            float baseLR = absError > 30f ? 0.7f : 0.5f;
            float scale = Mathf.Max(0.05f, Mathf.Min(1f, absError / 10f));
            float adaptiveLR = baseLR * scale;

            // Calculate normalization factor (sum of squared coefficients)
            float sumBetaSq = RegressionMath.SumBetaSq(model, freeFeatures, cur);
            
            // Check if optimizing lifeTime or RemoveHealthEveryLifeTime (affects EffectiveDrainRate)
            bool optimizingLifeTimeOrDrop = System.Array.IndexOf(freeFeatures, 3) >= 0 || System.Array.IndexOf(freeFeatures, 4) >= 0;

            bool anyChange = false;
            for (int k = 0; k < freeFeatures.Length; k++)
            {
                int j = freeFeatures[k];
                
                // Get effective coefficient (accounts for indirect effects via EffectiveDrainRate)
                float beta = RegressionMath.EffectiveBeta(model, j, cur, optimizingLifeTimeOrDrop);

                if (Mathf.Abs(beta) < 1e-6f) continue;

                // Calculate update step: move opposite to error direction
                float deltaHat = -adaptiveLR * error * beta / sumBetaSq;

                string featureName = fullFeatureNames[j];
                int modelIdx = System.Array.IndexOf(modelFeatureNames, featureName);
                if (modelIdx < 0) continue;

                float xRaw = ParameterHelper.Get(cur, j);
                float xHat = model.ToNormalized(modelIdx, xRaw);
                
                // Apply update in normalized space
                xHat += deltaHat;

                float newRaw = model.FromNormalized(modelIdx, xHat);
                float clampedRaw = ParameterHelper.Clamp(j, newRaw, bounds);

                // Only update if change is significant (prevents tiny oscillations)
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

    // ============================================
    // PRIVATE - Helper Utilities
    // ============================================

    // Converts raw parameter value to z-score using model's normalization stats.
    // Called from: SolveForTargetDifficultyMulti (line 283)
    private static float ApplyNormalization(float rawValue, int modelIdx, float[] means, float[] stdDevs)
    {
        if (means != null && stdDevs != null && modelIdx < means.Length)
            return (rawValue - means[modelIdx]) / Mathf.Max(1e-6f, stdDevs[modelIdx]);
        return rawValue;
    }

    // Converts z-score back to raw parameter value.
    // Called from: SolveForTargetDifficultyMulti (line 290)
    private static float ReverseNormalization(float normValue, int modelIdx, float[] means, float[] stdDevs)
    {
        if (means != null && stdDevs != null && modelIdx < means.Length)
            return normValue * Mathf.Max(1e-6f, stdDevs[modelIdx]) + means[modelIdx];
        return normValue;
    }

    // Selects the top N most important features, excluding banned ones like EffectiveDrainRate.
    // Called from: SolveForTargetDifficultyMulti (line 223)
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

    // Filters to features that have room to adjust (not already at min/max boundary).
    // Called from: SolveForTargetOxygen (line 80)
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

    // Creates set of features to exclude from optimization (derived features, Amadeo-only).
    // Called from: SolveForTargetDifficultyMulti (line 222)
    private static HashSet<string> CreateBannedFeatureSet(bool allowFactorForce)
    {
        var banned = new HashSet<string> { "EffectiveDrainRate" };
        if (!allowFactorForce)
        {
            banned.Add("factorForce");
        }
        return banned;
    }

    // Returns adaptive learning rate - higher when far from target, lower when close.
    // Called from: SolveForTargetDifficultyMulti (line 245)
    private static float GetAdaptiveLearningRate(float errorDistance)
    {
        if (errorDistance > 30f) return AGGRESSIVE_LEARNING_RATE;
        if (errorDistance > 15f) return MODERATE_LEARNING_RATE;
        return DEFAULT_LEARNING_RATE;
    }

    // Returns max iterations - more attempts when far from target.
    // Called from: SolveForTargetDifficultyMulti (line 246)
    private static int GetAdaptiveMaxIterations(float errorDistance)
    {
        if (errorDistance > 30f) return AGGRESSIVE_MAX_ITERATIONS;
        if (errorDistance > 15f) return MODERATE_MAX_ITERATIONS;
        return DEFAULT_MAX_ITERATIONS;
    }

}

