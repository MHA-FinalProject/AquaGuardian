using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

/** 
    * TrialRegressionAlgorithm
    * 
    * Performs machine learning regression analysis on trial data to predict oxygen levels
    * and identify key parameters affecting performance. 

*/
public class TrialRegressionAlgorithm
{
    // Default target oxygen level and tolerance for "perfect" trials
    private const float DEFAULT_TARGET_O2 = 10f;          // Default target: 10% oxygen (realistic based on training data)
    private const float TARGET_TOLERANCE = 5f;    // -+5% = [5%, 15%]
    
    
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
        public TrialDataModels.TrialData optimizedSolution;  // Gradient Descent solution
        public float optimizedSolutionError;  // Error of optimized solution
    }

    #region Public API



    public static RegressionResult PerformRegressionAnalysis(List<TrialDataModels.TrialData> allTrialData, float targetOxygen = DEFAULT_TARGET_O2)
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
            
            // Perfect: within tolerance of target (5% ± 2.5%)
            if (Mathf.Abs(trial.finalOxygenRemaining - targetOxygen) <= TARGET_TOLERANCE)
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
            topKFeatures = 10  // Use ALL 10 features for best accuracy (changed from 4)
            // Using all features improves R² and reduces error
        };
        // Train model with feature selection DISABLED to use all features
        bool trained = predictor.TrainModel(allTrialData, enableFeatureSelection: false);
        if (!trained)
        {
            string errorMsg = "ERROR: Failed to train ML model\nNot enough variance in data";
            result.summaryText = errorMsg;
            result.fullDetailsText = errorMsg;
            return result;
        }

        var model = predictor.GetModel();

        // K-Fold Cross Validation
        // Skip CV if we don't have enough samples (need at least 10 for reliable CV with feature selection)
        float cvRmse, cvMae, cvR2;
        int kFolds = 5; // Default value for reporting
        
        if (allTrialData.Count < 10)
        {
            Debug.LogWarning($"[CV] Skipped: Only {allTrialData.Count} samples (need 10+ for reliable CV)");
            cvRmse = cvMae = cvR2 = float.NaN;
            kFolds = 0; // Indicate CV was not performed
        }
        else
        {
            var (X, y) = FeatureExtractor.ExtractFeaturesAndTargets(allTrialData);
            kFolds = Mathf.Clamp(allTrialData.Count / 3, 2, 5);
            (cvRmse, cvMae, cvR2) = model.KFoldCV(X, y, kFolds);
        }

        // Find optimal parameters
        var optimal = predictor.FindOptimalParameters(targetOxygen: targetOxygen);

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

        // Use centralized feature names from FeatureExtractor
        string[] featureNames = FeatureExtractor.FeatureNames;
        
        Debug.Log($"[Regression] Using {FeatureExtractor.FeatureCount} effective features (calculated per-sample based on IsAmadeoMode)");

        // Check if any trials use Amadeo mode - if not, exclude factorForce from optimization
        bool anyAmadeoTrials = allTrialData.Any(t => t.IsAmadeoMode > 0.5f);
        if (!anyAmadeoTrials)
        {
            Debug.Log("[Regression] All trials use keyboard → factorForce will be 0, excluding from optimization");
        }

        // Analytical solution: Solve regression equation for target O2 BEFORE generating reports
        
        // Method 1: Single-parameter analytical solution (fast, good for understanding)
        var analyticalSolution = DifficultyParameterSolver.SolveForTargetDifficulty(
            model, targetOxygen, null, featureNames);
        
        // Method 2: ADVANCED MINIMAL-CHANGE SOLVER (most accurate, works in normalized space!)
        // Uses closed-form solution + projected gradient for precise targeting
        var optimizationReport = new DifficultyParameterSolver.OptimizationReport();
        TrialDataModels.TrialData optimizedSolution = null;
        float optimizedError = float.MaxValue;
        
        try
        {
            // Get feature indices for optimization
            var featureImportance = predictor.GetFeatureImportance();
            
            // Filter out factorForce if all trials are keyboard-only (since factorForceEff will always be 0)
            var featuresForOptimization = featureImportance.ToList();
            if (!anyAmadeoTrials)
            {
                // Remove factorForce from optimization list (it will be 0 anyway)
                featuresForOptimization = featuresForOptimization
                    .Where(f => f.Item1 != "factorForce")
                    .ToList();
                Debug.Log($"[Regression] Excluded factorForce from optimization (keyboard-only). Optimizing {featuresForOptimization.Count} features.");
            }
            
            var topFeatures = featuresForOptimization.Take(featuresForOptimization.Count).ToList();
            int[] topIndices = topFeatures.Select(f => System.Array.IndexOf(featureNames, f.Item1)).ToArray();
            
            // Get base parameters using centralized function (uses median for robustness)
            var ranges = new TrialDataModels.ParameterRanges();
            var baseParams = FeatureExtractor.GetPatientBaseline(allTrialData, ranges, useMedian: false);  // Use average for compatibility
            baseParams.IsAmadeoMode = 0f;  // Default to keyboard mode for optimization
            
            // Use the advanced solver
            optimizedSolution = DifficultyParameterSolver.SolveForTargetOxygen(
                model,
                baseParams,
                topIndices,
                targetOxygen,
                ranges,
                d => predictor.PredictOxygen(d),
                out optimizedError);
            
            // Build optimization report manually
           
            optimizationReport.iterationLog.Add($"Target O2: {targetOxygen:F2}%\n");
            optimizationReport.iterationLog.Add($"Top features optimized: {string.Join(", ", topFeatures.Select(f => f.Item1))}\n\n");
            optimizationReport.iterationLog.Add("OPTIMIZED PARAMETERS:\n");
            for (int i = 0; i < featureNames.Length; i++)
            {
                float val = 0f;
                // Calculate effective feature values for display
                // factorForceEff will be 0 if keyboard mode (IsAmadeoMode=0) or if no Amadeo trials
                bool isAmadeo = optimizedSolution.IsAmadeoMode > 0.5f && anyAmadeoTrials;
                float factorForceEff = isAmadeo ? optimizedSolution.factorForce : 0f;
                
                switch (i)
                {
                    case 0: val = optimizedSolution.speed; break;
                    case 1: val = optimizedSolution.verticalSpeed; break;
                    case 2: val = optimizedSolution.idleUpwardSpeed; break;
                    case 3: val = optimizedSolution.lifeTime; break;
                    case 4: val = optimizedSolution.RemoveHealthEveryLifeTime; break;
                    case 5: val = optimizedSolution.removeHealthWithCollide; break;
                    case 6: val = optimizedSolution.timeBetweenCollides; break;
                    case 7: val = optimizedSolution.healHealthPoint; break;
                    case 8: val = factorForceEff; break;  // factorForce (0 if keyboard)
                    case 9: val = optimizedSolution.EffectiveDrainRate; break;
                }
                optimizationReport.iterationLog.Add($"  {featureNames[i],-25} = {val:F2}\n");
            }
            float finalO2 = predictor.PredictOxygen(optimizedSolution);
            optimizationReport.iterationLog.Add($"\nRESULT:\n");
            optimizationReport.iterationLog.Add($"    Predicted O2: {finalO2:F2}%\n");
            optimizationReport.iterationLog.Add($"    Target O2:    {targetOxygen:F2}%\n");
            optimizationReport.iterationLog.Add($"    Error:        {optimizedError:F3}%\n");
            optimizationReport.finalReport = string.Join("", optimizationReport.iterationLog);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[TrialRegression] Advanced solver failed: {e.Message}. Falling back to gradient descent.");
            
            // Fallback: Use traditional gradient descent with ALL features
            optimizationReport = new DifficultyParameterSolver.OptimizationReport();
            optimizedSolution = DifficultyParameterSolver.SolveForTargetDifficultyMulti(
                model, targetOxygen, 9, null, featureNames, optimizationReport);  // Changed: 5→9 (use ALL features)
            optimizedError = optimizedSolution != null ? 
                Mathf.Abs(predictor.PredictOxygen(optimizedSolution) - targetOxygen) : float.NaN;
        }
        
        // Store optimized solution in result
        result.optimizedSolution = optimizedSolution;
        result.optimizedSolutionError = optimizedError;

        // Print coefficients to console in real-time (compact format)
        PrintCoefficientsRealTime(predictor, optimizedSolution, optimizedError, targetOxygen);

        // Generate reports using TrialReportGenerator (now with optimized solution and detailed log)
        result.summaryText = TrialReportGenerator.GenerateSummaryReport(
            allTrialData, avgError, predictor, optimal, optimizedSolution, result.optimizedSolutionError, targetOxygen);

        result.fullDetailsText = TrialReportGenerator.GenerateFullReport(
            allTrialData, avgError, perfectTrials, failedTrials, result.averageOxygen,
            predictor, optimal, cvRmse, cvMae, cvR2, kFolds, optimizedSolution, result.optimizedSolutionError,
            optimizationReport?.finalReport, targetOxygen);

        // Store feature importance
        var importance = predictor.GetFeatureImportance();
        foreach (var (feature, value) in importance.Take(5))
        {
            result.correlations[feature] = value;
        }
        
        return result;
    }

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

    // Predict optimal parameters for target oxygen level
    public static TrialDataModels.TrialData PredictOptimalParameters(
        List<TrialDataModels.TrialData> trials,
        float targetOxygen = 10.0f)
    {
        if (trials == null || trials.Count < 3)
        {
            Debug.LogError("Need at least 3 trials for ML prediction");
            return null;
        }

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
            // Results available in reports
        }

        return optimalParams;
    }

    #endregion

    #region Private Helper Methods

    /**
     * Prints regression coefficients and optimized parameters in real-time (compact format)
     */
    private static void PrintCoefficientsRealTime(OxygenPredictor predictor, TrialDataModels.TrialData optimized, float error, float target)
    {
        var model = predictor?.GetModel();
        if (model == null || model.coefficients == null) return;

        System.Text.StringBuilder output = new System.Text.StringBuilder();
        
        // Header
        output.Append("=== REGRESSION COEFFICIENTS (Real-Time) ===\n");
        
        // Coefficients (compact, no spaces)
        output.Append($"Intercept={model.coefficients[0]:F4} ");
        var featureNames = model.featureNames;
        for (int i = 1; i < model.coefficients.Length; i++)
        {
            string name = (featureNames != null && i - 1 < featureNames.Length) ? featureNames[i - 1] : $"F{i}";
            output.Append($"{name}={model.coefficients[i]:F4} ");
        }
        output.Append("\n");
        
        // Feature importance (compact)
        var importance = predictor.GetFeatureImportance();
        if (importance != null && importance.Length > 0)
        {
            output.Append("Importance: ");
            foreach (var (feature, value) in importance)
            {
                output.Append($"{feature}={value:F4} ");
            }
            output.Append("\n");
        }
        
        // Optimized parameters (compact) - show effective values
        if (optimized != null)
        {
            float predictedO2 = predictor.PredictOxygen(optimized);
            
            // Calculate effective values for display (same as used in prediction)
            bool isAmadeo = optimized.IsAmadeoMode > 0.5f;
            float factorForceEff = isAmadeo ? optimized.factorForce : 0f;
            float idleUpwardEffective = isAmadeo ? (optimized.idleUpwardSpeed * 0.5f) : optimized.idleUpwardSpeed;
            
            output.Append($"Target={target:F1}% Predicted={predictedO2:F2}% Error={error:F3}%\n");
            output.Append($"speed={optimized.speed:F2} verticalSpeed={optimized.verticalSpeed:F2} idleUpwardSpeed={idleUpwardEffective:F2} lifeTime={optimized.lifeTime:F2} RemoveHealthEveryLifeTime={optimized.RemoveHealthEveryLifeTime:F2} removeHealthWithCollide={optimized.removeHealthWithCollide:F2} timeBetweenCollides={optimized.timeBetweenCollides:F2} healHealthPoint={optimized.healHealthPoint:F2} factorForce={factorForceEff:F2}\n");
        }
        
        output.Append("==========================================");
        
        Debug.Log(output.ToString());
    }

    // BuildFeatureMatrix removed - use FeatureExtractor.ExtractFeaturesAndTargets instead

    #endregion
}
