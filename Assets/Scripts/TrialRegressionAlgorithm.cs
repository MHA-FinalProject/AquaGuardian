using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Globalization;
using System;

public class TrialRegressionAlgorithm
{
    
    public class RegressionResult
    {
        public string summaryText;             // Short version for UI display
        public string fullDetailsText;         // Full version for file export
        public Dictionary<string, float> correlations;
        public float averageOxygen;
        public int perfectTrials;
        public int failedTrials;
        public int totalTrials;
        public List<TrialDataModels.TrialData> analyzedTrials;
    }

    // --- New: centralized CSV path + culture + helpers ---
    private const string CsvRelativePath = "Data/Trials/Trial_5_runs_.csv";
    private static readonly CultureInfo CI = CultureInfo.InvariantCulture;

    private static string GetCsvPath()
    {
        return Path.Combine(Application.dataPath, CsvRelativePath);
    }

    private static bool TryParseFloat(string s, out float f)
    {
        f = 0f;
        if (string.IsNullOrWhiteSpace(s)) return false;
        s = s.Trim().Replace("%", string.Empty);
        return float.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, CI, out f);
    }

    private static string[] ReadCsvLines(string path)
    {
        if (!File.Exists(path)) return Array.Empty<string>();
        var text = File.ReadAllText(path);
        // Simple split is fine if file doesn’t contain quoted commas. Matches original behavior.
        return text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    }

    private static int IndexOf(string[] headers, string name)
    {
        for (int i = 0; i < headers.Length; i++)
        {
            if (string.Equals(headers[i].Trim(), name, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    private static List<int> FindOxygenColumns(string[] headers)
    {
        // Prefer o2_run* columns; fall back to columns ending with "oxygen"/"o2"
        var idxs = new List<int>();
        for (int i = 0; i < headers.Length; i++)
        {
            var h = headers[i].Trim().ToLowerInvariant();
            if (h.StartsWith("o2_run"))
                idxs.Add(i);
        }
        if (idxs.Count == 0)
        {
            for (int i = 0; i < headers.Length; i++)
            {
                var h = headers[i].Trim().ToLowerInvariant();
                if (h == "oxygen" || h.EndsWith("oxygen") || h == "o2" || h.EndsWith("o2"))
                    idxs.Add(i);
            }
        }
        return idxs;
    }

    private static List<int> SelectRandomIndices(int maxIndex, int count)
    {
        if (maxIndex <= 0 || count <= 0) return new List<int>();
        if (count > maxIndex) count = maxIndex;

        var all = Enumerable.Range(0, maxIndex).ToList();
        var rng = new System.Random(); // non-deterministic as before
        // Fisher–Yates shuffle
        for (int i = all.Count - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            (all[i], all[j]) = (all[j], all[i]);
        }
        // Return in RANDOM order (don't sort!)
        var picked = all.Take(count).ToList();
        return picked;
    }

    /// <summary>
    /// Load trial data from TrialDataCache (primary method)
    /// Falls back to CSV if cache is empty
    /// Randomly selects 5 different trials instead of taking first 5
    /// </summary>
    public static List<TrialDataModels.TrialData> LoadTrialDataFromCache()
    {
        var list = new List<TrialDataModels.TrialData>();
        try
        {
            var cachedO2 = TrialDataCache.Instance.GetLatestRunOxygenValues();
            if (cachedO2 == null || cachedO2.Count() < 5)
            {
                Debug.LogWarning($"Incomplete cached trial data ({cachedO2?.Count() ?? 0}/5) - trying CSV fallback...");
                return LoadTrialDataFromCSV();
            }

            Debug.Log("Loading data from CACHE (no CSV reading for oxygen).");

            var csvPath = GetCsvPath();
            var lines = ReadCsvLines(csvPath);
            if (lines.Length <= 1)
            {
                Debug.LogError($"CSV empty or missing: {csvPath}");
                return list;
            }

            // Parse header to locate columns robustly
            var header = lines[0].Split(',');
            int colId = IndexOf(header, "trialId");                // fallback to 0 later if missing
            int colSpeed = IndexOf(header, "speed");
            int colVSpeed = IndexOf(header, "verticalSpeed");
            int colIdleUp = IndexOf(header, "idleUpwardSpeed");
            int colLife = IndexOf(header, "lifeTime");
            int colDrop = IndexOf(header, "downHealthPairSec");
            int colCollide = IndexOf(header, "removeHealthWithCollide");
            int colBetween = IndexOf(header, "timeBetweenCollides");
            int colHeal = IndexOf(header, "healHealthPoint");
            int colForce = IndexOf(header, "factorForce");

            // We rely on the assumption that cachedO2 order matches the rows order (as in original code).
            int availableTrials = Math.Min(lines.Length - 1, cachedO2.Count());
            int trialsToSelect = Math.Min(5, availableTrials);
            var selected = SelectRandomIndices(availableTrials, trialsToSelect);

            Debug.Log($"Available trials in cache: {availableTrials}");
            Debug.Log($"Selecting {trialsToSelect} random trials from {availableTrials} available");
            Debug.Log($"Randomly selected trial IDs: [{string.Join(", ", selected.Select(x => x + 1))}]");

            foreach (var localIdx in selected)
            {
                int lineIndex = localIdx + 1;
                if (lineIndex < 1 || lineIndex >= lines.Length) continue;

                var fields = lines[lineIndex].Split(',');
                if (fields.Length < 10) continue;

                // Parse with robust float parsing (CI, strips %)
                float speed = ParseField(fields, colSpeed, 1);
                float vSpeed = ParseField(fields, colVSpeed, 2);
                float idleUp = ParseField(fields, colIdleUp, 3);
                float life   = ParseField(fields, colLife, 4);
                float drop   = ParseField(fields, colDrop, 5);
                float collide= ParseField(fields, colCollide, 6);
                float between= ParseField(fields, colBetween, 7);
                float heal   = ParseField(fields, colHeal, 8);
                float force  = ParseField(fields, colForce, 9);

                int trialId  = ParseIntField(fields, colId, 0);
                if (trialId == 0) trialId = localIdx + 1; // Fallback if trialId column missing

                // Oxygen comes from cache - use trialId to get correct value
                // Cache is 0-indexed, trialId is 1-indexed
                float cached = (trialId > 0 && trialId <= cachedO2.Count()) 
                    ? cachedO2.ElementAt(trialId - 1) 
                    : cachedO2.ElementAt(localIdx);

                var td = new TrialDataModels.TrialData
                {
                    trialId = trialId,
                    speed = speed,
                    verticalSpeed = vSpeed,
                    idleUpwardSpeed = idleUp,
                    lifeTime = life,
                    downHealthPairSec = drop,
                    removeHealthWithCollide = collide,
                    timeBetweenCollides = between,
                    healHealthPoint = heal,
                    factorForce = force,
                    finalOxygenRemaining = cached,
                    completed = cached > 0f
                };

                list.Add(td);
                Debug.Log($"  Trial {td.trialId}: Oxygen={cached:F1}% (from cache)");
            }

            Debug.Log($"Loaded {list.Count} random trials from cache");
            return list;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error loading from cache: {e.Message}");
            return list;
        }
    }

    /// <summary>
    /// Load trial data directly from CSV (fallback method)
    /// Reads the last non-empty o2_runX column for each trial
    /// Randomly selects 5 different trials instead of taking all
    /// </summary>
    public static List<TrialDataModels.TrialData> LoadTrialDataFromCSV()
    {
        var list = new List<TrialDataModels.TrialData>();
        try
        {
            var csvPath = GetCsvPath();
            var lines = ReadCsvLines(csvPath);

            if (lines.Length <= 1)
            {
                Debug.LogError($"Trial_5_runs_.csv not found or empty at: {csvPath}");
                return list;
            }

            Debug.Log($"Reading fresh data from disk: {csvPath}");

            var header = lines[0].Split(',');
            int colId = IndexOf(header, "trialId");
            int colSpeed = IndexOf(header, "speed");
            int colVSpeed = IndexOf(header, "verticalSpeed");
            int colIdleUp = IndexOf(header, "idleUpwardSpeed");
            int colLife = IndexOf(header, "lifeTime");
            int colDrop = IndexOf(header, "downHealthPairSec");
            int colCollide = IndexOf(header, "removeHealthWithCollide");
            int colBetween = IndexOf(header, "timeBetweenCollides");
            int colHeal = IndexOf(header, "healHealthPoint");
            int colForce = IndexOf(header, "factorForce");

            var o2Cols = FindOxygenColumns(header);
            if (o2Cols.Count == 0)
            {
                Debug.LogWarning("No oxygen columns found (o2_run* or oxygen/o2).");
            }

            // Find the LAST oxygen column with data (latest run)
            int lastO2ColWithData = -1;
            if (o2Cols.Count > 0)
            {
                // Check columns from last to first
                for (int c = o2Cols.Count - 1; c >= 0; c--)
                {
                    int col = o2Cols[c];
                    // Check if any trial has data in this column
                    for (int row = 1; row < lines.Length; row++)
                    {
                        var fields = lines[row].Split(',');
                        if (col < fields.Length && !string.IsNullOrWhiteSpace(fields[col]) && fields[col] != "")
                        {
                            lastO2ColWithData = col;
                            Debug.Log($"Using oxygen column: {header[col]} (index {col}) - latest run with data");
                            break;
                        }
                    }
                    if (lastO2ColWithData >= 0) break;
                }
            }

            var allValid = new List<(int lineIndex, TrialDataModels.TrialData data)>();

            Debug.Log($"Scanning CSV for trials with oxygen data from latest run...");
            for (int i = 1; i < lines.Length; i++)
            {
                var fields = lines[i].Split(',');
                if (fields.Length < 10)
                {
                    Debug.LogWarning($"  Line {i}: Skipped (insufficient columns: {fields.Length})");
                    continue;
                }

                int trialId = ParseIntField(fields, colId, 0);
                if (trialId == 0) trialId = i; // Fallback

                // Gather oxygen value ONLY from the latest run column
                float finalOxygen = 0f;
                bool found = false;

                if (lastO2ColWithData >= 0)
                {
                    // Only check the specific column for latest run
                    if (lastO2ColWithData < fields.Length && TryParseFloat(fields[lastO2ColWithData], out var f))
                    {
                        finalOxygen = f;
                        found = true;
                    }
                }
                else if (o2Cols.Count > 0)
                {
                    // Fallback: from last to first oxygen column
                    for (int c = o2Cols.Count - 1; c >= 0; c--)
                    {
                        int col = o2Cols[c];
                        if (col >= 0 && col < fields.Length && TryParseFloat(fields[col], out var f))
                        {
                            finalOxygen = f;
                            found = true;
                            break;
                        }
                    }
                }
                else
                {
                    // Fallback: mimic original column-range sweep (best effort)
                    for (int col = Math.Min(40, fields.Length - 1); col >= 10; col--)
                    {
                        if (TryParseFloat(fields[col], out var f))
                        {
                            finalOxygen = f;
                            found = true;
                            break;
                        }
                    }
                }

                if (!found)
                {
                    Debug.LogWarning($"  Trial {trialId}: Skipped (no oxygen data found)");
                    continue;
                }
                
                Debug.Log($"  Trial {trialId}: INCLUDED (Oxygen={finalOxygen:F1}%)");

                var td = new TrialDataModels.TrialData
                {
                    trialId = ParseIntField(fields, colId, 0),
                    speed = ParseField(fields, colSpeed, 1),
                    verticalSpeed = ParseField(fields, colVSpeed, 2),
                    idleUpwardSpeed = ParseField(fields, colIdleUp, 3),
                    lifeTime = ParseField(fields, colLife, 4),
                    downHealthPairSec = ParseField(fields, colDrop, 5),
                    removeHealthWithCollide = ParseField(fields, colCollide, 6),
                    timeBetweenCollides = ParseField(fields, colBetween, 7),
                    healHealthPoint = ParseField(fields, colHeal, 8),
                    factorForce = ParseField(fields, colForce, 9),
                    finalOxygenRemaining = finalOxygen,
                    completed = finalOxygen > 0f
                };

                allValid.Add((i, td));
            }

            int available = allValid.Count;
            int toPick = Mathf.Min(5, available);
            
            Debug.Log($"\nSummary: Found {available} valid trials out of {lines.Length - 1} total trials in CSV");
            if (available < lines.Length - 1)
            {
                Debug.LogWarning($"  {lines.Length - 1 - available} trials were skipped due to missing oxygen data");
            }
            
            var chosen = SelectRandomIndices(available, toPick);

            Debug.Log($"Selecting {toPick} random trials from {available} available");
            Debug.Log($"Randomly selected trial IDs: [{string.Join(", ", chosen.Select(i => allValid[i].data.trialId))}]");

            foreach (var idx in chosen)
            {
                list.Add(allValid[idx].data);
                Debug.Log($"  Trial {allValid[idx].data.trialId}: Oxygen={allValid[idx].data.finalOxygenRemaining:F1}%");
            }

            Debug.Log($"Loaded {list.Count} random trials from CSV");
            return list;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error loading CSV: {e.Message}");
            return list;
        }
    }

    private static float ParseField(string[] fields, int headerIndex, int fallbackIndex)
    {
        // First try by header index (from header lookup). If missing, fallback to legacy positional index.
        int idx = headerIndex >= 0 ? headerIndex : fallbackIndex;
        if (idx >= 0 && idx < fields.Length && TryParseFloat(fields[idx], out var f))
            return f;
        return 0f;
    }

    private static int ParseIntField(string[] fields, int headerIndex, int fallbackIndex)
    {
        int idx = headerIndex >= 0 ? headerIndex : fallbackIndex;
        if (idx >= 0 && idx < fields.Length)
        {
            var s = fields[idx].Trim();
            if (int.TryParse(s, NumberStyles.Integer, CI, out var v))
                return v;
        }
        return 0;
    }

    /// <summary>
    /// Perform ML regression analysis using Multiple Linear Regression
    /// Returns formatted text results with predictions and model metrics
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

        // Log which trials were selected (in RANDOM order, not sorted)
        var selectedTrialIds = allTrialData.Select(t => t.trialId).ToList();
        var sortedTrialIds = selectedTrialIds.OrderBy(x => x).ToList();
        Debug.Log("=======================================================");
        Debug.Log($"REGRESSION ANALYSIS");
        Debug.Log($"Trials Selected (random order): [{string.Join(", ", selectedTrialIds)}]");
        Debug.Log($"Trials Selected (sorted): [{string.Join(", ", sortedTrialIds)}]");
        Debug.Log($"Total trials analyzed: {allTrialData.Count}");
        Debug.Log("=======================================================");

        var result = new RegressionResult
        {
            correlations = new Dictionary<string, float>(),
            analyzedTrials = new List<TrialDataModels.TrialData>(allTrialData),
            totalTrials = allTrialData.Count
        };

        float totalOxygen = 0f;
        int perfectTrials = 0;
        int failedTrials = 0;

        foreach (var trial in allTrialData)
        {
            totalOxygen += trial.finalOxygenRemaining;
            // Perfect = within ±2.5% around target 5%  => 2.5% - 7.5%
            if (trial.finalOxygenRemaining >= 2.5f && trial.finalOxygenRemaining <= 7.5f)
                perfectTrials++;
            if (trial.finalOxygenRemaining <= 0f)
                failedTrials++;
        }

        result.averageOxygen = totalOxygen / allTrialData.Count;
        result.perfectTrials = perfectTrials;
        result.failedTrials = failedTrials;

        var predictor = new OxygenPredictor
        {
            topKFeatures = 4 // small datasets
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

        var (X, y) = BuildFeatureMatrix(allTrialData);
        int kFolds = Mathf.Min(5, Mathf.Max(2, allTrialData.Count));
        var (cvRmse, cvMae, cvR2) = model.KFoldCV(X, y, kFolds);

        string quality = cvR2 > 0.7f ? "Excellent!" :
                         cvR2 > 0.5f ? "Good" :
                         cvR2 > 0.3f ? "Fair" : "Poor";

        var optimal = predictor.FindOptimalParameters(targetOxygen: 5.0f);

        string summaryText = "REGRESSION ANALYSIS SUMMARY\n";
        summaryText += $"Trials Selected (random): [{string.Join(", ", selectedTrialIds)}]\n";
        summaryText += $"Total Trials: {result.totalTrials}\n";
        summaryText += $"Average Oxygen: {result.averageOxygen.ToString("F1", CI)}%\n";
        summaryText += $"Perfect Trials (2.5-7.5%): {perfectTrials}\n";
        summaryText += $"Failed Trials (0%): {failedTrials}\n";
        summaryText += $"Model Quality : {cvR2.ToString("F3", CI)} ({quality})\n\n";

        summaryText += "Predictions vs Actuals:\n";
        float totalError = 0f;
        for (int i = 0; i < allTrialData.Count; i++)
        {
            float actual = allTrialData[i].finalOxygenRemaining;
            float predicted = predictor.PredictOxygen(allTrialData[i]);
            float error = Mathf.Abs(actual - predicted);
            totalError += error;

            summaryText += $"Trial {allTrialData[i].trialId}: Actual={actual.ToString("F1", CI)}%, " +
                           $"Predicted={predicted.ToString("F1", CI)}% -> Error={error.ToString("F1", CI)}%\n";
        }
        float avgError = totalError / allTrialData.Count;
        summaryText += $"Average Error = {avgError.ToString("F2", CI)}%\n";

        if (optimal != null)
        {
            float predictedOptimal = predictor.PredictOxygen(optimal);
            summaryText += "Recommended Optimal Parameters:\n";
            summaryText += $"Target: 5.0% -> Predicted: {predictedOptimal.ToString("F1", CI)}%\n";
            summaryText += $"Speed: {optimal.speed.ToString("F2", CI)}\n";
            summaryText += $"Vertical Speed: {optimal.verticalSpeed.ToString("F2", CI)}\n";
            summaryText += $"Idle Upward Speed: {optimal.idleUpwardSpeed.ToString("F3", CI)}\n";
            summaryText += $"Life Time: {optimal.lifeTime.ToString("F2", CI)}\n";
            summaryText += $"O2 Drop/sec: {optimal.downHealthPairSec.ToString("F2", CI)}\n";
            summaryText += $"Collision Damage: {optimal.removeHealthWithCollide.ToString("F2", CI)}\n";
            summaryText += $"Time Between Collides: {optimal.timeBetweenCollides.ToString("F2", CI)}\n";
            summaryText += $"Heal Points: {optimal.healHealthPoint.ToString("F2", CI)}\n";
        }

        summaryText += "Full details saved to:\n";
        summaryText += "Assets/Data/RegressionResults/\n";
        summaryText += "RegressionAnalysis_[timestamp].txt\n";

        string fullDetailsText = "REGRESSION ANALYSIS - FULL REPORT\n\n";
        fullDetailsText += $"Trials Selected (random order): [{string.Join(", ", selectedTrialIds)}]\n";
        fullDetailsText += $"Trials Selected (sorted): [{string.Join(", ", sortedTrialIds)}]\n";
        fullDetailsText += $"Total Trials: {result.totalTrials}\n";
        fullDetailsText += $"Average Oxygen: {result.averageOxygen.ToString("F1", CI)}%\n";
        fullDetailsText += $"Perfect Trials (2.5-7.5%): {perfectTrials}\n";
        fullDetailsText += $"Failed Trials (0%): {failedTrials}\n\n";

        fullDetailsText += "MODEL VALIDATION (K-Fold CV)\n";
        fullDetailsText += $"Folds: {kFolds}\n";
        fullDetailsText += $"Cross-Val RMSE: {cvRmse.ToString("F2", CI)}%\n";
        fullDetailsText += $"Cross-Val MAE: {cvMae.ToString("F2", CI)}%\n";
        fullDetailsText += $"Cross-Val R2: {cvR2.ToString("F3", CI)}\n";
        fullDetailsText += $"Model Quality: {quality}\n\n";

        fullDetailsText += "(Actual vs Predicted Oxygen)\n\n";

        for (int i = 0; i < allTrialData.Count; i++)
        {
            float actual = allTrialData[i].finalOxygenRemaining;
            float predicted = predictor.PredictOxygen(allTrialData[i]);
            float error = Mathf.Abs(actual - predicted);

            fullDetailsText += $"Trial {allTrialData[i].trialId}:\n";
            fullDetailsText += $"  Actual: {actual.ToString("F1", CI)}%  Predicted: {predicted.ToString("F1", CI)}%\n";
            fullDetailsText += $"  Error: {error.ToString("F1", CI)}%\n\n";
        }

        fullDetailsText += $"Average Prediction Error: {avgError.ToString("F2", CI)}%\n\n";

        fullDetailsText += "=== FEATURE IMPORTANCE ===\n";
        fullDetailsText += "(Impact on oxygen level)\n\n";

        var importance = predictor.GetFeatureImportance();
        foreach (var (feature, value) in importance.Take(5))
        {
            int barLen = Mathf.Clamp(Mathf.RoundToInt(value * 20f), 0, 60);
            string bar = new string('#', barLen);
            fullDetailsText += $"{feature}:\n  {value.ToString("F4", CI)} {bar}\n";
            // Keep compatibility field
            result.correlations[feature] = value;
        }

        fullDetailsText += "OPTIMAL PARAMETER RECOMMENDATION\n\n";
        fullDetailsText += "Target: 5.0% oxygen remaining\n\n";

        if (optimal != null)
        {
            float predictedOptimal = predictor.PredictOxygen(optimal);

            fullDetailsText += $"Predicted Oxygen: {predictedOptimal.ToString("F2", CI)}%\n\n";
            fullDetailsText += "Recommended Parameters:\n";
            fullDetailsText += $"  Speed: {optimal.speed.ToString("F2", CI)}\n";
            fullDetailsText += $"  Vertical Speed: {optimal.verticalSpeed.ToString("F2", CI)}\n";
            fullDetailsText += $"  Idle Upward Speed: {optimal.idleUpwardSpeed.ToString("F3", CI)}\n";
            fullDetailsText += $"  Life Time: {optimal.lifeTime.ToString("F2", CI)}\n";
            fullDetailsText += $"  O2 Drop/sec: {optimal.downHealthPairSec.ToString("F2", CI)}\n";
            fullDetailsText += $"  Collision Damage: {optimal.removeHealthWithCollide.ToString("F2", CI)}\n";
            fullDetailsText += $"  Time Between Collides: {optimal.timeBetweenCollides.ToString("F2", CI)}\n";
            fullDetailsText += $"  Heal Points: {optimal.healHealthPoint.ToString("F2", CI)}\n";
        }
        else
        {
            fullDetailsText += "Could not find optimal parameters\n";
        }

        result.summaryText = summaryText;
        result.fullDetailsText = fullDetailsText;

        Debug.Log(summaryText);
        return result;
    }

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

    /// <summary>
    /// Calculate Pearson correlation coefficient between two arrays
    /// Returns value between -1 and +1
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

    public static bool SaveRegressionResultsToFile(RegressionResult result, string saveFolder = "RegressionResults")
    {
        if (result == null || string.IsNullOrEmpty(result.fullDetailsText))
        {
            Debug.LogWarning("No results to save!");
            return false;
        }

        try
        {
            string dataPath = Path.Combine(Application.dataPath, "Data");
            string savePath = Path.Combine(dataPath, saveFolder);

            if (!Directory.Exists(savePath))
                Directory.CreateDirectory(savePath);

            string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string fileName = $"RegressionAnalysis_{timestamp}.txt";
            string fullPath = Path.Combine(savePath, fileName);

            string fileContent = "=====================================\n";
            fileContent += "REGRESSION ANALYSIS - FULL REPORT\n";
            fileContent += "=====================================\n";
            fileContent += $"Generated: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\n";
            fileContent += $"Trials analyzed: {result.totalTrials}\n";
            fileContent += "=====================================\n\n";
            fileContent += result.fullDetailsText;
            fileContent += "\n\n=====================================\n";
            fileContent += "RAW TRIAL DATA:\n";

            if (result.analyzedTrials != null)
            {
                foreach (var trial in result.analyzedTrials)
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
            // Debug.Log($"Results saved: {fullPath}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Save failed: {e.Message}");
            return false;
        }
    }

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
            Debug.Log($"Predicted oxygen: {predictor.PredictOxygen(optimalParams).ToString("F2", CI)}%");
            Debug.Log($"Speed: {optimalParams.speed.ToString("F2", CI)}");
            Debug.Log($"Vertical Speed: {optimalParams.verticalSpeed.ToString("F2", CI)}");
            Debug.Log($"O2 Drop/sec: {optimalParams.downHealthPairSec.ToString("F2", CI)}");
        }

        return optimalParams;
    }
}
