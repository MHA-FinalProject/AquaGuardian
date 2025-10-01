using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class CaveStats
{
    public int caveIndex;
    public float timeSpent;    // seconds
    public int collisions;
    public float reactionTime = -1f; // seconds; -1 if not captured
    public float reactionTimeActual = -1f; // actual time-based reaction time
    public float avgForwardSpeed = 0f; // computed as distance/time
    public float theoreticalTime = 0f; // expected time based on cave length and speed
    // Entry/Exit timing
    public float entryTime = -1f; // when entered the cave (Time.time)
    public float exitTime = -1f; // when exited the cave (Time.time)
    // internals
    public float forwardDistance = 0f;
    public bool cueTriggered = false;
    public bool responseCaptured = false;
    public float cueZ;
    public float tCue;
    public float zAtCue;
}

public class CaveTracker : MonoBehaviour
{
    [Header("Refs")]
    public Transform player;           // Assign Player transform
    public PanelOpenUp panel;          // Assign PanelOpenUp instance

    [Header("Runtime")]
    public int currentCaveIndex = -1;  // -1 if not inside any cave
    public float forwardSpeed;          // current forward speed estimate
    
    [Header("Reporting Options")]
    [SerializeField] private bool printConsoleReport = false; // Verbose debug info (off by default)
    // [SerializeField] private bool enableLiveFileLogging = false; // Reserved for future use
    [Header("Reaction Time Settings")]
    [SerializeField] private float cueDistance = 5f; // meters before cave (on Z)
    [SerializeField] private float inputThresholdPC = 0.05f; // Input axis threshold
    [SerializeField] private float verticalVelThreshold = 0.5f; // m/s change for Amadeo
    [SerializeField] private float caveBoundsEpsilon = 0.05f; // expand bounds slightly to avoid edge misses
    
    [Header("Collision Counting")]
    [SerializeField] private float collisionDebounceSeconds = 0.5f; // Minimum interval between counted collisions
    private float lastCollisionTimestamp = -999f;

    private readonly Dictionary<int, CaveStats> statsByIndex = new Dictionary<int, CaveStats>();
    public int outsideCollisions = 0;
    private Vector3 lastPlayerPos;
    private float sessionStartTime;
    private Health health;
    private string inputMode = "PC";
    private PlayerMovement pm;
    private Rigidbody rb;
    private int previousCaveIndex = -1;
    private bool sessionEnded = false;
    private bool caveStatsInitialized = false;

    void Start()
    {
        Debug.Log("CaveTracker: Starting initialization...");
        
        // Auto-create CaveTracker singleton if needed
        var existing = FindObjectsOfType<CaveTracker>();
        if (existing.Length > 1)
        {
            Debug.LogWarning("Multiple CaveTracker instances found! Destroying duplicate.");
            DestroyImmediate(this);
            return;
        }
        
        if (panel == null)
        {
            panel = FindObjectOfType<PanelOpenUp>();
        }
        Debug.Log($"CaveTracker: Panel found: {panel != null}");
        
        if (player == null)
        {
            pm = FindObjectOfType<PlayerMovement>();
            if (pm != null) player = pm.transform;
            if (pm != null)
            {
                // Detect input mode
                inputMode = pm.notGetForcesFromAmadeo ? "PC" : "Amadeo";
            }
        }
        if (pm == null) pm = FindObjectOfType<PlayerMovement>();
        if (player != null) rb = player.GetComponent<Rigidbody>();
        if (printConsoleReport) Debug.Log($"CaveTracker: Player found: {player != null}, PlayerMovement: {pm != null}");
        
        health = FindObjectOfType<Health>();
        if (health == null)
        {
            Debug.LogWarning("CaveTracker: Health component not found - oxygen reading will be unavailable");
        }
        sessionStartTime = Time.time;
        if (player != null) lastPlayerPos = player.position;

        // Will initialize cave stats dynamically when caves are ready
        InitializeCaveStatsIfReady();
        
        if (printConsoleReport) Debug.Log("CaveTracker: Initialization complete");
    }

    void Update()
    {
        if (player == null || panel == null) 
        {
            return;
        }
        
        // Try to initialize cave stats if not done yet
        if (!caveStatsInitialized)
        {
            InitializeCaveStatsIfReady();
            if (!caveStatsInitialized) return; // Still not ready
        }

        // Forward speed estimate from Z progress (negative Z is forward). Use absolute forward rate
        float dzThisFrame = lastPlayerPos.z - player.position.z;
        forwardSpeed = Mathf.Max(0f, dzThisFrame / Mathf.Max(Time.deltaTime, 1e-5f));

        float z = player.position.z;
        int newIndex = -1;

        // Find which cave Z is currently within
        for (int i = 0; i < panel.caveInfos.Count; i++)
        {
            var c = panel.caveInfos[i];
            if (z >= c.minZ - caveBoundsEpsilon && z <= c.maxZ + caveBoundsEpsilon)
            {
                newIndex = c.index;
                break;
            }
        }
        
        // Debug cave detection every 60 frames
        if (Time.frameCount % 60 == 0)
        {
            if (printConsoleReport)
            {
                string currentLabel = (newIndex == -1) ? "On the way" : newIndex.ToString();
                Debug.Log($"CaveTracker: Player Z={z:F1}, CurrentCave={currentLabel}, Available caves: {panel.caveInfos.Count}");
                for (int i = 0; i < panel.caveInfos.Count; i++)
                {
                    var c = panel.caveInfos[i];
                    bool inside = z >= c.minZ - caveBoundsEpsilon && z <= c.maxZ + caveBoundsEpsilon;
                    Debug.Log($"  Cave {c.index}: Z[{c.minZ:F1},{c.maxZ:F1}] - Player inside: {inside}");
                }
            }
        }

        // Detect cave enter/exit transitions
        previousCaveIndex = currentCaveIndex;
        currentCaveIndex = newIndex;
        


        if (previousCaveIndex != currentCaveIndex)
        {
                        // Exited previous cave
            if (previousCaveIndex != -1 && statsByIndex.TryGetValue(previousCaveIndex, out var prevStats))
            {
                // Record exit time
                prevStats.exitTime = Time.time;
                
                // Compute theoretical time using cave length and configured forward speed (for analysis only)
                var prevInfo = GetCaveInfoByIndex(previousCaveIndex);
                if (prevInfo != null)
                {
                    float caveLength = Mathf.Abs(prevInfo.maxZ - prevInfo.minZ);
                    float configuredSpeed = pm != null ? Mathf.Max(0.01f, pm.speed) : Mathf.Max(0.01f, forwardSpeed);
                    prevStats.theoreticalTime = caveLength / configuredSpeed;
                }
                
                // Calculate exact time spent using entry/exit times
                float exactTimeSpent = prevStats.exitTime - prevStats.entryTime;
                string timeCompare = prevStats.theoreticalTime > 0 ? $" vs theoretical={prevStats.theoreticalTime:F2}s" : "";
                
                string reactionDist = prevStats.reactionTime > 0 ? $"{prevStats.reactionTime:F2}s" : "N/A";
                string reactionActual = prevStats.reactionTimeActual > 0 ? $"{prevStats.reactionTimeActual:F2}s" : "N/A";
                float oxygenEnd = -1f;
                try { if (health != null) oxygenEnd = health.GetOxygen(); } catch {}
                string oxygenStr = oxygenEnd >= 0 ? $", oxygen={oxygenEnd:F0}%" : "";
                
                Debug.Log($"*** EXIT Cave {previousCaveIndex} *** | Entry: T+{prevStats.entryTime:F1}s, Exit: T+{prevStats.exitTime:F1}s, exactTime: {exactTimeSpent:F2}s, ExpectedTime: {prevStats.theoreticalTime:F2}s, Collisions: {prevStats.collisions} | {System.DateTime.Now:HH:mm:ss.fff}");
            }

            // Entered new cave
            if (currentCaveIndex != -1 && statsByIndex.TryGetValue(currentCaveIndex, out var newStats))
            {
                newStats.entryTime = Time.time; // Record entry time
                var info = GetCaveInfoByIndex(currentCaveIndex);
                if (info != null)
                {
                    float caveLength = Mathf.Abs(info.maxZ - info.minZ);
                    float configuredSpeed = pm != null ? Mathf.Max(0.01f, pm.speed) : Mathf.Max(0.01f, forwardSpeed);
                    float theoreticalTime = caveLength / configuredSpeed;
                    Debug.Log($"*** ENTER Cave {currentCaveIndex} *** | Entry: T+{newStats.entryTime:F1}s, ExpectedTime: {theoreticalTime:F2}s | {System.DateTime.Now:HH:mm:ss.fff}");
                }
                else
                {
                    Debug.Log($"*** ENTER Cave {currentCaveIndex} *** | Entry: T+{newStats.entryTime:F1}s | {System.DateTime.Now:HH:mm:ss.fff}");
                }
            }
        }

        // Accumulate time and distance for current cave
        if (currentCaveIndex != -1 && statsByIndex.TryGetValue(currentCaveIndex, out var s))
        {
            s.timeSpent += Time.deltaTime;
            // forward distance accumulation for avg speed
            s.forwardDistance += Mathf.Max(0f, dzThisFrame);
            s.avgForwardSpeed = s.timeSpent > 0 ? s.forwardDistance / s.timeSpent : 0f;
        }

        // Reaction time tracking outside caves only
        for (int i = 0; i < panel.caveInfos.Count; i++)
        {
            var caveInfo = panel.caveInfos[i];
            if (statsByIndex.TryGetValue(caveInfo.index, out var cs))
            {
                // Check if player is approaching the cave (before entering)
                bool approachingCave = player.position.z > caveInfo.maxZ + 1f; // 1 meter before cave
                
                // Trigger cue when crossing the cue plane while approaching cave
                if (!cs.cueTriggered && approachingCave && player.position.z <= cs.cueZ)
                {
                    cs.cueTriggered = true;
                    cs.tCue = Time.time;
                    cs.zAtCue = player.position.z;
                    Debug.Log($"**CUE** Cave {caveInfo.index} triggered at Z={player.position.z:F2} (cueZ={cs.cueZ:F2})");
                }

                // Capture reaction time when inside cave area (after cue) with epsilon tolerance
                bool insideCave = player.position.z >= caveInfo.minZ - caveBoundsEpsilon && 
                                 player.position.z <= caveInfo.maxZ + caveBoundsEpsilon;
                                 
                if (cs.cueTriggered && !cs.responseCaptured && insideCave)
                {
                    bool responded = false;
                    if (inputMode == "PC")
                    {
                        float upDown = Input.GetAxis("UpDown");
                        responded = Mathf.Abs(upDown) >= inputThresholdPC;
                    }
                    else
                    {
                        // Amadeo: check for vertical movement
                        float vy = rb != null ? rb.velocity.y : 0f;
                        responded = Mathf.Abs(vy) >= verticalVelThreshold;
                        if (printConsoleReport)
                        {
                            Debug.Log($"Amadeo: responded={responded}, vy={vy:F2}");
                        }
                    }

                    if (responded) 
                    {
                        // Compute both reaction times
                        // 1. Distance-based (original method)
                        float configuredSpeed = pm != null ? Mathf.Max(0.01f, pm.speed) : Mathf.Max(0.01f, forwardSpeed);
                        float dzFromCue = Mathf.Abs(cs.zAtCue - player.position.z);
                        cs.reactionTime = dzFromCue / configuredSpeed;
                        if (printConsoleReport)
                        {
                            Debug.Log($"Response detected: reactionTime={cs.reactionTime:F2}, dzFromCue={dzFromCue:F2}, configuredSpeed={configuredSpeed:F2}");
                        }
                        
                        // 2. Actual time-based
                        cs.reactionTimeActual = Time.time - cs.tCue;
                        
                        cs.responseCaptured = true;
                        Debug.Log($"Reaction captured for Cave {caveInfo.index} | distance-based={cs.reactionTime:F2}s, time-based={cs.reactionTimeActual:F2}s");
                    }
                }
                
                // Fallback: If entered cave after cue but no explicit response detected, still calculate reaction time
                if (cs.cueTriggered && !cs.responseCaptured && insideCave && currentCaveIndex == caveInfo.index)
                {
                    float configuredSpeed = pm != null ? Mathf.Max(0.01f, pm.speed) : Mathf.Max(0.01f, forwardSpeed);
                    float dzFromCue = Mathf.Abs(cs.zAtCue - player.position.z);
                    cs.reactionTime = dzFromCue / configuredSpeed;
                    cs.reactionTimeActual = Time.time - cs.tCue;
                    cs.responseCaptured = true;
                    Debug.Log($"Fallback reaction time for Cave {caveInfo.index} | distance-based={cs.reactionTime:F2}s, time-based={cs.reactionTimeActual:F2}s (no explicit input detected)");
                }
            }
        }

        // Check if all caves completed or player reached end
        CheckSessionCompletion();
        
        // Check if player reached the end (beyond all caves)
        if (!sessionEnded && player.position.z < GetMinZOfAllCaves() - 100f)
        {
            sessionEnded = true;
            Debug.Log("=== Player reached end - printing summary ===");
            PrintResults();
        }

        // Debug summary on demand
        if (Input.GetKeyDown(KeyCode.F11))
        {
            PrintResults();
        }

        // Update last position at end of frame calculations
        lastPlayerPos = player.position;
    }

    // To be called by Player on collision
    public void RegisterCollision()
    {
        // Debounce to avoid multiple counts from the same physical impact
        if (Time.time - lastCollisionTimestamp < collisionDebounceSeconds)
        {
            Debug.Log($"CaveTracker: Collision ignored due to debounce (last: {lastCollisionTimestamp:F2}, now: {Time.time:F2}, diff: {Time.time - lastCollisionTimestamp:F2}s)");
            return;
        }

        lastCollisionTimestamp = Time.time;
        float playerZ = player != null ? player.position.z : 0f;

        if (currentCaveIndex != -1 && statsByIndex.TryGetValue(currentCaveIndex, out var s)) 
        {
            s.collisions++;
            // Always show core collision info
            Debug.Log($"Collision in Cave {currentCaveIndex} at Z={playerZ:F1} - Total hits: {s.collisions}");
        }
        else
        {
            outsideCollisions++;
            Debug.Log($" COLLISION OUTSIDE any cave at Z={playerZ:F1} | totalOutside={outsideCollisions}");
        }
    }

    void OnDestroy()
    {
        // Print results when object is destroyed (e.g., scene change)
        if (!sessionEnded && statsByIndex.Count > 0)
        {
            if (printConsoleReport) Debug.Log("=== SESSION ENDING - Exporting results ===");
            PrintResults();
        }
    }

    private void CheckSessionCompletion()
    {
        if (sessionEnded || panel == null || panel.caveInfos == null) return;

        // Check if all caves have been visited (have time spent > 0)
        int cavesVisited = 0;
        foreach (var kv in statsByIndex)
        {
            if (kv.Value.timeSpent > 0f)
            {
                cavesVisited++;
            }
        }

        // If all caves visited and currently not in any cave, session is complete
        if (cavesVisited >= panel.caveInfos.Count && currentCaveIndex == -1)
        {
            sessionEnded = true;
            Debug.Log("=== SESSION COMPLETED - Auto-printing results ===");
            PrintResults();
        }
    }

    private float GetMinZOfAllCaves()
    {
        if (panel == null || panel.caveInfos == null || panel.caveInfos.Count == 0) return 0f;
        
        float minZ = float.MaxValue;
        foreach (var caveInfo in panel.caveInfos)
        {
            if (caveInfo.minZ < minZ)
                minZ = caveInfo.minZ;
        }
        return minZ;
    }

    private PanelOpenUp.CaveInfo GetCaveInfoByIndex(int index)
    {
        if (panel == null || panel.caveInfos == null) return null;
        for (int i = 0; i < panel.caveInfos.Count; i++)
        {
            if (panel.caveInfos[i].index == index) return panel.caveInfos[i];
        }
        return null;
    }

    public void PrintResults()
    {

        // Show collisions per cave
        foreach (var kv in statsByIndex)
        {
            var s = kv.Value;
            if (s.timeSpent > 0) // Only show caves that were visited
            {
                Debug.Log($"Cave {s.caveIndex}: {s.collisions} collisions");
            }
        }
        
        // Show totals
        var playerLife = FindObjectOfType<PlayerLife>();
        int totalCaveCollisions = GetTotalCollisions();
        int trackerTotalCollisions = totalCaveCollisions + outsideCollisions; // CaveTracker's total count
        int playerLifeCollisions = playerLife != null ? playerLife.GetCollisionCount() : -1;
        
        Debug.Log($"CaveTracker - In caves: {totalCaveCollisions}");
        Debug.Log($"CaveTracker - Outside caves: {outsideCollisions}");
        Debug.Log($"CaveTracker - Total: {trackerTotalCollisions}");
        Debug.Log($"PlayerLife - Cave collisions (only when canCollide=true): {playerLifeCollisions}");
        Debug.Log("Note: CaveTracker counts ALL collisions, PlayerLife only counts cave collisions when damage can be applied");
 
        
        // Export CSV and TXT files
        ReportExporter.SaveSessionCsv(this, panel, health, pm);
        ReportExporter.SaveSessionTxt(this, panel, health, pm);
       /*
        // Export regression data for machine learning
        try 
        {
            ExcelExporter.ExportForLinearRegression(this, panel, health, pm);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"Failed to export regression data: {ex.Message}");
        }
        */
    }
    

    

    public CaveStats GetStats(int caveIndex)
    {
        return statsByIndex.TryGetValue(caveIndex, out var s) ? s : null;
    }

    public int GetTotalCollisions()
    {
        int total = 0;
        foreach (var kv in statsByIndex)
        {
            total += kv.Value.collisions;
        }
        return total;
    }

    /// <summary>
    /// Force print results (can be called externally)
    /// </summary>
    public void ForcePrintResults()
    {
        if (!sessionEnded)
        {
            Debug.Log("=== FORCED RESULTS PRINT ===");
            PrintResults();
            sessionEnded = true;
        }
    }
    
    /// <summary>
    /// Initialize cave stats when caves are ready (called dynamically)
    /// </summary>
    private void InitializeCaveStatsIfReady()
    {
        if (caveStatsInitialized || panel == null || panel.caveInfos == null || panel.caveInfos.Count == 0 || player == null)
        {
            return;
        }
        
        Debug.Log($"CaveTracker: Setting up {panel.caveInfos.Count} caves dynamically");
        statsByIndex.Clear(); // Clear any previous data
        
        // Determine forward direction by comparing player position to first cave
        bool forwardIsDecreasing = player.position.z > panel.caveInfos[0].maxZ;
        Debug.Log($"CaveTracker: Forward direction detected - Z {(forwardIsDecreasing ? "decreasing" : "increasing")}");
        
        foreach (var c in panel.caveInfos)
        {
            var stats = new CaveStats { caveIndex = c.index, timeSpent = 0f, collisions = 0 };
            
            // Cue is always before the cave entrance (higher Z for decreasing movement)
            stats.cueZ = c.maxZ + cueDistance;
            
            // Calculate theoretical time based on cave length and configured speed
            float caveLength = Mathf.Abs(c.maxZ - c.minZ);
            float configuredSpeed = pm != null ? Mathf.Max(0.01f, pm.speed) : 1f;
            stats.theoreticalTime = caveLength / configuredSpeed;
            
            statsByIndex[c.index] = stats;
            Debug.Log($"CaveTracker: Cave {c.index} setup - Z[{c.minZ:F1},{c.maxZ:F1}], cue at {stats.cueZ:F1}, theoretical time: {stats.theoreticalTime:F2}s");
        }
        
        caveStatsInitialized = true;
        Debug.Log("CaveTracker: Cave stats initialization complete!");
    }
}
