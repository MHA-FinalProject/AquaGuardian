using UnityEngine;
using TMPro;
using System.Globalization;

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
    [SerializeField] private string trialParametersPath = "Data/Trial_5_runs_.csv";
    [SerializeField] private TextAsset trialParametersFile;
    [SerializeField] private PanelOpenUp.ParameterRanges parameterRanges = new PanelOpenUp.ParameterRanges();
    
    /// <summary>
    /// Load and apply trial parameters for current trial
    /// </summary>
    public PanelOpenUp.TrialData LoadAndApplyTrialParameters(int trialNumber)
    {
        Debug.Log($"Loading parameters for trial {trialNumber}");
        
        // Load parameters from CSV
        PanelOpenUp.TrialData data = LoadParametersFromFilePath(trialNumber);
        
        if (data == null)
        {
            Debug.LogError($"CRITICAL: Failed to load parameters for trial {trialNumber}!");
            data = GenerateRandomParametersInternal(); // Emergency fallback
        }
        
        data.trialId = trialNumber;
        Debug.Log($"Parameters loaded: Speed={data.speed}, Heal={data.healHealthPoint}");
        
        // Apply parameters to game components
        ApplyParametersToGame(data);
        Debug.Log("Parameters applied to all game components");
        
        return data;
    }
    
    /// <summary>
    /// Save trial result to CSV
    /// </summary>
    public bool SaveTrialResultToCSV(PanelOpenUp.TrialData data)
    {
        try
        {
            string csvPath = System.IO.Path.Combine(Application.dataPath, "Data", "Trial_5_runs_.csv");
            Debug.Log($"=== SAVING TRIAL RESULT ===");
            Debug.Log($"Trial ID: {data.trialId}");
            Debug.Log($"Final Oxygen: {data.finalOxygenRemaining:F1}%");
            Debug.Log($"Completed: {data.completed}");
            Debug.Log($"CSV Path: {csvPath}");
            
            if (!System.IO.File.Exists(csvPath))
            {
                Debug.LogError($"CSV file not found at: {csvPath}");
                return false;
            }
            
            string[] lines = System.IO.File.ReadAllLines(csvPath);
            Debug.Log($"CSV has {lines.Length} lines");
            
            if (lines.Length < 2)
            {
                Debug.LogError("CSV file must have at least a header row and one data row");
                return false;
            }
            
            int dataRowIndex = data.trialId;
            if (dataRowIndex >= lines.Length)
            {
                Debug.LogError($"Trial ID {data.trialId} exceeds available rows!");
                return false;
            }
            
            string[] fields = lines[dataRowIndex].Split(',');
            Debug.Log($"Row {dataRowIndex} has {fields.Length} columns");
            
            const int EXPECTED_COLUMNS = 11;
            const int OXYGEN_COLUMN_INDEX = 10;
            
            if (fields.Length < EXPECTED_COLUMNS)
            {
                Debug.LogError($"CSV row {dataRowIndex} has insufficient columns!");
                return false;
            }
            
            if (int.TryParse(fields[0], out int csvTrialId) && csvTrialId != data.trialId)
            {
                Debug.LogWarning($"Trial ID mismatch! Expected {data.trialId}, found {csvTrialId}");
            }
            
            string previousValue = fields[OXYGEN_COLUMN_INDEX];
            fields[OXYGEN_COLUMN_INDEX] = data.finalOxygenRemaining.ToString("F1", CultureInfo.InvariantCulture);
            lines[dataRowIndex] = string.Join(",", fields);
            
            string backupPath = csvPath + ".backup";
            System.IO.File.Copy(csvPath, backupPath, true);
            System.IO.File.WriteAllLines(csvPath, lines);
            
            Debug.Log($"SUCCESS: Updated trial {data.trialId} oxygen result");
            Debug.Log($"  Previous value: '{previousValue}'");
            Debug.Log($"  New value: '{fields[OXYGEN_COLUMN_INDEX]}'");
            
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error saving trial result to CSV: {e.Message}");
            return false;
        }
    }
    
    // ========== PRIVATE METHODS ==========
    
    /// <summary>
    /// Load parameters from file path
    /// </summary>
    private PanelOpenUp.TrialData LoadParametersFromFilePath(int trialNumber)
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

            var lines = System.IO.File.ReadAllLines(absolutePath);
            Debug.Log($"Trial parameters file has {lines.Length} lines");
            
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
                if (searchFields.Length > 0 && int.TryParse(searchFields[0], out int trialId) && trialId == trialNumber)
                {
                    dataIndex = i;
                    break;
                }
            }
            
            if (dataIndex == -1)
            {
                Debug.LogError($"Trial {trialNumber} not found in CSV file!");
                return null;
            }
            
            Debug.Log($"Loading trial {trialNumber} from row {dataIndex}");
            
            string[] dataFields = lines[dataIndex].Split(',');
            
            if (dataFields.Length < 11)
            {
                Debug.LogError($"File row {dataIndex} has insufficient fields: {dataFields.Length}, needs 11");
                return null;
            }

            var data = new PanelOpenUp.TrialData
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

            Debug.Log($"Loaded parameters: speed={data.speed}, heal={data.healHealthPoint}");
            return data;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error loading trial parameters: {e.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Generate random parameters as fallback
    /// </summary>
    private PanelOpenUp.TrialData GenerateRandomParametersInternal()
    {
        var data = new PanelOpenUp.TrialData
        {
            speed = Random.Range(parameterRanges.speedRange.x, parameterRanges.speedRange.y),
            verticalSpeed = Random.Range(parameterRanges.verticalSpeedRange.x, parameterRanges.verticalSpeedRange.y),
            idleUpwardSpeed = Random.Range(parameterRanges.idleUpwardSpeedRange.x, parameterRanges.idleUpwardSpeedRange.y),
            healHealthPoint = Random.Range(parameterRanges.oxygenHealRange.x, parameterRanges.oxygenHealRange.y),
            timeBetweenCollides = Random.Range(parameterRanges.timeBetweenCollidesRange.x, parameterRanges.timeBetweenCollidesRange.y),
            removeHealthWithCollide = Random.Range(parameterRanges.collisionDamageRange.x, parameterRanges.collisionDamageRange.y),
            downHealthPairSec = Random.Range(parameterRanges.oxygenDropPerSecRange.x, parameterRanges.oxygenDropPerSecRange.y),
            lifeTime = Random.Range(parameterRanges.lifeTimeRange.x, parameterRanges.lifeTimeRange.y),
            factorForce = 3f
        };
        
        Debug.Log($"Generated random parameters: speed={data.speed:F1}, heal={data.healHealthPoint:F1}");
        return data;
    }
    
    /// <summary>
    /// Apply parameters to game components
    /// </summary>
    private void ApplyParametersToGame(PanelOpenUp.TrialData data)
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
            Debug.Log($"Applied movement parameters: speed={data.speed}, verticalSpeed={data.verticalSpeed}");
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
        
        Debug.Log($"=== PARAMETER APPLICATION COMPLETE ===");
        Debug.Log($"Movement: Speed={data.speed:F1}, VerticalSpeed={data.verticalSpeed:F1}, IdleUpward={data.idleUpwardSpeed:F3}");
        Debug.Log($"Health: LifeTime={data.lifeTime:F2}, DropPerSec={data.downHealthPairSec:F2}");
        Debug.Log($"Collision: Damage={data.removeHealthWithCollide:F1}, TimeBetween={data.timeBetweenCollides:F1}");
        Debug.Log($"Oxygen: Heal={data.healHealthPoint:F1}, FactorForce={data.factorForce:F1}");
    }
}
