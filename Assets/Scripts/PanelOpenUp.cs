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

public partial class PanelOpenUp : MonoBehaviour
{

    [Header("Amadeo Client and UI Components")]
    [SerializeField] private AmadeoClient _client;
    public GameObject Panel;

    [Header("Game Objects")]
    public GameObject caveObject = null;
    public GameObject oxygenObject = null;
    [SerializeField] private Transform oxygenSpawnRef;
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

    [Header("Configuration")]
    [SerializeField] private GameConfig gameConfig;


    [Header("Trial System Managers")]
    [SerializeField] private TrialSystemManager trialSystemManager;
    [SerializeField] private TrialParameterManager parameterManager;
    [SerializeField] private TrialFishSpawner fishSpawner;
    [SerializeField] private TrialUIController uiController;
    [SerializeField] private GameSystemResetter systemResetter;

    [Header("Cave Building")]
    [SerializeField] private CaveBuilder caveBuilder;

    // Cave info is now managed by CaveBuilder
    public List<TrialDataModels.CaveInfo> caveInfos => caveBuilder != null ? caveBuilder.CaveInfos : new List<TrialDataModels.CaveInfo>();


    [Header("Cave Files for Trials")]
    [SerializeField] private TextAsset[] caveFiles = new TextAsset[5];
    private TextAsset originalCaveFile;
    [SerializeField] private bool useCaveFilePathPattern = true;
    [SerializeField] private string caveFilePathPattern = "Data/Cave{n}.csv";



    void Start()
    {
        // Auto-wire managers if not assigned in Inspector
        AutoWireManagers();

        // Store original cave file
        originalCaveFile = csvFile;

        // Load CSV via CaveBuilder
        if (csvFile != null && caveBuilder != null)
        {
            caveBuilder.SetCSVFile(csvFile);
        }
        else if (csvFile == null)
        {
            Debug.LogError("No CSV file assigned!");
        }

        // Subscribe to events
        GameStateManager.OnGameEnded += OnGameEnded;
    }

    void Update()
    {
        // Keep cursor unlocked and visible
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void OnDestroy()
    {
        GameStateManager.OnGameEnded -= OnGameEnded;
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

        if (fishSpawner != null && chest != null)
        {
            fishSpawner.SetChestPrefab(chest);
        }

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
        bool isTrialMode = (trialSystemManager != null && trialSystemManager.TrialsMode);

        // Hide main panel
        if (Panel != null)
        {
            Panel.SetActive(false);
        }

        // Close any open regression panel (without opening trial panel)
        var regressionUI = FindObjectOfType<TrialRegressionUI>();
        if (regressionUI != null)
        {
            regressionUI.ForceCloseRegressionPanel();
        }

        // Build caves using CaveBuilder
        if (caveBuilder == null)
        {
            Debug.LogError("CaveBuilder not assigned - cannot build caves!");
            return;
        }

        // Set CSV file to CaveBuilder
        caveBuilder.SetCSVFile(csvFile);

        // Build all caves and get the last cave position
        Vector3 lastCavePosition = caveBuilder.BuildAllCaves(caveObject, oxygenObject, wall, arrows);

        // Track spawned objects from CaveBuilder
        foreach (var obj in caveBuilder.GetSpawnedObjects())
        {
            if (systemResetter != null)
            {
                systemResetter.TrackSpawned(obj);
            }
        }

        // Start Amadeo client
        if (_client != null)
        {
            _client.StartReceiveData();
        }

        // Get end position for chest/fish
        Vector3 endPosition = caveBuilder.GetEndObjectPosition(lastCavePosition);

        // Setup fish spawner
        if (fishSpawner != null && chest != null)
        {
            fishSpawner.SetChestPrefab(chest);
        }

        // Create end object (chest or trial fish)
        GameObject endObject = CreateEndObject(endPosition);

        // Set finish line in progress bar
        if (levelProgressUI != null && endObject != null)
        {
            levelProgressUI.SetFinishLine(endObject.transform);
        }

        // Initialize component flags
        playerLife.didntGetInputsYet = true;
        health.didntGetInputsYet = true;

        // Notify GameStateManager (only in normal mode, not trials)
        bool inTrialsMode = (trialSystemManager != null && trialSystemManager.TrialsMode);
        if (!inTrialsMode)
        {
            GameStateManager.Instance?.NotifyPanelClosed();
        }
    }

    private GameObject CreateEndObject(Vector3 position)
    {
        if (trialSystemManager != null)
        {
            // Debug.Log($"trialSystemManager.TrialsMode: {trialSystemManager.TrialsMode}");
        }

        bool inTrialsMode = (trialSystemManager != null && trialSystemManager.TrialsMode);
        Debug.Log($"Final inTrialsMode decision: {inTrialsMode}");

        if (inTrialsMode)
        {
            // Use TrialFishSpawner to create fish
            if (fishSpawner != null && trialSystemManager != null)
            {
                GameObject fish = fishSpawner.CreateTrialFish(position, trialSystemManager.CurrentTrialNumber);
                if (fish != null && systemResetter != null)
                {
                    systemResetter.TrackSpawned(fish);
                }
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
            if (systemResetter != null)
            {
                systemResetter.TrackSpawned(emergencyFish);
            }
            return emergencyFish;
        }
        else
        {
            // Normal mode - create chest
            GameObject chestObj = Instantiate(chest, position, Quaternion.identity);
            if (systemResetter != null)
            {
                systemResetter.TrackSpawned(chestObj);
            }
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

    private void OnGameEnded(float finalOxygen, bool completed)
    {

        if (trialSystemManager == null || !trialSystemManager.TrialsMode)
        {
            //Debug.Log($"Game ended (normal mode): oxygen={finalOxygen:F1}%, completed={completed}");
        }
    }

    public void LoadCaveFileForTrial(int trialNumber)
    {
        int caveIndex = trialNumber - 1;

        // Try TextAsset array first
        if (caveFiles != null && caveIndex >= 0 && caveIndex < caveFiles.Length && caveFiles[caveIndex] != null)
        {
            csvFile = caveFiles[caveIndex];
            if (caveBuilder != null)
            {
                caveBuilder.SetCSVFile(csvFile);
            }
            return;
        }

        Debug.LogWarning($"Cave file {caveIndex} not in array, trying path pattern...");

        // Try path pattern fallback
        if (useCaveFilePathPattern && !string.IsNullOrEmpty(caveFilePathPattern))
        {
            string relative = caveFilePathPattern.Replace("{n}", trialNumber.ToString());
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
        csvFile = originalCaveFile;
        if (caveBuilder != null)
        {
            caveBuilder.SetCSVFile(csvFile);
        }
    }


    public void RestoreOriginalCaveFile()
    {
        if (originalCaveFile != null)
        {
            csvFile = originalCaveFile;
            if (caveBuilder != null)
            {
                caveBuilder.SetCSVFile(csvFile);
            }
        }
    }
}