using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

/**
 * Feature extraction utility for regression analysis.
 * 
 * Converts TrialDataModels.TrialData into 10-feature vectors for ML training/prediction.
 * Handles input mode adjustments: Amadeo (idleUpwardSpeed *= 0.5, factorForce active) vs Keyboard (factorForce = 0).
 * Calculates derived feature: EffectiveDrainRate = RemoveHealthEveryLifeTime / lifeTime.
 * 
 * NOTE: FeatureNames defined in TrialDataModels.FeatureNames (single source of truth).
 */
public static class FeatureExtractor
{
    // Gets feature names array from TrialDataModels (10 features: speed, verticalSpeed, idleUpwardSpeed, lifeTime, RemoveHealthEveryLifeTime, removeHealthWithCollide, timeBetweenCollides, healHealthPoint, factorForce, EffectiveDrainRate)
    public static string[] FeatureNames => TrialDataModels.FeatureNames;

    // Gets total feature count (always 10)
    public static int FeatureCount => TrialDataModels.FeatureCount;

    // Extracts feature vector from a single trial. Adjusts idleUpwardSpeed (Amadeo *= 0.5) and factorForce (Keyboard = 0), calculates EffectiveDrainRate. Returns array of 10 features matching FeatureNames order.
    //  Called from: OxygenPredictor, DifficultyParameterSolver, PythonRegressionModel, TrialReportGenerator
    public static float[] ExtractFeatures(TrialDataModels.TrialData trial)
    {
        bool isAmadeo = trial.IsAmadeoMode > 0.5f;
        float idleUpSpeed = isAmadeo ? trial.idleUpwardSpeed * 0.5f : trial.idleUpwardSpeed;
        float factorForce = isAmadeo ? trial.factorForce : 0f;

        return new float[]
        {
            trial.speed,
            trial.verticalSpeed,
            idleUpSpeed,
            trial.lifeTime,
            trial.RemoveHealthEveryLifeTime,
            trial.removeHealthWithCollide,
            trial.timeBetweenCollides,
            trial.healHealthPoint,
            factorForce,
            trial.EffectiveDrainRate
        };
    }

    // Extracts feature matrix from multiple trials (2D array: [trial_index][feature_index]). 
    // Called from: OxygenPredictor.TrainModel, RegressionUtilities.PerformCrossValidationAndErrorCalculation
    // Returns: 2D array: [trial_index][feature_index] = feature_value
    public static float[][] ExtractFeatures(List<TrialDataModels.TrialData> trials)
    {
        float[][] X = new float[trials.Count][];
        for (int i = 0; i < trials.Count; i++)
            X[i] = ExtractFeatures(trials[i]);
        return X;
    }

    // Extracts feature matrix (X) and target vector (y) from trials. Called from: OxygenPredictor.TrainModel, RegressionUtilities.PerformCrossValidationAndErrorCalculation
    public static (float[][] X, float[] y) ExtractFeaturesAndTargets(List<TrialDataModels.TrialData> trials)
    {
        float[][] X = ExtractFeatures(trials);
        float[] y = trials.Select(t => t.finalOxygenRemaining).ToArray();
        return (X, y);
    }

    /**
     * Calculates personalized baseline from patient trial history.
     * 
     * If < 3 trials: returns mid-range defaults. If >= 3: calculates median (default) or mean per parameter.
     * For factorForce: only includes Amadeo trials if any exist, otherwise 0.
     * Used as optimization starting point to personalize solutions to patient's historical ranges.
     * 
     * Called from: RegressionUtilities.PrepareOptimizationBaseline, PythonRegressionHandler
     */
    public static TrialDataModels.TrialData GetPatientBaseline(
        List<TrialDataModels.TrialData> trials,
        TrialDataModels.ParameterRanges ranges,
        bool useMedian = true)
    {
        if (trials == null || trials.Count < 3)
            return GetMidRangeDefaults(ranges);

        Func<IEnumerable<float>, float> stat = useMedian ? ComputeMedian : values => values.Average();
        bool hasAmadeo = trials.Any(t => t.IsAmadeoMode > 0.5f);

        return new TrialDataModels.TrialData
        {
            speed                    = stat(trials.Select(t => t.speed)),
            verticalSpeed            = stat(trials.Select(t => t.verticalSpeed)),
            idleUpwardSpeed          = stat(trials.Select(t => t.idleUpwardSpeed)),
            lifeTime                 = stat(trials.Select(t => t.lifeTime)),
            RemoveHealthEveryLifeTime= stat(trials.Select(t => t.RemoveHealthEveryLifeTime)),
            removeHealthWithCollide  = stat(trials.Select(t => t.removeHealthWithCollide)),
            timeBetweenCollides      = stat(trials.Select(t => t.timeBetweenCollides)),
            healHealthPoint          = stat(trials.Select(t => t.healHealthPoint)),
            factorForce              = hasAmadeo 
                ? stat(trials.Where(t => t.IsAmadeoMode > 0.5f).Select(t => t.factorForce))
                : 0f,
            IsAmadeoMode             = trials.Last().IsAmadeoMode
        };
    }

    // Calculates mid-range defaults (min+max)/2 for each parameter. Used as fallback when patient has < 3 trials
    public static TrialDataModels.TrialData GetMidRangeDefaults(TrialDataModels.ParameterRanges ranges)
    {
        float GetMid(Vector2 range) => (range.x + range.y) * 0.5f;

        return new TrialDataModels.TrialData
        {
            speed                    = GetMid(ranges.speedRange),
            verticalSpeed            = GetMid(ranges.verticalSpeedRange),
            idleUpwardSpeed          = GetMid(ranges.idleUpwardSpeedRange),
            lifeTime                 = GetMid(ranges.lifeTimeRange),
            RemoveHealthEveryLifeTime= GetMid(ranges.RemoveHealthEveryLifeTimeRange),
            removeHealthWithCollide  = GetMid(ranges.removeHealthWithCollideRange),
            timeBetweenCollides      = GetMid(ranges.timeBetweenCollidesRange),
            healHealthPoint          = GetMid(ranges.healHealthPointRange),
            factorForce              = GetMid(ranges.factorForceRange),
            IsAmadeoMode             = 0f
        };
    }

    // Computes median value (robust to outliers). If even count, returns average of two middle values
    private static float ComputeMedian(IEnumerable<float> values)
    {
        var list = values as IList<float> ?? values.ToList();
        if (list.Count == 0) return 0f;

        var sorted = list.OrderBy(v => v).ToList();
        int n = sorted.Count;

        return (n % 2 == 1) 
            ? sorted[n / 2] 
            : 0.5f * (sorted[n / 2 - 1] + sorted[n / 2]);
    }
}
