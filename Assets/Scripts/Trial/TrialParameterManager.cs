using UnityEngine;
using TMPro;
using System.Globalization;
using System.Linq;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif

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
    [SerializeField] private getEventFromAmadeoClientDiver amadeoClientDiver;  // For applying factorForce
    
    [Header("Parameter Settings")]
    [SerializeField] private string trialParametersPath = "Data/Trials/Trial_5_runs_.csv";
    [SerializeField] private string randomParametersPath = "Data/Trials/random_trial.csv";
    [SerializeField] private TextAsset trialParametersFile;
    [SerializeField] private TrialDataModels.ParameterRanges parameterRanges = new TrialDataModels.ParameterRanges();
    
    private bool currentModeIsRandom = false;
    private int lastSavedTrialId = -1;
    
    public void ResetForNewRun()
    {
        lastSavedTrialId = -1;
    }
    
    public TrialDataModels.TrialData LoadAndApplyTrialParameters(int trialNumber, bool useRandomParameters = false)
    {
        if (trialNumber == 1)
        {
            ResetForNewRun();
        }
        
        // useRandomParameters: true = RANDOM mode, false = CONSTANT (CSV) mode
        currentModeIsRandom = useRandomParameters;
        
        TrialDataModels.TrialData data;
        
        if (useRandomParameters)
        {
            // Generate random parameters
            data = GenerateRandomParametersInternal();
            Debug.Log($"[TrialParameterManager] Trial {trialNumber}: Generated RANDOM parameters");
        }
        else
        {
            // Load constant parameters from CSV
            data = LoadParametersFromFilePath(trialNumber);
            
            if (data == null)
            {
                Debug.LogError($"CRITICAL: Failed to load parameters for trial {trialNumber}!");
                data = GenerateRandomParametersInternal(); // Emergency fallback
                Debug.LogWarning($"[TrialParameterManager] Trial {trialNumber}: Using RANDOM fallback due to CSV load failure");
            }
            else
            {
                Debug.Log($"[TrialParameterManager] Trial {trialNumber}: Loaded CONSTANT (CSV) parameters");
            }
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
            
            if (trialData.trialId == lastSavedTrialId)
            {
                Debug.LogWarning($"Trial {trialData.trialId} already saved - skipping duplicate save");
                return true;
            }
            
            bool saved;
            
            if (currentModeIsRandom)
            {
                saved = SaveToRandomParametersCSV(trialData);
            }
            else
            {
                saved = UpdateOriginalCSV(trialData);
            }
            
            TrialDataCache.Instance.SaveTrialOxygen(trialData.trialId, trialData.finalOxygenRemaining);
            
            if (saved)
            {
                lastSavedTrialId = trialData.trialId;
                //Debug.Log($"[TrialParameterManager] Trial {trialData.trialId} saved successfully (Mode: {(currentModeIsRandom ? "RANDOM" : "CONSTANT")})");
                return true;
            }
            else
            {
                Debug.LogWarning($"[TrialParameterManager] Failed to save trial {trialData.trialId}");
                return false;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error saving trial results: {e.Message}");
            return false;
        }
    }
    
    // Update the original Trial_5_runs_.csv - adds new column for each run
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
    
   
    /// Save random parameters to random_trial.csv
    /// Keeps random trials separate from regular CSV trials
  
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
                lines = new System.Collections.Generic.List<string>(CsvFileHelper.ReadAllLinesWithRetry(csvPath));
            }
            else
            {
                // Create new file with header (must match FormatTrialDataLine: 13 columns including duration)
                string header = "trial_id,speed,verticalSpeed,idleUpwardSpeed,lifeTime,RemoveHealthEveryLifeTime,removeHealthWithCollide,timeBetweenCollides,healHealthPoint,factorForce,IsAmadeoMode,o2_result,duration";
                lines = new System.Collections.Generic.List<string> { header };
            }
            
            // Fix header if it's missing columns (for existing files with old format)
            if (lines.Count > 0)
            {
                var headerFields = lines[0].Split(',');
                var expectedHeader = "trial_id,speed,verticalSpeed,idleUpwardSpeed,lifeTime,RemoveHealthEveryLifeTime,removeHealthWithCollide,timeBetweenCollides,healHealthPoint,factorForce,IsAmadeoMode,o2_result,duration";
                if (lines[0] != expectedHeader)
                {
                    Debug.LogWarning($"[TrialParameterManager] Fixing CSV header format (old: {lines[0]})");
                    lines[0] = expectedHeader;
                }
            }
            
            // Check if this trial already exists
            bool trialExists = false;
            for (int i = 1; i < lines.Count; i++)
            {
                var fields = lines[i].Split(',');
                if (fields.Length > 0 && fields[0] == trialData.trialId.ToString())
                {
                    // Update existing trial with correctly formatted line
                    lines[i] = FormatTrialDataLine(trialData);
                    trialExists = true;
                    break;
                }
            }
            
            if (!trialExists)
            {
                // Add new trial
                lines.Add(FormatTrialDataLine(trialData));
            }
            
            // Save file
            CsvFileHelper.WriteAllLinesWithRetry(csvPath, lines.ToArray());
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error saving to random parameters CSV: {e.Message}");
            return false;
        }
    }
    
    // Format trial data as CSV line
    private string FormatTrialDataLine(TrialDataModels.TrialData trialData)
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
    
    
    // Open the folder where trial results are saved (for debugging/access)
    // Can be called from Inspector, UI button, or Unity's Tools menu
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
    
    // Static menu item for opening results folder from Unity's Tools menu
    #if UNITY_EDITOR
    [MenuItem("Tools/AquaGuardian/Open Results Folder")]
    private static void OpenResultsFolderFromMenu()
    {
        string path = Application.persistentDataPath;
        System.Diagnostics.Process.Start("explorer.exe", path);
        Debug.Log($"Opened results folder: {path}");
    }
    
    [MenuItem("Tools/AquaGuardian/Open Trial Parameters Folder")]
    private static void OpenTrialParametersFolder()
    {
        string path = System.IO.Path.Combine(Application.dataPath, "Data", "Trials");
        if (System.IO.Directory.Exists(path))
        {
            System.Diagnostics.Process.Start("explorer.exe", path);
           // Debug.Log($"Opened trial parameters folder: {path}");
        }
        else
        {
            Debug.LogError($"Trial parameters folder not found: {path}");
        }
    }
    
    [MenuItem("Tools/AquaGuardian/Open Regression Results Folder")]
    private static void OpenRegressionResultsFolder()
    {
        string path = System.IO.Path.Combine(Application.dataPath, "Data", "RegressionResults");
        if (System.IO.Directory.Exists(path))
        {
            System.Diagnostics.Process.Start("explorer.exe", path);
            //Debug.Log($"Opened regression results folder: {path}");
        }
        else
        {
            Debug.LogError($"Regression results folder not found: {path}");
        }
    }
    #endif
    
 
    
  
    private TrialDataModels.TrialData LoadParametersFromFilePath(int trialNumber)
    {
     //   Debug.Log($"=== LOADING TRIAL PARAMETERS FOR TRIAL {trialNumber} ===");
        try
        {
            string absolutePath = System.IO.Path.Combine(Application.dataPath, trialParametersPath.Replace("\\", "/"));
            
            if (!System.IO.File.Exists(absolutePath))
            {
                Debug.LogError($"Trial parameters file not found at: {absolutePath}");
                return null;
            }

            // Use CsvFileHelper to handle file locks (e.g., Excel)
            var lines = CsvFileHelper.ReadAllLinesWithRetry(absolutePath);
            // Trim line endings (especially \r from Windows line endings)
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i] = lines[i].TrimEnd('\r');
            }
            
            if (lines.Length <= 1)
            {
                Debug.LogError("Trial parameters file is empty or has no data rows!");
                return null;
            }

            // Find the correct row for this trial number
            int dataIndex = -1;
            for (int i = 1; i < lines.Length; i++)
            {
                string[] searchFields = lines[i].Split(',');
                if (searchFields.Length > 0)
                {
                    if (int.TryParse(searchFields[0], out int trialId))
                    {
                        if (trialId == trialNumber)
                        {
                            dataIndex = i;
                            break;
                        }
                    }
                }
            }
            
            if (dataIndex == -1)
            {
                Debug.LogError($"Trial {trialNumber} not found in CSV file! Available trials: {string.Join(", ", lines.Skip(1).Select(l => l.Split(',')[0]))}");
                return null;
            }
            
            string[] dataFields = lines[dataIndex].Split(',');
            
            // Need only 10 fields for parameters (0-9: trialId + 9 parameters)
            // Oxygen columns (10+) are written AFTER running the trial
            if (dataFields.Length < 10)
            {
                Debug.LogError($"File row {dataIndex} has insufficient fields: {dataFields.Length}, needs at least 10 (trialId + 9 parameters)");
                return null;
            }

            // Parse header to get column indices
            var header = lines[0].Split(',');
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
            float isAmadeoMode = colIsAmadeo >= 0 ? CsvFileHelper.ParseField(dataFields, colIsAmadeo, 10) : 0f;
            
            // If keyboard mode (IsAmadeoMode = 0), don't read factorForce from CSV (set to 0)
            float factorForceValue = 0f;
            if (isAmadeoMode > 0.5f && colForce >= 0)
            {
                factorForceValue = CsvFileHelper.ParseField(dataFields, colForce, 9);
            }

            var data = new TrialDataModels.TrialData
            {
                trialId = trialNumber,
                speed = CsvFileHelper.ParseField(dataFields, colSpeed, 1),
                verticalSpeed = CsvFileHelper.ParseField(dataFields, colVSpeed, 2),
                idleUpwardSpeed = CsvFileHelper.ParseField(dataFields, colIdleUp, 3),
                lifeTime = CsvFileHelper.ParseField(dataFields, colLife, 4),
                RemoveHealthEveryLifeTime = CsvFileHelper.ParseField(dataFields, colDrop, 5),
                removeHealthWithCollide = CsvFileHelper.ParseField(dataFields, colCollide, 6),
                timeBetweenCollides = CsvFileHelper.ParseField(dataFields, colBetween, 7),
                healHealthPoint = CsvFileHelper.ParseField(dataFields, colHeal, 8),
                factorForce = factorForceValue,  // 0 if keyboard mode, read from CSV only if Amadeo mode
                IsAmadeoMode = isAmadeoMode
            };

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
            RemoveHealthEveryLifeTime = Random.Range(parameterRanges.RemoveHealthEveryLifeTimeRange.x, parameterRanges.RemoveHealthEveryLifeTimeRange.y),
            lifeTime = Random.Range(parameterRanges.lifeTimeRange.x, parameterRanges.lifeTimeRange.y),
            factorForce = Random.Range(parameterRanges.factorForceRange.x, parameterRanges.factorForceRange.y),
            IsAmadeoMode = 0f  // Will be set when trial ends based on actual input mode
        };
        
        return data;
    }
    
   
    private void ApplyParametersToGame(TrialDataModels.TrialData data)
    {
        // Log all parameters in single line
        Debug.Log($"[TrialParameters] Trial {data.trialId}: Speed={data.speed:F2} VerticalSpeed={data.verticalSpeed:F2} IdleUpwardSpeed={data.idleUpwardSpeed:F2} LifeTime={data.lifeTime:F2}s RemoveHealthPerLifeCycle={data.RemoveHealthEveryLifeTime:F2} RemoveHealthWithCollide={data.removeHealthWithCollide:F2} TimeBetweenCollides={data.timeBetweenCollides:F2}s HealHealthPoint={data.healHealthPoint:F2} FactorForce={data.factorForce:F2}");
        
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
        }
        
        // Apply to Health
        if (health != null)
        {
            if (health.RemoveHealthEveryLifeTime_inputField != null)
                health.RemoveHealthEveryLifeTime_inputField.text = data.RemoveHealthEveryLifeTime.ToString("F2");
            if (health.lifeTime_inputField != null)
                health.lifeTime_inputField.text = data.lifeTime.ToString("F2");
                
            health.didntGetInputsYet = true;
            health.ProcessUserInputs();
            health.StopAllCoroutines();
        }
        
        // Apply factorForce to Amadeo client diver (only relevant when Amadeo is connected/emulation)
        if (amadeoClientDiver != null && amadeoClientDiver.factor_force_inputField != null)
        {
            amadeoClientDiver.factor_force_inputField.text = data.factorForce.ToString("F2");
        }
    }
    
    
}
