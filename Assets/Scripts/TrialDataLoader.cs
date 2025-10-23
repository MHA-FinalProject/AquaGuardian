using UnityEngine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

public static class TrialDataLoader
{
    private const string CsvRelativePath = "Data/Trials/Trial_5_runs_.csv";
    private const string RandomCsvRelativePath = "Data/Trials/Trial_Random_Parameters.csv";
    private static readonly CultureInfo CI = CultureInfo.InvariantCulture;

    private static string GetCsvPath() => Path.Combine(Application.dataPath, CsvRelativePath);
    private static string GetRandomCsvPath() => Path.Combine(Application.dataPath, RandomCsvRelativePath);

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
        return text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    }

    private static int IndexOf(string[] headers, string name)
    {
        for (int i = 0; i < headers.Length; i++)
            if (string.Equals(headers[i].Trim(), name, StringComparison.OrdinalIgnoreCase))
                return i;
        return -1;
    }

    private static List<int> FindOxygenColumns(string[] headers)
    {
        var idxs = new List<int>();
        for (int i = 0; i < headers.Length; i++)
        {
            var h = headers[i].Trim().ToLowerInvariant();
            if (h.StartsWith("o2_run") || h == "o2_result")
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

    private static float ParseField(string[] fields, int headerIndex, int fallbackIndex)
    {
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

            var csvPath = GetCsvPath();
            var lines = ReadCsvLines(csvPath);
            if (lines.Length <= 1) return list;

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

            int availableTrials = Math.Min(lines.Length - 1, cachedO2.Count());
            int trialsToSelect = Math.Min(5, availableTrials);

            for (int localIdx = 0; localIdx < trialsToSelect; localIdx++)
            {
                int lineIndex = localIdx + 1;
                if (lineIndex >= lines.Length) continue;

                var fields = lines[lineIndex].Split(',');
                if (fields.Length < 10) continue;

                int trialId = ParseIntField(fields, colId, 0);
                if (trialId == 0) trialId = localIdx + 1;

                float cached = (trialId > 0 && trialId <= cachedO2.Count())
                    ? cachedO2.ElementAt(trialId - 1)
                    : cachedO2.ElementAt(localIdx);

                list.Add(new TrialDataModels.TrialData
                {
                    trialId = trialId,
                    speed = ParseField(fields, colSpeed, 1),
                    verticalSpeed = ParseField(fields, colVSpeed, 2),
                    idleUpwardSpeed = ParseField(fields, colIdleUp, 3),
                    lifeTime = ParseField(fields, colLife, 4),
                    downHealthPairSec = ParseField(fields, colDrop, 5),
                    removeHealthWithCollide = ParseField(fields, colCollide, 6),
                    timeBetweenCollides = ParseField(fields, colBetween, 7),
                    healHealthPoint = ParseField(fields, colHeal, 8),
                    factorForce = ParseField(fields, colForce, 9),
                    finalOxygenRemaining = cached,
                    completed = cached > 0f,
                    isRandomParameters = false
                });
            }
            return list;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error loading from cache: {e.Message}");
            return list;
        }
    }

    public static List<TrialDataModels.TrialData> LoadTrialDataFromCSV()
    {
        var list = new List<TrialDataModels.TrialData>();
        try
        {
            var regularTrials = LoadTrialsFromFile(GetCsvPath(), "Trial_5_runs_.csv", false);
            var randomTrials = LoadTrialsFromFile(GetRandomCsvPath(), "Trial_Random_Parameters.csv", true);

            var trialDict = new Dictionary<int, TrialDataModels.TrialData>();
            
            // First, add all regular trials
            foreach (var (_, data) in regularTrials)
                trialDict[data.trialId] = data;

            // Only add random trials if:
            // 1. The trial doesn't exist in regular CSV, OR
            // 2. The regular trial has no oxygen data (finalOxygenRemaining == 0)
            int replaced = 0;
            foreach (var (_, data) in randomTrials)
            {
                bool shouldUseRandom = false;
                
                if (!trialDict.ContainsKey(data.trialId))
                {
                    // Trial doesn't exist in regular CSV - use random
                    shouldUseRandom = true;
                    Debug.Log($"[Data Source] Trial {data.trialId}: Using Random CSV (not in regular CSV)");
                }
                else if (trialDict[data.trialId].finalOxygenRemaining == 0f)
                {
                    // Trial exists but has no data - replace with random
                    shouldUseRandom = true;
                    replaced++;
                    Debug.Log($"[Data Source] Trial {data.trialId}: Replacing empty regular trial with Random CSV");
                }
                else
                {
                    // Trial has valid data - keep regular
                    Debug.Log($"[Data Source] Trial {data.trialId}: Using Regular CSV (has valid O2 data: {trialDict[data.trialId].finalOxygenRemaining}%)");
                }
                
                if (shouldUseRandom)
                    trialDict[data.trialId] = data;
            }

            if (replaced > 0)
                Debug.Log($"Replaced {replaced} empty regular trials with random trial data");

            var sortedTrials = trialDict.OrderBy(kvp => kvp.Key).Take(5).Select(kvp => kvp.Value).ToList();
            
            // Log final data sources
            int regularCount = sortedTrials.Count(t => !t.isRandomParameters);
            int randomCount = sortedTrials.Count(t => t.isRandomParameters);
            Debug.Log($"[Final Data Mix] Regular: {regularCount}, Random: {randomCount}");
            
            return sortedTrials;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error loading CSV: {e.Message}");
            return list;
        }
    }

    private static List<(int lineIndex, TrialDataModels.TrialData data)> LoadTrialsFromFile(string csvPath, string fileName, bool isRandom)
    {
        var allValid = new List<(int lineIndex, TrialDataModels.TrialData data)>();
        try
        {
            if (!File.Exists(csvPath)) return allValid;

            var lines = ReadCsvLines(csvPath);
            if (lines.Length <= 1) return allValid;

            var header = lines[0].Split(',');
            int colId = IndexOf(header, "trialId");
            if (colId < 0) colId = IndexOf(header, "trial_id");
            
            var o2Cols = FindOxygenColumns(header);
            if (fileName.Contains("Random") || fileName.Contains("random"))
            {
                int o2ResultCol = IndexOf(header, "o2_result");
                if (o2ResultCol >= 0 && !o2Cols.Contains(o2ResultCol))
                    o2Cols.Add(o2ResultCol);
            }
            if (o2Cols.Count == 0) return allValid;

            for (int i = 1; i < lines.Length; i++)
            {
                var fields = lines[i].Split(',');
                if (fields.Length < 10) continue;

                int trialId = ParseIntField(fields, colId, 0);
                if (trialId == 0) trialId = i;

                // Get oxygen values from all available runs
                var validO2Values = new List<float>();
                foreach (int col in o2Cols)
                {
                    if (col >= 0 && col < fields.Length)
                    {
                        if (TryParseFloat(fields[col], out var f))
                        {
                            validO2Values.Add(f);
                        }
                    }
                }

                if (validO2Values.Count == 0) continue;

                // Use LAST run (most recent) instead of average
                float finalOxygen = validO2Values[validO2Values.Count - 1];
                Debug.Log($"[{fileName}] Trial {trialId}: Found {validO2Values.Count} O2 runs: [{string.Join(", ", validO2Values)}] → Using LAST run: {finalOxygen:F1}%");

                allValid.Add((i, new TrialDataModels.TrialData
                {
                    trialId = trialId,
                    speed = ParseField(fields, IndexOf(header, "speed"), 1),
                    verticalSpeed = ParseField(fields, IndexOf(header, "verticalSpeed"), 2),
                    idleUpwardSpeed = ParseField(fields, IndexOf(header, "idleUpwardSpeed"), 3),
                    lifeTime = ParseField(fields, IndexOf(header, "lifeTime"), 4),
                    downHealthPairSec = ParseField(fields, IndexOf(header, "downHealthPairSec"), 5),
                    removeHealthWithCollide = ParseField(fields, IndexOf(header, "removeHealthWithCollide"), 6),
                    timeBetweenCollides = ParseField(fields, IndexOf(header, "timeBetweenCollides"), 7),
                    healHealthPoint = ParseField(fields, IndexOf(header, "healHealthPoint"), 8),
                    factorForce = ParseField(fields, IndexOf(header, "factorForce"), 9),
                    finalOxygenRemaining = finalOxygen,
                    completed = finalOxygen > 0f,
                    isRandomParameters = isRandom
                }));
            }
            return allValid;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error loading from {fileName}: {e.Message}");
            return allValid;
        }
    }
}


