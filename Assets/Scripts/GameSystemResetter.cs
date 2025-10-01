using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/**
 * GameSystemResetter - Manages player and game system resets for trials
 * Handles player positioning, component resets, and cleanup

 */
public class GameSystemResetter : MonoBehaviour
{
    [Header("Component References")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerLife playerLife;
    [SerializeField] private Health health;
    [SerializeField] private LevelProgressUI levelProgressUI;
    
    [Header("Trial Player Start")]
    [SerializeField] private Transform playerStart;
    [SerializeField] private Vector3 trialStartPosition = new Vector3(291.74f, -35.95f, 262.73f);
    [SerializeField] private Quaternion trialStartRotation = default;
    [SerializeField] private Vector3 trialScale = new Vector3(5.6000f, 5.5999f, 1.4000f);
    
    [Header("Protected Scene Objects")]
    [SerializeField] private List<string> protectedObjectNames = new List<string> { "tank", "OxygenBottle", "oxygen" };
    [SerializeField] private List<string> protectedTags = new List<string> { "OxygenObject", "OxygenTank", "SceneObject" };
    
    // Spawned objects tracking
    private List<GameObject> spawnedTrialObjects = new List<GameObject>();
    
    private Dictionary<GameObject, ObjectState> sceneObjectStates = new Dictionary<GameObject, ObjectState>();
    
    private struct ObjectState
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        public bool wasActive;
    }
    
    void Awake()
    {
        // Initialize trial start rotation if not set
        if (trialStartRotation == default(Quaternion))
        {
            trialStartRotation = Quaternion.Euler(3.91f, 179.7f, 0f);
        }
        
        SaveSceneObjectStates();
    }
    
    private void SaveSceneObjectStates()
    {
        foreach (string objName in protectedObjectNames)
        {
            GameObject obj = GameObject.Find(objName);
            if (obj != null && !sceneObjectStates.ContainsKey(obj))
            {
                sceneObjectStates[obj] = new ObjectState
                {
                    position = obj.transform.position,
                    rotation = obj.transform.rotation,
                    scale = obj.transform.localScale,
                    wasActive = obj.activeSelf
                };
                Debug.Log($"Saved initial state for protected object: {objName}");
            }
        }
        
        foreach (string tag in protectedTags)
        {
            try
            {
                GameObject[] taggedObjects = GameObject.FindGameObjectsWithTag(tag);
                foreach (GameObject obj in taggedObjects)
                {
                    if (obj != null && !sceneObjectStates.ContainsKey(obj))
                    {
                        sceneObjectStates[obj] = new ObjectState
                        {
                            position = obj.transform.position,
                            rotation = obj.transform.rotation,
                            scale = obj.transform.localScale,
                            wasActive = obj.activeSelf
                        };
                        Debug.Log($"Saved initial state for tagged object: {obj.name} (tag: {tag})");
                    }
                }
            }
            catch (UnityException)
            {
                Debug.LogWarning($"Tag '{tag}' not found in project - skipping");
            }
        }
    }
    
    private bool IsProtectedSceneObject(GameObject go)
    {
        if (go == null) return false;
        
        if (sceneObjectStates.ContainsKey(go))
        {
            return true;
        }
        
        foreach (string protectedName in protectedObjectNames)
        {
            if (go.name.Contains(protectedName))
            {
                Debug.Log($"Protected by name: {go.name}");
                return true;
            }
        }
        
        foreach (string protectedTag in protectedTags)
        {
            try
            {
                if (go.CompareTag(protectedTag))
                {
                    Debug.Log($"Protected by tag: {go.name} (tag: {protectedTag})");
                    return true;
                }
            }
            catch (UnityException)
            {
                // Tag doesn't exist - continue
            }
        }
        
        if (!string.IsNullOrEmpty(go.scene.name) && go.scene.IsValid())
        {
            if ((go.hideFlags & HideFlags.DontSave) != 0)
            {
                return false;
            }
        }
        
        return false;
    }
    
    public void TrackSpawned(GameObject go)
    {
        if (go == null) return;
        
        if (IsProtectedSceneObject(go))
        {
            Debug.Log($"Not tracking protected scene object: {go.name}");
            return;
        }
        
        if (!spawnedTrialObjects.Contains(go))
        {
            spawnedTrialObjects.Add(go);
            Debug.Log($"Tracking spawned object: {go.name}");
        }
    }
    
    public void ResetPlayerToStartPosition()
    {
        if (playerMovement == null)
        {
            Debug.LogError("PlayerMovement component not found!");
            return;
        }

        playerMovement.StopAllCoroutines();
        
        var rb = playerMovement.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.Sleep();
            rb.WakeUp();
        }

        playerMovement.transform.SetPositionAndRotation(trialStartPosition, trialStartRotation);
        playerMovement.transform.localScale = trialScale;
        
        playerMovement.afterText = false;
        playerMovement.canMove = true;
        
        Debug.Log($"Player reset to EXACT trial position: {trialStartPosition}, rotation: {trialStartRotation.eulerAngles}, scale: {trialScale}");
    }
    
    public void ResetGameSystemsForTrial()
    {
        if (playerLife != null)
        {
            playerLife.StopAllCoroutines();
            playerLife.didntGetInputsYet = true;
            Debug.Log("PlayerLife reset - ready for new parameters");
        }

        if (health != null)
        {
            health.StopAllCoroutines();
            health.didntGetInputsYet = true;
            health.heal(100f);
            Debug.Log("Health reset to 100%");
        }

        var playerIntro = FindObjectOfType<PlayerIntro>();
        if (playerIntro != null)
        {
            playerIntro.ResetIntro();
            Debug.Log("PlayerIntro reset");
        }

        if (levelProgressUI != null)
        {
            var slider = levelProgressUI.GetComponent<Slider>();
            if (slider != null)
            {
                slider.value = 0f;
                Debug.Log("Progress bar reset");
            }
        }

        var caveTracker = FindObjectOfType<CaveTracker>();
        if (caveTracker != null)
        {
            caveTracker.currentCaveIndex = -1;
            caveTracker.outsideCollisions = 0;
            Debug.Log("CaveTracker reset");
        }

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ResetState();
            Debug.Log("GameStateManager reset");
        }

        GameStateManager.SetTrialsActive(true);
        
        RestoreSceneObjects();
        
        Debug.Log("All game systems reset for trial");
    }
    
    private void RestoreSceneObjects()
    {
        foreach (var kvp in sceneObjectStates)
        {
            GameObject obj = kvp.Key;
            ObjectState state = kvp.Value;
            
            if (obj != null)
            {
                obj.transform.position = state.position;
                obj.transform.rotation = state.rotation;
                obj.transform.localScale = state.scale;
                obj.SetActive(state.wasActive);
                
                Debug.Log($"Restored scene object: {obj.name} to original state");
            }
        }
    }
    
    public void PrepareComponentsForClosePanel()
    {
        if (playerLife != null)
        {
            playerLife.didntGetInputsYet = true;
        }
        
        if (health != null)
        {
            health.didntGetInputsYet = true;
        }
    }
    
    public void PrepareForNextTrial()
    {
        Debug.Log("=== PREPARING FOR NEXT TRIAL ===");
        
        try
        {
            if (health != null) 
            {
                health.StopAllCoroutines();
                Debug.Log("Health coroutines stopped");
            }
            if (playerLife != null) 
            {
                playerLife.StopAllCoroutines();
                Debug.Log("PlayerLife coroutines stopped");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Error stopping coroutines: {e.Message}");
        }

        try
        {
            CleanupSpawned();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error during CleanupSpawned: {e.Message}");
        }


        RestoreSceneObjects();
        
        Debug.Log("Trial preparation complete");
    }
    
    public void CleanupSpawned()
    {
        Debug.Log($"CleanupSpawned called - {spawnedTrialObjects.Count} objects to clean");
        
        if (spawnedTrialObjects.Count == 0) 
        {
            Debug.Log("No spawned objects to clean up");
            return;
        }
        
        int destroyed = 0;
        int protected_count = 0;
        var objectsToDestroy = new List<GameObject>(spawnedTrialObjects);
        
        foreach (var go in objectsToDestroy)
        {
            if (go != null) 
            {
                if (IsProtectedSceneObject(go))
                {
                    Debug.Log($"Skipping protected scene object: {go.name}");
                    protected_count++;
                    continue;
                }
                
                try
                {
                    Debug.Log($"Destroying spawned object: {go.name}");
                    go.SetActive(false);
                    
                    #if UNITY_EDITOR
                    if (Application.isPlaying)
                    {
                        Destroy(go);
                    }
                    else
                    {
                        DestroyImmediate(go);
                    }
                    #else
                    Destroy(go);
                    #endif
                    
                    destroyed++;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"Error destroying object {go.name}: {e.Message}");
                }
            }
        }
        
        spawnedTrialObjects.Clear();
        Debug.Log($"Cleanup complete - destroyed {destroyed} objects, protected {protected_count} scene objects");

        if (destroyed > 10)
        {
            System.GC.Collect();
        }
    }
    
    public void CleanupAllTrialObjects()
    {
        Debug.Log("Cleaning up trial objects...");
        
        CleanupSpawned();
        
        // DON'T cleanup trial fish - they're managed by TrialFishSpawner
        Debug.Log("Trial fish kept active (managed by spawner)");
        
        RestoreSceneObjects();
    }
    
    void OnDestroy()
    {
        try
        {
            if (spawnedTrialObjects != null && spawnedTrialObjects.Count > 0)
            {
                Debug.Log($"OnDestroy: Cleaning up {spawnedTrialObjects.Count} spawned objects");
                CleanupSpawned();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Error during OnDestroy cleanup: {e.Message}");
        }
    }
}
