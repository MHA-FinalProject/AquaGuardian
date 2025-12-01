using UnityEngine;
using System.Collections.Generic;
using System.Linq;


/**
* Handler for Python-trained regression models
* Manages loading, switching, and using Python-trained models (ElasticNet, Ridge, etc.)
* alongside Unity's built-in model. Loads patient-specific models from Python server,
* performs predictions, optimization, and generates reports for hybrid ML workflows.
* See also: PythonRegressionModel, PythonRegressionServerClient, TrialRegressionAlgorithm, OxygenPredictor
*/
public static class PythonRegressionHandler
{
    private static PythonRegressionModel pythonModel = null;
    
    // Flag to control whether to use Python model or Unity model
    public static bool usePythonModel = false;
    
    // Flag to enable optimization comparison (Python Gradient vs C# RandomSweep)
    // If false, only C# RandomSweep is used (more reliable)
    public static bool enableOptimizationComparison = false;

    // Check if Python model is loaded and active
    public static bool IsModelReady => usePythonModel && pythonModel != null && pythonModel.IsLoaded;

    // Load Python-trained model from JSON
    // Enables hybrid mode - can use either Unity or Python model
    public static bool LoadPythonModel(string jsonPath)
    {
        pythonModel = new PythonRegressionModel();
        bool success = pythonModel.LoadFromJSON(jsonPath);
        if (success)
        {
            usePythonModel = true;
        }
        else
        {
            usePythonModel = false;
            pythonModel = null;
        }
        return success;
    }

    // Get current model type description
    public static string GetCurrentModelType()
    {
        if (usePythonModel && pythonModel != null && pythonModel.IsLoaded)
            return $"Python ({pythonModel.Model.model_type})";
        return "Unity (Ridge)";
    }

 

    // Perform regression analysis using Python-trained model
    public static TrialDataModels.RegressionResult PerformPythonRegressionAnalysis(
        List<TrialDataModels.TrialData> allTrialData,
        float targetOxygen)
    {
        if (!IsModelReady)
        {
            Debug.LogError("[PythonHandler] Cannot perform analysis - model not ready!");
            return new TrialDataModels.RegressionResult
            {
                summaryText = "ERROR: Python model not loaded",
                fullDetailsText = "ERROR: Python model not loaded",
                correlations = new Dictionary<string, float>()
            };
        }

        var result = new TrialDataModels.RegressionResult
        {
            correlations = new Dictionary<string, float>()
        };

        RegressionUtilities.CalculateTrialStatistics(allTrialData, result);

        // Calculate prediction error on training data
        float avgError = allTrialData.Average(t => 
            Mathf.Abs(t.finalOxygenRemaining - pythonModel.PredictOxygen(t)));

        // Get feature importance from Python model
        var allImportance = pythonModel.GetFeatureImportance();
        
        // Store top 5 in correlations
        foreach (var item in allImportance.Take(5))
        {
            result.correlations[item.feature] = item.importance;
        }

        // Optimization using Python model
        bool anyAmadeoTrials = allTrialData.Any(t => t.IsAmadeoMode > 0.5f);
        
        var bannedFeatures = new HashSet<string> { "EffectiveDrainRate" };
        if (!anyAmadeoTrials)
            bannedFeatures.Add("factorForce");

        var filteredImportance = allImportance
            .Where(t => !bannedFeatures.Contains(t.feature))
            .ToArray();

        int[] topIndices = RegressionUtilities.BuildOptimizationIndices(
            filteredImportance, TrialDataModels.FeatureNames);

        var (ranges, baseline) = PrepareOptimizationBaseline(allTrialData, targetOxygen);

        TrialDataModels.TrialData solution;
        float error;
        TrialDataModels.TrialData pythonGradientSolution = null;
        float pythonGradientError = float.MaxValue;
        TrialDataModels.TrialData sweepSolution = null;
        float sweepError = float.MaxValue;

        // ALWAYS run both methods and choose the best one
        
        // Method 1: Python Gradient (optimized for sparse coefficients)
            pythonGradientSolution = OptimizeWithPythonModel(baseline, topIndices, targetOxygen, ranges, anyAmadeoTrials);
            pythonGradientError = pythonGradientSolution != null 
                ? Mathf.Abs(pythonModel.PredictOxygen(pythonGradientSolution) - targetOxygen)
                : float.MaxValue;

        // Method 2: RandomSweep (more samples if gradient failed or had high error)
        int sweepSamples = pythonGradientError > 10f ? 800 : (pythonGradientError > 5f ? 500 : 300);
        (sweepSolution, sweepError) = DifficultyParameterSolver.RandomSweepOptimizer(
                d => pythonModel.PredictOxygenUnclamped(d),
                ranges, targetOxygen, anyAmadeoTrials, samples: sweepSamples);

            // Choose best solution
        bool gradientValid = pythonGradientSolution != null && !float.IsNaN(pythonGradientError);
        bool sweepValid = sweepSolution != null && !float.IsNaN(sweepError);
        string selectedMethod = "Unknown";

        if (gradientValid && sweepValid)
        {
            // Both valid - choose the better one
            if (pythonGradientError <= sweepError)
            {
                solution = pythonGradientSolution;
                error = pythonGradientError;
                selectedMethod = "Python Gradient";
            }
            else
            {
                solution = sweepSolution;
                error = sweepError;
                selectedMethod = "RandomSweep";
            }
        }
        else if (gradientValid)
        {
            solution = pythonGradientSolution;
            error = pythonGradientError;
            selectedMethod = "Python Gradient";
        }
        else if (sweepValid)
        {
            solution = sweepSolution;
            error = sweepError;
            selectedMethod = "RandomSweep";
        }
        else
        {
            // Both failed - use baseline
            solution = baseline;
            error = Mathf.Abs(pythonModel.PredictOxygenUnclamped(baseline) - targetOxygen);
            selectedMethod = "Baseline Fallback";
        }


        result.optimizedSolution = solution;
        result.optimizedSolutionError = error;

        // Generate reports - always pass selectedMethod
        if (enableOptimizationComparison && pythonGradientSolution != null && sweepSolution != null)
        {
            result.summaryText = TrialReportGenerator.GeneratePythonModelSummary(
                allTrialData, avgError, pythonModel, solution, error, targetOxygen, allImportance,
                pythonGradientSolution, pythonGradientError, sweepSolution, sweepError);
            result.fullDetailsText = TrialReportGenerator.GeneratePythonModelFullReport(
                allTrialData, avgError, result.averageOxygen, pythonModel,
                allImportance, solution, error, targetOxygen,
                pythonGradientSolution, pythonGradientError, sweepSolution, sweepError, selectedMethod);
        }
        else
        {
            // No comparison details, but still pass the selected method
            result.summaryText = TrialReportGenerator.GeneratePythonModelSummary(
                allTrialData, avgError, pythonModel, solution, error, targetOxygen, allImportance);
            result.fullDetailsText = TrialReportGenerator.GeneratePythonModelFullReport(
                allTrialData, avgError, result.averageOxygen, pythonModel,
                allImportance, solution, error, targetOxygen,
                null, float.MaxValue, null, float.MaxValue, selectedMethod);
        }

        return result;
    }

    // Optimize parameters using Python model coefficients (gradient-based, optimized for sparse coefficients)
    private static TrialDataModels.TrialData OptimizeWithPythonModel(
        TrialDataModels.TrialData baseline,
        int[] topIndices,
        float targetOxygen,
        TrialDataModels.ParameterRanges ranges,
        bool anyAmadeoTrials)
    {
        if (pythonModel == null || !pythonModel.IsLoaded)
            return null;

        var model = pythonModel.Model;
        if (model.betas == null || model.betas.Length == 0)
            return null;

        // Use gradient descent based on Python model coefficients
        var solution = baseline;
        float currentO2 = pythonModel.PredictOxygenUnclamped(solution);
        float error = Mathf.Abs(currentO2 - targetOxygen);
        
        if (error < 0.5f)
            return solution; // Already close enough

        // If error is too large, gradient descent may not work well - return null to use RandomSweep
        if (error > 50f)
            return null;

        // Adaptive learning rate - start smaller and use adaptive decay
        float initialLearningRate = error > 20f ? 0.1f : (error > 10f ? 0.05f : 0.02f);
        float learningRate = initialLearningRate;
        int maxIterations = error > 20f ? 200 : (error > 10f ? 150 : 100);
        float bestError = error;
        TrialDataModels.TrialData bestSolution = solution;

        string[] featureNames = TrialDataModels.FeatureNames;
        
        // Count non-zero coefficients (Python often has sparse betas from ElasticNet/Lasso)
        int nonZeroCount = 0;
        float sumSquaredCoeffs = 0f;
        foreach (int idx in topIndices)
        {
            if (idx >= 0 && idx < model.betas.Length)
            {
                float coeff = model.betas[idx];
                if (Mathf.Abs(coeff) > 1e-9f)
                {
                    nonZeroCount++;
                    sumSquaredCoeffs += coeff * coeff;
                }
            }
        }

        if (sumSquaredCoeffs < 1e-9f || nonZeroCount < 2)
            return null; // Not enough meaningful coefficients for gradient descent
        
        for (int iter = 0; iter < maxIterations && error > 0.2f; iter++)
        {
            float delta = currentO2 - targetOxygen;
            if (Mathf.Abs(delta) < 0.2f)
                break;

            // Adaptive learning rate decay
            if (iter > 0 && iter % 20 == 0)
            {
                learningRate *= 0.9f; // Decay learning rate
            }

            // Update each top feature
            bool anyUpdate = false;
            foreach (int idx in topIndices)
            {
                if (idx < 0 || idx >= featureNames.Length || idx >= model.betas.Length)
                    continue;

                float coeff = model.betas[idx];
                if (Mathf.Abs(coeff) < 1e-9f)
                    continue;

                // Get current value and range
                float currentVal = ParameterHelper.Get(solution, idx);
                Vector2 range = GetRangeForIndex(ranges, idx);
                
                // Calculate normalized coefficient contribution
                float normalizedCoeff = coeff / Mathf.Sqrt(sumSquaredCoeffs);
                
                // Update: move opposite to error direction, scaled by coefficient
                // Use smaller step size for more stability
                float update = -delta * normalizedCoeff * learningRate * 0.5f;
                float newVal = Mathf.Clamp(currentVal + update, range.x, range.y);
                
                if (Mathf.Abs(newVal - currentVal) > 1e-6f)
                {
                    ParameterHelper.Set(ref solution, idx, newVal);
                    anyUpdate = true;
                }
            }

            if (!anyUpdate)
                break; // No more updates possible

            currentO2 = pythonModel.PredictOxygenUnclamped(solution);
            error = Mathf.Abs(currentO2 - targetOxygen);
            
            // Track best solution
            if (error < bestError)
            {
                bestError = error;
                bestSolution = solution;
            }
            
            // Early stopping if error increases significantly
            if (iter > 10 && error > bestError * 2f)
            {
                solution = bestSolution;
                break;
            }
        }

        // Only return if error is reasonable
        if (bestError > 5f)
            return null; // Gradient descent failed, let RandomSweep handle it
            
        return bestSolution;
    }

    // Get parameter range for feature index
    private static Vector2 GetRangeForIndex(TrialDataModels.ParameterRanges ranges, int idx)
    {
        switch (idx)
        {
            case 0: return ranges.speedRange;
            case 1: return ranges.verticalSpeedRange;
            case 2: return ranges.idleUpwardSpeedRange;
            case 3: return ranges.lifeTimeRange;
            case 4: return ranges.RemoveHealthEveryLifeTimeRange;
            case 5: return ranges.removeHealthWithCollideRange;
            case 6: return ranges.timeBetweenCollidesRange;
            case 7: return ranges.healHealthPointRange;
            case 8: return ranges.factorForceRange;
            default: return new Vector2(0f, 100f);
        }
    }

    // Prepare optimization baseline from trial data
    private static (TrialDataModels.ParameterRanges ranges, TrialDataModels.TrialData baseline)
        PrepareOptimizationBaseline(List<TrialDataModels.TrialData> trials, float targetOxygen)
    {
        var ranges = new TrialDataModels.ParameterRanges();
        ranges = RegressionUtilities.ConstrainRangesToObserved(ranges, trials, targetOxygen);
        var baseline = FeatureExtractor.GetPatientBaseline(trials, ranges, useMedian: true);
        baseline.IsAmadeoMode = 0f;
        return (ranges, baseline);
    }

}

