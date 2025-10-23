using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Verifies that Trial_5_runs_.csv data matches the regression analysis
/// Shows detailed breakdown of oxygen calculations
/// </summary>
public class TrialDataVerifier : MonoBehaviour
{
    [ContextMenu("Verify Trial Data and Show Calculations")]
    public void VerifyTrialData()
    {
        Debug.Log("\n" + new string('=', 80));
        Debug.Log("TRIAL DATA VERIFICATION");
        Debug.Log(new string('=', 80) + "\n");

        // Load data from CSV
        var trials = TrialDataLoader.LoadTrialDataFromCSV();
        
        if (trials == null || trials.Count == 0)
        {
            Debug.LogError("ERROR: No trial data loaded!");
            return;
        }

        Debug.Log($"Loaded {trials.Count} trials from CSV\n");

        // Show manual calculation vs loaded data
        ShowManualCalculations();
        
        Debug.Log(new string('-', 80));
        Debug.Log("LOADED DATA FROM CSV:");
        Debug.Log(new string('-', 80) + "\n");

        foreach (var trial in trials)
        {
            string source = trial.isRandomParameters ? "Random CSV" : "Regular CSV";
            Debug.Log($"Trial {trial.trialId} ({source}):");
            Debug.Log($"  Speed: {trial.speed:F1}, VSpeed: {trial.verticalSpeed:F1}, Idle: {trial.idleUpwardSpeed:F2}");
            Debug.Log($"  O2 Consumption: {trial.downHealthPairSec:F1}/sec, Collision: {trial.removeHealthWithCollide:F1}");
            Debug.Log($"  → Final Oxygen: {trial.finalOxygenRemaining:F2}%");
            Debug.Log("");
        }

        Debug.Log(new string('=', 80) + "\n");
    }

    private void ShowManualCalculations()
    {
        Debug.Log(new string('-', 80));
        Debug.Log("MANUAL CALCULATION FROM CSV FILE:");
        Debug.Log(new string('-', 80) + "\n");

        // Based on Trial_5_runs_.csv structure
        var manualTrials = new Dictionary<int, (string runs, float average)>
        {
            { 1, ("70.4, 80, 80, 78, 80, 80, 80", 0f) },
            { 2, ("40.8, 44, 54.5, 46.5, 36.5, 28.5, 20.5", 0f) },
            { 3, ("EMPTY, 75.8, 53, 62, 56, 50, 47", 0f) },
            { 4, ("EMPTY, 64.6, 83, 82, 61, 72, 62", 0f) },
            { 5, ("EMPTY, EMPTY, EMPTY, 56, 34, 60, 46", 0f) }
        };

        // Calculate averages manually
        var trial1Values = new float[] { 70.4f, 80f, 80f, 78f, 80f, 80f, 80f };
        var trial2Values = new float[] { 40.8f, 44f, 54.5f, 46.5f, 36.5f, 28.5f, 20.5f };
        var trial3Values = new float[] { 75.8f, 53f, 62f, 56f, 50f, 47f };
        var trial4Values = new float[] { 64.6f, 83f, 82f, 61f, 72f, 62f };
        var trial5Values = new float[] { 56f, 34f, 60f, 46f };

        var calculations = new Dictionary<int, (float[] values, float avg)>
        {
            { 1, (trial1Values, trial1Values.Average()) },
            { 2, (trial2Values, trial2Values.Average()) },
            { 3, (trial3Values, trial3Values.Average()) },
            { 4, (trial4Values, trial4Values.Average()) },
            { 5, (trial5Values, trial5Values.Average()) }
        };

        foreach (var (trialId, (values, avg)) in calculations)
        {
            Debug.Log($"Trial {trialId}:");
            Debug.Log($"  Runs: [{string.Join(", ", values.Select(v => v.ToString("F1")))}]");
            Debug.Log($"  Sum: {values.Sum():F1}");
            Debug.Log($"  Count: {values.Length}");
            Debug.Log($"  Average: {values.Sum():F1} / {values.Length} = {avg:F2}%");
            Debug.Log("");
        }
    }

    [ContextMenu("Compare with Regression Results")]
    public void CompareWithRegressionResults()
    {
        Debug.Log("\n" + new string('=', 80));
        Debug.Log("COMPARISON: CSV DATA vs REGRESSION PREDICTIONS");
        Debug.Log(new string('=', 80) + "\n");

        var trials = TrialDataLoader.LoadTrialDataFromCSV();
        
        if (trials == null || trials.Count < 3)
        {
            Debug.LogError("ERROR: Not enough trials for regression");
            return;
        }

        // Train predictor
        var predictor = new OxygenPredictor();
        bool trained = predictor.TrainModel(trials);
        
        if (!trained)
        {
            Debug.LogError("ERROR: Failed to train predictor");
            return;
        }

        Debug.Log("Predictor trained successfully\n");
        Debug.Log("ACTUAL vs PREDICTED:");
        Debug.Log(new string('-', 80));
        Debug.Log($"{"Trial",-7} {"Actual O2",-12} {"Predicted O2",-14} {"Error",-10} {"Status"}");
        Debug.Log(new string('-', 80));

        float totalError = 0f;
        foreach (var trial in trials)
        {
            float actual = trial.finalOxygenRemaining;
            float predicted = predictor.PredictOxygen(trial);
            float error = Mathf.Abs(actual - predicted);
            totalError += error;
            
            string status = error < 5f ? "Good" : 
                           error < 10f ? "OK" : 
                           "Poor";
            
            Debug.Log($"{trial.trialId,-7} {actual,-12:F2}% {predicted,-14:F2}% {error,-10:F2}% {status}");
        }
        
        float avgError = totalError / trials.Count;
        Debug.Log(new string('-', 80));
        Debug.Log($"Average Error: {avgError:F2}%");
        Debug.Log(new string('=', 80) + "\n");

        // Show model quality
        var model = predictor.GetModel();
        if (model != null)
        {
            Debug.Log("MODEL QUALITY:");
            Debug.Log($"  R² Score: {model.rSquared:F4} {GetR2Interpretation(model.rSquared)}");
            Debug.Log($"  RMSE: {model.rootMeanSquaredError:F2}%");
            Debug.Log($"  Avg Error: {avgError:F2}%\n");
        }
    }

    private string GetR2Interpretation(float r2)
    {
        if (r2 > 0.9f) return "(Excellent!)";
        if (r2 > 0.7f) return "(Good)";
        if (r2 > 0.5f) return "(Moderate)";
        if (r2 > 0.3f) return "(Poor)";
        return "(Very Poor)";
    }

    [ContextMenu("Show Feature Importance")]
    public void ShowFeatureImportance()
    {
        var trials = TrialDataLoader.LoadTrialDataFromCSV();
        
        if (trials == null || trials.Count < 3)
        {
            Debug.LogError("Not enough trials");
            return;
        }

        var predictor = new OxygenPredictor { topKFeatures = 4 };
        bool trained = predictor.TrainModel(trials, enableFeatureSelection: true);
        
        if (!trained)
        {
            Debug.LogError("Failed to train");
            return;
        }

        Debug.Log("\n=== FEATURE IMPORTANCE ===");
        Debug.Log("(Which parameters affect oxygen the most?)\n");

        var importance = predictor.GetFeatureImportance();
        int rank = 1;
        foreach (var (feature, value) in importance.OrderByDescending(x => x.Item2))
        {
            string bars = new string('█', Mathf.RoundToInt(value * 50));
            Debug.Log($"{rank}. {feature,-25} {value:F4} {bars}");
            rank++;
        }
        Debug.Log("");
    }
}

