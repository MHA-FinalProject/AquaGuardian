using UnityEngine;
using TMPro;
using System.Globalization;
using System.Linq;

/**
 * TrialParameterManager - Manages trial parameter loading and application
 * Handles CSV reading, parameter generation, and applying to game components
 * Extracted from PanelOpenUp.cs for better code organization
 */
public class TrialParameterManager : MonoBehaviour
{
    [Header("Component References")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerLife playerLife;
    [SerializeField] private Health health;
    
    [Header("Parameter Settings")]
    [SerializeField] private string trialParametersPath = "Data/Trials/Trial_5_runs_.csv";
    [SerializeField] private string randomParametersPath = "Data/Trials/random_trial.csv";
    [SerializeField] private TextAsset trialParametersFile;
    [SerializeField] private TrialDataModels.ParameterRanges parameterRanges = new TrialDataModels.ParameterRanges();
    
    private bool currentModeIsRandom = false; // Track current mode for saving
    
    // Load and apply parameters for a given trial number
    public TrialDataModels.TrialData LoadAndApplyTrialParameters(int trialNumber, bool useRandomParameters = false)
    {
        currentModeIsRandom = useRandomParameters;
        Debug.Log($"Loading parameters for trial {trialNumber} - Mode: {(useRandomParameters ? "RANDOM" : "CSV")}");
        
        TrialDataModels.TrialData data;
        
        if (useRandomParameters)
        {
            // Generate random parameters
            data = GenerateRandomParametersInternal();
            Debug.Log($"Generated random parameters: Speed={data.speed:F2}, Heal={data.healHealthPoint:F2}");
        }
        else
        {
            // Load parameters from CSV
            data = LoadParametersFromFilePath(trialNumber);
            
            if (data == null)
            {
                Debug.LogError($"CRITICAL: Failed to load parameters for trial {trialNumber}!");
                data = GenerateRandomParametersInternal(); // Emergency fallback
            }
            Debug.Log($"Parameters loaded from CSV: Speed={data.speed:F2}, Heal={data.healHealthPoint:F2}");
        }
        
        data.trialId = trialNumber;
        
        // Apply parameters to game components
        ApplyParametersToGame(data);
        return data;
    }
    
   // Save trial result (final oxygen) back to CSV
    public bool SaveTrialResultToCSV(TrialDataModels.TrialData trialData)
    {
        try
        {
            if (trialData == null)
            {
                Debug.LogError("TrialData is null - cannot save");
                return false;
            }
            
            bool originalUpdated, timestampedSaved;
            
            if (currentModeIsRandom)
            {
                // RANDOM MODE: Save to separate random parameters file
                Debug.Log($"Saving random parameters trial {trialData.trialId} to {randomParametersPath}");
                originalUpdated = SaveToRandomParametersCSV(trialData);
                timestampedSaved = SaveToTimestampedCSV(trialData, "_Random");
            }
            else
            {
                // CSV MODE: Save to THREE locations:
                // 1. Update original Trial_5_runs_.csv
                originalUpdated = UpdateOriginalCSV(trialData);
                
                // 2. Append to timestamped results file
                timestampedSaved = SaveToTimestampedCSV(trialData);
                
                // 3. Update O2_Wide_AllSets.csv if this is trial 5 (end of set)
                if (trialData.trialId == 5)
                {
                    UpdateO2WideAllSets();
                }
            }
            
            // 4. ALWAYS CACHE the oxygen value for regression (NO CSV READING NEEDED!)
            TrialDataCache.Instance.SaveTrialOxygen(trialData.trialId, trialData.finalOxygenRemaining);
            
            if (originalUpdated && timestampedSaved)
            {
                Debug.Log($"Trial {trialData.trialId} saved: Oxygen={trialData.finalOxygenRemaining:F1}%, Completed={trialData.completed}, Mode={( currentModeIsRandom ? "Random" : "CSV")}");
                return true;
            }
            else
            {
                Debug.LogWarning($"Partial save for trial {trialData.trialId}: Original={originalUpdated}, Timestamped={timestampedSaved}");
                return originalUpdated; // At least original must succeed
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error saving trial results: {e.Message}");
            Debug.LogError($"Stack trace: {e.StackTrace}");
            return false;
        }
    }
    
    /// <summary>
    /// Update the original Trial_5_runs_.csv - adds new column for each run
    /// </summary>
    private bool UpdateOriginalCSV(TrialDataModels.TrialData trialData)
    {
        try
        {
            string csvPath = System.IO.Path.Combine(Application.dataPath, trialParametersPath);
            
            if (!System.IO.File.Exists(csvPath))
            {
                Debug.LogError($"Original CSV not found at: {csvPath}");
                return false;
            }
            
            var lines = System.IO.File.ReadAllLines(csvPath).ToList();
            
            if (lines.Count == 0)
            {
                Debug.LogError("CSV file is empty!");
                return false;
            }
            
            // Determine if this is the first trial of a new run
            bool isFirstTrialOfRun = (trialData.trialId == 1);
            
            // Parse header
            var headerFields = lines[0].Split(',').ToList();
            
            // Find or create new oxygen column
            string newColumnName = "";
            int newColumnIndex = -1;
            
            if (isFirstTrialOfRun)
            {
                // Count existing oxygen columns to determine run number
                int runNumber = headerFields.Count(h => h.StartsWith("o2_run")) + 1;
                newColumnName = $"o2_run{runNumber}";
                
                // Add new column to header
                headerFields.Add(newColumnName);
                lines[0] = string.Join(",", headerFields);
                newColumnIndex = headerFields.Count - 1;
                
                Debug.Log($" Creating new oxygen column: {newColumnName} (column {newColumnIndex})");
            }
            else
            {
                // Use the last oxygen column (most recent run)
                for (int i = headerFields.Count - 1; i >= 0; i--)
                {
                    if (headerFields[i].StartsWith("o2_run"))
                    {
                        newColumnIndex = i;
                        newColumnName = headerFields[i];
                        break;
                    }
                }
                
                if (newColumnIndex == -1)
                {
                    Debug.LogError("No oxygen column found for subsequent trials!");
                    return false;
                }
            }
            
            // Update the trial row
            bool updated = false;
            for (int i = 1; i < lines.Count; i++)
            {
                var fields = lines[i].Split(',').ToList();
                
                if (fields.Count > 0 && int.TryParse(fields[0], out int trialId) && trialId == trialData.trialId)
                {
                    // Ensure the row has enough columns
                    while (fields.Count <= newColumnIndex)
                    {
                        fields.Add("");
                    }
                    
                    // Update the oxygen value
                    fields[newColumnIndex] = trialData.finalOxygenRemaining.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                    lines[i] = string.Join(",", fields);
                    updated = true;
                    break;
                }
            }
            
            if (updated)
            {
                System.IO.File.WriteAllLines(csvPath, lines);
                //Debug.Log($"Updated Trial_5_runs_.csv: Trial {trialData.trialId} to {newColumnName} = {trialData.finalOxygenRemaining:F1}%");
                return true;
            }
            else
            {
                Debug.LogWarning($"Trial {trialData.trialId} not found in original CSV for update");
                return false;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error updating original CSV: {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Save random parameters to random_trial.csv
    /// Keeps random trials separate from regular CSV trials
    /// </summary>
    private bool SaveToRandomParametersCSV(TrialDataModels.TrialData trialData)
    {
        try
        {
            string csvPath = System.IO.Path.Combine(Application.dataPath, randomParametersPath);
            bool fileExists = System.IO.File.Exists(csvPath);
            
            // Read existing file or create header
            System.Collections.Generic.List<string> lines;
            if (fileExists)
            {
                lines = new System.Collections.Generic.List<string>(ReadAllLinesWithRetry(csvPath));
            }
            else
            {
                // Create new file with header
                string header = "trial_id,speed,verticalSpeed,idleUpwardSpeed,lifeTime,downHealthPairSec,removeHealthWithCollide,timeBetweenCollides,healHealthPoint,factor_force,o2_result";
                lines = new System.Collections.Generic.List<string> { header };
                Debug.Log($"Creating new random parameters file: {csvPath}");
            }
            
            // Check if this trial already exists
            bool trialExists = false;
            for (int i = 1; i < lines.Count; i++)
            {
                var fields = lines[i].Split(',');
                if (fields.Length > 0 && fields[0] == trialData.trialId.ToString())
                {
                    // Update existing trial
                    lines[i] = FormatTrialDataLine(trialData);
                    trialExists = true;
                    Debug.Log($"Updated existing trial {trialData.trialId} in random parameters file");
                    break;
                }
            }
            
            if (!trialExists)
            {
                // Add new trial
                lines.Add(FormatTrialDataLine(trialData));
                Debug.Log($"Added new trial {trialData.trialId} to random parameters file");
            }
            
            // Save file
            WriteAllLinesWithRetry(csvPath, lines.ToArray());
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error saving to random parameters CSV: {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Format trial data as CSV line
    /// </summary>
    private string FormatTrialDataLine(TrialDataModels.TrialData trialData)
    {
        return string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "{0},{1:F2},{2:F2},{3:F2},{4:F2},{5:F2},{6:F2},{7:F2},{8:F2},{9:F2},{10:F2}",
            trialData.trialId,
            trialData.speed,
            trialData.verticalSpeed,
            trialData.idleUpwardSpeed,
            trialData.lifeTime,
            trialData.downHealthPairSec,
            trialData.removeHealthWithCollide,
            trialData.timeBetweenCollides,
            trialData.healHealthPoint,
            trialData.factorForce,
            trialData.finalOxygenRemaining
        );
    }
    
    /// <summary>
    /// Save to timestamped results file (backup/history)
    /// </summary>
    private bool SaveToTimestampedCSV(TrialDataModels.TrialData trialData, string suffix = "")
    {
        try
        {
            string resultsPath = GetResultsCSVPath(suffix);
            bool fileExists = System.IO.File.Exists(resultsPath);
            
            using (System.IO.StreamWriter writer = new System.IO.StreamWriter(resultsPath, true))
            {
                if (!fileExists)
                {
                    string header = "TrialID,Speed,VerticalSpeed,IdleUpwardSpeed,LifeTime," +
                                   "DownHealthPerSec,CollisionDamage,TimeBetweenCollisions," +
                                   "HealHealthPoint,FactorForce,FinalOxygen,Completed";
                    writer.WriteLine(header);
                }
                
                string dataLine = string.Format("{0},{1:F2},{2:F2},{3:F2},{4:F2},{5:F2},{6:F2},{7:F2},{8:F2},{9:F2},{10:F2},{11}",
                    trialData.trialId,
                    trialData.speed,
                    trialData.verticalSpeed,
                    trialData.idleUpwardSpeed,
                    trialData.lifeTime,
                    trialData.downHealthPairSec,
                    trialData.removeHealthWithCollide,
                    trialData.timeBetweenCollides,
                    trialData.healHealthPoint,
                    trialData.factorForce,
                    trialData.finalOxygenRemaining,
                    trialData.completed ? 1 : 0
                );
                
                writer.WriteLine(dataLine);
                writer.Flush();
            }
            
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error saving to timestamped CSV: {e.Message}");
            return false;
        }
    }

    private string GetResultsCSVPath(string suffix = "")
    {
        string fileName = $"TrialResults{suffix}_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv";
        string path = System.IO.Path.Combine(Application.persistentDataPath, fileName);
        Debug.Log($"========================================");
        Debug.Log($"SAVING TRIAL RESULTS TO: {path}");
        Debug.Log($"Folder: {Application.persistentDataPath}");
        Debug.Log($"========================================");
        return path;
    }
    
    /// <summary>
    /// Open the folder where trial results are saved (for debugging/access)
    /// Can be called from Inspector or UI button
    /// </summary>
    [ContextMenu("Open Results Folder")]
    public void OpenResultsFolder()
    {
        string path = Application.persistentDataPath;
        
        #if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        System.Diagnostics.Process.Start("explorer.exe", path);
        #elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        System.Diagnostics.Process.Start("open", path);
        #else
        System.Diagnostics.Process.Start(path);
        #endif
        
        Debug.Log($"Opened folder: {path}");
    }
    
 
    
  
    private TrialDataModels.TrialData LoadParametersFromFilePath(int trialNumber)
    {
        Debug.Log($"=== LOADING TRIAL PARAMETERS FOR TRIAL {trialNumber} ===");
        try
        {
            string absolutePath = System.IO.Path.Combine(Application.dataPath, trialParametersPath.Replace("\\", "/"));
            Debug.Log($"Attempting to load trial parameters from: {absolutePath}");
            
            if (!System.IO.File.Exists(absolutePath))
            {
                Debug.LogError($"Trial parameters file not found at: {absolutePath}");
                return null;
            }

            // Use ReadAllLinesWithRetry to handle file locks (e.g., Excel)
            var lines = ReadAllLinesWithRetry(absolutePath);
            // Trim line endings (especially \r from Windows line endings)
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i] = lines[i].TrimEnd('\r');
            }
            Debug.Log($"Trial parameters file has {lines.Length} lines");
            
            if (lines.Length <= 1)
            {
                Debug.LogError("Trial parameters file is empty or has no data rows!");
                return null;
            }

            // Find the correct row for this trial number
            int dataIndex = -1;
            Debug.Log($"Searching for trial {trialNumber} in {lines.Length} lines...");
            for (int i = 1; i < lines.Length; i++)
            {
                string[] searchFields = lines[i].Split(',');
                if (searchFields.Length > 0)
                {
                    Debug.Log($"  Line {i}: First field = '{searchFields[0]}' (length={searchFields.Length})");
                    if (int.TryParse(searchFields[0], out int trialId))
                    {
                        Debug.Log($"    Parsed trial ID: {trialId}");
                        if (trialId == trialNumber)
                        {
                            dataIndex = i;
                            Debug.Log($"  FOUND: Trial {trialNumber} at line {i}");
                            break;
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"    Could not parse '{searchFields[0]}' as integer");
                    }
                }
            }
            
            if (dataIndex == -1)
            {
                Debug.LogError($"Trial {trialNumber} not found in CSV file!");
                Debug.LogError($"Available trials: {string.Join(", ", lines.Skip(1).Select(l => l.Split(',')[0]))}");
                return null;
            }
            
            Debug.Log($"Loading trial {trialNumber} from row {dataIndex}");
            
            string[] dataFields = lines[dataIndex].Split(',');
            
            // Need only 10 fields for parameters (0-9: trialId + 9 parameters)
            // Oxygen columns (10+) are written AFTER running the trial
            if (dataFields.Length < 10)
            {
                Debug.LogError($"File row {dataIndex} has insufficient fields: {dataFields.Length}, needs at least 10 (trialId + 9 parameters)");
                return null;
            }

            var data = new TrialDataModels.TrialData
            {
                trialId = trialNumber,
                speed = float.Parse(dataFields[1], CultureInfo.InvariantCulture),
                verticalSpeed = float.Parse(dataFields[2], CultureInfo.InvariantCulture),
                idleUpwardSpeed = float.Parse(dataFields[3], CultureInfo.InvariantCulture),
                lifeTime = float.Parse(dataFields[4], CultureInfo.InvariantCulture),
                downHealthPairSec = float.Parse(dataFields[5], CultureInfo.InvariantCulture),
                removeHealthWithCollide = float.Parse(dataFields[6], CultureInfo.InvariantCulture),
                timeBetweenCollides = float.Parse(dataFields[7], CultureInfo.InvariantCulture),
                healHealthPoint = float.Parse(dataFields[8], CultureInfo.InvariantCulture),
                factorForce = float.Parse(dataFields[9], CultureInfo.InvariantCulture)
            };

            //Debug.Log($"Loaded parameters: speed={data.speed}, heal={data.healHealthPoint}");
            return data;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error loading trial parameters: {e.Message}");
            return null;
        }
    }
    
   // Generate random parameters within specified ranges
    private TrialDataModels.TrialData GenerateRandomParametersInternal()
    {
        var data = new TrialDataModels.TrialData
        {
            speed = Random.Range(parameterRanges.speedRange.x, parameterRanges.speedRange.y),
            verticalSpeed = Random.Range(parameterRanges.verticalSpeedRange.x, parameterRanges.verticalSpeedRange.y),
            idleUpwardSpeed = Random.Range(parameterRanges.idleUpwardSpeedRange.x, parameterRanges.idleUpwardSpeedRange.y),
            healHealthPoint = Random.Range(parameterRanges.healHealthPointRange.x, parameterRanges.healHealthPointRange.y),
            timeBetweenCollides = Random.Range(parameterRanges.timeBetweenCollidesRange.x, parameterRanges.timeBetweenCollidesRange.y),
            removeHealthWithCollide = Random.Range(parameterRanges.removeHealthWithCollideRange.x, parameterRanges.removeHealthWithCollideRange.y),
            downHealthPairSec = Random.Range(parameterRanges.downHealthPairSecRange.x, parameterRanges.downHealthPairSecRange.y),
            lifeTime = Random.Range(parameterRanges.lifeTimeRange.x, parameterRanges.lifeTimeRange.y),
            factorForce = 3f
        };
        
        Debug.Log($"Generated random parameters: speed={data.speed:F1}, heal={data.healHealthPoint:F1}");
        return data;
    }
    
   
    private void ApplyParametersToGame(TrialDataModels.TrialData data)
    {
        // Apply to PlayerMovement
        if (playerMovement != null)
        {
            if (playerMovement.speed_inputField != null)
                playerMovement.speed_inputField.text = data.speed.ToString("F1");
            if (playerMovement.vertical_speed_inputField != null)
                playerMovement.vertical_speed_inputField.text = data.verticalSpeed.ToString("F1");
            if (playerMovement.idle_upward_speed_inputField != null)
                playerMovement.idle_upward_speed_inputField.text = data.idleUpwardSpeed.ToString("F2");
            
            playerMovement.speed = data.speed;
            playerMovement.verticalSpeed = data.verticalSpeed;
            playerMovement.idleUpwardSpeed = data.idleUpwardSpeed;
         //   Debug.Log($"Applied movement parameters: speed={data.speed}, verticalSpeed={data.verticalSpeed}");
        }
        
        // Apply to PlayerLife
        if (playerLife != null)
        {
            if (playerLife.healHealthPoints_inputField != null)
                playerLife.healHealthPoints_inputField.text = data.healHealthPoint.ToString("F1");
            if (playerLife.timeBetweenCollides_inputField != null)
                playerLife.timeBetweenCollides_inputField.text = data.timeBetweenCollides.ToString("F1");
            if (playerLife.removeHealthWithCollide_inputField != null)
                playerLife.removeHealthWithCollide_inputField.text = data.removeHealthWithCollide.ToString("F1");
                
            playerLife.didntGetInputsYet = true;
            playerLife.ProcessUserInputs();
            Debug.Log($"Applied collision parameters: heal={data.healHealthPoint}, damage={data.removeHealthWithCollide}");
        }
        
        // Apply to Health
        if (health != null)
        {
            if (health.downHealthPairSec_inputField != null)
                health.downHealthPairSec_inputField.text = data.downHealthPairSec.ToString("F2");
            if (health.lifeTime_inputField != null)
                health.lifeTime_inputField.text = data.lifeTime.ToString("F2");
                
            health.didntGetInputsYet = true;
            health.ProcessUserInputs();
            health.StopAllCoroutines();
            
            Debug.Log($"Applied health parameters: dropPerSec={data.downHealthPairSec:F2}, lifeTime={data.lifeTime:F2}");
        }
        
        Debug.Log($"Movement: Speed={data.speed:F1}, VerticalSpeed={data.verticalSpeed:F1}, IdleUpward={data.idleUpwardSpeed:F3}");
        Debug.Log($"Health: LifeTime={data.lifeTime:F2}, DropPerSec={data.downHealthPairSec:F2}");
        Debug.Log($"Collision: Damage={data.removeHealthWithCollide:F1}, TimeBetween={data.timeBetweenCollides:F1}");
        Debug.Log($"Oxygen: Heal={data.healHealthPoint:F1}, FactorForce={data.factorForce:F1}");
    }
    
    /// <summary>
    /// Update O2_Wide_AllSets.csv with ALL oxygen values from all trials
    /// Called automatically when Trial 5 completes
    /// </summary>
    private void UpdateO2WideAllSets()
    {
        try
        {
           
            
            string csvPath = System.IO.Path.Combine(Application.dataPath, trialParametersPath);
            
            if (!System.IO.File.Exists(csvPath))
            {
                Debug.LogError($"Trial_5_runs_.csv not found at: {csvPath}");
                return;
            }
            
            var lines = System.IO.File.ReadAllLines(csvPath);
            if (lines.Length <= 1)
            {
                Debug.LogError("Trial_5_runs_.csv is empty!");
                return;
            }
            
            // Parse header to find o2_run columns
            var headerFields = lines[0].Split(',');
            var o2ColumnIndices = new System.Collections.Generic.List<int>();
            
            for (int i = 0; i < headerFields.Length; i++)
            {
                if (headerFields[i].StartsWith("o2_run"))
                {
                    o2ColumnIndices.Add(i);
                }
            }
            
            if (o2ColumnIndices.Count == 0)
            {
                Debug.LogError("No o2_run columns found in Trial_5_runs_.csv!");
                return;
            }
            
            // Find the LAST o2_run column (most recent run)
            int lastO2ColumnIndex = o2ColumnIndices[o2ColumnIndices.Count - 1];
            string lastO2ColumnName = headerFields[lastO2ColumnIndex];
            Debug.Log($"Using last oxygen column: {lastO2ColumnName} (index {lastO2ColumnIndex})");
            
            // Extract ALL oxygen values from ALL trials for this run
            var oxygenValues = new System.Collections.Generic.List<string>();
            
            for (int i = 1; i < lines.Length; i++) // Skip header
            {
                var fields = lines[i].Split(',');
                
                if (fields.Length <= lastO2ColumnIndex)
                    continue;
                    
                string oxygenValue = fields[lastO2ColumnIndex].Trim();
                
                if (!string.IsNullOrEmpty(oxygenValue) && float.TryParse(oxygenValue, out float o2))
                {
                    oxygenValues.Add(o2.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
                    Debug.Log($"  Trial {i}: {o2:F1}%");
                }
            }
            
            if (oxygenValues.Count == 0)
            {
                Debug.LogWarning("No oxygen values found in last column!");
                return;
            }
            
            // Now update O2_Wide_AllSets.csv
            string o2WidePath = System.IO.Path.Combine(Application.dataPath, "Data", "RegressionResults", "O2_Wide_AllSets.csv");
            
            // Ensure directory exists
            string directory = System.IO.Path.GetDirectoryName(o2WidePath);
            if (!System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }
            
            // Build header
            var wideHeader = new System.Collections.Generic.List<string> { "timestamp" };
            for (int i = 0; i < oxygenValues.Count; i++)
            {
                wideHeader.Add($"o2_remaining_{i+1}");
            }
            
            // Build new row
            var newRow = new System.Collections.Generic.List<string>();
            newRow.Add(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            newRow.AddRange(oxygenValues);
            
            // If file doesn't exist, create it with header
            if (!System.IO.File.Exists(o2WidePath))
            {
                System.IO.File.WriteAllLines(o2WidePath, new[]
                {
                    string.Join(",", wideHeader),
                    string.Join(",", newRow)
                });
                Debug.Log($"Created O2_Wide_AllSets.csv with {oxygenValues.Count} oxygen values");
                return;
            }
            
            // File exists - read it and append
            var allLines = System.IO.File.ReadAllLines(o2WidePath).ToList();
            
            if (allLines.Count == 0)
            {
                allLines.Add(string.Join(",", wideHeader));
            }
            
            // Update header if needed (in case we have more columns now)
            var existingHeader = allLines[0].Split(',').ToList();
            int existingO2Cols = System.Math.Max(0, existingHeader.Count - 1);
            int neededCols = oxygenValues.Count;
            
            if (existingO2Cols < neededCols)
            {
                // Add more column headers
                for (int i = existingO2Cols; i < neededCols; i++)
                {
                    existingHeader.Add($"o2_remaining_{i+1}");
                }
                
                // Pad existing rows with empty values
                for (int i = 1; i < allLines.Count; i++)
                {
                    var parts = allLines[i].Split(',').ToList();
                    while (parts.Count < existingHeader.Count)
                    {
                        parts.Add(string.Empty);
                    }
                    allLines[i] = string.Join(",", parts);
                }
                
                allLines[0] = string.Join(",", existingHeader);
            }
            
            // Pad new row if needed
            while (newRow.Count < existingHeader.Count)
            {
                newRow.Add(string.Empty);
            }
            
            // Append new row
            allLines.Add(string.Join(",", newRow));
            
            // Save file
            System.IO.File.WriteAllLines(o2WidePath, allLines);
            
            Debug.Log($"Updated O2_Wide_AllSets.csv: Added {oxygenValues.Count} oxygen values from {lastO2ColumnName}");
            Debug.Log($"   Values: {string.Join(", ", oxygenValues)}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error updating O2_Wide_AllSets.csv: {e.Message}");
            Debug.LogError($"Stack trace: {e.StackTrace}");
        }
    }
    
    /// <summary>
    /// Read all lines from a file with retry logic to handle file locks
    /// </summary>
    private string[] ReadAllLinesWithRetry(string path, int maxRetries = 3, int delayMs = 100)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                using (var stream = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite))
                using (var reader = new System.IO.StreamReader(stream))
                {
                    var lines = new System.Collections.Generic.List<string>();
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        lines.Add(line);
                    }
                    return lines.ToArray();
                }
            }
            catch (System.IO.IOException) when (i < maxRetries - 1)
            {
                Debug.LogWarning($"File locked, retrying... ({i + 1}/{maxRetries})");
                System.Threading.Thread.Sleep(delayMs);
            }
        }
        throw new System.IO.IOException($"Failed to read file after {maxRetries} attempts: {path}");
    }
    
    /// <summary>
    /// Write all lines to a file with retry logic to handle file locks
    /// </summary>
    private void WriteAllLinesWithRetry(string path, string[] lines, int maxRetries = 3, int delayMs = 100)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                using (var stream = new System.IO.FileStream(path, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.Read))
                using (var writer = new System.IO.StreamWriter(stream))
                {
                    foreach (var line in lines)
                    {
                        writer.WriteLine(line);
                    }
                }
                return;
            }
            catch (System.IO.IOException) when (i < maxRetries - 1)
            {
                Debug.LogWarning($"File locked, retrying write... ({i + 1}/{maxRetries})");
                System.Threading.Thread.Sleep(delayMs);
            }
        }
        throw new System.IO.IOException($"Failed to write file after {maxRetries} attempts: {path}");
    }
}
