using UnityEngine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

/// <summary>
/// Generates regression analysis reports
/// </summary>
public static class TrialReportGenerator
{
    private static readonly CultureInfo CI = CultureInfo.InvariantCulture;

    public static string GenerateSummaryReport(
        List<TrialDataModels.TrialData> trials,
        float avgError,
        OxygenPredictor predictor,
        TrialDataModels.TrialData optimalParams)
    {
        int randomCount = trials.Count(t => t.isRandomParameters);
        int regularCount = trials.Count - randomCount;

        string summary = "Regression analysis:\n";
        summary += $"Trials: {trials.Count} (Regular: {regularCount}, Random: {randomCount})\n";
        
        if (randomCount > 0)
        {
            var randomIds = trials.Where(t => t.isRandomParameters).Select(t => t.trialId).ToList();
            summary += $"Random: [{string.Join(", ", randomIds)}]\n";
        }
        
        summary += $"Average Error: {avgError.ToString("F1", CI)}%\n";
        summary += $"Target: 5.0% oxygen remaining\n";

        if (optimalParams != null)
        {
            float predictedOptimal = predictor.PredictOxygen(optimalParams);
            summary += "Recommended parameters:\n";
            summary += $"Predicted Result: {predictedOptimal.ToString("F1", CI)}%\n";
            summary += $"Speed: {optimalParams.speed.ToString("F1", CI)}\n";
            summary += $"Vertical Speed: {optimalParams.verticalSpeed.ToString("F1", CI)}\n";
            summary += $"Idle Upward Speed: {optimalParams.idleUpwardSpeed.ToString("F2", CI)}\n";
            summary += $"Life Time: {optimalParams.lifeTime.ToString("F1", CI)}\n";
            summary += $"O2 Drop/sec: {optimalParams.downHealthPairSec.ToString("F1", CI)}\n";
            summary += $"Collision Damage: {optimalParams.removeHealthWithCollide.ToString("F1", CI)}\n";
            summary += $"Time Between Collides: {optimalParams.timeBetweenCollides.ToString("F1", CI)}\n";
            summary += $"Heal Points: {optimalParams.healHealthPoint.ToString("F1", CI)}\n";
        }
        else
        {
            summary += "Could not calculate optimal parameters\n";
        }

        return summary;
    }

    public static string GenerateFullReport(
        List<TrialDataModels.TrialData> trials,
        float avgError,
        int perfectTrials,
        int failedTrials,
        float avgOxygen,
        OxygenPredictor predictor,
        TrialDataModels.TrialData optimalParams,
        float cvRmse,
        float cvMae,
        float cvR2,
        int kFolds)
    {
        int randomCount = trials.Count(t => t.isRandomParameters);
        int regularCount = trials.Count - randomCount;
        var selectedTrialIds = trials.Select(t => t.trialId).ToList();

        string quality = cvR2 > 0.7f ? "Excellent!" :
                        cvR2 > 0.5f ? "Good" :
                        cvR2 > 0.3f ? "Fair" : "Poor";

        string report = "Regression analysis - full report:\n\n";
        report += $"Trials Selected: [{string.Join(", ", selectedTrialIds)}]\n";
        report += $"Total Trials: {trials.Count} (Regular: {regularCount}, Random: {randomCount})\n";
        
        if (randomCount > 0)
        {
            var randomIds = trials.Where(t => t.isRandomParameters).Select(t => t.trialId).ToList();
            report += $"Random Parameter Trials: [{string.Join(", ", randomIds)}]\n";
        }
        
        report += $"Average Oxygen: {avgOxygen.ToString("F1", CI)}%\n";
        report += $"Perfect Trials (2.5-7.5%): {perfectTrials}\n";
        report += $"Failed Trials (0%): {failedTrials}\n\n";

        report += "Model validation (K-Fold CV):\n";
        report += $"Folds: {kFolds}\n";
        report += $"Cross-Val RMSE: {cvRmse.ToString("F2", CI)}%\n";
        report += $"Cross-Val MAE: {cvMae.ToString("F2", CI)}%\n";
        report += $"Cross-Val R2: {cvR2.ToString("F3", CI)}\n";
        report += $"Model Quality: {quality}\n\n";

        report += "(Actual vs Predicted Oxygen)\n\n";

        foreach (var trial in trials)
        {
            float actual = trial.finalOxygenRemaining;
            float predicted = predictor.PredictOxygen(trial);
            float error = Mathf.Abs(actual - predicted);

            string paramMode = trial.isRandomParameters ? " (Random Parameters)" : " (Regular Parameters)";
            report += $"Trial {trial.trialId}{paramMode}:\n";
            report += $"  Actual: {actual.ToString("F1", CI)}%  Predicted: {predicted.ToString("F1", CI)}%\n";
            report += $"  Error: {error.ToString("F1", CI)}%\n\n";
        }

        report += $"Average Prediction Error: {avgError.ToString("F2", CI)}%\n\n";

        report += "Feature importance:\n";
        report += "(Impact on oxygen level)\n\n";

        var importance = predictor.GetFeatureImportance();
        foreach (var (feature, value) in importance.Take(5))
        {
            int barLen = Mathf.Clamp(Mathf.RoundToInt(value * 20f), 0, 60);
            string bar = new string('#', barLen);
            report += $"{feature}:\n  {value.ToString("F4", CI)} {bar}\n";
        }

        report += "Optimal parameter recommendation:\n\n";
        report += "Target: 5.0% oxygen remaining\n\n";

        if (optimalParams != null)
        {
            float predictedOptimal = predictor.PredictOxygen(optimalParams);

            report += $"Predicted Oxygen: {predictedOptimal.ToString("F2", CI)}%\n\n";
            report += "Recommended Parameters:\n";
            report += $"  Speed: {optimalParams.speed.ToString("F2", CI)}\n";
            report += $"  Vertical Speed: {optimalParams.verticalSpeed.ToString("F2", CI)}\n";
            report += $"  Idle Upward Speed: {optimalParams.idleUpwardSpeed.ToString("F3", CI)}\n";
            report += $"  Life Time: {optimalParams.lifeTime.ToString("F2", CI)}\n";
            report += $"  O2 Drop/sec: {optimalParams.downHealthPairSec.ToString("F2", CI)}\n";
            report += $"  Collision Damage: {optimalParams.removeHealthWithCollide.ToString("F2", CI)}\n";
            report += $"  Time Between Collides: {optimalParams.timeBetweenCollides.ToString("F2", CI)}\n";
            report += $"  Heal Points: {optimalParams.healHealthPoint.ToString("F2", CI)}\n";
        }
        else
        {
            report += "Could not find optimal parameters\n";
        }

        return report;
    }

    public static bool SaveToFile(string fullReport, List<TrialDataModels.TrialData> trials, int totalTrials, string saveFolder = "RegressionResults")
    {
        try
        {
            string dataPath = Path.Combine(Application.dataPath, "Data");
            string savePath = Path.Combine(dataPath, saveFolder);

            if (!Directory.Exists(savePath))
                Directory.CreateDirectory(savePath);

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string fileName = $"RegressionAnalysis_{timestamp}.txt";
            string fullPath = Path.Combine(savePath, fileName);

            string fileContent = "Regression analysis - full report:\n";
            fileContent += $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n";
            fileContent += $"Trials analyzed: {totalTrials}\n\n";
            fileContent += fullReport;
            fileContent += "\n\nRaw trial data:\n";

            if (trials != null)
            {
                foreach (var trial in trials)
                {
                    fileContent += $"\nTrial {trial.trialId}:\n";
                    fileContent += $"  Speed: {trial.speed.ToString("F2", CI)}\n";
                    fileContent += $"  VerticalSpeed: {trial.verticalSpeed.ToString("F2", CI)}\n";
                    fileContent += $"  IdleUpwardSpeed: {trial.idleUpwardSpeed.ToString("F2", CI)}\n";
                    fileContent += $"  LifeTime: {trial.lifeTime.ToString("F2", CI)}\n";
                    fileContent += $"  O2DropPerSec: {trial.downHealthPairSec.ToString("F2", CI)}\n";
                    fileContent += $"  CollisionDamage: {trial.removeHealthWithCollide.ToString("F2", CI)}\n";
                    fileContent += $"  TimeBetweenCollides: {trial.timeBetweenCollides.ToString("F2", CI)}\n";
                    fileContent += $"  HealPoints: {trial.healHealthPoint.ToString("F2", CI)}\n";
                    fileContent += $"  FactorForce: {trial.factorForce.ToString("F2", CI)}\n";
                    fileContent += $"  FinalO2: {trial.finalOxygenRemaining.ToString("F1", CI)}%\n";
                }
            }

            File.WriteAllText(fullPath, fileContent);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Save failed: {e.Message}");
            return false;
        }
    }
}


