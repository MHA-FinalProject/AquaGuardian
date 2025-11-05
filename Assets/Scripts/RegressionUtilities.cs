using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/**
 * Helper methods for TrialRegressionAlgorithm
 * Contains statistics calculation, cross-validation, feature preparation, and optimization logic
 * 
 * See also: TrialRegressionAlgorithm, OxygenPredictor, MultipleLinearRegression, DifficultyParameterSolver, FeatureExtractor
 */
public static class RegressionUtilities
{
    private const float TARGET_TOLERANCE = 5f;
    private const float RANDOM_SWEEP_THRESHOLD = 5.0f;

    public static void CalculateTrialStatistics(
        List<TrialDataModels.TrialData> trials,
        TrialDataModels.RegressionResult result)
    {
        result.averageOxygen = trials.Average(t => t.finalOxygenRemaining);
    }

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
     * Main optimization function that coordinates the entire optimization pipeline
     * 
     * IMPORTANCE: Primary entry point for parameter optimization in @TrialRegressionAlgorithm
     * strategy: SolveForTargetOxygen (primary) then RandomSweepOptimizer (fallback)
     *   then SolveForTargetDifficultyMulti (final fallback) and return the best solution
     * if the error is still too high, return the best solution
     * ensures robust optimization even if primary method fails
    */

    public static (TrialDataModels.TrialData solution, float error) OptimizeParameters(
        MultipleLinearRegression model, OxygenPredictor predictor, List<TrialDataModels.TrialData> trials, (string feature, float importance)[] filteredImportance, string[] featureNames, float targetOxygen, bool anyAmadeoTrials)
    {
        try
        {
            int[] topIndices = BuildOptimizationIndices(filteredImportance, featureNames);
            var (ranges, baseline) = PrepareOptimizationBaseline(trials, targetOxygen);

            var solution = DifficultyParameterSolver.SolveForTargetOxygen(
                model, baseline, topIndices, targetOxygen, ranges,
                d => predictor.PredictOxygenUnclamped(d),
                out float error);

            if (solution == null || float.IsNaN(error) || error > RANDOM_SWEEP_THRESHOLD)
            {
                var (sweepCandidate, sweepError) = DifficultyParameterSolver.RandomSweepOptimizer(
                    d => predictor.PredictOxygenUnclamped(d),
                    ranges, targetOxygen, anyAmadeoTrials, samples: 150);

                if (sweepCandidate != null && sweepError < (float.IsNaN(error) ? float.MaxValue : error))
                {
                    solution = sweepCandidate;
                    error = sweepError;
                }
            }

            if (solution == null || float.IsNaN(error) || error > 5f)
            {
                solution = DifficultyParameterSolver.SolveForTargetDifficultyMulti(
                    model, targetOxygen, topIndices.Length, baseline,
                    ranges, featureNames, trials);

                if (solution != null)
                    error = CalculateError(predictor, solution, targetOxygen);
            }

            return (solution, error);
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

            return (solution, error);
        }
    }


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

        if (!isConstantData && deltaFromTarget > 60f)
        {
            Debug.LogWarning($"[Regression] EXTREME gap ({deltaFromTarget:F1}%) using ORIGINAL ranges");
            return originalRanges;
        }

        ApplyRangeConstraints(constrained, trials, buffer, originalRanges);

        return constrained;
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
        if (isConstantData)
        {
            if (deltaFromTarget < 20f)
                return 0.10f;
            if (deltaFromTarget < 40f)
                return 0.10f + (deltaFromTarget - 20f) / 20f * 0.15f;
            return 0.25f;
        }
        else
        {
            if (deltaFromTarget < 15f)
                return 0.15f;
            if (deltaFromTarget < 40f)
                return 0.15f + (deltaFromTarget - 15f) / 25f * 0.35f;
            return 0.50f + Mathf.Min((deltaFromTarget - 40f) / 60f * 0.50f, 0.50f);
        }
    }

    private static Vector2 ExpandRange(float observedMin, float observedMax, float bufferPercent, Vector2 originalRange)
    {
        float range = observedMax - observedMin;
        float buffer = range < 0.01f ? 0.5f : range * bufferPercent;

        return new Vector2(
            Mathf.Max(originalRange.x, observedMin - buffer),
            Mathf.Min(originalRange.y, observedMax + buffer)
        );
    }

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

    private static (TrialDataModels.ParameterRanges ranges, TrialDataModels.TrialData baseline)
        PrepareOptimizationBaseline(List<TrialDataModels.TrialData> trials, float targetOxygen)
    {
        var ranges = new TrialDataModels.ParameterRanges();
        ranges = ConstrainRangesToObserved(ranges, trials, targetOxygen);
        var baseline = FeatureExtractor.GetPatientBaseline(trials, ranges, useMedian: true);
        baseline.IsAmadeoMode = 0f;
        return (ranges, baseline);
    }

    private static float CalculateError(OxygenPredictor predictor, TrialDataModels.TrialData solution, float targetOxygen)
    {
        return Mathf.Abs(predictor.PredictOxygen(solution) - targetOxygen);
    }
}
