using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;


/**
 * Main orchestrator for Unity Ridge regression analysis workflow.
 * 
 * Called from TrialRegressionUI.CalculateRegression when Python server is disabled or unavailable.
 * Python server mode is handled separately by PythonRegressionServerClient.TrainAndAnalyze.
 * 
 * See also: TrialRegressionUI, OxygenPredictor, RegressionUtilities, DifficultyParameterSolver, 
 *           FeatureExtractor, TrialReportGenerator, PythonRegressionServerClient
 */
public class TrialRegressionAlgorithm
{
    private const float DEFAULT_TARGET_O2 = 10f;

    #region Public API - Main Functions

    /**
     * Main entry point for Unity Ridge regression analysis.
     * Validates input (requires at least 3 trials), then calls PerformUnityRegressionAnalysis.
     * 
     * Note: This method is kept for potential future use, but single-target analysis
     * is no longer used in the UI. Multi-target analysis is now the primary method.
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

        return PerformUnityRegressionAnalysis(allTrialData, targetOxygen);
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

    /**
     * Performs multi-target optimization (10%-90% oxygen targets).
     * Saves results to Assets/Data/MultiTargets/target.csv
     * 
     * @param allTrialData Patient trial data (minimum 3 trials required)
     * @return Text report for display
     */
    public static string GetMultiTargetReport(List<TrialDataModels.TrialData> allTrialData)
    {
        return MultiTargetOptimizer.RunMultiTargetAnalysis(allTrialData);
    }

    #endregion

    #region Helper Functions

    /**
     * Checks if Python server is available for patient-specific training.
     * 
     * Returns true if PythonRegressionServerClient exists in scene and server is available.
     * Useful for UI to check server status before attempting Python-based analysis.
     * 
     * Called from: TrialRegressionUI or other UI components
     * 
     * @return true if Python server is available, false otherwise
     */
    public static bool IsServerAvailable()
    {
        var serverClient = UnityEngine.Object.FindObjectOfType<PythonRegressionServerClient>();
        return serverClient != null && serverClient.IsServerAvailable;
    }

    /**
     * Executes Unity Ridge regression analysis workflow.
     * 
     * Complete pipeline:
     * 1. Calculates trial statistics (average oxygen) using RegressionUtilities.CalculateTrialStatistics
     * 2. Trains OxygenPredictor (Ridge regression) on patient-specific data with all features
     * 3. Performs cross-validation to evaluate model quality (K-fold CV, calculates RMSE, MAE, R^2)
     * 4. Prepares optimization features (importance ranking, banned features like EffectiveDrainRate)
     * 5. Optimizes parameters using RegressionUtilities.OptimizeParameters:
     *    - Primary: 3-phase gradient descent (analytical → gradient → iterative refinement)
     *    - Fallback: RandomSweep if error > 5% or solution is null
     *    - Last resort: Multi-Gradient if all else fails
     * 6. Generates summary and full reports using TrialReportGenerator
     * 7. Stores top 5 feature importances in result.correlations dictionary
     * 
     * Called from: PerformRegressionAnalysis
     * 
     * Calls:
     * - RegressionUtilities.CalculateTrialStatistics
     * - OxygenPredictor.TrainModel
     * - RegressionUtilities.PerformCrossValidationAndErrorCalculation
     * - RegressionUtilities.PrepareOptimizationFeatures
     * - RegressionUtilities.OptimizeParameters
     * - TrialReportGenerator.GenerateSummaryReport
     * - TrialReportGenerator.GenerateFullReport
     */
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

        
        if (!trained)
        {
            string errorMsg = "ERROR: Failed to train ML model\nNot enough variance in data";
            result.summaryText = errorMsg;
            result.fullDetailsText = errorMsg;
            return result;
        }

        var model = predictor.GetModel();

        var (cvRmse, cvMae, cvR2, kFolds, avgError) = RegressionUtilities.PerformCrossValidationAndErrorCalculation(
            allTrialData, model, predictor, featureCount);

        var (bannedFeatures, filteredImportance) = RegressionUtilities.PrepareOptimizationFeatures(allTrialData, predictor);

        // C# mode: Gradient 3-Phase as primary, RandomSweep as fallback, Multi-Gradient as last resort
        var (optimizedSolution, optimizedError, selectedMethod) = RegressionUtilities.OptimizeParameters(
            model, predictor, allTrialData, filteredImportance,
            FeatureExtractor.FeatureNames, targetOxygen,
            allTrialData.Any(t => t.IsAmadeoMode > 0.5f));

        result.optimizedSolution = optimizedSolution;
        result.optimizedSolutionError = optimizedError;

        // C# mode: Show selected method in report
        result.summaryText = TrialReportGenerator.GenerateSummaryReport(
            allTrialData, avgError, predictor, optimizedSolution, result.optimizedSolutionError, targetOxygen,
            selectedMethod: selectedMethod);

        result.fullDetailsText = TrialReportGenerator.GenerateFullReport(
            allTrialData, avgError, result.averageOxygen, predictor, optimizedSolution, result.optimizedSolutionError, targetOxygen,
            selectedMethod: selectedMethod);

        // Store feature importance
        foreach (var (feature, value) in filteredImportance.Take(5))
        {
            result.correlations[feature] = value;
        }

        return result;
    }

    #endregion
}
