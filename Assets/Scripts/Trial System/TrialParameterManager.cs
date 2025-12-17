using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif

/**
 * Manages trial parameter loading and application
 * Handles CSV reading, parameter generation, and applying to game components
 */
public class TrialParameterManager : MonoBehaviour
{
    [Header("Component References")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerLife playerLife;
    [SerializeField] private Health health;
    [SerializeField] private getEventFromAmadeoClientDiver amadeoClientDiver;

    [Header("Parameter Settings")]
    [SerializeField] private string trialParametersPath = "Data/Trials/Trial_5_runs_.csv";
    [SerializeField] private TextAsset trialParametersFile; // Fallback if file path doesn't work
    [SerializeField] private string randomParametersPath = "Data/Trials/Trial_Random_Parameters.csv";
    [SerializeField] private TextAsset randomParametersFile; // Fallback if file path doesn't work
    [SerializeField] private TrialDataModels.ParameterRanges parameterRanges = new();

    private bool currentModeIsRandom = false;

    public TrialDataModels.TrialData LoadAndApplyTrialParameters(int trialNumber, bool useRandomParameters = false)
    {
        currentModeIsRandom = useRandomParameters;

        TrialDataModels.TrialData data;

        if (useRandomParameters)
        {
            // Try to load from random parameters CSV/TextAsset first
            data = TrialDataService.LoadTrialParameters(
                trialId: trialNumber,
                useRandomParameters: true,
                customPath: randomParametersPath,
                fallbackTextAsset: randomParametersFile);

            // If loading failed, generate random parameters
            if (data == null)
            {
                data = TrialDataService.GenerateRandomParameters(parameterRanges);
            }
        }
        else
        {
            data = TrialDataService.LoadTrialParameters(
                trialId: trialNumber,
                useRandomParameters: false,
                customPath: trialParametersPath,
                fallbackTextAsset: trialParametersFile);

            if (data == null)
            {
                Debug.LogError($"CRITICAL: Failed to load parameters for trial {trialNumber}!");
                data = TrialDataService.GenerateRandomParameters(parameterRanges);
            }
        }

        data.trialId = trialNumber;
        ApplyParametersToGame(data);
        return data;
    }

    public bool SaveTrialResultToCSV(TrialDataModels.TrialData trialData)
    {
        if (trialData == null)
        {
            Debug.LogError("TrialData is null - cannot save");
            return false;
        }

        // Note: Duplicate save prevention is handled by TrialDataService.UpdateOriginalCSV
        // - Failed attempts (0%) are overwritten in the same column
        // - Successful retries create a new column (o2_run2, o2_run3, etc.)

        // Save to CSV and update cache (cache handled inside SaveTrialResult)
        bool saved = TrialDataService.SaveTrialResult(trialData, currentModeIsRandom, trialParametersPath, randomParametersPath);

        if (saved)
        {
            Debug.Log($"[TrialParameterManager] Saved trial {trialData.trialId} - O2: {trialData.finalOxygenRemaining:F1}%");
        }
        else
        {
            Debug.LogWarning($"[TrialParameterManager] Failed to save trial {trialData.trialId}");
        }

        return saved;
    }

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
    }

#if UNITY_EDITOR
    [MenuItem("Tools/AquaGuardian/Open Results Folder")]
    private static void OpenResultsFolderFromMenu()
    {
        string path = Application.persistentDataPath;
        System.Diagnostics.Process.Start("explorer.exe", path);
    }

    [MenuItem("Tools/AquaGuardian/Open Trial Parameters Folder")]
    private static void OpenTrialParametersFolder()
    {
        string path = Path.Combine(Application.dataPath, "Data", "Trials");
        if (Directory.Exists(path))
        {
            System.Diagnostics.Process.Start("explorer.exe", path);
        }
        else
        {
            Debug.LogError($"Trial parameters folder not found: {path}");
        }
    }

    [MenuItem("Tools/AquaGuardian/Open Regression Results Folder")]
    private static void OpenRegressionResultsFolder()
    {
        string path = Path.Combine(Application.dataPath, "Data", "RegressionResults");
        if (Directory.Exists(path))
        {
            System.Diagnostics.Process.Start("explorer.exe", path);
        }
        else
        {
            Debug.LogError($"Regression results folder not found: {path}");
        }
    }
#endif

    private void ApplyParametersToGame(TrialDataModels.TrialData data)
    {
        Debug.Log($"[TrialParameters] Trial {data.trialId}: Speed={data.speed:F2} VerticalSpeed={data.verticalSpeed:F2} IdleUpwardSpeed={data.idleUpwardSpeed:F2} LifeTime={data.lifeTime:F2}s RemoveHealthPerLifeCycle={data.RemoveHealthEveryLifeTime:F2} RemoveHealthWithCollide={data.removeHealthWithCollide:F2} TimeBetweenCollides={data.timeBetweenCollides:F2}s HealHealthPoint={data.healHealthPoint:F2} FactorForce={data.factorForce:F2}");

        ApplyToPlayerMovement(data);
        ApplyToPlayerLife(data);
        ApplyToHealth(data);
        ApplyFactorForce(data);
    }

    private void ApplyToPlayerMovement(TrialDataModels.TrialData data)
    {
        if (playerMovement == null) return;

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

    private void ApplyToPlayerLife(TrialDataModels.TrialData data)
    {
        if (playerLife == null) return;

        if (playerLife.healHealthPoints_inputField != null)
            playerLife.healHealthPoints_inputField.text = data.healHealthPoint.ToString("F1");
        if (playerLife.timeBetweenCollides_inputField != null)
            playerLife.timeBetweenCollides_inputField.text = data.timeBetweenCollides.ToString("F1");
        if (playerLife.removeHealthWithCollide_inputField != null)
            playerLife.removeHealthWithCollide_inputField.text = data.removeHealthWithCollide.ToString("F1");

        playerLife.didntGetInputsYet = true;
        playerLife.ProcessUserInputs();
    }

    private void ApplyToHealth(TrialDataModels.TrialData data)
    {
        if (health == null) return;

        if (health.RemoveHealthEveryLifeTime_inputField != null)
            health.RemoveHealthEveryLifeTime_inputField.text = data.RemoveHealthEveryLifeTime.ToString("F2");
        if (health.lifeTime_inputField != null)
            health.lifeTime_inputField.text = data.lifeTime.ToString("F2");

        health.didntGetInputsYet = true;
        health.ProcessUserInputs();
        health.StopAllCoroutines();
    }

    private void ApplyFactorForce(TrialDataModels.TrialData data)
    {
        if (amadeoClientDiver != null && amadeoClientDiver.factor_force_inputField != null)
        {
            amadeoClientDiver.factor_force_inputField.text = data.factorForce.ToString("F2");
        }
    }
}
