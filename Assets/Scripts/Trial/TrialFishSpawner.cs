using UnityEngine;

/**
 * TrialFishSpawner - Manages trial fish creation and trigger setup
 * Handles fish instantiation and trigger configuration
 * Extracted from PanelOpenUp.cs for better code organization
 */
public class TrialFishSpawner : MonoBehaviour
{
    [Header("Fish Settings")]
    [SerializeField] private GameObject fishPrefab;
    [SerializeField] private GameObject[] trialFishPrefabs = new GameObject[5];
    [SerializeField] private GameObject chestPrefab;
    [SerializeField] private bool addGoToEndGameToFish = true;
    
    [Header("Component References")]
    [SerializeField] private PlayerMovement playerMovement;
    
    private GameObject currentTrialFish;
    
    public void SetChestPrefab(GameObject chest)
    {
        if (chest != null)
        {
            chestPrefab = chest;
        }
    }
    
    public GameObject CreateTrialFish(Vector3 position, int trialNumber)
    {
        if (currentTrialFish != null)
        {
            Destroy(currentTrialFish);
            currentTrialFish = null;
        }
        
        int trialIndex = Mathf.Clamp(trialNumber - 1, 0, 4);
        GameObject selectedPrefab = null;
        
        if (trialFishPrefabs != null && trialIndex < trialFishPrefabs.Length)
        {
            selectedPrefab = trialFishPrefabs[trialIndex];
        }
        
        if (selectedPrefab == null && fishPrefab != null)
        {
            selectedPrefab = fishPrefab;
        }
        
        if (selectedPrefab == null && chestPrefab != null)
        {
            selectedPrefab = chestPrefab;
        }

        GameObject fish;
        if (selectedPrefab != null)
        {
            fish = Instantiate(selectedPrefab, position, Quaternion.identity);
        }
        else
        {
            fish = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fish.transform.position = position;
            fish.transform.localScale = new Vector3(2f, 1.5f, 3f);
            
            var renderer = fish.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material.color = Color.cyan;
            }
        }
        
        fish.name = $"TrialFish_{trialNumber}";
        fish.tag = "TrialFish";
        fish.layer = LayerMask.NameToLayer("Default");
        fish.SetActive(true);
        
        currentTrialFish = fish;
        SetupFishTrigger(fish);
  
        return fish;
    }
    
    private void SetupFishTrigger(GameObject fish)
    {
        var existingCollider = fish.GetComponent<Collider>();
        if (existingCollider != null)
        {
            existingCollider.isTrigger = true;
            existingCollider.enabled = true;
            
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
        }
        
        var rb = fish.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = fish.AddComponent<Rigidbody>();
        }
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        
        EnsurePlayerTriggerSetup();
        
        if (addGoToEndGameToFish)
        {
            var goToEnd = fish.AddComponent<GoToEndGame>();
            var sceneNameField = typeof(GoToEndGame).GetField("sceneName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (sceneNameField != null)
            {
                sceneNameField.SetValue(goToEnd, "Win_Scene");
            }
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
  
    public void CleanupCurrentFish()
    {
        if (currentTrialFish != null)
        {
            Destroy(currentTrialFish);
            currentTrialFish = null;
        }
    }
    
    [ContextMenu("Verify Fish Position")]
    public void VerifyFishPosition()
    {
        var allFish = GameObject.FindGameObjectsWithTag("TrialFish");
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
        if (fishPrefab == null)
        {
            Debug.LogError("WARNING: No global fish prefab assigned! Fish will be replaced by chest!");
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
