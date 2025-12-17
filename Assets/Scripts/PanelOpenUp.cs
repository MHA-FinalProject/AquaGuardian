using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections.Generic;
using System.Linq;

/**
   * PanelOpenUp
   * This class manages the opening and closing of the main panel,
   * cave building, trial management, and interaction with other
   * game systems such as AmadeoClient and TrialSystemManager.
   */

public class PanelOpenUp : MonoBehaviour
{

    [Header("Amadeo Client and UI Components")]
    [SerializeField] private AmadeoClient _client;
    public GameObject Panel;

    [Header("Game Objects")]
    public GameObject caveObject = null;
    public GameObject oxygenObject = null;
    public GameObject wall = null;
    public GameObject arrows = null;
    public GameObject chest = null;

    [Header("Game Settings")]
    [SerializeField] private TextAsset csvFile;

    [Header("Component References")]
    [SerializeField] private LevelProgressUI levelProgressUI;
    [SerializeField] private PlayerLife playerLife;
    [SerializeField] private Health health;
    [SerializeField] private PlayerMovement playerMovement;

    [Header("Trial System Managers")]
    [SerializeField] private TrialSystemManager trialSystemManager;
    [SerializeField] private TrialParameterManager parameterManager;
    [SerializeField] private TrialFishSpawner fishSpawner;
    [SerializeField] private TrialUIController uiController;
    [SerializeField] private GameSystemResetter systemResetter;

    [Header("Cave Building")]
    [SerializeField] private CaveBuilder caveBuilder;

    [Header("Game Data (Optional - leave empty to use singleton)")]
    [SerializeField] private GameDataSO gameDataOverride;

    // Cave info is now managed by CaveBuilder
    // Access via: caveBuilder.CaveInfos

    // Public property to access cave infos
    public List<TrialDataModels.CaveInfo> caveInfos
    {
        get { return caveBuilder != null ? caveBuilder.CaveInfos : null; }
    }

    // Method to clear cave infos (delegates to CaveBuilder)
    public void ClearCaveInfos()
    {
        if (caveBuilder != null)
        {
            caveBuilder.CaveInfos.Clear();
        }
    }

    private TextAsset originalCaveFile;

    // Helper properties and methods
    private bool IsInTrialsMode => trialSystemManager != null && trialSystemManager.TrialsMode;

    private void SetCaveFile(TextAsset file)
    {
        csvFile = file;
        if (caveBuilder != null)
        {
            caveBuilder.SetCSVFile(file);
        }
    }

    private void SetupChestPrefab()
    {
        if (fishSpawner != null && chest != null)
        {
            fishSpawner.SetChestPrefab(chest);
        }
    }

    private void TrackSpawnedObject(GameObject obj)
    {
        if (systemResetter != null && obj != null)
        {
            systemResetter.TrackSpawned(obj);
        }
    }

    void Start()
    {
        // Auto-wire managers if not assigned in Inspector
        AutoWireManagers();

        // Store original cave file
        originalCaveFile = csvFile;

        // Load CSV via CaveBuilder
        if (csvFile != null)
        {
            SetCaveFile(csvFile);
        }
        else
        {
            Debug.LogError("No CSV file assigned!");
        }

        // Load selected parameters to input fields (if file exists)
        // This allows the user to see and modify the values before starting
        LoadSelectedParametersToInputFields();

        // Check if panel is already closed (e.g., after scene restart)
        // If panel is closed, notify GameStateManager immediately
        if (Panel != null && !Panel.activeSelf)
        {
            // Panel already closed at Start
            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.NotifyPanelClosed();
            }
        }
    }

    void Update()
    {
        // Keep cursor unlocked and visible
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }


    private void AutoWireManagers()
    {
        // Auto-find or create managers if not assigned in Inspector
        if (trialSystemManager == null)
        {
            trialSystemManager = GetComponent<TrialSystemManager>();
            if (trialSystemManager == null)
            {
                Debug.LogWarning("TrialSystemManager not found - add it manually to this GameObject");
            }
        }

        if (parameterManager == null)
        {
            parameterManager = GetComponent<TrialParameterManager>();
            if (parameterManager == null)
            {
                Debug.LogWarning("TrialParameterManager not found - add it manually to this GameObject");
            }
        }

        if (fishSpawner == null)
        {
            fishSpawner = GetComponent<TrialFishSpawner>();
            if (fishSpawner == null)
            {
                Debug.LogWarning("TrialFishSpawner not found - add it manually to this GameObject");
            }
        }

        SetupChestPrefab();

        if (uiController == null)
        {
            uiController = GetComponent<TrialUIController>();
            if (uiController == null)
            {
                Debug.LogWarning("TrialUIController not found - add it manually to this GameObject");
            }
        }

        if (systemResetter == null)
        {
            systemResetter = GetComponent<GameSystemResetter>();
            if (systemResetter == null)
            {
                Debug.LogWarning("GameSystemResetter not found - add it manually to this GameObject");
            }
        }

        if (caveBuilder == null)
        {
            caveBuilder = GetComponent<CaveBuilder>();
            if (caveBuilder == null)
            {
                Debug.LogWarning("CaveBuilder not found - add it manually to this GameObject");
            }
        }
    }



    public void ClosePanel()
    {
        if (Panel != null)
        {
            Panel.SetActive(false);
        }

        var regressionUI = FindObjectOfType<TrialRegressionUI>();
        if (regressionUI != null)
        {
            regressionUI.ForceCloseRegressionPanel();
        }

        if (caveBuilder == null)
        {
            Debug.LogError("CaveBuilder not assigned - cannot build caves!");
            return;
        }

        SetCaveFile(csvFile);

        Vector3 lastCavePosition = caveBuilder.BuildAllCaves(caveObject, oxygenObject, wall, arrows);

        foreach (var obj in caveBuilder.GetSpawnedObjects())
        {
            TrackSpawnedObject(obj);
        }

        if (_client != null)
        {
            _client.StartReceiveData();
        }

        Vector3 endPosition = caveBuilder.GetEndObjectPosition(lastCavePosition);
        SetupChestPrefab();

        GameObject endObject = CreateEndObject(endPosition);

        if (levelProgressUI != null && endObject != null)
        {
            levelProgressUI.SetFinishLine(endObject.transform);
        }

        playerLife.didntGetInputsYet = true;
        health.didntGetInputsYet = true;

        // If not in trials mode and we have selected parameters, apply them
        if (!IsInTrialsMode)
        {
            ApplySelectedParametersToGame();
            
            if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.NotifyPanelClosed();
        }
        }
    }

    /// <summary>
    /// Applies selected parameters from the multi-target analysis to the main game.
    /// Called when starting the main game (not trials).
    /// Sets the input field values which are then read by the game components.
    /// </summary>
    private void ApplySelectedParametersToGame()
    {
        if (!SelectedParametersService.HasSelectedParameters())
        {
            Debug.Log("[PanelOpenUp] No selected parameters found - using defaults from UI");
            return;
        }

        var selected = SelectedParametersService.LoadSelectedParameters();
        if (selected == null)
        {
            Debug.LogWarning("[PanelOpenUp] Failed to load selected parameters");
            return;
        }

        Debug.Log($"[PanelOpenUp] Applying selected parameters for target {selected.targetOxygen}%");

        // Apply to PlayerMovement - has public fields AND input fields
        if (playerMovement != null)
        {
            // Set the public fields directly
            playerMovement.speed = selected.speed;
            playerMovement.verticalSpeed = selected.verticalSpeed;
            playerMovement.idleUpwardSpeed = selected.idleUpwardSpeed;
            
            // Also update the input fields so the UI reflects the values
            if (playerMovement.speed_inputField != null)
                playerMovement.speed_inputField.text = selected.speed.ToString("F2");
            if (playerMovement.vertical_speed_inputField != null)
                playerMovement.vertical_speed_inputField.text = selected.verticalSpeed.ToString("F2");
            if (playerMovement.idle_upward_speed_inputField != null)
                playerMovement.idle_upward_speed_inputField.text = selected.idleUpwardSpeed.ToString("F2");
                
            Debug.Log($"  PlayerMovement: speed={selected.speed}, vSpeed={selected.verticalSpeed}, idle={selected.idleUpwardSpeed}");
        }

        // Apply to Health - uses input fields (lifeTime, RemoveHealthEveryLifeTime)
        if (health != null)
        {
            if (health.lifeTime_inputField != null)
                health.lifeTime_inputField.text = selected.lifeTime.ToString("F2");
            if (health.RemoveHealthEveryLifeTime_inputField != null)
                health.RemoveHealthEveryLifeTime_inputField.text = selected.RemoveHealthEveryLifeTime.ToString("F2");
                
            Debug.Log($"  Health: lifeTime={selected.lifeTime}, drain={selected.RemoveHealthEveryLifeTime}");
        }

        // Apply to PlayerLife - uses input fields (removeHealthWithCollide, timeBetweenCollides, healHealthPoint)
        if (playerLife != null)
        {
            if (playerLife.removeHealthWithCollide_inputField != null)
                playerLife.removeHealthWithCollide_inputField.text = selected.removeHealthWithCollide.ToString("F2");
            if (playerLife.timeBetweenCollides_inputField != null)
                playerLife.timeBetweenCollides_inputField.text = selected.timeBetweenCollides.ToString("F2");
            if (playerLife.healHealthPoints_inputField != null)
                playerLife.healHealthPoints_inputField.text = selected.healHealthPoint.ToString("F2");
                
            Debug.Log($"  PlayerLife: collide={selected.removeHealthWithCollide}, timeBetween={selected.timeBetweenCollides}, heal={selected.healHealthPoint}");
        }

        // Apply to Amadeo component if in Amadeo mode
        if (selected.IsAmadeoMode > 0.5f)
        {
            // Find getEventFromAmadeoClientDiver component (on Player object)
            var amadeoHandler = FindObjectOfType<getEventFromAmadeoClientDiver>();
            if (amadeoHandler != null && amadeoHandler.factor_force_inputField != null)
            {
                amadeoHandler.factor_force_inputField.text = selected.factorForce.ToString("F2");
                Debug.Log($"  AmadeoHandler: factorForce={selected.factorForce}");
            }
        }

        Debug.Log($"[PanelOpenUp] Successfully applied parameters for target {selected.targetOxygen}%");
    }

    private GameObject CreateEndObject(Vector3 position)
    {
        if (IsInTrialsMode)
        {
            // Use TrialFishSpawner to create fish
            if (fishSpawner != null && trialSystemManager != null)
            {
                GameObject fish = fishSpawner.CreateTrialFish(position, trialSystemManager.CurrentTrialNumber);
                TrackSpawnedObject(fish);
                return fish;
            }

            // Emergency fallback
            Debug.LogError("FishSpawner not available - creating emergency fish");
            GameObject emergencyFish = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            emergencyFish.transform.position = position;
            emergencyFish.transform.localScale = new Vector3(3f, 2f, 4f);
            emergencyFish.name = "EmergencyFish";
            emergencyFish.tag = "TrialFish";
            emergencyFish.GetComponent<Collider>().isTrigger = true;
            emergencyFish.GetComponent<MeshRenderer>().material.color = Color.red;
            TrackSpawnedObject(emergencyFish);
            return emergencyFish;
        }
        else
        {
            // Normal mode - create chest
            GameObject chestObj = Instantiate(chest, position, Quaternion.identity);
            TrackSpawnedObject(chestObj);
            return chestObj;
        }
    }

    public void OnTrialFishReached(float finalOxygen, bool completed)
    {
        if (trialSystemManager != null)
        {
            trialSystemManager.OnTrialFishReached(finalOxygen, completed);
        }
        else
        {
            Debug.LogError("TrialSystemManager not found - cannot handle fish reach event");
        }
    }


    public void LoadCaveFileForTrial(int trialNumber)
    {
        int caveIndex = trialNumber - 1;

        // Get config from GameDataSO (override or singleton)
        GameDataSO config = gameDataOverride != null ? gameDataOverride : GameDataSO.Instance;
        if (config == null)
        {
            Debug.LogError("GameDataSO is null! Cannot load cave file for trial.");
            return;
        }

        // Try TextAsset array first
        if (config.caveFiles != null && caveIndex >= 0 && caveIndex < config.caveFiles.Length && config.caveFiles[caveIndex] != null)
        {
            SetCaveFile(config.caveFiles[caveIndex]);
            return;
        }

        Debug.LogWarning($"Cave file {caveIndex} not in array, trying path pattern...");

        // Try path pattern fallback
        if (config.useCaveFilePathPattern && !string.IsNullOrEmpty(config.caveFilePathPattern))
        {
            string relative = config.caveFilePathPattern.Replace("{n}", trialNumber.ToString());
            string absolute = System.IO.Path.Combine(Application.dataPath, relative.Replace("\\", "/"));

            if (System.IO.File.Exists(absolute))
            {
                if (caveBuilder != null)
                {
                    caveBuilder.LoadCSVFromPath(absolute);
                }
                return;
            }
        }

        // Final fallback to original
        SetCaveFile(originalCaveFile);
    }


    public void RestoreOriginalCaveFile()
    {
        if (originalCaveFile != null)
        {
            SetCaveFile(originalCaveFile);
        }
    }

    /// <summary>
    /// Loads selected parameters from JSON file and populates input fields.
    /// Called at Start() so user can see and modify the values before starting game.
    /// </summary>
    public void LoadSelectedParametersToInputFields()
    {
        if (!SelectedParametersService.HasSelectedParameters())
        {
            Debug.Log("[PanelOpenUp] No selected parameters file found - using current UI values");
            return;
        }

        var selected = SelectedParametersService.LoadSelectedParameters();
        if (selected == null)
        {
            Debug.LogWarning("[PanelOpenUp] Failed to load selected parameters from file");
            return;
        }

        Debug.Log($"[PanelOpenUp] Loading parameters for target {selected.targetOxygen}% to input fields");

        // Populate PlayerMovement input fields
        if (playerMovement != null)
        {
            if (playerMovement.speed_inputField != null)
                playerMovement.speed_inputField.text = selected.speed.ToString("F2");
            if (playerMovement.vertical_speed_inputField != null)
                playerMovement.vertical_speed_inputField.text = selected.verticalSpeed.ToString("F2");
            if (playerMovement.idle_upward_speed_inputField != null)
                playerMovement.idle_upward_speed_inputField.text = selected.idleUpwardSpeed.ToString("F2");
        }

        // Populate Health input fields
        if (health != null)
        {
            if (health.lifeTime_inputField != null)
                health.lifeTime_inputField.text = selected.lifeTime.ToString("F2");
            if (health.RemoveHealthEveryLifeTime_inputField != null)
                health.RemoveHealthEveryLifeTime_inputField.text = selected.RemoveHealthEveryLifeTime.ToString("F2");
        }

        // Populate PlayerLife input fields
        if (playerLife != null)
        {
            if (playerLife.removeHealthWithCollide_inputField != null)
                playerLife.removeHealthWithCollide_inputField.text = selected.removeHealthWithCollide.ToString("F2");
            if (playerLife.timeBetweenCollides_inputField != null)
                playerLife.timeBetweenCollides_inputField.text = selected.timeBetweenCollides.ToString("F2");
            if (playerLife.healHealthPoints_inputField != null)
                playerLife.healHealthPoints_inputField.text = selected.healHealthPoint.ToString("F2");
        }

        // Populate Amadeo input field if in Amadeo mode
        if (selected.IsAmadeoMode > 0.5f)
        {
            var amadeoHandler = FindObjectOfType<getEventFromAmadeoClientDiver>();
            if (amadeoHandler != null && amadeoHandler.factor_force_inputField != null)
            {
                amadeoHandler.factor_force_inputField.text = selected.factorForce.ToString("F2");
            }
        }

        Debug.Log($"[PanelOpenUp] Loaded parameters: speed={selected.speed}, vSpeed={selected.verticalSpeed}, idle={selected.idleUpwardSpeed}, lifeTime={selected.lifeTime}, drain={selected.RemoveHealthEveryLifeTime}, collide={selected.removeHealthWithCollide}, heal={selected.healHealthPoint}");
    }

    /// <summary>
    /// Clears the selected parameters file and resets to default values.
    /// </summary>
    public void ClearSelectedParameters()
    {
        SelectedParametersService.ClearSelectedParameters();
        Debug.Log("[PanelOpenUp] Cleared selected parameters");
    }

    /// <summary>
    /// Returns the current target oxygen value if selected parameters exist.
    /// </summary>
    public float GetSelectedTargetOxygen()
    {
        return SelectedParametersService.GetSelectedTargetOxygen();
    }

    /// <summary>
    /// Checks if selected parameters file exists.
    /// </summary>
    public bool HasSelectedParameters()
    {
        return SelectedParametersService.HasSelectedParameters();
    }
}