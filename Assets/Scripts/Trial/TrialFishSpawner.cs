using UnityEngine;
using System.Collections;

/**
 * TrialFishSpawner - Manages trial fish creation and trigger setup
 * Handles fish instantiation, trigger configuration, and animation
 * Extracted from PanelOpenUp.cs for better code organization
 * 
 *
 */
public class TrialFishSpawner : MonoBehaviour
{
    [Header("Fish Settings")]
    [SerializeField] private GameObject fishPrefab;
    [SerializeField] private GameObject[] trialFishPrefabs = new GameObject[5];
    [SerializeField] private GameObject chestPrefab; // Fallback ONLY - should rarely be used
    [SerializeField] private bool animateFish = false;
    [SerializeField] private Vector3 fishScale = new Vector3(2f, 0.8f, 1f);
    [SerializeField] private bool debugFishPosition = true;
    [SerializeField] private bool addGoToEndGameToFish = true;
    
    [Header("Component References")]
    [SerializeField] private PlayerMovement playerMovement;
    
    // Track created fish to prevent duplicates
    private GameObject currentTrialFish;
    
    /// <summary>
    /// Set chest prefab from PanelOpenUp (auto-wiring)
    /// ALWAYS update to ensure it persists between trials
    /// </summary>
    public void SetChestPrefab(GameObject chest)
    {
        if (chest != null)
        {
            if (chestPrefab == null)
            {
                Debug.Log($"TrialFishSpawner: Initial assignment of chest prefab: {chest.name}");
            }
            else if (chestPrefab != chest)
            {
                Debug.LogWarning($"TrialFishSpawner: Chest prefab changed from {chestPrefab.name} to {chest.name}");
            }
            chestPrefab = chest;
        }
    }
    
    /// <summary>
    /// Create a trial fish at specified position
    /// </summary>
    public GameObject CreateTrialFish(Vector3 position, int trialNumber)
    {
       // Debug.Log($"=== CreateTrialFish called for Trial {trialNumber} ===");
       // Debug.Log($"Position: {position}");
        // Debug.Log($"fishPrefab: {(fishPrefab != null ? fishPrefab.name : "NULL")}");
        
        // Clean up previous trial fish if exists
        if (currentTrialFish != null)
        {
          
            Destroy(currentTrialFish);
            currentTrialFish = null;
        }
        
        GameObject fish;
        
        // Select prefab (per-trial override or global)
        int trialIndex = Mathf.Clamp(trialNumber - 1, 0, 4);
        GameObject selectedPrefab = null;
        
        // Try trial-specific prefab first
        if (trialFishPrefabs != null && trialIndex < trialFishPrefabs.Length)
        {
            selectedPrefab = trialFishPrefabs[trialIndex];
            if (selectedPrefab != null)
            {
               // Debug.Log($"Using trial-specific prefab [{trialIndex}]: {selectedPrefab.name}");
            }
        }
        
        // Fallback to global fish prefab
        if (selectedPrefab == null && fishPrefab != null)
        {
            selectedPrefab = fishPrefab;
            //Debug.Log($"Using global fishPrefab: {selectedPrefab.name}");
        }
        
        // LAST RESORT: Use chest only if NO fish prefab exists
        if (selectedPrefab == null && chestPrefab != null)
        {
          
            selectedPrefab = chestPrefab;
        }

        // Create fish/target
        if (selectedPrefab != null)
        {
            fish = Instantiate(selectedPrefab, position, Quaternion.identity);
   
        }
        else
        {
            // Emergency fallback: Create procedural fish (Sphere)

            fish = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fish.transform.position = position;
            fish.transform.localScale = new Vector3(2f, 1.5f, 3f);
            
            var renderer = fish.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material.color = Color.cyan;
            }
          
        }
        
        // Basic setup
        fish.name = $"TrialFish_{trialNumber}";
        fish.tag = "TrialFish";
        fish.layer = LayerMask.NameToLayer("Default");
        
       
        fish.SetActive(true);
        
        // Track this fish
        currentTrialFish = fish;
        
        // Setup trigger system
        SetupFishTrigger(fish);
        
        // Add animation if enabled
        if (animateFish)
        {
            StartCoroutine(AnimateFish(fish));
        }
  
        return fish;
    }
    
    /// <summary>
    /// Setup reliable trigger system for fish
    /// </summary>
    private void SetupFishTrigger(GameObject fish)
    {
       
        
        fish.tag = "TrialFish";
        fish.layer = LayerMask.NameToLayer("Default");
        
        // Configure existing collider or add new one
        var existingCollider = fish.GetComponent<Collider>();
        if (existingCollider != null)
        {
            existingCollider.isTrigger = true;
            existingCollider.enabled = true;
            
            // Expand collider for better detection
            if (existingCollider is SphereCollider sc)
            {
                sc.radius = Mathf.Max(sc.radius, 2.0f);
            }
            else if (existingCollider is BoxCollider bc)
            {
                bc.size = Vector3.Max(bc.size, Vector3.one * 3f);
            }
            else if (existingCollider is CapsuleCollider cc)
            {
                cc.radius = Mathf.Max(cc.radius, 2.0f);
                cc.height = Mathf.Max(cc.height, 3.0f);
            }
            
           
        }
        else
        {
            var triggerCollider = fish.AddComponent<SphereCollider>();
            triggerCollider.isTrigger = true;
            triggerCollider.radius = 2.5f;
            //Debug.Log("Added new SphereCollider as trigger");
        }
        
        // Setup kinematic rigidbody
        var rb = fish.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = fish.AddComponent<Rigidbody>();
        
        }
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        
        // Ensure player can detect trigger
        EnsurePlayerTriggerSetup();
        
        // Add backup trigger detection
        if (addGoToEndGameToFish)
        {

            var goToEnd = fish.AddComponent<GoToEndGame>();
            
            var sceneNameField = typeof(GoToEndGame).GetField("sceneName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (sceneNameField != null)
            {
                sceneNameField.SetValue(goToEnd, "Win_Scene");
             
            }
        }
        
        // Verify setup
        VerifyTriggerSetup(fish);
        
       // Debug.Log($"Fish trigger setup complete for {fish.name}");
    }
    
    private void VerifyTriggerSetup(GameObject fish)
    {
        var fishCollider = fish.GetComponent<Collider>();
        var fishRb = fish.GetComponent<Rigidbody>();
        
        
      
        
        if (playerMovement != null)
        {
            var playerCollider = playerMovement.GetComponent<Collider>();
            var playerRb = playerMovement.GetComponent<Rigidbody>();

            //Debug.Log($"Player collider: {playerCollider?.GetType().Name}, isTrigger: {playerCollider?.isTrigger}");
            //Debug.Log($"Player rigidbody: exists: {playerRb != null}");
            
            bool layersIgnore = Physics.GetIgnoreLayerCollision(fish.layer, playerMovement.gameObject.layer);
            if (layersIgnore)
            {
               // Debug.LogError($"[CRITICAL] Layer collision DISABLED between fish and player!");
            }
            else
            {
                //Debug.Log($" Layer collision ENABLED between fish and player");
            }
            
            float distance = Vector3.Distance(fish.transform.position, playerMovement.transform.position);
           
        }
        
        
    }
    

    private void EnsurePlayerTriggerSetup()
    {
        if (playerMovement == null) return;
        
        var playerCollider = playerMovement.GetComponent<Collider>();
        if (playerCollider == null)
        {
            playerCollider = playerMovement.gameObject.AddComponent<CapsuleCollider>();
            
        }
        playerCollider.isTrigger = false;
        
        var playerRb = playerMovement.GetComponent<Rigidbody>();
        if (playerRb == null)
        {
            playerRb = playerMovement.gameObject.AddComponent<Rigidbody>();
            
        }
        playerRb.useGravity = false;
        playerRb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        
       
    }
    
    
    private IEnumerator AnimateFish(GameObject fish)
    {
        if (fish == null) yield break;
        
        Vector3 originalPosition = fish.transform.position;
        float time = 0f;
        
        while (fish != null && fish == currentTrialFish)
        {
            time += Time.deltaTime;
            
            float yOffset = Mathf.Sin(time * 2f) * 0.2f;
            float zOffset = Mathf.Cos(time * 1.5f) * 0.1f;
            
            fish.transform.position = originalPosition + new Vector3(0, yOffset, zOffset);
            
            fish.transform.rotation = Quaternion.Euler(
                Mathf.Sin(time * 1.2f) * 10f,
                Mathf.Cos(time * 0.8f) * 15f,
                0f
            );
            
            if (debugFishPosition && (Time.frameCount % 120 == 0))
            {
                float distance = Vector3.Distance(fish.transform.position, originalPosition);
                if (distance > 0.5f)
                {
                    //Debug.LogWarning($"Fish '{fish.name}' drifted {distance:F2} units from target!");
                }
            }
            
            yield return null;
        }
    }
    
  
    public void CleanupCurrentFish()
    {
        if (currentTrialFish != null)
        {
          
            Destroy(currentTrialFish);
            currentTrialFish = null;
        }
    }
    
  //  DEBUGGING FUNCTION
    [ContextMenu("Verify Fish Position")]
    public void VerifyFishPosition()
    {
        var allFish = GameObject.FindGameObjectsWithTag("TrialFish");
        
        foreach (var fish in allFish)
        {
           //Debug.Log($"Found fish '{fish.name}' at position: {fish.transform.position}");
        }
        
        if (allFish.Length == 0)
        {
            //Debug.Log("No trial fish found");
        }
        
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && allFish.Length > 0)
        {
            foreach (var fish in allFish)
            {
                float distance = Vector3.Distance(player.transform.position, fish.transform.position);
                Debug.Log($"Distance from player to fish '{fish.name}': {distance:F1} units");
                
                if (distance > 1000f)
                {
                    Debug.LogWarning($"Fish might be too far from player");
                }
            }
        }
    }
    
   
    [ContextMenu("Check Fish Prefab Status")]
    public void CheckPrefabStatus()
    {
       // Debug.Log("=== PREFAB STATUS ===");
       // Debug.Log($"Global fishPrefab: {(fishPrefab != null ? fishPrefab.name : " NOT ASSIGNED")}");
       // Debug.Log($"chestPrefab: {(chestPrefab != null ? chestPrefab.name : "NOT ASSIGNED")}");
        
        if (trialFishPrefabs != null)
        {
            for (int i = 0; i < trialFishPrefabs.Length; i++)
            {
                string status = trialFishPrefabs[i] != null ? trialFishPrefabs[i].name : "NOT ASSIGNED";
                //Debug.Log($"Trial {i + 1} prefab: {status}");
            }
        }
        
        if (fishPrefab == null)
        {
            Debug.LogError(" WARNING: No global fish prefab assigned! Fish will be replaced by chest!");
        }
    }
    
    void OnDestroy()
    {
        if (currentTrialFish != null)
        {
            Destroy(currentTrialFish);
        }
    }
}
