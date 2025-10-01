using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections.Generic;
using System.Linq;


public partial class PanelOpenUp : MonoBehaviour
{
    // ========== ORIGINAL FIELDS (kept for backwards compatibility) ==========

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
    private int numOfLines = 0;
    [SerializeField] private TextAsset csvFile;
    private string[] lines = null;

    [Header("Component References")]
    [SerializeField] private LevelProgressUI levelProgressUI;
    [SerializeField] private PlayerLife playerLife;
    [SerializeField] private Health health;
    [SerializeField] private PlayerMovement playerMovement;

    [Header("Configuration")]
    [SerializeField] private GameConfig gameConfig;

    // ========== TRIAL SYSTEM MANAGERS (NEW!) ==========

    [Header("Trial System Managers")]
    [SerializeField] private TrialSystemManager trialSystemManager;
    [SerializeField] private TrialParameterManager parameterManager;
    [SerializeField] private TrialFishSpawner fishSpawner;
    [SerializeField] private TrialUIController uiController;
    [SerializeField] private GameSystemResetter systemResetter;

    // ========== CAVE DATA STRUCTURES ==========

    [System.Serializable]
    public class CaveInfo
    {
        public int index;
        public float minZ;
        public float maxZ;
        public float diameter;
        public float height;
        public float length;
        public float distanceFromPrevious;
    }

    [System.NonSerialized]
    public List<CaveInfo> caveInfos = new List<CaveInfo>();

    // ========== TRIAL DATA CLASSES (public for other scripts) ==========

    [System.Serializable]
    public class TrialData
    {
        public int trialId;
        public float speed;
        public float verticalSpeed;
        public float idleUpwardSpeed;
        public float lifeTime;
        public float downHealthPairSec;
        public float removeHealthWithCollide;
        public float timeBetweenCollides;
        public float healHealthPoint;
        public float factorForce;
        public float finalOxygenRemaining;
        public bool completed;
    }

    [System.Serializable]
    public class ParameterRanges
    {
        [Header("Movement Parameters")]
        public Vector2 speedRange = new Vector2(10f, 25f);
        public Vector2 verticalSpeedRange = new Vector2(15f, 40f);
        public Vector2 idleUpwardSpeedRange = new Vector2(0.5f, 2f);

        [Header("Health Parameters")]
        public Vector2 oxygenHealRange = new Vector2(3f, 15f);
        public Vector2 timeBetweenCollidesRange = new Vector2(1f, 5f);
        public Vector2 collisionDamageRange = new Vector2(5f, 15f);
        public Vector2 oxygenDropPerSecRange = new Vector2(0.5f, 2f);
        public Vector2 lifeTimeRange = new Vector2(0.8f, 3f);
    }

    // ========== CAVE FILES FOR TRIALS ==========

    [Header("Cave Files for Trials")]
    [SerializeField] private TextAsset[] caveFiles = new TextAsset[5];
    private TextAsset originalCaveFile;
    [SerializeField] private bool useCaveFilePathPattern = true;
    [SerializeField] private string caveFilePathPattern = "Data/Cave{n}.csv";

    // ========== LIFECYCLE METHODS ==========

    void Start()
    {
        Debug.Log("=== PANELOPENUP START() (REFACTORED VERSION) ===");

        // Auto-wire managers if not assigned in Inspector
        AutoWireManagers();

        // Store original cave file
        originalCaveFile = csvFile;

        // Load CSV
        if (csvFile != null)
        {
            ReadCSVFromTextAsset();
            Debug.Log($"CSV file loaded: {csvFile.name} ({numOfLines} caves)");
        }
        else
        {
            Debug.LogError("No CSV file assigned!");
        }

        // Subscribe to events
        GameStateManager.OnGameEnded += OnGameEnded;

        Debug.Log("=== PanelOpenUp initialized successfully ===");
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

    // ========== AUTO-WIRE MANAGERS ==========

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

        // ✅ Auto-assign chest prefab to fishSpawner
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

        Debug.Log("Manager components verified");
    }

    // ========== CSV READING ==========

    void ReadCSVFromTextAsset()
    {
        try
        {
            string csvText = csvFile.text;
            lines = csvText.Split(new[] { "\r\n", "\r", "\n" }, System.StringSplitOptions.RemoveEmptyEntries);
            numOfLines = lines.Length;
            Debug.Log($"CSV loaded: {numOfLines} cave definitions");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error reading CSV: {e.Message}");
        }
    }

    void ReadCSVFromAbsolutePath(string absolutePath)
    {
        try
        {
            if (!System.IO.File.Exists(absolutePath))
            {
                Debug.LogError($"Cave CSV not found at: {absolutePath}");
                return;
            }

            string[] fileLines = System.IO.File.ReadAllLines(absolutePath);
            lines = fileLines.Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
            numOfLines = lines.Length;
            Debug.Log($"Loaded {numOfLines} caves from path: {absolutePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error reading CSV from path: {e.Message}");
        }
    }

    // ========== CLOSE PANEL - CAVE BUILDING (CORE FUNCTIONALITY) ==========

    public void ClosePanel()
    {
        Debug.Log("=== CLOSE PANEL - BUILDING CAVES ===");

        // ✅ DEBUG: Check trial system state
        bool isTrialMode = (trialSystemManager != null && trialSystemManager.TrialsMode);
        Debug.Log($"ClosePanel START: trialSystemManager={trialSystemManager != null}, TrialsMode={trialSystemManager?.TrialsMode ?? false}, Decision={isTrialMode}");

        // Hide main panel
        if (Panel != null)
        {
            Panel.SetActive(false);
        }

        // Reset cave tracking
        caveInfos.Clear();

        // Get initial positions
        Vector3 currentPositionCave = caveObject.transform.position;
        Vector3 currentPositionOxygen = oxygenObject.transform.position;
        Vector3 currentPositionWall = wall.transform.position;
        Vector3 currentPositionArrows = arrows.transform.position;

        Vector3 newCavePosition = currentPositionCave;
        Vector3 newOxygenPosition;
        Vector3 newWallPosition;
        Vector3 newArrowsPosition;

        Vector3 currentCaveScale = caveObject.transform.localScale;
        Vector3 newCaveScale = currentCaveScale;

        // Add first cave (already in scene)
        if (numOfLines > 0)
        {
            AddFirstCaveBounds(currentPositionCave, currentCaveScale);
        }

        // Build remaining caves from CSV
        for (int i = 1; i < numOfLines; i++)
        {
            string[] fields = lines[i].Split(',');

            float diameter = float.Parse(fields[1]);
            float heightOffset = float.Parse(fields[2]);
            float length = float.Parse(fields[3]);

            newCaveScale = new Vector3(newCaveScale.x, diameter, length);
            newCavePosition = new Vector3(currentPositionCave.x, currentPositionCave.y + heightOffset,
                currentPositionWall.z - gameConfig.pivotCavePlace);
            currentPositionCave = new Vector3(currentPositionCave.x, currentPositionCave.y, newCavePosition.z);

            // Instantiate cave
            GameObject newCaveObject = Instantiate(caveObject, newCavePosition, Quaternion.identity);
            TrackSpawned(newCaveObject);
            newCaveObject.transform.localScale = newCaveScale;

            // Add cave bounds
            AddCaveBounds(newCaveObject, i + 1, newCavePosition, diameter, heightOffset, length);

            // Calculate object positions
            newOxygenPosition = new Vector3(currentPositionOxygen.x, currentPositionOxygen.y,
                currentPositionCave.z - gameConfig.generalPivot);
            newWallPosition = new Vector3(currentPositionWall.x, currentPositionWall.y,
                currentPositionCave.z - gameConfig.generalPivot);
            currentPositionWall = new Vector3(currentPositionWall.x, currentPositionWall.y, newWallPosition.z);
            newArrowsPosition = new Vector3(currentPositionArrows.x, currentPositionWall.y + gameConfig.pivotArrowsToWall,
                currentPositionCave.z - gameConfig.generalPivot);

            // Instantiate oxygen, wall, arrows (except for last cave)
            if (i != numOfLines - 1)
            {
                if (oxygenObject != null)
                {
                    // ⚠️ CRITICAL: Check prefab state BEFORE instantiation
                    if (!oxygenObject.activeSelf)
                    {
                        Debug.LogWarning($"⚠️ WARNING: oxygenObject prefab is DISABLED! This will cause all tanks to spawn disabled!");
                        Debug.LogWarning($"Fix: Select 'tank' prefab in Project window and enable it in Inspector");
                    }

                    var oxy = Instantiate(oxygenObject, newOxygenPosition, Quaternion.identity);
                    oxy.name = $"tank_{i + 1}"; // Name: tank_2, tank_3, etc.

                    //  CRITICAL: Force tank to be active immediately after instantiation
                    oxy.SetActive(true);
                    Debug.Log($"✓ Created tank_{i + 1}: prefab.activeSelf={oxygenObject.activeSelf}, instance.activeSelf={oxy.activeSelf}");

                    // Ensure tag is correct (should already be OxygenObject from prefab)
                    if (oxy.tag != "OxygenObject")
                    {
                        oxy.tag = "OxygenObject";
                    }
                    TrackSpawned(oxy);
                    Debug.Log($" Created oxygen tank_{i + 1} at {newOxygenPosition} with tag: {oxy.tag}, active: {oxy.activeSelf}");
                }
                else
                {
                    Debug.LogError($" oxygenObject prefab is NULL! Cannot create tank_{i + 1}");
                }
                var wallObj = Instantiate(wall, newWallPosition, Quaternion.identity);
                TrackSpawned(wallObj);
            }

            var arrowsObj = Instantiate(arrows, newArrowsPosition, Quaternion.identity);
            TrackSpawned(arrowsObj);

            // Start Amadeo client
            if (_client != null)
            {
                _client.StartReceiveData();
            }
        }

        // Create chest or fish at end - CALCULATE FROM LAST CAVE IN caveInfos
        Vector3 endPosition;

        if (caveInfos.Count > 0)
        {
            // ✅ Use the LAST cave from caveInfos (works for 1 or more caves)
            var lastCave = caveInfos[caveInfos.Count - 1];
            float lastCaveEndZ = lastCave.maxZ; // End of last cave (maxZ)

            // Position chest/fish AFTER the last cave
            endPosition = new Vector3(gameConfig.chestX, currentPositionCave.y,
                lastCaveEndZ - gameConfig.pivotChest);

            Debug.Log($"=== END OBJECT POSITION CALCULATION (FROM caveInfos) ===");
            Debug.Log($"Number of caves: {caveInfos.Count}");
            Debug.Log($"Last cave #{lastCave.index}: maxZ={lastCave.maxZ:F2}");
            Debug.Log($"Chest/Fish Z position: {endPosition.z:F2}");
            Debug.Log($"Distance from last cave end: {lastCave.maxZ - endPosition.z:F2} (should be gameConfig.pivotChest={gameConfig.pivotChest})");
        }
        else
        {
            // Fallback if no caves (shouldn't happen)
            Debug.LogError("No caves in caveInfos - using fallback position");
            endPosition = new Vector3(gameConfig.chestX, currentPositionCave.y,
                newCavePosition.z - gameConfig.pivotChest);
        }


        // (Removed AddFirstCaveOxygen call as per user request)

        // ✅ CRITICAL: Re-assign chest prefab before creating end object
        if (fishSpawner != null && chest != null)
        {
            fishSpawner.SetChestPrefab(chest);
            Debug.Log($"Re-assigned chest prefab to fishSpawner: {chest.name}");
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

    // ========== CAVE BUILDING HELPERS ==========

    private void AddFirstCaveBounds(Vector3 position, Vector3 scale)
    {
        float minZ = position.z;
        float maxZ = position.z;

        var existingRend = caveObject.GetComponentInChildren<Renderer>();
        if (existingRend != null)
        {
            minZ = existingRend.bounds.min.z;
            maxZ = existingRend.bounds.max.z;
        }
        else
        {
            var existingCol = caveObject.GetComponentInChildren<Collider>();
            if (existingCol != null)
            {
                minZ = existingCol.bounds.min.z;
                maxZ = existingCol.bounds.max.z;
            }
            else
            {
                float half = scale.z * 0.5f;
                minZ = position.z - half;
                maxZ = position.z + half;
            }
        }

        caveInfos.Add(new CaveInfo
        {
            index = 1,
            minZ = minZ,
            maxZ = maxZ,
            diameter = scale.x,
            height = scale.y,
            length = scale.z,
            distanceFromPrevious = 0f
        });

        Debug.Log($"Cave 1 (existing): Z[{minZ:F1},{maxZ:F1}], d={scale.x:F2}, h={scale.y:F2}, l={scale.z:F2}");
    }

    private void AddCaveBounds(GameObject cave, int index, Vector3 position, float diameter, float height, float length)
    {
        float minZ = position.z;
        float maxZ = position.z;

        var rend = cave.GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            minZ = rend.bounds.min.z;
            maxZ = rend.bounds.max.z;
        }
        else
        {
            var col = cave.GetComponentInChildren<Collider>();
            if (col != null)
            {
                minZ = col.bounds.min.z;
                maxZ = col.bounds.max.z;
            }
            else
            {
                float half = length * 0.5f;
                minZ = position.z - half;
                maxZ = position.z + half;
            }
        }

        // Calculate distance from previous cave
        float distanceFromPrev = 0f;
        if (caveInfos.Count >= 1)
        {
            var prev = caveInfos[caveInfos.Count - 1];
            distanceFromPrev = Mathf.Abs(minZ - prev.maxZ);
        }

        caveInfos.Add(new CaveInfo
        {
            index = index,
            minZ = minZ,
            maxZ = maxZ,
            diameter = diameter,
            height = height,
            length = length,
            distanceFromPrevious = distanceFromPrev
        });

        Debug.Log($"Cave {index}: Z[{minZ:F1},{maxZ:F1}], dist={distanceFromPrev:F1}");
    }




    private GameObject CreateEndObject(Vector3 position)
    {
        Debug.Log($"=== CREATE END OBJECT DEBUG ===");
        Debug.Log($"trialSystemManager exists: {trialSystemManager != null}");
        if (trialSystemManager != null)
        {
            Debug.Log($"trialSystemManager.TrialsMode: {trialSystemManager.TrialsMode}");
        }

        bool inTrialsMode = (trialSystemManager != null && trialSystemManager.TrialsMode);
        Debug.Log($"Final inTrialsMode decision: {inTrialsMode}");

        if (inTrialsMode)
        {
            Debug.Log("=== CREATING TRIAL FISH ===");
            return CreateTrialFish(position);
        }
        else
        {
            Debug.Log("=== CREATING CHEST (not in trials mode) ===");
            GameObject chestObj = Instantiate(chest, position, Quaternion.identity);
            TrackSpawned(chestObj);
            return chestObj;
        }
    }

    private GameObject CreateTrialFish(Vector3 position)
    {
        if (fishSpawner != null && trialSystemManager != null)
        {
            GameObject fish = fishSpawner.CreateTrialFish(position, trialSystemManager.CurrentTrialNumber);
            if (fish != null)
            {
                TrackSpawned(fish);
                Debug.Log($"Trial fish created: {fish.name}");
                return fish;
            }
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
        TrackSpawned(emergencyFish);
        return emergencyFish;
    }

    // ========== OBJECT TRACKING ==========

    private void TrackSpawned(GameObject go)
    {
        if (go != null && systemResetter != null)
        {
            systemResetter.TrackSpawned(go);
        }
    }

    // ========== TRIAL SYSTEM PUBLIC API (delegates to managers) ==========

    /// <summary>
    /// Called by PlayerMovement when trial fish is reached
    /// PUBLIC API: Must remain for backwards compatibility
    /// </summary>
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
        // Trial system manager handles this via its own subscription
        // This is just for logging in normal mode
        if (trialSystemManager == null || !trialSystemManager.TrialsMode)
        {
            Debug.Log($"Game ended (normal mode): oxygen={finalOxygen:F1}%, completed={completed}");
        }
    }

    // ========== CAVE FILE MANAGEMENT FOR TRIALS ==========


    /// Load cave file for specific trial
    /// PUBLIC API: Called by TrialSystemManager

    public void LoadCaveFileForTrial(int trialNumber)
    {
        Debug.Log($"=== LOADING CAVE FILE FOR TRIAL {trialNumber} ===");
        int caveIndex = trialNumber - 1;

        // Try TextAsset array first
        if (caveFiles != null && caveIndex >= 0 && caveIndex < caveFiles.Length && caveFiles[caveIndex] != null)
        {
            csvFile = caveFiles[caveIndex];
            ReadCSVFromTextAsset();
            Debug.Log($"SUCCESS: Loaded cave file from array: {caveFiles[caveIndex].name}");
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
                Debug.Log($"SUCCESS: Loading from path: {relative}");
                ReadCSVFromAbsolutePath(absolute);
                return;
            }

            Debug.LogError($"Cave CSV not found at path: {absolute}");
        }

        // Final fallback to original
        Debug.LogWarning($"Using original cave file as last resort");
        csvFile = originalCaveFile;
        ReadCSVFromTextAsset();
    }


    /// Restore original cave file after trials
    /// PUBLIC API: Called by TrialSystemManager

    public void RestoreOriginalCaveFile()
    {
        if (originalCaveFile != null)
        {
            csvFile = originalCaveFile;
            ReadCSVFromTextAsset();
            Debug.Log($"Restored original cave file: {originalCaveFile.name}");
        }
        else
        {
            Debug.LogWarning("Original cave file was null - cannot restore");
        }
    }

    // ========== TRIAL HELPER METHODS (FROM ORIGINAL CODE) ==========
    // These methods are kept here for direct access by trial system

    /// <summary>
    /// Reset player to standardized start position
    /// Position: (291.74, -35.95, 262.73)
    /// Rotation: (3.91, 179.7, 0)
    /// Scale: (5.6000, 5.5999, 1.4000)
    /// </summary>
    public void ResetPlayerToStartPosition()
    {
        if (playerMovement == null)
        {
            Debug.LogError("PlayerMovement component not found! Cannot reset player position!");
            return;
        }

        // Stop any ongoing movement
        playerMovement.StopAllCoroutines();

        // Reset physics
        var rb = playerMovement.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.Sleep();
            rb.WakeUp();
        }

        // Always use fixed trial start position as specified
        Vector3 spawnPosition = new Vector3(291.74f, -35.95f, 262.73f);
        Quaternion spawnRotation = Quaternion.Euler(3.91f, 179.7f, 0f);
        Debug.Log($"Using fixed trial start position: {spawnPosition} with rotation: {spawnRotation.eulerAngles}");

        // Set position, rotation, and scale
        playerMovement.transform.SetPositionAndRotation(spawnPosition, spawnRotation);

        // Apply specified scale for trials
        Vector3 trialScale = new Vector3(5.6000f, 5.5999f, 1.4000f);
        playerMovement.transform.localScale = trialScale;
        Debug.Log($"Applied trial scale: {trialScale}");

        // Reset movement flags
        playerMovement.afterText = false; // PlayerIntro will set this to true
        playerMovement.canMove = true;

        Debug.Log($"Player reset to position: {spawnPosition}, rotation: {spawnRotation.eulerAngles}");
    }

    /// <summary>
    /// Reset all game systems for new trial
    /// </summary>
    public void ResetGameSystemsForTrial()
    {
        // Reset PlayerLife system
        if (playerLife != null)
        {
            playerLife.StopAllCoroutines();
            playerLife.didntGetInputsYet = true;
            // Don't call ProcessUserInputs yet - wait for parameters to be loaded
            Debug.Log("PlayerLife reset - ready for new parameters");
        }

        // Reset Health system
        if (health != null)
        {
            health.StopAllCoroutines();
            health.didntGetInputsYet = true;
            health.heal(100f); // Full health for new trial
            // Don't call ProcessUserInputs yet - wait for parameters to be loaded
            Debug.Log("Health reset to 100% - ready for new parameters");
        }

        // Reset PlayerIntro
        var playerIntro = FindObjectOfType<PlayerIntro>();
        if (playerIntro != null)
        {
            playerIntro.ResetIntro();
            Debug.Log("PlayerIntro reset - ready to show intro text");
        }

        // Reset progress UI
        if (levelProgressUI != null)
        {
            var slider = levelProgressUI.GetComponent<UnityEngine.UI.Slider>();
            if (slider != null)
            {
                slider.value = 0f;
                Debug.Log("Progress bar reset to zero");
            }
        }

        // Reset CaveTracker
        var caveTracker = FindObjectOfType<CaveTracker>();
        if (caveTracker != null)
        {
            caveTracker.currentCaveIndex = -1;
            caveTracker.outsideCollisions = 0;
            Debug.Log("CaveTracker reset");
        }

        // Reset GameStateManager for new trial
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ResetState();
            Debug.Log("GameStateManager reset for new trial");
        }

        // Ensure trials mode is active
        GameStateManager.SetTrialsActive(true);


    }
}
