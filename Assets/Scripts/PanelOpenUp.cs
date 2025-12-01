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

        if (!IsInTrialsMode && GameStateManager.Instance != null)
        {
            GameStateManager.Instance.NotifyPanelClosed();
        }
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
}