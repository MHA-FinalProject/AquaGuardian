using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;


/**
 * Main orchestrator for regression analysis workflow
 * 
 * Coordinates ML regression pipeline: trains model, performs cross-validation, optimizes
 * parameters for target oxygen (default: 10%), and generates reports. Primary entry point
 * for regression analysis. Python model integration handled by PythonRegressionHandler.
 * 
 * See also: OxygenPredictor, RegressionUtilities, DifficultyParameterSolver, FeatureExtractor, 
 *           TrialReportGenerator, PythonRegressionHandler
 */
public class TrialRegressionAlgorithm
{
    private const float DEFAULT_TARGET_O2 = 10f;
    private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;

    #region Public API - Model Management (Delegates to PythonRegressionHandler)

    // Load Python-trained model from JSON
    public static bool LoadPythonModel(string jsonPath) => PythonRegressionHandler.LoadPythonModel(jsonPath);

    // Switch to Unity built-in model
    public static void UseUnityModel() => PythonRegressionHandler.UseUnityModel();

    // Switch to Python model (if loaded)
    public static bool UsePythonModel() => PythonRegressionHandler.UsePythonModel();

    // Get current model type
    public static string GetCurrentModelType() => PythonRegressionHandler.GetCurrentModelType();

    // AUTO-LOAD: Try to automatically load Python model from common locations
    public static bool TryAutoLoadPythonModel(string preferredModelName = "regression_model_elasticnet.json") 
        => PythonRegressionHandler.TryAutoLoadPythonModel(preferredModelName);

    #endregion

    #region Public API - Regression Analysis

    // Main function for regression analysis that coordinates the entire pipeline from input validation to report generation
    // Supports both Unity built-in model and Python-trained models (hybrid mode)
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

        // Check if Python model is available and active
        if (PythonRegressionHandler.IsModelReady)
        {
            Debug.Log($"[TrialRegression] Using Python model: {PythonRegressionHandler.GetCurrentModelType()}");
            return PythonRegressionHandler.PerformPythonRegressionAnalysis(allTrialData, targetOxygen);
        }

        // Unity model path (fallback)
        Debug.Log("[TrialRegression] Using Unity built-in model");
        return PerformUnityRegressionAnalysis(allTrialData, targetOxygen);
    }

    // Original Unity regression
    private static TrialDataModels.RegressionResult PerformUnityRegressionAnalysis(
        List<TrialDataModels.TrialData> allTrialData,
        float targetOxygen)
    {
        var result = new TrialDataModels.RegressionResult
        {
            correlations = new Dictionary<string, float>()
        };

        RegressionUtilities.CalculateTrialStatistics(allTrialData, result);

        int featureCount = FeatureExtractor.FeatureCount;
        var predictor = new OxygenPredictor { maxFeaturesForTraining = featureCount };
        bool trained = predictor.TrainModel(allTrialData, enableFeatureSelection: false);

        // EXPERIMENTAL: Aggressive feature selection (COMMENTED - kept for future reference)
        // Note: This increased error on clean/constant data but may help with noisy real-world data
        // 
        // int maxFeatures;
        // bool enableFeatureSelection;
        // if (allTrialData.Count <= 5) {
        //     maxFeatures = 3;  // TOP 3 features
        //     enableFeatureSelection = true;
        // } else if (allTrialData.Count <= 8) {
        //     maxFeatures = 5;  // TOP 5 features
        //     enableFeatureSelection = true;
        // } else {
        //     maxFeatures = featureCount;
        //     enableFeatureSelection = false;
        // }
        // predictor.maxFeaturesForTraining = maxFeatures;
        // predictor.TrainModel(allTrialData, enableFeatureSelection: enableFeatureSelection);
        
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

        // Detect if this is a Python model report
        bool isPythonModel = result.fullDetailsText.Contains("PYTHON MODEL REGRESSION REPORT");
        return TrialReportGenerator.SaveToFile(result.fullDetailsText, saveFolder, isPythonModel);
    }

    #endregion
}
