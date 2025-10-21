using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

/// <summary>
/// Main coordinator for trial regression analysis
/// Delegates data loading, analysis, and reporting to specialized classes
/// </summary>
public class TrialRegressionAlgorithm
{
    public class RegressionResult
    {
        public string summaryText;
        public string fullDetailsText;
        public Dictionary<string, float> correlations;
        public float averageOxygen;
        public int perfectTrials;
        public int failedTrials;
        public int totalTrials;
        public List<TrialDataModels.TrialData> analyzedTrials;
    }

    #region Public API

    /// <summary>
    /// Load trial data from cache (delegates to TrialDataLoader)
    /// </summary>
    public static List<TrialDataModels.TrialData> LoadTrialDataFromCache()
    {
        return TrialDataLoader.LoadTrialDataFromCache();
    }

    /// <summary>
    /// Load trial data from CSV files (delegates to TrialDataLoader)
    /// </summary>
    public static List<TrialDataModels.TrialData> LoadTrialDataFromCSV()
    {
        return TrialDataLoader.LoadTrialDataFromCSV();
    }

    /// <summary>
    /// Perform ML regression analysis using Multiple Linear Regression
    /// </summary>
    public static RegressionResult PerformRegressionAnalysis(List<TrialDataModels.TrialData> allTrialData)
    {
        if (allTrialData == null || allTrialData.Count < 3)
        {
            string errorMsg = $"ERROR: Need at least 3 trials for ML analysis\nFound: {allTrialData?.Count ?? 0} trials";
            return new RegressionResult
            {
                summaryText = errorMsg,
                fullDetailsText = errorMsg,
                correlations = new Dictionary<string, float>(),
                totalTrials = allTrialData?.Count ?? 0
            };
        }

        var selectedTrialIds = allTrialData.Select(t => t.trialId).ToList();
        Debug.Log("REGRESSION ANALYSIS");
        Debug.Log($"Trials Selected: [{string.Join(", ", selectedTrialIds)}]");
        Debug.Log($"Total trials analyzed: {allTrialData.Count}");

        var result = new RegressionResult
        {
            correlations = new Dictionary<string, float>(),
            analyzedTrials = new List<TrialDataModels.TrialData>(allTrialData),
            totalTrials = allTrialData.Count
        };

        // Calculate statistics
        float totalOxygen = 0f;
        int perfectTrials = 0;
        int failedTrials = 0;

        foreach (var trial in allTrialData)
        {
            totalOxygen += trial.finalOxygenRemaining;
            if (trial.finalOxygenRemaining >= 2.5f && trial.finalOxygenRemaining <= 7.5f)
                perfectTrials++;
            if (trial.finalOxygenRemaining <= 0f)
                failedTrials++;
        }

        result.averageOxygen = totalOxygen / allTrialData.Count;
        result.perfectTrials = perfectTrials;
        result.failedTrials = failedTrials;

        // Train ML model
        var predictor = new OxygenPredictor
        {
            topKFeatures = 4
        };

        bool trained = predictor.TrainModel(allTrialData, enableFeatureSelection: true);
        if (!trained)
        {
            string errorMsg = "ERROR: Failed to train ML model\nNot enough variance in data";
            result.summaryText = errorMsg;
            result.fullDetailsText = errorMsg;
            return result;
        }

        var model = predictor.GetModel();

        // K-Fold Cross Validation
        var (X, y) = BuildFeatureMatrix(allTrialData);
        int kFolds = Mathf.Min(5, Mathf.Max(2, allTrialData.Count));
        var (cvRmse, cvMae, cvR2) = model.KFoldCV(X, y, kFolds);

        // Find optimal parameters
        var optimal = predictor.FindOptimalParameters(targetOxygen: 5.0f);

        // Calculate average error
        float totalError = 0f;
        foreach (var trial in allTrialData)
        {
            float actual = trial.finalOxygenRemaining;
            float predicted = predictor.PredictOxygen(trial);
            float error = Mathf.Abs(actual - predicted);
            totalError += error;
        }
        float avgError = totalError / allTrialData.Count;

        // Generate reports using TrialReportGenerator
        result.summaryText = TrialReportGenerator.GenerateSummaryReport(
            allTrialData, avgError, predictor, optimal);

        result.fullDetailsText = TrialReportGenerator.GenerateFullReport(
            allTrialData, avgError, perfectTrials, failedTrials, result.averageOxygen,
            predictor, optimal, cvRmse, cvMae, cvR2, kFolds);

        // Store feature importance
        var importance = predictor.GetFeatureImportance();
        foreach (var (feature, value) in importance.Take(5))
        {
            result.correlations[feature] = value;
        }

        Debug.Log(result.summaryText);
        return result;
    }

    /// <summary>
    /// Save regression results to file (delegates to TrialReportGenerator)
    /// </summary>
    public static bool SaveRegressionResultsToFile(RegressionResult result, string saveFolder = "RegressionResults")
    {
        if (result == null || string.IsNullOrEmpty(result.fullDetailsText))
        {
            Debug.LogWarning("No results to save!");
            return false;
        }

        return TrialReportGenerator.SaveToFile(
            result.fullDetailsText, result.analyzedTrials, result.totalTrials, saveFolder);
    }

    /// <summary>
    /// Calculate Pearson correlation coefficient between two arrays
    /// </summary>
    public static float CalculateCorrelation(float[] x, float[] y)
    {
        if (x == null || y == null || x.Length != y.Length || x.Length == 0) return 0f;

        int n = x.Length;
        float sumX = x.Sum();
        float sumY = y.Sum();
        float sumXY = 0f;
        float sumX2 = 0f;
        float sumY2 = 0f;

        for (int i = 0; i < n; i++)
        {
            sumXY += x[i] * y[i];
            sumX2 += x[i] * x[i];
            sumY2 += y[i] * y[i];
        }

        float numerator = n * sumXY - sumX * sumY;
        float denomTerm = (n * sumX2 - sumX * sumX) * (n * sumY2 - sumY * sumY);
        if (denomTerm <= 0f) return 0f;

        float denominator = Mathf.Sqrt(denomTerm);
        return denominator == 0f ? 0f : numerator / denominator;
    }

    /// <summary>
    /// Predict optimal parameters for target oxygen level
    /// </summary>
    public static TrialDataModels.TrialData PredictOptimalParameters(
        List<TrialDataModels.TrialData> trials,
        float targetOxygen = 5.0f)
    {
        if (trials == null || trials.Count < 3)
        {
            Debug.LogError("Need at least 3 trials for ML prediction");
            return null;
        }

        Debug.Log("=== MACHINE LEARNING PREDICTION ===");

        var predictor = new OxygenPredictor();
        bool trained = predictor.TrainModel(trials);

        if (!trained)
        {
            Debug.LogError("Failed to train ML model");
            return null;
        }

        var optimalParams = predictor.FindOptimalParameters(targetOxygen);

        if (optimalParams != null)
        {
            Debug.Log($"Target oxygen: {targetOxygen}%");
            Debug.Log($"Predicted oxygen: {predictor.PredictOxygen(optimalParams):F2}%");
            Debug.Log($"Speed: {optimalParams.speed:F2}");
            Debug.Log($"Vertical Speed: {optimalParams.verticalSpeed:F2}");
            Debug.Log($"O2 Drop/sec: {optimalParams.downHealthPairSec:F2}");
        }

        return optimalParams;
    }

    #endregion

    #region Private Helper Methods

    private static (float[][], float[]) BuildFeatureMatrix(List<TrialDataModels.TrialData> trials)
    {
        int n = trials.Count;
        int k = 8; // 8 features

        float[][] X = new float[n][];
        float[] y = new float[n];

        for (int i = 0; i < n; i++)
        {
            X[i] = new float[k];
            X[i][0] = trials[i].speed;
            X[i][1] = trials[i].verticalSpeed;
            X[i][2] = trials[i].idleUpwardSpeed;
            X[i][3] = trials[i].lifeTime;
            X[i][4] = trials[i].downHealthPairSec;
            X[i][5] = trials[i].removeHealthWithCollide;
            X[i][6] = trials[i].timeBetweenCollides;
            X[i][7] = trials[i].healHealthPoint;

            y[i] = trials[i].finalOxygenRemaining;
        }

        return (X, y);
    }

    #endregion
}
