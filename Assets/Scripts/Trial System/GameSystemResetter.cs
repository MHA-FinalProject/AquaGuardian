using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/**
 * Resets game systems and scene objects between trials
 * Manages player position, health, spawned objects, and protected scene objects
 * Tracks and cleans up trial-specific objects while preserving protected ones
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
    [SerializeField] private List<string> protectedObjectNames = new List<string> { "tank", "oxygen" };
    [SerializeField] private List<string> protectedTags = new List<string> { "OxygenObject" };
    
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
        if (trialStartRotation == default(Quaternion))
        {
            trialStartRotation = Quaternion.Euler(3.91f, 179.7f, 0f);
        }
        
        SaveSceneObjectStates();
    }
    
    private void SaveSceneObjectStates()
    {
        SaveObjectsByName(protectedObjectNames);
        SaveObjectsByTag(protectedTags);
    }

    private void SaveObjectsByName(List<string> names)
    {
        foreach (string objName in names)
        {
            GameObject obj = GameObject.Find(objName);
            if (obj != null && !sceneObjectStates.ContainsKey(obj))
            {
                SaveObjectState(obj);
            }
        }
    }

    private void SaveObjectsByTag(List<string> tags)
    {
        foreach (string tag in tags)
        {
            try
            {
                GameObject[] taggedObjects = GameObject.FindGameObjectsWithTag(tag);
                foreach (GameObject obj in taggedObjects)
                {
                    if (obj != null && !sceneObjectStates.ContainsKey(obj))
                    {
                        SaveObjectState(obj);
                    }
                }
            }
            catch (UnityException)
            {
                continue;
            }
        }
    }

    private void SaveObjectState(GameObject obj)
    {
        sceneObjectStates[obj] = new ObjectState
        {
            position = obj.transform.position,
            rotation = obj.transform.rotation,
            scale = obj.transform.localScale,
            wasActive = obj.activeSelf
        };
    }
    
    private bool IsProtectedSceneObject(GameObject go)
    {
        if (go == null) return false;
        
        if (sceneObjectStates.ContainsKey(go))
            return true;
        
        foreach (string protectedName in protectedObjectNames)
        {
            if (go.name.Contains(protectedName))
                return true;
        }
        
        return HasProtectedTag(go);
    }

    private bool HasProtectedTag(GameObject go)
    {
        foreach (string protectedTag in protectedTags)
        {
            try
            {
                if (go.CompareTag(protectedTag))
                    return true;
            }
            catch (UnityException)
            {
                continue;
            }
        }
        
        return false;
    }
    
    public void TrackSpawned(GameObject go)
    {
        if (go == null || IsProtectedSceneObject(go))
            return;
        
        if (!spawnedTrialObjects.Contains(go))
            spawnedTrialObjects.Add(go);
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
    }
    
    public void ResetGameSystemsForTrial()
    {
        ResetLifeComponents();
        ResetPlayerIntro();
        ResetLevelProgress();
        ResetGameState();

        GameStateManager.SetTrialsActive(true);
        RestoreSceneObjects();
    }

    private void ResetLifeComponents()
    {
        if (playerLife != null)
        {
            playerLife.StopAllCoroutines();
            playerLife.didntGetInputsYet = true;
            playerLife.ResetBloodSplatter();
        }

        if (health != null)
        {
            health.StopAllCoroutines();
            health.didntGetInputsYet = true;
            health.heal(100f);
        }
        }

    private void ResetPlayerIntro()
    {
        var playerIntro = FindObjectOfType<PlayerIntro>();
        if (playerIntro != null)
        {
            playerIntro.ResetIntro();
        }
        }

    private void ResetLevelProgress()
    {
        if (levelProgressUI != null)
        {
            var slider = levelProgressUI.GetComponent<Slider>();
            if (slider != null)
            {
                slider.value = 0f;
            }
            }
        }

    private void ResetGameState()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ResetState();
        }
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
            }
        }
    }
    
    public void PrepareComponentsForClosePanel()
    {
        if (playerLife != null)
            playerLife.didntGetInputsYet = true;
        
        if (health != null)
            health.didntGetInputsYet = true;
    }
    
    public void PrepareForNextTrial()
    {
        StopLifeCoroutines();
        CleanupSpawned();
        RestoreSceneObjects();
    }

    private void StopLifeCoroutines()
    {
        try
        {
            if (health != null) 
                health.StopAllCoroutines();

            if (playerLife != null) 
            {
                playerLife.StopAllCoroutines();
                playerLife.ResetBloodSplatter();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Error stopping coroutines: {e.Message}");
        }
    }
    
    public void CleanupSpawned()
    {
        if (spawnedTrialObjects.Count == 0) 
            return;
        
        int destroyed = 0;
        var objectsToDestroy = new List<GameObject>(spawnedTrialObjects);
        
        foreach (var go in objectsToDestroy)
        {
            if (go != null && !IsProtectedSceneObject(go))
            {
                try
                {
                    go.SetActive(false);
                    
                    #if UNITY_EDITOR
                    if (Application.isPlaying)
                        Destroy(go);
                    else
                        DestroyImmediate(go);
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

        if (destroyed > 10)
        {
            System.GC.Collect();
        }
    }
    
    public void CleanupAllTrialObjects()
    {
        CleanupSpawned();
        RestoreSceneObjects();
    }
    
    void OnDestroy()
    {
        try
        {
            if (spawnedTrialObjects != null && spawnedTrialObjects.Count > 0)
            {
                CleanupSpawned();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Error during OnDestroy cleanup: {e.Message}");
        }
    }
}
