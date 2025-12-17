using UnityEngine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

/**
 * Generates optimized parameters for multiple target oxygen levels (10%-90%).
 * 
 * Creates a lookup table of game parameters for different difficulty levels.
 * Each row represents optimal parameters to achieve a specific oxygen target.
 * 
 * Usage:
 *   string report = MultiTargetOptimizer.RunMultiTargetAnalysis(trials);
 *   // Results saved to Assets/Data/MultiTargets/target.csv
 * 
 * See also: DifficultyParameterSolver, RegressionUtilities, OxygenPredictor
 */
public static class MultiTargetOptimizer
{
    private static readonly CultureInfo CI = CultureInfo.InvariantCulture;

    // Default target file path
    private static readonly string MULTI_TARGETS_FOLDER = Path.Combine(Application.dataPath, "Data", "MultiTargets");
    private static readonly string TARGET_CSV_PATH = Path.Combine(MULTI_TARGETS_FOLDER, "target.csv");

    /// <summary>
    /// Result for a single target oxygen optimization
    /// </summary>
    [Serializable]
    public class TargetResult
    {
        public float targetOxygen;
        public float predictedOxygen;
        public float error;
        public string method;
        public TrialDataModels.TrialData parameters;
    }

    /// <summary>
    /// Complete multi-target optimization results
    /// </summary>
    [Serializable]
    public class MultiTargetResult
    {
        public List<TargetResult> results = new List<TargetResult>();
        public DateTime timestamp;
        public int trialCount;
        public float modelR2;
        public float modelRMSE;
        public string inputType;
        public string modelSource = "Unity Model";  // "Unity Model" or "Python Model"
    }

    /// <summary>
    /// Default target values: 10% to 90% in steps of 10%
    /// </summary>
    public static readonly float[] DefaultTargets = { 10f, 20f, 30f, 40f, 50f, 60f, 70f, 80f, 90f };

    #region Main API

    /// <summary>
    /// Main entry point - runs multi-target analysis for targets 10%-90%.
    /// Saves results to Assets/Data/MultiTargets/target.csv
    /// </summary>
    public static string RunMultiTargetAnalysis(List<TrialDataModels.TrialData> trials)
    {
        if (trials == null || trials.Count < 3)
        {
            return $"ERROR: Need at least 3 trials for analysis. Found: {trials?.Count ?? 0}";
        }

        try
        {
            // Train model
            int featureCount = FeatureExtractor.FeatureCount;
            var predictor = new OxygenPredictor { maxFeaturesForTraining = featureCount };
            bool trained = predictor.TrainModel(trials, enableFeatureSelection: false);

            if (!trained)
            {
                return "ERROR: Failed to train regression model. Not enough variance in data.";
            }

            var model = predictor.GetModel();

            // Optimize for all targets
            var results = OptimizeForAllTargets(trials, predictor, model);

            if (results == null)
            {
                return "ERROR: Optimization failed.";
            }

            // Save to target.csv
            SaveToTargetCSV(results);
            
            // Save CSV report (Excel compatible)
            string timestamp = results.timestamp.ToString("yyyy-MM-dd_HH-mm-ss");
            SaveReportCSV(results, $"MultiTarget_Report_{timestamp}.csv");

            // Return text report for UI display
            return GenerateReport(results);
        }
        catch (Exception e)
        {
            return $"ERROR: {e.Message}\n{e.StackTrace}";
        }
    }

    /// <summary>
    /// Optimizes for all target oxygen levels (10%-90%).
    /// </summary>
    public static MultiTargetResult OptimizeForAllTargets(
        List<TrialDataModels.TrialData> trials,
        OxygenPredictor predictor,
        MultipleLinearRegression model,
        float[] customTargets = null)
    {
        if (trials == null || trials.Count < 3)
        {
            Debug.LogError("[MultiTargetOptimizer] Need at least 3 trials");
            return null;
        }

        if (predictor == null || model == null)
        {
            Debug.LogError("[MultiTargetOptimizer] Predictor and model are required");
            return null;
        }

        float[] targets = customTargets ?? DefaultTargets;
        var result = new MultiTargetResult
        {
            timestamp = DateTime.Now,
            trialCount = trials.Count,
            modelR2 = model.rSquared,
            modelRMSE = model.rootMeanSquaredError,
            inputType = trials.Any(t => t.IsAmadeoMode > 0.5f) ? "amadeo" : "keyboard"
        };

        // Prepare optimization features
        var (bannedFeatures, filteredImportance) = RegressionUtilities.PrepareOptimizationFeatures(trials, predictor);
        bool anyAmadeoTrials = trials.Any(t => t.IsAmadeoMode > 0.5f);
        string[] featureNames = FeatureExtractor.FeatureNames;

        Debug.Log($"[MultiTargetOptimizer] Starting optimization for {targets.Length} targets...");

        foreach (float targetO2 in targets)
        {
            try
            {
                // Optimize for this target
                var (solution, error, selectedMethod) = RegressionUtilities.OptimizeParameters(
                    model, predictor, trials, filteredImportance,
                    featureNames, targetO2, anyAmadeoTrials);

                float predicted = solution != null ? predictor.PredictOxygen(solution) : float.NaN;

                var targetResult = new TargetResult
                {
                    targetOxygen = targetO2,
                    predictedOxygen = predicted,
                    error = error,
                    method = selectedMethod,
                    parameters = solution
                };

                result.results.Add(targetResult);
                Debug.Log($"[MultiTarget] Target {targetO2}%: Predicted={predicted:F2}%, Error={error:F2}%");
            }
            catch (Exception e)
            {
                Debug.LogError($"[MultiTarget] Failed for target {targetO2}%: {e.Message}");
                result.results.Add(new TargetResult
                {
                    targetOxygen = targetO2,
                    predictedOxygen = float.NaN,
                    error = float.NaN,
                    method = "FAILED",
                    parameters = null
                });
            }
        }

        Debug.Log($"[MultiTargetOptimizer] Completed {result.results.Count} optimizations");
        return result;
    }

    #endregion

    #region Save Methods

    /// <summary>
    /// Saves results to Assets/Data/MultiTargets/target.csv
    /// </summary>
    public static bool SaveToTargetCSV(MultiTargetResult results)
    {
        if (results == null || results.results.Count == 0)
        {
            Debug.LogError("[MultiTargetOptimizer] No results to save");
            return false;
        }

        try
        {
            if (!Directory.Exists(MULTI_TARGETS_FOLDER))
            {
                Directory.CreateDirectory(MULTI_TARGETS_FOLDER);
            }

            var sb = new StringBuilder();
            sb.AppendLine("oygenTarget,predicted_oygen,error,speed,verticalSpeed,idleUpwardSpeed,lifeTime,RemoveHealthEveryLifeTime,removeHealthWithCollide,timeBetweenCollides,healHealthPoint,factorForce");

            foreach (var r in results.results)
            {
                var p = r.parameters;
                if (p == null)
                {
                    sb.AppendLine($"{r.targetOxygen.ToString("F0", CI)}%,,,,,,,,,,,");
                    continue;
                }

                bool isAmadeo = p.IsAmadeoMode > 0.5f;
                float factorForceEff = isAmadeo ? p.factorForce : 0f;

                sb.AppendLine(string.Join(",",
                    $"{r.targetOxygen.ToString("F0", CI)}%",
                    r.predictedOxygen.ToString("F2", CI),
                    r.error.ToString("F3", CI),
                    p.speed.ToString("F3", CI),
                    p.verticalSpeed.ToString("F3", CI),
                    p.idleUpwardSpeed.ToString("F3", CI),
                    p.lifeTime.ToString("F3", CI),
                    p.RemoveHealthEveryLifeTime.ToString("F3", CI),
                    p.removeHealthWithCollide.ToString("F3", CI),
                    p.timeBetweenCollides.ToString("F3", CI),
                    p.healHealthPoint.ToString("F3", CI),
                    factorForceEff.ToString("F3", CI)
                ));
            }

            File.WriteAllText(TARGET_CSV_PATH, sb.ToString());
            Debug.Log($"[MultiTargetOptimizer] Saved to: {TARGET_CSV_PATH}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[MultiTargetOptimizer] Failed to save: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Saves detailed report as CSV (Excel compatible)
    /// </summary>
    public static bool SaveReportCSV(MultiTargetResult results, string fileName)
    {
        try
        {
            if (!Directory.Exists(MULTI_TARGETS_FOLDER))
            {
                Directory.CreateDirectory(MULTI_TARGETS_FOLDER);
            }

            string fullPath = Path.Combine(MULTI_TARGETS_FOLDER, fileName);
            var sb = new StringBuilder();

            // Metadata
            sb.AppendLine($"Report Type,MULTI-TARGET ANALYSIS");
            sb.AppendLine($"Generated,{results.timestamp:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Trials,{results.trialCount}");
            sb.AppendLine($"Model R^2,{results.modelR2.ToString("F4", CI)}");
            sb.AppendLine($"Model RMSE,{results.modelRMSE.ToString("F2", CI)}%");
            sb.AppendLine($"Input Type,{results.inputType}");
            sb.AppendLine();

            // Data
            sb.AppendLine("Target,Predicted,Error,Speed,VerticalSpeed,IdleUpwardSpeed,LifeTime,Drain,Collide,TimeBetweenCollides,Heal,FactorForce,EffectiveDrainRate");

            foreach (var r in results.results)
            {
                var p = r.parameters;
                if (p == null)
                {
                    sb.AppendLine($"{r.targetOxygen.ToString("F0", CI)}%,FAILED,,,,,,,,,,,");
                    continue;
                }

                bool isAmadeo = p.IsAmadeoMode > 0.5f;
                float factorForceEff = isAmadeo ? p.factorForce : 0f;
                float effectiveDrain = p.RemoveHealthEveryLifeTime / Mathf.Max(0.1f, p.lifeTime);

                sb.AppendLine(string.Join(",",
                    $"{r.targetOxygen.ToString("F0", CI)}%",
                    $"{r.predictedOxygen.ToString("F2", CI)}%",
                    $"{r.error.ToString("F3", CI)}%",
                    p.speed.ToString("F3", CI),
                    p.verticalSpeed.ToString("F3", CI),
                    p.idleUpwardSpeed.ToString("F3", CI),
                    p.lifeTime.ToString("F3", CI),
                    p.RemoveHealthEveryLifeTime.ToString("F3", CI),
                    p.removeHealthWithCollide.ToString("F3", CI),
                    p.timeBetweenCollides.ToString("F3", CI),
                    p.healHealthPoint.ToString("F3", CI),
                    factorForceEff.ToString("F3", CI),
                    effectiveDrain.ToString("F3", CI)
                ));
            }

            // Summary
            sb.AppendLine();
            var validResults = results.results.Where(r => r.parameters != null && !float.IsNaN(r.error)).ToList();
            if (validResults.Count > 0)
            {
                float avgError = validResults.Average(r => r.error);
                float maxError = validResults.Max(r => r.error);
                float minError = validResults.Min(r => r.error);
                
                sb.AppendLine("Summary");
                sb.AppendLine($"Successful,{validResults.Count}/{results.results.Count}");
                sb.AppendLine($"Avg Error,{avgError.ToString("F2", CI)}%");
                sb.AppendLine($"Min Error,{minError.ToString("F2", CI)}%");
                sb.AppendLine($"Max Error,{maxError.ToString("F2", CI)}%");
            }

            File.WriteAllText(fullPath, sb.ToString());
            Debug.Log($"[MultiTargetOptimizer] Saved report to: {fullPath}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[MultiTargetOptimizer] Failed to save report: {e.Message}");
            return false;
        }
    }

    #endregion

    #region Report Generation

    /// <summary>
    /// Generates text report for UI display
    /// </summary>
    public static string GenerateReport(MultiTargetResult results)
    {
        if (results == null || results.results.Count == 0)
            return "No results available";

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════════════════════════════════════════");
        sb.AppendLine("                    MULTI-TARGET ANALYSIS");
        sb.AppendLine("═══════════════════════════════════════════════════════════════════════");
        sb.AppendLine($"Generated: {results.timestamp:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Trials: {results.trialCount} | R^2: {results.modelR2:F3} | RMSE: {results.modelRMSE:F2}% | Input: {results.inputType}");
        sb.AppendLine("───────────────────────────────────────────────────────────────────────");
        sb.AppendLine();

        // Table header
        sb.AppendLine(string.Format("{0,-8} {1,-10} {2,-8} {3,-8} {4,-8} {5,-8} {6,-8} {7,-8} {8,-8}",
            "Target", "Predicted", "Error", "Speed", "vSpeed", "life", "drain", "collide", "heal"));
        sb.AppendLine("───────────────────────────────────────────────────────────────────────");

        foreach (var r in results.results)
        {
            if (r.parameters == null)
            {
                sb.AppendLine(string.Format("{0,-8:F0}% {1,-10} {2,-8}",
                    r.targetOxygen, "FAILED", "-"));
                continue;
            }

            var p = r.parameters;
            sb.AppendLine(string.Format("{0,-8:F0}% {1,-10:F1}% {2,-8:F2}% {3,-8:F2} {4,-8:F2} {5,-8:F2} {6,-8:F2} {7,-8:F2} {8,-8:F2}",
                r.targetOxygen, r.predictedOxygen, r.error,
                p.speed, p.verticalSpeed, p.lifeTime, p.RemoveHealthEveryLifeTime, p.removeHealthWithCollide, p.healHealthPoint));
        }

        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════════════════════════");

        // Summary
        var validResults = results.results.Where(r => r.parameters != null && !float.IsNaN(r.error)).ToList();
        if (validResults.Count > 0)
        {
            float avgError = validResults.Average(r => r.error);
            float maxError = validResults.Max(r => r.error);
            float minError = validResults.Min(r => r.error);
            
            sb.AppendLine($"Summary: {validResults.Count}/{results.results.Count} successful");
            sb.AppendLine($"Error: Avg={avgError:F2}% | Min={minError:F2}% | Max={maxError:F2}%");
        }

        return sb.ToString();
    }

    #endregion

    #region Load Methods

    /// <summary>
    /// Loads target parameters from target.csv
    /// </summary>
    public static List<TargetResult> LoadFromTargetCSV()
    {
        var results = new List<TargetResult>();

        if (!File.Exists(TARGET_CSV_PATH))
        {
            Debug.LogWarning($"[MultiTargetOptimizer] target.csv not found at: {TARGET_CSV_PATH}");
            return results;
        }

        try
        {
            var lines = File.ReadAllLines(TARGET_CSV_PATH);
            
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var parts = line.Split(',');
                if (parts.Length < 12) continue;

                string targetStr = parts[0].Replace("%", "").Trim();
                if (!float.TryParse(targetStr, NumberStyles.Any, CI, out float target))
                    continue;

                var result = new TargetResult
                {
                    targetOxygen = target,
                    method = "Loaded"
                };

                if (float.TryParse(parts[1], NumberStyles.Any, CI, out float predicted))
                    result.predictedOxygen = predicted;
                if (float.TryParse(parts[2], NumberStyles.Any, CI, out float error))
                    result.error = error;

                if (!string.IsNullOrEmpty(parts[3]))
                {
                    result.parameters = new TrialDataModels.TrialData();
                    
                    if (float.TryParse(parts[3], NumberStyles.Any, CI, out float speed))
                        result.parameters.speed = speed;
                    if (float.TryParse(parts[4], NumberStyles.Any, CI, out float vSpeed))
                        result.parameters.verticalSpeed = vSpeed;
                    if (float.TryParse(parts[5], NumberStyles.Any, CI, out float idle))
                        result.parameters.idleUpwardSpeed = idle;
                    if (float.TryParse(parts[6], NumberStyles.Any, CI, out float life))
                        result.parameters.lifeTime = life;
                    if (float.TryParse(parts[7], NumberStyles.Any, CI, out float drain))
                        result.parameters.RemoveHealthEveryLifeTime = drain;
                    if (float.TryParse(parts[8], NumberStyles.Any, CI, out float collide))
                        result.parameters.removeHealthWithCollide = collide;
                    if (float.TryParse(parts[9], NumberStyles.Any, CI, out float timeColl))
                        result.parameters.timeBetweenCollides = timeColl;
                    if (float.TryParse(parts[10], NumberStyles.Any, CI, out float heal))
                        result.parameters.healHealthPoint = heal;
                    if (float.TryParse(parts[11], NumberStyles.Any, CI, out float force))
                    {
                        result.parameters.factorForce = force;
                        result.parameters.IsAmadeoMode = force > 0 ? 1f : 0f;
                    }
                }

                results.Add(result);
            }

            Debug.Log($"[MultiTargetOptimizer] Loaded {results.Count} targets from target.csv");
        }
        catch (Exception e)
        {
            Debug.LogError($"[MultiTargetOptimizer] Failed to load target.csv: {e.Message}");
        }

        return results;
    }

    /// <summary>
    /// Gets parameters for a specific target oxygen level
    /// </summary>
    public static TrialDataModels.TrialData GetParametersForTarget(float targetOxygen)
    {
        var targets = LoadFromTargetCSV();
        var match = targets.FirstOrDefault(t => Mathf.Abs(t.targetOxygen - targetOxygen) < 1f);
        return match?.parameters;
    }

    #endregion
}
