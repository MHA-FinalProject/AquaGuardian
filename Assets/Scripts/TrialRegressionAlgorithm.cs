using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

/**
 * Main orchestrator for regression analysis workflow
 * 
 * Coordinates ML regression pipeline: trains model, performs cross-validation, optimizes
 * parameters for target oxygen (default: 10%), and generates reports. Primary entry point
 * for regression analysis
 * 
 * See also: OxygenPredictor, RegressionUtilities, DifficultyParameterSolver, FeatureExtractor, TrialReportGenerator
 */
public class TrialRegressionAlgorithm
{
    private const float DEFAULT_TARGET_O2 = 10f;
    private const float TARGET_TOLERANCE = 5f;
    private const float RANDOM_SWEEP_THRESHOLD = 5.0f;

    #region Public API
  /**
  * Main function for regression analysis that coordinates the entire pipeline from input validation to report generation
  
  */

    public static TrialDataModels.RegressionResult PerformRegressionAnalysis(List<TrialDataModels.TrialData> allTrialData, float targetOxygen = DEFAULT_TARGET_O2)
    {
        if (allTrialData == null || allTrialData.Count < 3)
        {
            string errorMsg = $"ERROR: Need at least 3 trials for ML analysis\nFound: {allTrialData?.Count ?? 0} trials";
            return new TrialDataModels.RegressionResult
            {
                summaryText = errorMsg,
                fullDetailsText = errorMsg,
                correlations = new Dictionary<string, float>()
            };
        }

        var result = new TrialDataModels.RegressionResult
        {
            correlations = new Dictionary<string, float>()
        };

        RegressionUtilities.CalculateTrialStatistics(allTrialData, result);

        int featureCount = FeatureExtractor.FeatureCount;
        var predictor = new OxygenPredictor { maxFeaturesForTraining = featureCount };
        bool trained = predictor.TrainModel(allTrialData, enableFeatureSelection: false);

        /**
         * EXPERIMENTAL: Aggressive feature selection (COMMENTED - kept for future reference)
         * Note: This increased error on clean/constant data but may help with noisy real-world data
         * 
         * int maxFeatures;
         * bool enableFeatureSelection;
         * if (allTrialData.Count <= 5) {
         *     maxFeatures = 3;  // TOP 3 features
         *     enableFeatureSelection = true;
         * } else if (allTrialData.Count <= 8) {
         *     maxFeatures = 5;  // TOP 5 features
         *     enableFeatureSelection = true;
         * } else {
         *     maxFeatures = featureCount;
         *     enableFeatureSelection = false;
         * }
         * predictor.maxFeaturesForTraining = maxFeatures;
         * predictor.TrainModel(allTrialData, enableFeatureSelection: enableFeatureSelection);
         */
        if (!trained)
        {
            string errorMsg = "ERROR: Failed to train ML model\nNot enough variance in data";
            result.summaryText = errorMsg;
            result.fullDetailsText = errorMsg;
            return result;
        }

        var model = predictor.GetModel();

        if (model?.featureNames == null || model.featureNames.Length + 1 != model.coefficients?.Length)
        {
            Debug.LogError("[Regression] Feature names must align with coefficients (excluding intercept).");
        }

        var (cvRmse, cvMae, cvR2, kFolds, avgError) = RegressionUtilities.PerformCrossValidationAndErrorCalculation(
            allTrialData, model, predictor, featureCount);

        var (bannedFeatures, filteredImportance) = RegressionUtilities.PrepareOptimizationFeatures(allTrialData, predictor);

        var (optimizedSolution, optimizedError) = RegressionUtilities.OptimizeParameters(
            model, predictor, allTrialData, filteredImportance,
            FeatureExtractor.FeatureNames, targetOxygen,
            allTrialData.Any(t => t.IsAmadeoMode > 0.5f));

        result.optimizedSolution = optimizedSolution;
        result.optimizedSolutionError = optimizedError;

        result.summaryText = TrialReportGenerator.GenerateSummaryReport(allTrialData, avgError, predictor, optimizedSolution, result.optimizedSolutionError, targetOxygen);

        result.fullDetailsText = TrialReportGenerator.GenerateFullReport(allTrialData, avgError, result.averageOxygen, predictor, optimizedSolution, result.optimizedSolutionError, targetOxygen);

        // Store feature importance
        foreach (var (feature, value) in filteredImportance.Take(5))
        {
            result.correlations[feature] = value;
        }

        return result;
    }

    public static bool SaveRegressionResultsToFile(TrialDataModels.RegressionResult result, string saveFolder = "RegressionResults")
    {
        if (result == null || string.IsNullOrEmpty(result.fullDetailsText))
        {
            Debug.LogWarning("No results to save!");
            return false;
        }

        return TrialReportGenerator.SaveToFile(result.fullDetailsText, saveFolder);
    }

    #endregion
}
