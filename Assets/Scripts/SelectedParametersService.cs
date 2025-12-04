using UnityEngine;
using System;
using System.IO;
using System.Globalization;

/**
 * SelectedParametersService - Manages saving and loading user-selected parameters
 * 
 * When the user clicks on a row in the Multi-Target table, the selected parameters
 * are saved to a JSON file. The main game (not trials) uses these parameters.
 * 
 * Usage:
 *   // Save selected parameters
 *   SelectedParametersService.SaveSelectedParameters(trialData, targetOxygen);
 *   
 *   // Check if parameters exist
 *   if (SelectedParametersService.HasSelectedParameters()) { ... }
 *   
 *   // Load parameters
 *   var parameters = SelectedParametersService.LoadSelectedParameters();
 */
public static class SelectedParametersService
{
    private static readonly string SELECTED_FOLDER = Path.Combine(Application.dataPath, "Data", "SelectedParameters");
    private static readonly string SELECTED_JSON_PATH = Path.Combine(SELECTED_FOLDER, "SelectedParameters.json");

    /// <summary>
    /// Data structure for storing selected parameters
    /// </summary>
    [Serializable]
    public class SelectedParameters
    {
        public float targetOxygen;
        public float predictedOxygen;
        public float speed;
        public float verticalSpeed;
        public float idleUpwardSpeed;
        public float lifeTime;
        public float RemoveHealthEveryLifeTime;
        public float removeHealthWithCollide;
        public float timeBetweenCollides;
        public float healHealthPoint;
        public float factorForce;
        public float IsAmadeoMode;
        public string savedAt;
    }

    /// <summary>
    /// Saves selected parameters to JSON file
    /// </summary>
    public static bool SaveSelectedParameters(TrialDataModels.TrialData parameters, float targetOxygen, float predictedOxygen = 0f)
    {
        if (parameters == null)
        {
            Debug.LogError("[SelectedParametersService] Cannot save null parameters");
            return false;
        }

        try
        {
            if (!Directory.Exists(SELECTED_FOLDER))
            {
                Directory.CreateDirectory(SELECTED_FOLDER);
            }

            var selected = new SelectedParameters
            {
                targetOxygen = targetOxygen,
                predictedOxygen = predictedOxygen,
                speed = parameters.speed,
                verticalSpeed = parameters.verticalSpeed,
                idleUpwardSpeed = parameters.idleUpwardSpeed,
                lifeTime = parameters.lifeTime,
                RemoveHealthEveryLifeTime = parameters.RemoveHealthEveryLifeTime,
                removeHealthWithCollide = parameters.removeHealthWithCollide,
                timeBetweenCollides = parameters.timeBetweenCollides,
                healHealthPoint = parameters.healHealthPoint,
                factorForce = parameters.factorForce,
                IsAmadeoMode = parameters.IsAmadeoMode,
                savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            string json = JsonUtility.ToJson(selected, true);
            File.WriteAllText(SELECTED_JSON_PATH, json);

            Debug.Log($"[SelectedParametersService] Saved parameters for target {targetOxygen}% to: {SELECTED_JSON_PATH}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SelectedParametersService] Failed to save: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Loads selected parameters from JSON file
    /// </summary>
    public static SelectedParameters LoadSelectedParameters()
    {
        if (!File.Exists(SELECTED_JSON_PATH))
        {
            Debug.LogWarning($"[SelectedParametersService] No selected parameters file found");
            return null;
        }

        try
        {
            string json = File.ReadAllText(SELECTED_JSON_PATH);
            var selected = JsonUtility.FromJson<SelectedParameters>(json);
            Debug.Log($"[SelectedParametersService] Loaded parameters for target {selected.targetOxygen}%");
            return selected;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SelectedParametersService] Failed to load: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Checks if selected parameters file exists
    /// </summary>
    public static bool HasSelectedParameters()
    {
        return File.Exists(SELECTED_JSON_PATH);
    }

    /// <summary>
    /// Gets the target oxygen value of the selected parameters
    /// </summary>
    public static float GetSelectedTargetOxygen()
    {
        var selected = LoadSelectedParameters();
        return selected?.targetOxygen ?? 0f;
    }

    /// <summary>
    /// Converts selected parameters to TrialData format
    /// </summary>
    public static TrialDataModels.TrialData ToTrialData(SelectedParameters selected)
    {
        if (selected == null) return null;

        return new TrialDataModels.TrialData
        {
            speed = selected.speed,
            verticalSpeed = selected.verticalSpeed,
            idleUpwardSpeed = selected.idleUpwardSpeed,
            lifeTime = selected.lifeTime,
            RemoveHealthEveryLifeTime = selected.RemoveHealthEveryLifeTime,
            removeHealthWithCollide = selected.removeHealthWithCollide,
            timeBetweenCollides = selected.timeBetweenCollides,
            healHealthPoint = selected.healHealthPoint,
            factorForce = selected.factorForce,
            IsAmadeoMode = selected.IsAmadeoMode
        };
    }

    /// <summary>
    /// Clears selected parameters
    /// </summary>
    public static bool ClearSelectedParameters()
    {
        if (!File.Exists(SELECTED_JSON_PATH))
            return true;

        try
        {
            File.Delete(SELECTED_JSON_PATH);
            Debug.Log("[SelectedParametersService] Cleared selected parameters");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SelectedParametersService] Failed to clear: {e.Message}");
            return false;
        }
    }
}

