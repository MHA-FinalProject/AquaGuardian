using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/**
 * TrialDataService - Centralized service for all trial data CSV operations
 * Consolidates CSV loading, saving, and parameter generation logic
 * Replaces duplicate code from TrialParameterManager and TrialDataLoader
 */
public static class TrialDataService
{
    // Default CSV paths
    private const string DefaultTrialParametersPath = "Data/Trials/Trial_5_runs_.csv";
    private const string DefaultRandomParametersPath = "Data/Trials/Trial_Random_Parameters.csv";

    // Get full paths
    private static string GetTrialParametersPath(string relativePath = null)
    {
        return Path.Combine(Application.dataPath, relativePath ?? DefaultTrialParametersPath);
    }

    private static string GetRandomParametersPath(string relativePath = null)
    {
        return Path.Combine(Application.dataPath, relativePath ?? DefaultRandomParametersPath);
    }

    /**
     * Load single trial parameters by ID (used during gameplay)
     * Returns null if trial not found or load failed
     * @param fallbackTextAsset Optional TextAsset to use if file path doesn't work
     */
    public static TrialDataModels.TrialData LoadTrialParameters(int trialId, bool useRandomParameters = false, string customPath = null, TextAsset fallbackTextAsset = null)
    {
        try
        {
            string csvPath = useRandomParameters
                ? GetRandomParametersPath(customPath)
                : GetTrialParametersPath(customPath);

            string[] lines = null;

            // Try to load from file path first
            if (File.Exists(csvPath))
            {
                // Use CsvFileHelper to handle file locks (e.g., Excel)
                lines = CsvFileHelper.ReadAllLinesWithRetry(csvPath);
            }
            // Fallback to TextAsset if file not found
            else if (fallbackTextAsset != null)
            {
                Debug.LogWarning($"Trial parameters file not found at: {csvPath}, using TextAsset fallback");
                lines = fallbackTextAsset.text.Split(new[] { "\r\n", "\n", "\r" }, System.StringSplitOptions.RemoveEmptyEntries);
            }
            else
            {
                Debug.LogError($"Trial parameters file not found at: {csvPath} and no TextAsset fallback provided");
                return null;
            }

            // Clean line endings (Windows \r\n, Unix \n, Mac \r)
            lines = CsvFileHelper.CleanLineEndings(lines);

            if (lines.Length <= 1)
            {
                Debug.LogError("Trial parameters file is empty or has no data rows!");
                return null;
            }

            // Find the correct row for this trial number
            int dataIndex = CsvFileHelper.FindRowByIntValue(lines, trialId, columnIndex: 0, skipHeaderRows: 1);

            if (dataIndex == -1)
            {
                Debug.LogError($"Trial {trialId} not found in CSV file! Available trials: {string.Join(", ", lines.Skip(1).Select(l => l.Split(',')[0]))}");
                return null;
            }

            string[] dataFields = lines[dataIndex].Split(',');

            // Need at least 10 fields for parameters (trialId + 9 parameters)
            // Oxygen columns (10+) are written AFTER running the trial
            if (dataFields.Length < 10)
            {
                Debug.LogError($"File row {dataIndex} has insufficient fields: {dataFields.Length}, needs at least 10 (trialId + 9 parameters)");
                return null;
            }

            // Parse header to get column indices
            var header = lines[0].Split(',');

            return ParseTrialDataRow(dataFields, header, dataIndex, useRandomParameters);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error loading trial parameters: {e.Message}");
            return null;
        }
    }

    /**
     * Load trial data from cache (with CSV fallback)
     * Tries to load from TrialDataCache first, falls back to CSV if cache is incomplete
     * Returns list of trials with oxygen values from cache if available
     */
    public static List<TrialDataModels.TrialData> LoadTrialDataFromCache(string customPath = null)
    {
        var list = new List<TrialDataModels.TrialData>();
        try
        {
            var cachedO2 = TrialDataCache.Instance.GetLatestRunOxygenValues();
            if (cachedO2 == null || cachedO2.Count() < 5)
            {
                Debug.LogWarning($"Incomplete cached trial data ({cachedO2?.Count() ?? 0}/5) - trying CSV fallback...");
                return LoadAllTrials(false, customPath);
            }

            string csvPath = GetTrialParametersPath(customPath);
            var lines = CsvFileHelper.ReadAllLinesWithRetry(csvPath);
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

                // Map O2 by row index (localIdx), not by trialId
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

    /**
     * Load all trials with oxygen data (used for regression analysis)
     * Returns list of trials with calculated oxygen values
     */
    public static List<TrialDataModels.TrialData> LoadAllTrials(bool useRandomParameters = false, string customPath = null)
    {
        var list = new List<TrialDataModels.TrialData>();
        try
        {
            string csvPath = useRandomParameters
                ? GetRandomParametersPath(customPath)
                : GetTrialParametersPath(customPath);

            string fileName = useRandomParameters ? "Trial_Random_Parameters.csv" : "Trial_5_runs_.csv";

            var trials = LoadTrialsFromFile(csvPath, fileName, useRandomParameters);

            if (useRandomParameters)
            {
                // Random shuffle and take first 5
                list = trials
                    .OrderBy(t => UnityEngine.Random.value)
                    .Take(5)
                    .Select(t => t.data)
                    .ToList();
            }
            else
            {
                // Take first 5 ordered by line index
                list = trials
                    .OrderBy(t => t.lineIndex)
                    .Take(5)
                    .Select(t => t.data)
                    .ToList();
            }

            return list;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error loading all trials: {e.Message}");
            return list;
        }
    }

    /**
     * Save trial result back to CSV and update cache
     * Handles both regular CSV (adds o2_run columns) and random CSV (updates o2_result column)
     * Also manages TrialDataCache lifecycle (BeginRun/AppendTrial/EndRun)
     */
    public static bool SaveTrialResult(TrialDataModels.TrialData trialData, bool isRandomMode, string customTrialPath = null, string customRandomPath = null)
    {
        if (trialData == null)
        {
            Debug.LogError("TrialData is null - cannot save");
            return false;
        }

        try
        {
            // Save to CSV
            bool csvSaved;
            if (isRandomMode)
            {
                csvSaved = SaveToRandomParametersCSV(trialData, customRandomPath);
            }
            else
            {
                csvSaved = UpdateOriginalCSV(trialData, customTrialPath);
            }

            // Update cache (only for regular trials, not random)
            if (!isRandomMode)
            {
                if (trialData.trialId == 1)
                {
                    TrialDataCache.Instance.BeginRun();
                }

                TrialDataCache.Instance.AppendTrial(trialData.trialId, trialData.finalOxygenRemaining);

                if (trialData.trialId == 5)
                {
                    TrialDataCache.Instance.EndRun();
                }
            }

            return csvSaved;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error saving trial result: {e.Message}");
            return false;
        }
    }

    /**
     * Generate random parameters within specified ranges
     */
    public static TrialDataModels.TrialData GenerateRandomParameters(TrialDataModels.ParameterRanges ranges)
    {
        if (ranges == null)
        {
            Debug.LogError("ParameterRanges is null - cannot generate random parameters");
            return null;
        }

        var data = new TrialDataModels.TrialData
        {
            speed = UnityEngine.Random.Range(ranges.speedRange.x, ranges.speedRange.y),
            verticalSpeed = UnityEngine.Random.Range(ranges.verticalSpeedRange.x, ranges.verticalSpeedRange.y),
            idleUpwardSpeed = UnityEngine.Random.Range(ranges.idleUpwardSpeedRange.x, ranges.idleUpwardSpeedRange.y),
            healHealthPoint = UnityEngine.Random.Range(ranges.healHealthPointRange.x, ranges.healHealthPointRange.y),
            timeBetweenCollides = UnityEngine.Random.Range(ranges.timeBetweenCollidesRange.x, ranges.timeBetweenCollidesRange.y),
            removeHealthWithCollide = UnityEngine.Random.Range(ranges.removeHealthWithCollideRange.x, ranges.removeHealthWithCollideRange.y),
            RemoveHealthEveryLifeTime = UnityEngine.Random.Range(ranges.RemoveHealthEveryLifeTimeRange.x, ranges.RemoveHealthEveryLifeTimeRange.y),
            lifeTime = UnityEngine.Random.Range(ranges.lifeTimeRange.x, ranges.lifeTimeRange.y),
            factorForce = UnityEngine.Random.Range(ranges.factorForceRange.x, ranges.factorForceRange.y),
            IsAmadeoMode = 0f  // Will be set when trial ends based on actual input mode
        };

        return data;
    }

    // ========== Private Helper Methods ==========

    /**
     * Parse CSV row to TrialData object
     */
    private static TrialDataModels.TrialData ParseTrialDataRow(string[] fields, string[] header, int rowIndex, bool isRandom)
    {
        // Get column indices
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

        // Read IsAmadeoMode first to determine if we should read factorForce
        float isAmadeoMode = colIsAmadeo >= 0 ? CsvFileHelper.ParseField(fields, colIsAmadeo, 10) : 0f;

        // If keyboard mode (IsAmadeoMode = 0), don't read factorForce from CSV (set to 0)
        float factorForceValue = 0f;
        if (isAmadeoMode > 0.5f && colForce >= 0)
        {
            factorForceValue = CsvFileHelper.ParseField(fields, colForce, 9);
        }

        // Parse trial ID
        int trialId = CsvFileHelper.ParseIntField(fields, CsvFileHelper.IndexOf(header, "trialId"), 0);
        if (trialId == 0)
        {
            trialId = CsvFileHelper.ParseIntField(fields, CsvFileHelper.IndexOf(header, "trial_id"), rowIndex);
        }

        var data = new TrialDataModels.TrialData
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
            factorForce = factorForceValue,  // 0 if keyboard mode, read from CSV only if Amadeo mode
            IsAmadeoMode = isAmadeoMode,
            isRandomParameters = isRandom
        };

        // Try to parse duration if available
        int colDuration = CsvFileHelper.IndexOf(header, "duration");
        if (colDuration >= 0)
        {
            data.trialDuration = CsvFileHelper.ParseField(fields, colDuration, 0);
        }

        return data;
    }

    /**
     * Load all trials from a CSV file (for regression)
     * Returns list of (lineIndex, TrialData) tuples
     */
    private static List<(int lineIndex, TrialDataModels.TrialData data)> LoadTrialsFromFile(string csvPath, string fileName, bool isRandom)
    {
        var allValid = new List<(int lineIndex, TrialDataModels.TrialData data)>();
        try
        {
            if (!File.Exists(csvPath)) return allValid;

            var lines = CsvFileHelper.ReadAllLinesWithRetry(csvPath);
            if (lines.Length <= 1) return allValid;

            var header = lines[0].Split(',');
            int colId = CsvFileHelper.IndexOf(header, "trialId");
            if (colId < 0) colId = CsvFileHelper.IndexOf(header, "trial_id");

            // Find oxygen columns
            var o2Cols = CsvFileHelper.FindOxygenColumns(header);
            if (fileName.Contains("Random") || fileName.Contains("random"))
            {
                int o2ResultCol = CsvFileHelper.IndexOf(header, "o2_result");
                if (o2ResultCol >= 0 && !o2Cols.Contains(o2ResultCol))
                    o2Cols.Add(o2ResultCol);
            }

            // For single trial loading, oxygen columns are optional
            // For regression loading, we need at least one oxygen column
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
                var oxygenSettings = OxygenCalculationSettings.Instance;
                if (oxygenSettings != null)
                {
                    finalOxygen = oxygenSettings.CalculateOxygen(validO2Values.ToArray(), validO2ColumnNames.ToArray());
                }
                else
                {
                    // Fallback: Use LAST run if no settings found (each column = different person)
                    finalOxygen = validO2Values[validO2Values.Count - 1];
                    Debug.Log($"[TrialDataService] No OxygenCalculationSettings found, using LastRun fallback: {finalOxygen}% from column {validO2ColumnNames[validO2ColumnNames.Count - 1]}");
                }

                // Parse trial data
                var trialData = ParseTrialDataRow(fields, header, i, isRandom);
                trialData.finalOxygenRemaining = finalOxygen;
                trialData.completed = finalOxygen > 0f;

                allValid.Add((i, trialData));
            }
            return allValid;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error loading from {fileName}: {e.Message}");
            return allValid;
        }
    }

    /**
     * Update the original Trial_5_runs_.csv - adds new column for each run
     */
    private static bool UpdateOriginalCSV(TrialDataModels.TrialData trialData, string customPath)
    {
        try
        {
            // Handle both relative and absolute paths
            string csvPath;
            if (customPath != null)
            {
                // If customPath is already absolute, use it; otherwise combine with Application.dataPath
                csvPath = Path.IsPathRooted(customPath)
                    ? customPath
                    : Path.Combine(Application.dataPath, customPath);
            }
            else
            {
                csvPath = GetTrialParametersPath();
            }

            // Ensure directory exists
            string directory = Path.GetDirectoryName(csvPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (!File.Exists(csvPath))
            {
                Debug.LogError($"Original CSV not found at: {csvPath}");
                return false;
            }

            var lines = CsvFileHelper.ReadAllLinesWithRetry(csvPath).ToList();

            if (lines.Count == 0)
            {
                Debug.LogError("CSV file is empty!");
                return false;
            }

            // Parse header
            var headerFields = lines[0].Split(',').ToList();

            // Find the last oxygen column
            int lastO2ColumnIndex = -1;
            for (int i = headerFields.Count - 1; i >= 0; i--)
            {
                if (headerFields[i].StartsWith("o2_run"))
                {
                    lastO2ColumnIndex = i;
                    break;
                }
            }

            // Check if we need to create a new column or use existing one
            string newColumnName = "";
            int newColumnIndex = -1;
            bool needNewColumn = false;

            // Determine if this is the first trial of a new run
            bool isFirstTrialOfRun = (trialData.trialId == 1);

            // Check if we need a new column by examining if current trial already has data in last column
            int trialRowIndex = -1;
            if (lastO2ColumnIndex >= 0)
            {
                // Find this trial's row using helper function
                trialRowIndex = CsvFileHelper.FindRowByIntValue(lines.ToArray(), trialData.trialId, columnIndex: 0, skipHeaderRows: 1);
                
                if (trialRowIndex >= 0)
                {
                    var fields = lines[trialRowIndex].Split(',');
                    
                    // Check if last column has data for THIS trial
                    if (lastO2ColumnIndex < fields.Length && !string.IsNullOrWhiteSpace(fields[lastO2ColumnIndex]))
                    {
                        // Data exists - check if it's a failed attempt (0 or very close to 0)
                        if (CsvFileHelper.TryParseFloat(fields[lastO2ColumnIndex], out float existingO2) && existingO2 <= 0.1f)
                        {
                            // Previous attempt was a failure (0%) - overwrite it
                            newColumnIndex = lastO2ColumnIndex;
                            newColumnName = headerFields[lastO2ColumnIndex];
                            trialData.attemptNumber = 2;
                            Debug.Log($"[TrialDataService] Trial {trialData.trialId} has failed attempt ({existingO2}%) in {headerFields[lastO2ColumnIndex]}, overwriting with attempt #{trialData.attemptNumber}");
                        }
                        else
                        {
                            // Previous attempt was successful - create new column for retry
                            needNewColumn = true;
                            Debug.Log($"[TrialDataService] Trial {trialData.trialId} already has successful data ({existingO2}%) in {headerFields[lastO2ColumnIndex]}, creating new column for retry");
                        }
                    }
                    else
                    {
                        // No data yet - use existing column
                        newColumnIndex = lastO2ColumnIndex;
                        newColumnName = headerFields[lastO2ColumnIndex];
                    }
                }
                else
                {
                    // Trial row not found, use existing column
                    newColumnIndex = lastO2ColumnIndex;
                    newColumnName = headerFields[lastO2ColumnIndex];
                }
            }
            else
            {
                // No oxygen columns exist yet - create first one
                needNewColumn = true;
            }

            // Create new column if needed
            if (needNewColumn || (isFirstTrialOfRun && lastO2ColumnIndex == -1))
            {
                // Count existing oxygen columns to determine run number
                int runNumber = headerFields.Count(h => h.StartsWith("o2_run")) + 1;
                newColumnName = $"o2_run{runNumber}";

                // Add new column to header
                headerFields.Add(newColumnName);
                lines[0] = string.Join(",", headerFields);
                newColumnIndex = headerFields.Count - 1;
                
                Debug.Log($"[TrialDataService] Creating new run column: {newColumnName} (Trial {trialData.trialId})");
            }

            // Update the trial row (reuse trialRowIndex if we already found it)
            bool updated = false;
            if (trialRowIndex == -1)
            {
                // Need to find the row if we didn't find it earlier
                trialRowIndex = CsvFileHelper.FindRowByIntValue(lines.ToArray(), trialData.trialId, columnIndex: 0, skipHeaderRows: 1);
            }
            
            if (trialRowIndex >= 0)
            {
                // Update using helper function
                string newValue = trialData.finalOxygenRemaining.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                lines[trialRowIndex] = CsvFileHelper.UpdateCellValue(lines[trialRowIndex], newColumnIndex, newValue);
                updated = true;
            }

            if (updated)
            {
                CsvFileHelper.WriteAllLinesWithRetry(csvPath, lines.ToArray());
                return true;
            }
            else
            {
                Debug.LogWarning($"Trial {trialData.trialId} not found in original CSV for update");
                return false;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error updating original CSV: {e.Message}");
            return false;
        }
    }

   
    private static bool SaveToRandomParametersCSV(TrialDataModels.TrialData trialData, string customPath)
    {
        try
        {
            // Handle both relative and absolute paths
            string csvPath;
            if (customPath != null)
            {
                // If customPath is already absolute, use it; otherwise combine with Application.dataPath
                csvPath = Path.IsPathRooted(customPath)
                    ? customPath
                    : Path.Combine(Application.dataPath, customPath);
            }
            else
            {
                csvPath = GetRandomParametersPath();
            }

            // Ensure directory exists
            string directory = Path.GetDirectoryName(csvPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            bool fileExists = File.Exists(csvPath);

            // Read existing file or create header
            List<string> lines;
            if (fileExists)
            {
                lines = new List<string>(CsvFileHelper.ReadAllLinesWithRetry(csvPath));
            }
            else
            {
                // Create new file with header (must match FormatTrialDataLine: 13 columns including duration)
                string header = "trial_id,speed,verticalSpeed,idleUpwardSpeed,lifeTime,RemoveHealthEveryLifeTime,removeHealthWithCollide,timeBetweenCollides,healHealthPoint,factorForce,IsAmadeoMode,o2_result,duration";
                lines = new List<string> { header };
            }

            // Fix header if it's missing columns (for existing files with old format)
            if (lines.Count > 0)
            {
                var headerFields = lines[0].Split(',');
                var expectedHeader = "trial_id,speed,verticalSpeed,idleUpwardSpeed,lifeTime,RemoveHealthEveryLifeTime,removeHealthWithCollide,timeBetweenCollides,healHealthPoint,factorForce,IsAmadeoMode,o2_result,duration";
                if (lines[0] != expectedHeader)
                {
                    Debug.LogWarning($"[TrialDataService] Fixing CSV header format (old: {lines[0]})");
                    lines[0] = expectedHeader;
                }
            }

            // Check if this trial already exists using helper function
            int existingRowIndex = CsvFileHelper.FindRowByIntValue(lines.ToArray(), trialData.trialId, columnIndex: 0, skipHeaderRows: 1);
            
            if (existingRowIndex >= 0)
            {
                // Update existing trial
                lines[existingRowIndex] = FormatTrialDataLine(trialData);
            }
            else
            {
                // Add new trial
                lines.Add(FormatTrialDataLine(trialData));
            }

            // Save file
            CsvFileHelper.WriteAllLinesWithRetry(csvPath, lines.ToArray());
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error saving to random parameters CSV: {e.Message}");
            return false;
        }
    }

  
    private static string FormatTrialDataLine(TrialDataModels.TrialData trialData)
    {
        return string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "{0},{1:F2},{2:F2},{3:F2},{4:F2},{5:F2},{6:F2},{7:F2},{8:F2},{9:F2},{10:F2},{11:F2},{12:F1}",
            trialData.trialId,
            trialData.speed,
            trialData.verticalSpeed,
            trialData.idleUpwardSpeed,
            trialData.lifeTime,
            trialData.RemoveHealthEveryLifeTime,
            trialData.removeHealthWithCollide,
            trialData.timeBetweenCollides,
            trialData.healHealthPoint,
            trialData.factorForce,
            trialData.IsAmadeoMode,
            trialData.finalOxygenRemaining,
            trialData.trialDuration
        );
    }
}

