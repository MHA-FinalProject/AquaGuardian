using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public static class TrialDataLoader
{
    private const string CsvRelativePath = "Data/Trials/Trial_5_runs_.csv";
    private const string RandomCsvRelativePath = "Data/Trials/Trial_Random_Parameters.csv";

    private static string GetCsvPath() => Path.Combine(Application.dataPath, CsvRelativePath);
    private static string GetRandomCsvPath() => Path.Combine(Application.dataPath, RandomCsvRelativePath);

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
            var lines = CsvFileHelper.ReadCsvLines(csvPath);
            if (lines.Length <= 1) return list;

            var header = lines[0].Split(',');
            int colId = CsvFileHelper.IndexOf(header, "trialId");
            int colSpeed = CsvFileHelper.IndexOf(header, "speed");
            int colVSpeed = CsvFileHelper.IndexOf(header, "verticalSpeed");
            int colIdleUp = CsvFileHelper.IndexOf(header, "idleUpwardSpeed");
            int colLife = CsvFileHelper.IndexOf(header, "lifeTime");
            int colDrop = CsvFileHelper.IndexOf(header, "RemoveHealthEveryLifeTime");
            int colCollide = CsvFileHelper.IndexOf(header, "removeHealthWithCollide");
            int colBetween = CsvFileHelper.IndexOf(header, "timeBetweenCollides");
            int colHeal = CsvFileHelper.IndexOf(header, "healHealthPoint");
            int colForce = CsvFileHelper.IndexOf(header, "factorForce");
            int colIsAmadeo = CsvFileHelper.IndexOf(header, "IsAmadeoMode");

            int availableTrials = Math.Min(lines.Length - 1, cachedO2.Count());
            int trialsToSelect = Math.Min(5, availableTrials);

            // Check if all trials are keyboard-only (IsAmadeoMode = 0)
            bool allKeyboard = true;
            if (colIsAmadeo >= 0)
            {
                for (int checkIdx = 0; checkIdx < trialsToSelect && checkIdx + 1 < lines.Length; checkIdx++)
                {
                    var checkFields = lines[checkIdx + 1].Split(',');
                    float isAmadeo = CsvFileHelper.ParseField(checkFields, colIsAmadeo, 10);
                    if (isAmadeo > 0.5f)
                    {
                        allKeyboard = false;
                        break;
                    }
                }
            }

            for (int localIdx = 0; localIdx < trialsToSelect; localIdx++)
            {
                int lineIndex = localIdx + 1;
                if (lineIndex >= lines.Length) continue;

                var fields = lines[lineIndex].Split(',');
                if (fields.Length < 10) continue;

                int trialId = CsvFileHelper.ParseIntField(fields, colId, 0);
                if (trialId == 0) trialId = localIdx + 1;

                // FIXED: Map O2 by row index (localIdx), not by trialId
                // This ensures correct mapping even if trialId is not sequential
                float cached = (localIdx < cachedO2.Count())
                    ? cachedO2.ElementAt(localIdx)
                    : 0f;

                float isAmadeoMode = (colIsAmadeo >= 0) ? CsvFileHelper.ParseField(fields, colIsAmadeo, 10) : 0f;
                
                // If all trials are keyboard-only, don't read factorForce from CSV (set to 0)
                float factorForceValue = 0f;
                if (!allKeyboard && isAmadeoMode > 0.5f && colForce >= 0)
                {
                    factorForceValue = CsvFileHelper.ParseField(fields, colForce, 9);
                }

                list.Add(new TrialDataModels.TrialData
                {
                    trialId = trialId,
                    speed = CsvFileHelper.ParseField(fields, colSpeed, 1),
                    verticalSpeed = CsvFileHelper.ParseField(fields, colVSpeed, 2),
                    idleUpwardSpeed = CsvFileHelper.ParseField(fields, colIdleUp, 3),
                    lifeTime = CsvFileHelper.ParseField(fields, colLife, 4),
                    RemoveHealthEveryLifeTime = CsvFileHelper.ParseField(fields, colDrop, 5),
                    removeHealthWithCollide = CsvFileHelper.ParseField(fields, colCollide, 6),
                    timeBetweenCollides = CsvFileHelper.ParseField(fields, colBetween, 7),
                    healHealthPoint = CsvFileHelper.ParseField(fields, colHeal, 8),
                    factorForce = factorForceValue,  // 0 if keyboard-only, read from CSV only if Amadeo
                    IsAmadeoMode = isAmadeoMode,
                    finalOxygenRemaining = cached,
                    completed = cached > 0f,
                    isRandomParameters = false,
                    trialDuration = 0f // Duration not available from cache
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

    public static List<TrialDataModels.TrialData> LoadTrialDataFromCSV(bool useRandomParameters = false)
    {
        var list = new List<TrialDataModels.TrialData>();
        try
        {
            List<TrialDataModels.TrialData> sortedTrials;
            
            if (useRandomParameters)
            {
                // Load ONLY from Random CSV (random parameters mode)
                var randomTrials = LoadTrialsFromFile(GetRandomCsvPath(), "Trial_Random_Parameters.csv", true);
                
                // FIXED: True random sampling instead of taking first 5
                sortedTrials = randomTrials
                    .OrderBy(t => UnityEngine.Random.value)  // Random shuffle
                    .Take(5)
                    .Select(t => t.data)
                    .ToList();
            }
            else
            {
                // Load ONLY from Regular CSV (constant parameters from CSV)
                var regularTrials = LoadTrialsFromFile(GetCsvPath(), "Trial_5_runs_.csv", false);
                
                sortedTrials = regularTrials
                    .OrderBy(t => t.lineIndex)
                    .Take(5)
                    .Select(t => t.data)
                    .ToList();
            }
            
            
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

            var lines = CsvFileHelper.ReadCsvLines(csvPath);
            if (lines.Length <= 1) return allValid;

            var header = lines[0].Split(',');
            int colId = CsvFileHelper.IndexOf(header, "trialId");
            if (colId < 0) colId = CsvFileHelper.IndexOf(header, "trial_id");
            
            var o2Cols = CsvFileHelper.FindOxygenColumns(header);
            if (fileName.Contains("Random") || fileName.Contains("random"))
            {
                int o2ResultCol = CsvFileHelper.IndexOf(header, "o2_result");
                if (o2ResultCol >= 0 && !o2Cols.Contains(o2ResultCol))
                    o2Cols.Add(o2ResultCol);
            }
            if (o2Cols.Count == 0) return allValid;

            for (int i = 1; i < lines.Length; i++)
            {
                var fields = lines[i].Split(',');
                if (fields.Length < 10) continue;

                int trialId = CsvFileHelper.ParseIntField(fields, colId, 0);
                if (trialId == 0) trialId = i;

                // Get oxygen values from all available runs
                var validO2Values = new List<float>();
                var validO2ColumnNames = new List<string>();
                foreach (int col in o2Cols)
                {
                    if (col >= 0 && col < fields.Length)
                    {
                        if (CsvFileHelper.TryParseFloat(fields[col], out var f))
                        {
                            validO2Values.Add(f);
                            validO2ColumnNames.Add(header[col].Trim());
                        }
                    }
                }

                if (validO2Values.Count == 0) continue;

                // Calculate oxygen using OxygenCalculationSettings (respects LastRun/Average/Median/etc.)
                float finalOxygen;
                // FIXED: Use Singleton instead of FindObjectOfType (faster)
                var oxygenSettings = OxygenCalculationSettings.Instance;
                if (oxygenSettings != null)
                {
                    finalOxygen = oxygenSettings.CalculateOxygen(validO2Values.ToArray(), validO2ColumnNames.ToArray());
                }
                else
                {
                    // Fallback: Use LAST run if no settings found
                    finalOxygen = validO2Values[validO2Values.Count - 1];
                }

                int colIsAmadeoMode = CsvFileHelper.IndexOf(header, "IsAmadeoMode");
                int colFactorForce = CsvFileHelper.IndexOf(header, "factorForce");
                
                // Read IsAmadeoMode first to determine if we should read factorForce
                float isAmadeoMode = (colIsAmadeoMode >= 0) ? CsvFileHelper.ParseField(fields, colIsAmadeoMode, 10) : 0f;
                
                // If keyboard mode (IsAmadeoMode = 0), don't read factorForce from CSV (set to 0)
                float factorForceValue = 0f;
                if (isAmadeoMode > 0.5f && colFactorForce >= 0)
                {
                    factorForceValue = CsvFileHelper.ParseField(fields, colFactorForce, 9);
                }
                
                allValid.Add((i, new TrialDataModels.TrialData
                {
                    trialId = trialId,
                    speed = CsvFileHelper.ParseField(fields, CsvFileHelper.IndexOf(header, "speed"), 1),
                    verticalSpeed = CsvFileHelper.ParseField(fields, CsvFileHelper.IndexOf(header, "verticalSpeed"), 2),
                    idleUpwardSpeed = CsvFileHelper.ParseField(fields, CsvFileHelper.IndexOf(header, "idleUpwardSpeed"), 3),
                    lifeTime = CsvFileHelper.ParseField(fields, CsvFileHelper.IndexOf(header, "lifeTime"), 4),
                    RemoveHealthEveryLifeTime = CsvFileHelper.ParseField(fields, CsvFileHelper.IndexOf(header, "RemoveHealthEveryLifeTime"), 5),
                    removeHealthWithCollide = CsvFileHelper.ParseField(fields, CsvFileHelper.IndexOf(header, "removeHealthWithCollide"), 6),
                    timeBetweenCollides = CsvFileHelper.ParseField(fields, CsvFileHelper.IndexOf(header, "timeBetweenCollides"), 7),
                    healHealthPoint = CsvFileHelper.ParseField(fields, CsvFileHelper.IndexOf(header, "healHealthPoint"), 8),
                    factorForce = factorForceValue,  // 0 if keyboard mode, read from CSV only if Amadeo mode
                    IsAmadeoMode = isAmadeoMode,
                    finalOxygenRemaining = finalOxygen,
                    completed = finalOxygen > 0f,
                    isRandomParameters = isRandom,
                    trialDuration = CsvFileHelper.ParseField(fields, CsvFileHelper.IndexOf(header, "duration"), 0)
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


