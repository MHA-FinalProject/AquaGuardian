using UnityEngine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

// Generates regression analysis reports
public static class TrialReportGenerator
{
    private static readonly CultureInfo CI = CultureInfo.InvariantCulture;
    private static readonly Dictionary<string, string> FeatureDisplayNames = new Dictionary<string, string>
    {
        {"speed", "Forward speed"},
        {"verticalSpeed", "Vertical speed"},
        {"idleUpwardSpeed", "Idle upward speed"},
        {"lifeTime", "Life span"},
        {"RemoveHealthEveryLifeTime", "Health removed per life cycle"},
        {"removeHealthWithCollide", "Collision damage"},
        {"timeBetweenCollides", "Time between collisions"},
        {"healHealthPoint", "Heal health points"},
        {"factorForce", "Force multiplier"},
        {"EffectiveDrainRate", "Effective drain rate"}
        // {"EffectiveCollisionDamageRate", "Effective collision damage rate"}  // REMOVED: redundant
        // To restore: uncomment above and update FeatureExtractor
    };

    private static string FormatDisplayName(string rawName)
    {
        if (string.IsNullOrEmpty(rawName))
            return rawName;

        if (FeatureDisplayNames.TryGetValue(rawName, out var mapped))
            return mapped;

        var builder = new StringBuilder();
        for (int i = 0; i < rawName.Length; i++)
        {
            char c = rawName[i];
            if (i == 0)
            {
                builder.Append(char.ToUpperInvariant(c));
                continue;
            }

            if (char.IsUpper(c))
            {
                builder.Append(' ');
            }

            builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }

    public static string GenerateSummaryReport(
        List<TrialDataModels.TrialData> trials,
        float avgError,
        OxygenPredictor predictor,
        TrialDataModels.TrialData optimizedSolution,
        float optimizedSolutionError,
        float targetOxygen)
    {
        int randomCount = trials.Count(t => t.isRandomParameters);
        int regularCount = trials.Count - randomCount;

        // Short summary (5-8 lines max)
        string summary = "Summary of the regression analysis:\n\n";
        
        // Line 1: Basic info
        summary += $"Trials: {trials.Count} | Target: {targetOxygen.ToString("F1", CI)}% | Avg Error: {avgError.ToString("F2", CI)}%\n\n";
        
        // Line 2-3: Optimized parameters (all in one compact line)
        if (optimizedSolution != null)
        {
            float predictedOptimal = predictor.PredictOxygen(optimizedSolution);
            
            // Calculate effective values for display (same as used in prediction)
            bool isAmadeo = optimizedSolution.IsAmadeoMode > 0.5f;
            float factorForceEff = isAmadeo ? optimizedSolution.factorForce : 0f;
            float idleUpwardEffective = isAmadeo ? (optimizedSolution.idleUpwardSpeed * 0.5f) : optimizedSolution.idleUpwardSpeed;
            
            summary += "Based on your trial performance, the following parameters have been determined:\n";
            summary += $"speed: {optimizedSolution.speed.ToString("F2", CI)}, verticalSpeed: {optimizedSolution.verticalSpeed.ToString("F2", CI)}, idleUpwardSpeed: {idleUpwardEffective.ToString("F2", CI)}, lifeTime: {optimizedSolution.lifeTime.ToString("F2", CI)}, RemoveHealthEveryLifeTime: {optimizedSolution.RemoveHealthEveryLifeTime.ToString("F2", CI)}, removeHealthWithCollide: {optimizedSolution.removeHealthWithCollide.ToString("F2", CI)}, timeBetweenCollides: {optimizedSolution.timeBetweenCollides.ToString("F2", CI)}, healHealthPoint: {optimizedSolution.healHealthPoint.ToString("F2", CI)}, factorForce: {factorForceEff.ToString("F2", CI)}\n\n";
            
            // Line 4: Prediction results
            summary += $"Prediction: {predictedOptimal.ToString("F2", CI)}% | Error: {optimizedSolutionError.ToString("F3", CI)}%\n\n";
        }

        // Line 5-6: Model coefficients (compact)
        var model = predictor?.GetModel();
        if (model != null && model.coefficients != null && model.coefficients.Length > 0)
        {
            summary += "The coefficients of the regression model are as follows:\n";
            summary += $"Intercept: {model.coefficients[0].ToString("F4", CI)}, ";

            var featureNames = model.featureNames;
            List<string> coeffStrings = new List<string>();
            for (int i = 1; i < model.coefficients.Length; i++)
            {
                string featureName = (featureNames != null && i - 1 < featureNames.Length)
                    ? featureNames[i - 1]
                    : $"Feature_{i}";
                coeffStrings.Add($"{featureName}: {model.coefficients[i].ToString("F4", CI)}");
            }
            summary += string.Join(", ", coeffStrings) + "\n";
        }

        return summary;
    }

    public static string GenerateFullReport(
        List<TrialDataModels.TrialData> trials,
        float avgError,
        float avgOxygen,
        OxygenPredictor predictor,
        TrialDataModels.TrialData optimizedSolution,
        float optimizedSolutionError,
        float targetOxygen)
    {
        int randomCount = trials.Count(t => t.isRandomParameters);
        int regularCount = trials.Count - randomCount;
        var selectedTrialIds = trials.Select(t => t.trialId).ToList();

        // Format header like the example: "Regression | 2025-11-02 15:31:46 | 5 trials"
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        string report = $"Regression | {timestamp} | {trials.Count} trials\n";
        
        // Line 2: "Avg : 57.6%  | Data Source: Constant Parameters CSV | Trials: [1,2,3,4,5] (5R+0Rand)"
        bool isRandom = randomCount > 0;
        string dataSource = isRandom ? "Random Parameters CSV" : "Constant Parameters CSV";
        string trialList = $"[{string.Join(",", selectedTrialIds)}]";
        report += $"Avg : {avgOxygen.ToString("F1", CI)}%  | Data Source: {dataSource} | Trials: {trialList} \n";
        
        // Line 3: "R^2=0.996 AdjR^2=NaN RMSE=1.21% MAE=0.93%"
        var model = predictor?.GetModel();
        if (model != null)
        {
            string adjR2 = float.IsNaN(model.adjustedRSquared) ? "NaN" : model.adjustedRSquared.ToString("F3", CI);
            report += $"R^2={model.rSquared.ToString("F3", CI)} AdjR^2={adjR2} RMSE={model.rootMeanSquaredError.ToString("F2", CI)}% MAE={avgError.ToString("F2", CI)}%\n";
        }
        
        // Line 4: "input type : keyboard" or "input type : amadeo"
        // Determine input type based on majority of trials
        int amadeoCount = trials.Count(t => t.IsAmadeoMode > 0.5f);
        string inputType = amadeoCount > trials.Count / 2 ? "amadeo" : "keyboard";
        report += $"input type : {inputType}\n";
        report += "\n";
        
        // Raw Data section with full names and duration
        report += "Raw Data:\n";
        foreach (var trial in trials)
        {
            string attemptInfo = trial.attemptNumber > 1 ? $" (Attempt #{trial.attemptNumber})" : "";
            report += $"T{trial.trialId}{attemptInfo}: Speed={trial.speed.ToString("F2", CI)} VerticalSpeed={trial.verticalSpeed.ToString("F2", CI)} IdleUpwardSpeed={trial.idleUpwardSpeed.ToString("F2", CI)} ";
            report += $"LifeTime={trial.lifeTime.ToString("F2", CI)} HealthPerCycle={trial.RemoveHealthEveryLifeTime.ToString("F2", CI)} ";
            report += $"CollisionDamage={trial.removeHealthWithCollide.ToString("F2", CI)} TimeBetweenCollides={trial.timeBetweenCollides.ToString("F2", CI)} ";
            report += $"HealPoints={trial.healHealthPoint.ToString("F2", CI)} FactorForce={trial.factorForce.ToString("F2", CI)} ";
            report += $"FinalOxygen={trial.finalOxygenRemaining.ToString("F1", CI)}% Duration={trial.trialDuration.ToString("F1", CI)}s\n";
        }
        report += "\n";
        
        // Detailed regression analysis for each trial
        if (model != null && model.coefficients != null && model.coefficients.Length > 0)
        {
            var featureNames = model.featureNames;
        
        foreach (var trial in trials)
        {
                float predicted = predictor.PredictOxygen(trial);
            float actual = trial.finalOxygenRemaining;
            float error = Mathf.Abs(actual - predicted);
                float errorPercent = (error / Mathf.Max(0.1f, actual)) * 100f;
            
                string attemptInfo = trial.attemptNumber > 1 ? $" (Attempt #{trial.attemptNumber})" : "";
                report += $"Trial {trial.trialId}{attemptInfo} Analysis:\n";
                report += $"  Actual Oxygen: {actual.ToString("F2", CI)}%\n";
                report += $"  Predicted Oxygen: {predicted.ToString("F2", CI)}%\n";
                report += $"  Error: {error.ToString("F2", CI)}% ({errorPercent.ToString("F1", CI)}%)\n";
                report += $"  Duration: {trial.trialDuration.ToString("F1", CI)}s\n";
                
                // Calculate effective values for this trial
                bool isAmadeo = trial.IsAmadeoMode > 0.5f;
                float factorForceEff = isAmadeo ? trial.factorForce : 0f;
                float idleUpwardEffective = isAmadeo ? (trial.idleUpwardSpeed * 0.5f) : trial.idleUpwardSpeed;
                
                // Show contribution of each feature (coefficient * feature value) - compact format
                report += $"  Feature Contributions:\n";
                report += $"    Intercept: {model.coefficients[0].ToString("F4", CI)}\n";
                
                // FIXED: Map model features to correct raw values using FeatureExtractor
                string[] fullFeatureNames = FeatureExtractor.FeatureNames;
                
                for (int i = 0; i < featureNames.Length && i + 1 < model.coefficients.Length; i++)
                {
                    string featureName = featureNames[i];
                    string fullName = FormatDisplayName(featureName);
        
                    // Find this feature's index in the full feature array
                    int fullIdx = System.Array.IndexOf(fullFeatureNames, featureName);
                    if (fullIdx < 0) continue;
                    
                    // Get raw value using ParameterHelper
                    float xRaw = ParameterHelper.Get(trial, fullIdx);
                    
                    // Apply effective transformations if needed
                    if (featureName == "idleUpwardSpeed")
                        xRaw = idleUpwardEffective;
                    else if (featureName == "factorForce")
                        xRaw = factorForceEff;
                    
                    // Get normalized value (what the model actually sees)
                    float xHat = model.ToNormalized(i, xRaw);
                    float coeff = model.coefficients[i + 1];
                    float contribution = coeff * xHat;
                    
                    // Display: show raw value but contribution is based on normalized
                    report += $"    {fullName}: {coeff.ToString("F4", CI)} * {xRaw.ToString("F2", CI)} = {contribution.ToString("F4", CI)}\n";
        }
                
                report += "\n";
            }
        }
        
        // Feature Importance (show all features the model trained on)
        var importance = predictor.GetFeatureImportance();
        if (importance != null && importance.Length > 0)
        {
            report += "Feature Importance (Impact on Oxygen):\n";
            
            // Hide only inactive features (factorForce in keyboard mode)
            var bannedForDisplay = new HashSet<string>();
            if (amadeoCount == 0)
            {
                bannedForDisplay.Add("factorForce");
            }
            
            foreach (var (feature, value) in importance)
            {
                if (bannedForDisplay.Contains(feature)) continue;
                
                // Add note for derived features
                string note = "";
                if (feature == "EffectiveDrainRate")
                {
                    note = " (derived: RemoveHealthEveryLifeTime / lifeTime)";
                }
                
                report += $"  {feature + note,-50} {value.ToString("F4", CI)}\n";
            }
            report += "\n";
        }
        
        // Regression Coefficients
        if (model != null && model.coefficients != null && model.coefficients.Length > 0)
        {
            report += "Regression Coefficients:\n";
            report += $"  Intercept                              {model.coefficients[0].ToString("F4", CI)}\n";
            
            var featureNames = model.featureNames;
            for (int i = 1; i < model.coefficients.Length; i++)
            {
                string featureName = (featureNames != null && i - 1 < featureNames.Length)
                    ? featureNames[i - 1]
                    : $"Feature_{i}";
                string fullName = FormatDisplayName(featureName);
                // Format like in example: name aligned to 24 chars, then coefficient
                report += $"  {fullName,-24} {model.coefficients[i].ToString("F4", CI)}\n";
            }
            report += "\n";
            
            // Regression Equation - formatted for readability
            report += "Regression Equation:\n";
            report += "Oxygen = ";
            report += model.coefficients[0].ToString("F4", CI);
            for (int i = 1; i < model.coefficients.Length; i++)
            {
                string featureName = (featureNames != null && i - 1 < featureNames.Length)
                    ? featureNames[i - 1]
                    : $"Feature_{i}";
                string fullName = FormatDisplayName(featureName);
                float coeff = model.coefficients[i];
                string sign = coeff >= 0 ? " + " : " - ";
                // Each feature on a new line with indentation for readability
                report += $"\n         {sign}{Mathf.Abs(coeff).ToString("F4", CI)} * {fullName}";
            }
            report += "\n";
            report += "\n";
        }
        
        // Optimized Parameters
        if (optimizedSolution != null)
        {
            float predictedOptimal = predictor.PredictOxygen(optimizedSolution);
            
            // Calculate effective values for display (same as used in prediction)
            bool isAmadeo = optimizedSolution.IsAmadeoMode > 0.5f;
            float factorForceEff = isAmadeo ? optimizedSolution.factorForce : 0f;
            float idleUpwardEffective = isAmadeo ? (optimizedSolution.idleUpwardSpeed * 0.5f) : optimizedSolution.idleUpwardSpeed;
            
            report += $"OPTIMIZED PARAMETERS FOR TARGET {targetOxygen.ToString("F0", CI)}% OXYGEN\n";
            report += "\n";
            report += $"Predicted: {predictedOptimal.ToString("F2", CI)}% | Error: {optimizedSolutionError.ToString("F3", CI)}%\n";
            report += "\n";
            report += "Parameters:\n";
            report += $"  Speed               = {optimizedSolution.speed.ToString("F2", CI).PadLeft(8)}\n";
            report += $"  VerticalSpeed       = {optimizedSolution.verticalSpeed.ToString("F2", CI).PadLeft(8)}\n";
            report += $"  IdleUpwardSpeed     = {idleUpwardEffective.ToString("F2", CI).PadLeft(8)}\n";
            report += $"  LifeTime            = {optimizedSolution.lifeTime.ToString("F2", CI).PadLeft(8)}s\n";
            report += $"  HealthPerLifeCycle  = {optimizedSolution.RemoveHealthEveryLifeTime.ToString("F2", CI).PadLeft(8)}\n";
            report += $"  CollisionDamage     = {optimizedSolution.removeHealthWithCollide.ToString("F2", CI).PadLeft(8)}%\n";
            report += $"  TimeBetweenCollides = {optimizedSolution.timeBetweenCollides.ToString("F2", CI).PadLeft(8)}s\n";
            report += $"  HealPoints          = {optimizedSolution.healHealthPoint.ToString("F2", CI).PadLeft(8)}%\n";
            report += $"  FactorForce         = {factorForceEff.ToString("F2", CI).PadLeft(8)}x\n";
            report += "\n";
        }

        return report;
    }

    // Python model report generation
    public static string GeneratePythonModelSummary(
        List<TrialDataModels.TrialData> trials,
        float avgError,
        PythonRegressionModel pythonModel,
        TrialDataModels.TrialData solution,
        float solutionError,
        float targetOxygen,
        (string feature, float importance)[] importance = null)
    {
        var modelData = pythonModel?.Model;
        string modelType = modelData?.model_type ?? "Unknown";
        int amadeoCount = trials.Count(t => t.IsAmadeoMode > 0.5f);
        var featureNames = modelData?.feature_names ?? FeatureExtractor.FeatureNames;
        
        var builder = new StringBuilder();
        
        // Python model training metrics - FIRST and compact
        if (modelData != null)
        {
            builder.AppendLine("Python model training metrics:");
            builder.AppendLine($"Samples: {modelData.n_samples} | Train MAE: {modelData.train_mae.ToString("F2", CI)}% | Train RMSE: {modelData.train_rmse.ToString("F2", CI)}% | Train R^2: {modelData.train_r2.ToString("F3", CI)}");
            builder.AppendLine();
        }
        
        // Compact trial info
        int randomCount = trials.Count(t => t.isRandomParameters);
        int regularCount = trials.Count - randomCount;
        float avgOxygen = trials.Average(t => t.finalOxygenRemaining);
        string inputType = amadeoCount > trials.Count / 2 ? "amadeo" : "keyboard";
        builder.AppendLine($"Trials: {trials.Count} ({regularCount}R+{randomCount}Rand) | Target: {targetOxygen.ToString("F1", CI)}% | Avg Error: {avgError.ToString("F2", CI)}%");
        builder.AppendLine();
        
        // Optimized Parameters - compact
        if (solution != null && pythonModel != null)
        {
            float predicted = pythonModel.PredictOxygen(solution);
            bool isAmadeo = solution.IsAmadeoMode > 0.5f;
            float factorForceEff = isAmadeo ? solution.factorForce : 0f;
            float idleEffective = isAmadeo ? solution.idleUpwardSpeed * 0.5f : solution.idleUpwardSpeed;
            
            builder.AppendLine("Based on your trial performance, the following parameters have been determined:");
            builder.AppendLine($"speed: {solution.speed.ToString("F2", CI)}, verticalSpeed: {solution.verticalSpeed.ToString("F2", CI)}, idleUpwardSpeed: {idleEffective.ToString("F2", CI)}, lifeTime: {solution.lifeTime.ToString("F2", CI)}, RemoveHealthEveryLifeTime: {solution.RemoveHealthEveryLifeTime.ToString("F2", CI)}, removeHealthWithCollide: {solution.removeHealthWithCollide.ToString("F2", CI)}, timeBetweenCollides: {solution.timeBetweenCollides.ToString("F2", CI)}, healHealthPoint: {solution.healHealthPoint.ToString("F2", CI)}, factorForce: {factorForceEff.ToString("F2", CI)}");
            builder.AppendLine();
            builder.AppendLine($"Prediction: {predicted.ToString("F2", CI)}% | Error: {solutionError.ToString("F3", CI)}%");
            builder.AppendLine();
        }
        
        // Model coefficients - compact (exclude derived features)
        if (modelData != null && modelData.betas != null)
        {
            builder.AppendLine("The coefficients of the regression model are as follows:");
            builder.Append($"Intercept: {modelData.intercept.ToString("F4", CI)}, ");
            
            List<string> coeffStrings = new List<string>();
            for (int i = 0; i < modelData.betas.Length; i++)
            {
                string featureName = (featureNames != null && i < featureNames.Length)
                    ? featureNames[i]
                    : $"Feature_{i + 1}";
                
                // Skip derived features in coefficients display
                if (featureName == "EffectiveDrainRate")
                    continue;
                    
                coeffStrings.Add($"{featureName}: {modelData.betas[i].ToString("F4", CI)}");
            }
            builder.AppendLine(string.Join(", ", coeffStrings));
        }
        
        return builder.ToString();
    }

    public static string GeneratePythonModelFullReport(
        List<TrialDataModels.TrialData> trials,
        float avgError,
        float avgOxygen,
        PythonRegressionModel pythonModel,
        (string feature, float importance)[] importance,
        TrialDataModels.TrialData solution,
        float solutionError,
        float targetOxygen)
    {
        var modelData = pythonModel?.Model;
        var featureNames = modelData?.feature_names ?? FeatureExtractor.FeatureNames;
        var builder = new StringBuilder();
        string modelType = modelData?.model_type ?? "Unknown";
        int amadeoCount = trials.Count(t => t.IsAmadeoMode > 0.5f);
        
        // Clear Python model identification at the top
        builder.AppendLine($"PYTHON MODEL REGRESSION REPORT | {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")} | {trials.Count} trials");
        builder.AppendLine();
        
        // Python model training metrics - FIRST
        if (modelData != null)
        {
            builder.AppendLine("Python model training metrics:");
            builder.AppendLine($"Samples: {modelData.n_samples} | Train MAE: {modelData.train_mae.ToString("F2", CI)}% | Train RMSE: {modelData.train_rmse.ToString("F2", CI)}% | Train R^2: {modelData.train_r2.ToString("F3", CI)}");
            builder.AppendLine();
        }
        
        // Data summary - compact
        int randomCount = trials.Count(t => t.isRandomParameters);
        int regularCount = trials.Count - randomCount;
        string dataSource = randomCount > 0 ? "Random Parameters CSV" : "Constant Parameters CSV";
        string inputType = amadeoCount > trials.Count / 2 ? "amadeo" : "keyboard";
        builder.AppendLine($"Avg : {avgOxygen.ToString("F1", CI)}% | Data Source: {dataSource} | input type : {inputType}");
        builder.AppendLine($"Test ({trials.Count} trials): Avg Error: {avgError.ToString("F2", CI)}%");
        builder.AppendLine();
        
        // Feature Importance
        if (importance != null && importance.Length > 0)
        {
            builder.AppendLine("=== FEATURE IMPORTANCE ===");
            builder.AppendLine();
            
            var bannedForDisplay = new HashSet<string>();
            if (amadeoCount == 0)
                bannedForDisplay.Add("factorForce");

            foreach (var (feature, value) in importance)
            {
                if (bannedForDisplay.Contains(feature))
                    continue;

                // Format: feature name aligned to 30 chars, then importance value
                builder.AppendLine($"{feature,-30} : {value.ToString("F4", CI)}");
            }
            builder.AppendLine();
        }
        
        // Optimized Parameters
        if (solution != null && pythonModel != null)
        {
            builder.AppendLine("=== OPTIMIZED PARAMETERS ===");
            builder.AppendLine();
            float predicted = pythonModel.PredictOxygen(solution);
            bool isAmadeo = solution.IsAmadeoMode > 0.5f;
            float factorForceEff = isAmadeo ? solution.factorForce : 0f;
            float idleEffective = isAmadeo ? solution.idleUpwardSpeed * 0.5f : solution.idleUpwardSpeed;
            
            builder.AppendLine($"Optimized for {targetOxygen.ToString("F1", CI)}%: Predicted: {predicted.ToString("F1", CI)}% Error: {solutionError.ToString("F2", CI)}%");
            builder.AppendLine();
            builder.AppendLine($"speed: {solution.speed.ToString("F2", CI)} verticalSpeed: {solution.verticalSpeed.ToString("F2", CI)} idleUpwardSpeed: {idleEffective.ToString("F2", CI)} lifeTime: {solution.lifeTime.ToString("F2", CI)} RemoveHealthEveryLifeTime: {solution.RemoveHealthEveryLifeTime.ToString("F2", CI)} removeHealthWithCollide: {solution.removeHealthWithCollide.ToString("F2", CI)} timeBetweenCollides: {solution.timeBetweenCollides.ToString("F2", CI)} healHealthPoint: {solution.healHealthPoint.ToString("F2", CI)} factorForce: {factorForceEff.ToString("F2", CI)}");
            builder.AppendLine();
        }
        
        // Additional details section
        builder.AppendLine($"--- Detailed Analysis ({DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}) ---");
        builder.AppendLine();
        builder.AppendLine();

        builder.AppendLine("Raw Data:");
        foreach (var trial in trials)
        {
            builder.Append("T").Append(trial.trialId).Append(": ");
            builder.Append($"Speed={trial.speed.ToString("F2", CI)} ");
            builder.Append($"VerticalSpeed={trial.verticalSpeed.ToString("F2", CI)} ");
            builder.Append($"IdleUpwardSpeed={trial.idleUpwardSpeed.ToString("F2", CI)} ");
            builder.Append($"LifeTime={trial.lifeTime.ToString("F2", CI)} ");
            builder.Append($"HealthPerCycle={trial.RemoveHealthEveryLifeTime.ToString("F2", CI)} ");
            builder.Append($"CollisionDamage={trial.removeHealthWithCollide.ToString("F2", CI)} ");
            builder.Append($"TimeBetweenCollides={trial.timeBetweenCollides.ToString("F2", CI)} ");
            builder.Append($"HealPoints={trial.healHealthPoint.ToString("F2", CI)} ");
            builder.Append($"FactorForce={trial.factorForce.ToString("F2", CI)} ");
            builder.Append($"FinalOxygen={trial.finalOxygenRemaining.ToString("F1", CI)}% ");
            builder.Append($"Duration={trial.trialDuration.ToString("F1", CI)}s");
            builder.AppendLine();
        }
        builder.AppendLine();

        if (modelData != null && modelData.betas != null && modelData.means != null && modelData.stds != null && pythonModel != null)
        {
            foreach (var trial in trials)
            {
                float predicted = pythonModel.PredictOxygen(trial);
                float actual = trial.finalOxygenRemaining;
                float error = Mathf.Abs(actual - predicted);
                float errorPercent = (error / Mathf.Max(0.1f, Mathf.Abs(actual))) * 100f;
                builder.AppendLine($"Trial {trial.trialId} Analysis:");
                builder.AppendLine($"  Actual Oxygen: {actual.ToString("F2", CI)}%");
                builder.AppendLine($"  Predicted Oxygen: {predicted.ToString("F2", CI)}%");
                builder.AppendLine($"  Error: {error.ToString("F2", CI)}% ({errorPercent.ToString("F1", CI)}%)");
                builder.AppendLine($"  Duration: {trial.trialDuration.ToString("F1", CI)}s");

                float[] featureVector = FeatureExtractor.ExtractFeatures(trial);
                builder.AppendLine("  Feature Contributions:");
                builder.AppendLine($"    Intercept: {modelData.intercept.ToString("F4", CI)}");

                for (int i = 0; i < modelData.betas.Length; i++)
                {
                    string featureName = (featureNames != null && i < featureNames.Length)
                        ? featureNames[i]
                        : $"Feature_{i + 1}";
                    int idx = System.Array.IndexOf(FeatureExtractor.FeatureNames, featureName);
                    if (idx < 0 || idx >= featureVector.Length)
                        continue;

                    float raw = featureVector[idx];
                    float mean = i < modelData.means.Length ? modelData.means[i] : 0f;
                    float std = i < modelData.stds.Length ? modelData.stds[i] : 1f;
                    if (Mathf.Abs(std) < 1e-6f)
                        std = 1f;
                    float normalized = (raw - mean) / std;
                    float contribution = modelData.betas[i] * normalized;
                    string displayName = FormatDisplayName(featureName);
                    builder.AppendLine($"    {displayName}: {modelData.betas[i].ToString("F4", CI)} * {raw.ToString("F2", CI)} = {contribution.ToString("F4", CI)}");
                }

                builder.AppendLine();
            }
        }


        if (modelData != null && modelData.betas != null)
        {
            builder.AppendLine("The coefficients of the regression model are as follows:");
            builder.AppendLine("Regression Coefficients:");
            builder.AppendLine($"  Intercept                              {modelData.intercept.ToString("F4", CI)}");
            for (int i = 0; i < modelData.betas.Length; i++)
            {
                string featureName = (featureNames != null && i < featureNames.Length)
                    ? featureNames[i]
                    : $"Feature_{i + 1}";
                string displayName = FormatDisplayName(featureName);
                builder.AppendLine($"  {displayName,-24} {modelData.betas[i].ToString("F4", CI)}");
            }
            builder.AppendLine();

            builder.AppendLine("Regression Equation:");
            builder.Append("Oxygen = ");
            builder.Append(modelData.intercept.ToString("F4", CI));
            for (int i = 0; i < modelData.betas.Length; i++)
            {
                string featureName = (featureNames != null && i < featureNames.Length)
                    ? featureNames[i]
                    : $"Feature_{i + 1}";
                float coeff = modelData.betas[i];
                string sign = coeff >= 0 ? " + " : " - ";
                builder.AppendLine();
                builder.Append("         ");
                builder.Append(sign);
                builder.Append(Mathf.Abs(coeff).ToString("F4", CI));
                builder.Append(" * ");
                builder.Append(FormatDisplayName(featureName));
            }
            builder.AppendLine();
            builder.AppendLine();
        }


        return builder.ToString();
    }

    public static bool SaveToFile(string fullReport, string saveFolder = "RegressionResults", bool isPythonModel = false)
    {
        try
        {
            string dataPath = Path.Combine(Application.dataPath, "Data");
            string savePath = Path.Combine(dataPath, saveFolder);

            if (!Directory.Exists(savePath))
            {
                Directory.CreateDirectory(savePath);
                Debug.Log($"[TrialReportGenerator] Created directory: {savePath}");
            }

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string prefix = isPythonModel ? "PythonRegression" : "UnityRegression";
            string fileName = $"{prefix}_{timestamp}.txt";
            string fullPath = Path.Combine(savePath, fileName);

            Debug.Log($"[TrialReportGenerator] Saving report - IsPythonModel: {isPythonModel}, FileName: {fileName}, Path: {fullPath}");
            
            File.WriteAllText(fullPath, fullReport);
            Debug.Log($"[TrialReportGenerator] Report saved successfully: {fileName}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[TrialReportGenerator] Save failed: {e.Message}\nStackTrace: {e.StackTrace}");
            return false;
        }
    }
}