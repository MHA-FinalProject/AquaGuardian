using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

/**
 * Feature extraction for regression analysis
 * 
 * NOTE: FeatureNames are defined in TrialDataModels.FeatureNames (single source of truth)
 * If adding/removing features, update:
 * 1. TrialDataModels.TrialData (field definition)
 * 2. TrialDataModels.FeatureNames (this array - single source of truth)
 * 3. ParameterHelper.Get/Set/Range (switch cases)
 * 
 * INPUT MODE ADJUSTMENTS:
 *  - Amadeo: idleUpwardSpeed *= 0.5 (weaker drift), factorForce active
 *  - Keyboard: factorForce = 0 (no force sensitivity)
 */
public static class FeatureExtractor
{
    // Use FeatureNames from TrialDataModels (single source of truth)
    // Delegates to TrialDataModels to avoid duplication
    public static string[] FeatureNames => TrialDataModels.FeatureNames;
    public static int FeatureCount => TrialDataModels.FeatureCount;

    // Extract features from single trial
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

    // Extract features from multiple trials
    public static float[][] ExtractFeatures(List<TrialDataModels.TrialData> trials)
    {
        float[][] X = new float[trials.Count][];
        for (int i = 0; i < trials.Count; i++)
            X[i] = ExtractFeatures(trials[i]);
        return X;
    }

    // Extract features and targets
    public static (float[][] X, float[] y) ExtractFeaturesAndTargets(List<TrialDataModels.TrialData> trials)
    {
        float[][] X = ExtractFeatures(trials);
        float[] y = trials.Select(t => t.finalOxygenRemaining).ToArray();
        return (X, y);
    }

    // Get personalized baseline from patient history (median by default, falls back to mid-range if <3 trials)
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

    // Get mid-range defaults from parameter ranges
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

    // Compute median of values
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
