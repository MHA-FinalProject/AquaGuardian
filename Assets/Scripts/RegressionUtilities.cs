using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/**
 * Helper utility class for regression analysis workflow.
 * 
 * Provides utility functions for statistics, cross-validation, feature preparation,
 * parameter optimization, range constraints, and baseline preparation.
 * 
 * See also: TrialRegressionAlgorithm, OxygenPredictor, MultipleLinearRegression, 
 *           DifficultyParameterSolver, FeatureExtractor, PythonRegressionHandler
 */
public static class RegressionUtilities
{
    private const float RANDOM_SWEEP_THRESHOLD = 5.0f;

    // Buffer mode can be set from UI (TrialRegressionUI)
    public static BufferMode CurrentBufferMode { get; set; } = BufferMode.Conservative;

    #region Public API - Main Functions

    /**
     * Main optimization function for C# (Unity) mode.
     * 
     * Strategy: Gradient 3-Phase as PRIMARY, RandomSweep only as FALLBACK.
     * 
     * PRIMARY: Calls DifficultyParameterSolver.SolveForTargetOxygen (3-phase gradient descent).
     * FALLBACK: If solution is null, error is NaN, or error > 5%, calls RandomSweepOptimizer.
     * LAST RESORT: If solution is still null or error is NaN, calls SolveForTargetDifficultyMulti.
     * 
     * @return (solution, error, selectedMethod) where selectedMethod is "Gradient 3-Phase", "RandomSweep", "Multi-Gradient", or "Fallback"
     */
    public static (TrialDataModels.TrialData solution, float error, string selectedMethod) OptimizeParameters(
        MultipleLinearRegression model, OxygenPredictor predictor, List<TrialDataModels.TrialData> trials, (string feature, float importance)[] filteredImportance, string[] featureNames, float targetOxygen, bool anyAmadeoTrials)
    {
        string selectedMethod = "Gradient 3-Phase";
        
        try
        {
            int[] topIndices = BuildOptimizationIndices(filteredImportance, featureNames);
            var (ranges, baseline) = PrepareOptimizationBaseline(trials, targetOxygen);

            // PRIMARY: Gradient 3-Phase (works great with C# Ridge dense coefficients)
            var solution = DifficultyParameterSolver.SolveForTargetOxygen(
                model, baseline, topIndices, targetOxygen, ranges,
                d => predictor.PredictOxygenUnclamped(d),
                out float error);

            // FALLBACK: RandomSweep only if Gradient failed or error is too high
            if (solution == null || float.IsNaN(error) || error > RANDOM_SWEEP_THRESHOLD)
            {
                Debug.Log($"[Optimization] Gradient 3-Phase error={error:F2}%, trying RandomSweep fallback");
                var (sweepSolution, sweepError) = DifficultyParameterSolver.RandomSweepOptimizer(
                    d => predictor.PredictOxygenUnclamped(d),
                    ranges, targetOxygen, anyAmadeoTrials, samples: 300);

                if (sweepSolution != null && !float.IsNaN(sweepError) &&
                    (solution == null || sweepError < error))
                {
                    solution = sweepSolution;
                    error = sweepError;
                    selectedMethod = "RandomSweep";
                }
            }

            // gradient descent fallback
            if (solution == null || float.IsNaN(error))
            {
                solution = DifficultyParameterSolver.SolveForTargetDifficultyMulti(
                    model, targetOxygen, topIndices.Length, baseline,
                    ranges, featureNames, trials);
                error = solution != null ? CalculateError(predictor, solution, targetOxygen) : float.NaN;
                if (solution != null) selectedMethod = "Multi-Gradient";
            }

            return (solution, error, selectedMethod);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[TrialRegression] Optimization failed: {e.Message}");

            var (ranges, baseline) = PrepareOptimizationBaseline(trials, targetOxygen);
            var solution = DifficultyParameterSolver.SolveForTargetDifficultyMulti(
                model, targetOxygen, model.numFeatures, baseline, ranges, featureNames, trials);

            float error = solution != null
                ? CalculateError(predictor, solution, targetOxygen)
                : float.NaN;

            return (solution, error, "Fallback");
        }
    }

    /**
     * Performs K-fold cross-validation and calculates average prediction error.
     * 
     * Only performs CV if trials.Count >= 10 and trials.Count > p + 1 (where p = number of features).
     * Calculates K-folds: Mathf.Clamp(trials.Count / 3, 2, 5) - between 2 and 5 folds.
     * 
     * Returns: (cvRmse, cvMae, cvR2, kFolds, avgError)
     * - CV metrics are NaN if CV not performed (too few trials)
     * - avgError is always calculated (average absolute error on training data)
     */
    public static (float cvRmse, float cvMae, float cvR2, int kFolds, float avgError)
        PerformCrossValidationAndErrorCalculation(
            List<TrialDataModels.TrialData> trials,
            MultipleLinearRegression model,
            OxygenPredictor predictor,
            int featureCount)
    {
        float cvRmse = float.NaN;
        float cvMae = float.NaN;
        float cvR2 = float.NaN;
        int kFolds = 0;

        if (trials.Count >= 10)
        {
            int p = model?.numFeatures > 0
                ? model.numFeatures
                : (model?.featureNames?.Length ?? featureCount);

            if (trials.Count > p + 1)
            {
                kFolds = Mathf.Clamp(trials.Count / 3, 2, 5);
                if (kFolds > 1)
                {
                    var (X, y) = FeatureExtractor.ExtractFeaturesAndTargets(trials);
                    var cvMetrics = model.KFoldCV(X, y, kFolds);
                    cvRmse = cvMetrics.RMSE;
                    cvMae = cvMetrics.MAE;
                    cvR2 = cvMetrics.R2;
                }
            }
        }

        float avgError = trials.Average(t => Mathf.Abs(t.finalOxygenRemaining - predictor.PredictOxygen(t)));

        return (cvRmse, cvMae, cvR2, kFolds, avgError);
    }

    /**
     * Prepares features for optimization by filtering out banned features.
     * 
     * Always bans "EffectiveDrainRate" (derived feature, causes multicollinearity).
     * Bans "factorForce" if no Amadeo trials (not applicable for keyboard mode).
     * 
     * Returns: (bannedFeatures, filteredImportance)
     */
    public static (HashSet<string> bannedFeatures, (string feature, float importance)[] filteredImportance)
        PrepareOptimizationFeatures(
            List<TrialDataModels.TrialData> trials,
            OxygenPredictor predictor)
    {
        bool anyAmadeoTrials = trials.Any(t => t.IsAmadeoMode > 0.5f);

        var bannedFeatures = new HashSet<string> { "EffectiveDrainRate" };
        if (!anyAmadeoTrials)
            bannedFeatures.Add("factorForce");

        var filteredImportance = predictor.GetFeatureImportance()
            ?.Where(t => !bannedFeatures.Contains(t.feature))
            .ToArray() ?? System.Array.Empty<(string feature, float importance)>();

        return (bannedFeatures, filteredImportance);
    }

    /**
     * Builds array of feature indices for optimization from importance ranking.
     * 
     * Only includes indices < 9 (excludes EffectiveDrainRate at index 9).
     * Fallback: if no indices found, uses [4, 5] (RemoveHealthEveryLifeTime, removeHealthWithCollide).
     * Final fallback: if still empty, uses [0] (speed).
     * 
     * Returns: Array of feature indices to optimize (max 9 features, excluding derived features)
     */
    public static int[] BuildOptimizationIndices(
        (string feature, float importance)[] filteredImportance,
        string[] featureNames)
    {
        var topIndexList = new List<int>();

        foreach (var (feature, _) in filteredImportance)
        {
            int idx = System.Array.IndexOf(featureNames, feature);
            if (idx >= 0 && idx < 9 && !topIndexList.Contains(idx))
                topIndexList.Add(idx);
        }

        if (topIndexList.Count == 0)
        {
            topIndexList.AddRange(new[] { 4, 5 });
        }

        if (topIndexList.Count == 0)
        {
            topIndexList.Add(0);
        }

        return topIndexList.ToArray();
    }

    /**
     * Constrains parameter ranges to observed data with adaptive buffer.
     * 
     * Calculates adaptive buffer based on data variability and distance from target.
     * If deltaFromTarget > threshold (35% for constant, 40% for variable), returns original ranges.
     * Otherwise expands observed min/max by buffer, clamped to original ranges.
     * 
     * Prevents optimization from exploring unrealistic parameter combinations while allowing
     * some extrapolation when target is far from observed data.
     */
    public static TrialDataModels.ParameterRanges ConstrainRangesToObserved(
        TrialDataModels.ParameterRanges originalRanges,
        List<TrialDataModels.TrialData> trials,
        float targetOxygen)
    {
        if (trials == null || trials.Count == 0)
            return originalRanges;

        var constrained = new TrialDataModels.ParameterRanges();

        float observedMean = trials.Average(t => t.finalOxygenRemaining);
        float deltaFromTarget = Mathf.Abs(observedMean - targetOxygen);

        float stdDevO2 = CalculateStdDev(trials, observedMean);
        bool isConstantData = stdDevO2 < 15f;

        float buffer = CalculateBuffer(isConstantData, deltaFromTarget);

       // Threshold for using original ranges for better extrapolation
        float threshold = isConstantData ? 35f : 40f;
        if (deltaFromTarget > threshold)
        {
            Debug.LogWarning($"[Regression] Large gap ({deltaFromTarget:F1}%) using ORIGINAL ranges for better extrapolation");
            return originalRanges;
        }

        ApplyRangeConstraints(constrained, trials, buffer, originalRanges);

        return constrained;
    }

    // this is used to calculate the basic statistics from the trial data.
    // currently calculates the average oxygen level from all trials.
    public static void CalculateTrialStatistics(
        List<TrialDataModels.TrialData> trials,
        TrialDataModels.RegressionResult result)
    {
        result.averageOxygen = trials.Average(t => t.finalOxygenRemaining);
    }

    #endregion

    #region Helper Functions

    // this is used to prepare the parameter ranges and baseline for optimization.
    private static (TrialDataModels.ParameterRanges ranges, TrialDataModels.TrialData baseline)
        PrepareOptimizationBaseline(List<TrialDataModels.TrialData> trials, float targetOxygen)
    {
        var ranges = new TrialDataModels.ParameterRanges();
        ranges = ConstrainRangesToObserved(ranges, trials, targetOxygen);
        var baseline = FeatureExtractor.GetPatientBaseline(trials, ranges, useMedian: true);
        baseline.IsAmadeoMode = 0f;

        // OPTIONAL: Add small noise for optimization variability (currently disabled for deterministic results)
        // Uncomment to enable ±1% random variation on baseline parameters:
        // float noise = 0.01f;
        // baseline.speed += baseline.speed * Random.Range(-noise, noise);
        // baseline.verticalSpeed += baseline.verticalSpeed * Random.Range(-noise, noise);
        // baseline.lifeTime += baseline.lifeTime * Random.Range(-noise, noise);
        // baseline.RemoveHealthEveryLifeTime += baseline.RemoveHealthEveryLifeTime * Random.Range(-noise, noise);
        // baseline.removeHealthWithCollide += baseline.removeHealthWithCollide * Random.Range(-noise, noise);
        // baseline.timeBetweenCollides += baseline.timeBetweenCollides * Random.Range(-noise, noise);
        // baseline.healHealthPoint += baseline.healHealthPoint * Random.Range(-noise, noise);

        return (ranges, baseline);
    }

    // this is used to calculate the error between the predicted and target oxygen.
    private static float CalculateError(OxygenPredictor predictor, TrialDataModels.TrialData solution, float targetOxygen)
    {
        return Mathf.Abs(predictor.PredictOxygen(solution) - targetOxygen);
    }

    private static float CalculateStdDev(List<TrialDataModels.TrialData> trials, float mean)
    {
        if (trials.Count <= 1)
            return 0f;

        float variance = trials.Average(t =>
        {
            float diff = t.finalOxygenRemaining - mean;
            return diff * diff;
        });

        return Mathf.Sqrt(variance);
    }

    private static float CalculateBuffer(bool isConstantData, float deltaFromTarget)
    {
        // Choose buffer calculation based on mode
        if (CurrentBufferMode == BufferMode.Conservative)
        {
            return CalculateBufferConservative(isConstantData, deltaFromTarget);
        }
        else
        {
            return CalculateBufferExpanded(isConstantData, deltaFromTarget);
        }
    }

    /// <summary>
    /// Conservative buffer: 10%-25% - stays closer to observed data.
    /// Best when observed data is representative of target range.
    /// </summary>
    private static float CalculateBufferConservative(bool isConstantData, float deltaFromTarget)
    {
        if (isConstantData)
        {
            if (deltaFromTarget < 20f)
                return 0.10f;
            if (deltaFromTarget < 40f)
                return 0.10f + (deltaFromTarget - 20f) / 20f * 0.15f;  // 10% to 25%
            return 0.25f;
        }
        else
        {
            if (deltaFromTarget < 15f)
                return 0.15f;
            if (deltaFromTarget < 40f)
                return 0.15f + (deltaFromTarget - 15f) / 25f * 0.10f;  // 15% to 25%
            return 0.25f;
        }
    }

    /// <summary>
    /// Expanded buffer: 25%-50% - allows more extrapolation.
    /// Best for extreme targets (10%, 90%) far from observed data.
    /// </summary>
    private static float CalculateBufferExpanded(bool isConstantData, float deltaFromTarget)
    {
        if (isConstantData)
        {
            if (deltaFromTarget < 20f)
                return 0.25f;
            if (deltaFromTarget < 40f)
                return 0.25f + (deltaFromTarget - 20f) / 20f * 0.15f;  // 25% to 40%
            return 0.40f;
        }
        else
        {
            if (deltaFromTarget < 15f)
                return 0.25f;
            if (deltaFromTarget < 40f)
                return 0.25f + (deltaFromTarget - 15f) / 25f * 0.25f;  // 25% to 50%
            return 0.50f;
        }
    }

    // this is used to apply the range constraints to all 9 parameters. 
    // the buffer is the percentage of the range that is allowed to be expanded.
    private static void ApplyRangeConstraints(
        TrialDataModels.ParameterRanges constrained,
        List<TrialDataModels.TrialData> trials,
        float buffer,
        TrialDataModels.ParameterRanges originalRanges)
    {
        constrained.speedRange = ExpandRange(
            trials.Min(t => t.speed), trials.Max(t => t.speed), buffer, originalRanges.speedRange);
        constrained.verticalSpeedRange = ExpandRange(
            trials.Min(t => t.verticalSpeed), trials.Max(t => t.verticalSpeed), buffer, originalRanges.verticalSpeedRange);
        constrained.idleUpwardSpeedRange = ExpandRange(
            trials.Min(t => t.idleUpwardSpeed), trials.Max(t => t.idleUpwardSpeed), buffer, originalRanges.idleUpwardSpeedRange);
        constrained.lifeTimeRange = ExpandRange(
            trials.Min(t => t.lifeTime), trials.Max(t => t.lifeTime), buffer, originalRanges.lifeTimeRange);
        constrained.RemoveHealthEveryLifeTimeRange = ExpandRange(
            trials.Min(t => t.RemoveHealthEveryLifeTime), trials.Max(t => t.RemoveHealthEveryLifeTime), buffer, originalRanges.RemoveHealthEveryLifeTimeRange);
        constrained.removeHealthWithCollideRange = ExpandRange(
            trials.Min(t => t.removeHealthWithCollide), trials.Max(t => t.removeHealthWithCollide), buffer, originalRanges.removeHealthWithCollideRange);
        constrained.timeBetweenCollidesRange = ExpandRange(
            trials.Min(t => t.timeBetweenCollides), trials.Max(t => t.timeBetweenCollides), buffer, originalRanges.timeBetweenCollidesRange);
        constrained.healHealthPointRange = ExpandRange(
            trials.Min(t => t.healHealthPoint), trials.Max(t => t.healHealthPoint), buffer, originalRanges.healHealthPointRange);
        constrained.factorForceRange = ExpandRange(
            trials.Min(t => t.factorForce), trials.Max(t => t.factorForce), buffer, originalRanges.factorForceRange);
    }

    // this is used to expand the observed range by the buffer percentage, clamped to the original range.
    // if the observed range is very small (<0.01), uses a fixed buffer of 0.5.
    // otherwise, expands by bufferPercent of the range.
    private static Vector2 ExpandRange(float observedMin, float observedMax, float bufferPercent, Vector2 originalRange)
    {
        float range = observedMax - observedMin;
        float buffer = range < 0.01f ? 0.5f : range * bufferPercent;

        return new Vector2(
            Mathf.Max(originalRange.x, observedMin - buffer),
            Mathf.Min(originalRange.y, observedMax + buffer)
        );
    }

    #endregion
}
