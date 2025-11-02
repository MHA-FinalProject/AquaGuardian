using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Centralized feature extraction logic for regression analysis
/// Eliminates code duplication across OxygenPredictor, DifficultyParameterSolver, and TrialRegressionAlgorithm
/// </summary>
public static class FeatureExtractor
{
    #region Feature Names - Single Source of Truth

    /// <summary>
    /// Always 10 effective features (calculated per-sample based on IsAmadeoMode)
    /// This is the ONLY place where feature names are defined
    /// </summary>
    public static readonly string[] FeatureNames = new string[]
    {
        "speed",
        "verticalSpeed",
        "idleUpwardSpeed",
        "lifeTime",
        "RemoveHealthEveryLifeTime",
        "removeHealthWithCollide",
        "timeBetweenCollides",
        "healHealthPoint",
        "factorForce",           // Effective factor force (0 if keyboard, factorForce if Amadeo)
        "EffectiveDrainRate"     // Derived feature (RemoveHealthEveryLifeTime / lifeTime)
    };

    public static int FeatureCount => FeatureNames.Length;

    #endregion

    #region Feature Extraction - Single Implementation

    /// <summary>
    /// Extract features from a single trial
    /// Used by: OxygenPredictor.PredictOxygen, DifficultyParameterSolver
    /// </summary>
    public static float[] ExtractFeatures(TrialDataModels.TrialData trial)
    {
        // Calculate effective values based on IsAmadeoMode
        bool isAmadeo = trial.IsAmadeoMode > 0.5f;
        
        // Take original values
        float vSpd = trial.verticalSpeed;
        float iuSpd = trial.idleUpwardSpeed;
        float fForce = trial.factorForce;
        
        // Adjust values based on input mode (but keep same feature names)
        // idleUpwardSpeed: In Amadeo it's added only when moving up (~50% effect), in Keyboard always added
        if (isAmadeo)
        {
            iuSpd = trial.idleUpwardSpeed * 0.5f;
        }
        
        // factorForce: disable in keyboard mode
        if (!isAmadeo)
        {
            fForce = 0f;
        }
        
        // Always 10 features with same names, but adjusted values
        return new float[]
        {
            trial.speed,
            vSpd,                           // verticalSpeed (adjusted)
            iuSpd,                          // idleUpwardSpeed (adjusted)
            trial.lifeTime,
            trial.RemoveHealthEveryLifeTime,
            trial.removeHealthWithCollide,
            trial.timeBetweenCollides,
            trial.healHealthPoint,
            fForce,                         // factorForce (0 if keyboard)
            trial.EffectiveDrainRate
        };
    }

    /// <summary>
    /// Extract features from multiple trials (builds feature matrix)
    /// Used by: OxygenPredictor.TrainModel, TrialRegressionAlgorithm.BuildFeatureMatrix
    /// </summary>
    public static float[][] ExtractFeatures(List<TrialDataModels.TrialData> trials)
    {
        float[][] X = new float[trials.Count][];
        
        for (int i = 0; i < trials.Count; i++)
        {
            X[i] = ExtractFeatures(trials[i]);
        }
        
        return X;
    }

    /// <summary>
    /// Extract features AND targets (convenience method)
    /// Used by: K-Fold CV, regression training
    /// </summary>
    public static (float[][] X, float[] y) ExtractFeaturesAndTargets(List<TrialDataModels.TrialData> trials)
    {
        float[][] X = ExtractFeatures(trials);
        float[] y = trials.Select(t => t.finalOxygenRemaining).ToArray();
        return (X, y);
    }

    #endregion

    #region Trial Statistics - Centralized Calculations

    /// <summary>
    /// Get personalized starting point based on patient's trial statistics
    /// Uses MEDIAN for robustness (less sensitive to outliers than average)
    /// </summary>
    public static TrialDataModels.TrialData GetPatientBaseline(
        List<TrialDataModels.TrialData> trials,
        TrialDataModels.ParameterRanges ranges,
        bool useMedian = true)
    {
        // Need at least 3 trials for reliable statistics
        if (trials == null || trials.Count < 3)
        {
            Debug.Log("[FeatureExtractor] Insufficient trials for personalized baseline, using mid-range defaults");
            return GetMidRangeDefaults(ranges);
        }

        // Calculate statistics (median or average)
        System.Func<System.Collections.Generic.IEnumerable<float>, float> statFunc;
        if (useMedian)
        {
            statFunc = values => {
                var sorted = values.OrderBy(v => v).ToList();
                int count = sorted.Count;
                if (count == 0) return 0f;
                if (count % 2 == 1)
                    return sorted[count / 2];
                else
                    return (sorted[count / 2 - 1] + sorted[count / 2]) / 2f;
            };
            Debug.Log($"[FeatureExtractor] Using MEDIAN baseline from {trials.Count} trials");
        }
        else
        {
            statFunc = values => values.Average();
            Debug.Log($"[FeatureExtractor] Using AVERAGE baseline from {trials.Count} trials");
        }

        // Check if any trials use Amadeo
        bool anyAmadeo = trials.Any(t => t.IsAmadeoMode > 0.5f);

        return new TrialDataModels.TrialData
        {
            speed = statFunc(trials.Select(t => t.speed)),
            verticalSpeed = statFunc(trials.Select(t => t.verticalSpeed)),
            idleUpwardSpeed = statFunc(trials.Select(t => t.idleUpwardSpeed)),
            lifeTime = statFunc(trials.Select(t => t.lifeTime)),
            RemoveHealthEveryLifeTime = statFunc(trials.Select(t => t.RemoveHealthEveryLifeTime)),
            removeHealthWithCollide = statFunc(trials.Select(t => t.removeHealthWithCollide)),
            timeBetweenCollides = statFunc(trials.Select(t => t.timeBetweenCollides)),
            healHealthPoint = statFunc(trials.Select(t => t.healHealthPoint)),
            factorForce = anyAmadeo 
                ? statFunc(trials.Where(t => t.IsAmadeoMode > 0.5f).Select(t => t.factorForce))
                : 0f,
            IsAmadeoMode = trials.Last().IsAmadeoMode  // Use mode from most recent trial
        };
    }

    /// <summary>
    /// Get mid-range defaults (fallback when insufficient data)
    /// </summary>
    public static TrialDataModels.TrialData GetMidRangeDefaults(TrialDataModels.ParameterRanges ranges)
    {
        return new TrialDataModels.TrialData
        {
            speed = (ranges.speedRange.x + ranges.speedRange.y) / 2f,
            verticalSpeed = (ranges.verticalSpeedRange.x + ranges.verticalSpeedRange.y) / 2f,
            idleUpwardSpeed = (ranges.idleUpwardSpeedRange.x + ranges.idleUpwardSpeedRange.y) / 2f,
            lifeTime = (ranges.lifeTimeRange.x + ranges.lifeTimeRange.y) / 2f,
            RemoveHealthEveryLifeTime = (ranges.RemoveHealthEveryLifeTimeRange.x + ranges.RemoveHealthEveryLifeTimeRange.y) / 2f,
            removeHealthWithCollide = (ranges.removeHealthWithCollideRange.x + ranges.removeHealthWithCollideRange.y) / 2f,
            timeBetweenCollides = (ranges.timeBetweenCollidesRange.x + ranges.timeBetweenCollidesRange.y) / 2f,
            healHealthPoint = (ranges.healHealthPointRange.x + ranges.healHealthPointRange.y) / 2f,
            factorForce = (ranges.factorForceRange.x + ranges.factorForceRange.y) / 2f,
            IsAmadeoMode = 0f
        };
    }

    #endregion
}

