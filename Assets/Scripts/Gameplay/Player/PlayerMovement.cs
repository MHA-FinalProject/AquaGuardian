using UnityEngine;
using TMPro;

/**
 * Manages player movement (horizontal/vertical) and handles keyboard/Amadeo device input
 * Tracks input source for regression analysis. Supports unified movement system for both input modes
 * See also: getEventFromAmadeoClientDiver, GameStateManager, GameDataSO
 */
public class PlayerMovement : MonoBehaviour
{
   
    [Header("Movement Settings")]
    public float speed;  // Speed of the player
    public TMP_InputField speed_inputField;  // Input field for speed
    public float verticalSpeed;  // Speed for upward and downward movement
    public TMP_InputField vertical_speed_inputField;  // Input field for vertical speed
    public float idleUpwardSpeed;  // Speed for upward movement when no input is detected
    public TMP_InputField idle_upward_speed_inputField;  // Input field for idle upward speed

    private Rigidbody rb;  // Reference to the Rigidbody component

    // ----- UI References -----
    [Header("UI References")]
    public GameObject Panel;

    // ----- Game Data -----
    [Header("Game Data (Optional - leave empty to use singleton)")]
    [SerializeField] private GameDataSO gameDataOverride;

    // ----- Game State -----
    [Header("Game State")]
    public bool canMove = true;  // Flag to control if the player can move
    public bool afterText = false;  // Flag to check if the intro text has been shown
    private bool _debugLoggedOnce = false;  // Flag to log state once after intro

    // Helper method to get GameDataSO
    private GameDataSO GetGameData()
    {
        return gameDataOverride != null ? gameDataOverride : GameDataSO.Instance;
    }

    private float GetIdleUpwardFactor()
    {
        GameDataSO gameData = GetGameData();
        return gameData != null ? gameData.idleUpwardFactor : 0.5f;
    }

    public float GetCollisionDelay()
    {
        GameDataSO gameData = GetGameData();
        return gameData != null ? gameData.playerCollisionDelay : 2f;
    }

    // ----- Scene References -----
    [Header("Scene References")]
    [SerializeField] string sceneName;  // Name of the scene
    [SerializeField] GameObject surface;  // Reference to the surface object
    [SerializeField] GameObject ground;  // Reference to the ground object

    // ----- Amadeo Device Connection -----
    [Header("Amadeo Device Connection")]
    public bool notGetForcesFromAmadeo = true;  // Flag to check if Amadeo device is connected or using keyboard

    // Track actual input source during trial (for regression analysis)
    public int amadeoInputCount = 0;  // Count of frames with Amadeo input
    public int keyboardInputCount = 0;  // Count of frames with keyboard input
    public bool ActuallyUsedAmadeoInput => amadeoInputCount > keyboardInputCount;  // True if Amadeo was used more than keyboard

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        // Make sure to set the Rigidbody's collision detection mode to Continuous for accurate collision handling
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Ensure the player has the correct tag for cave trigger detection
        if (!gameObject.CompareTag("Player"))
        {
            gameObject.tag = "Player";
        }
    }

    void Update()
    {
        // Use GameStateManager for reliable state checking
        // Only allow movement after panel is closed AND intro text is complete
        bool isPanelClosed = GameStateManager.Instance != null
            ? GameStateManager.Instance.IsPanelClosed
            : (Panel == null || !Panel.activeSelf);

        canMove = isPanelClosed && afterText;

        // Debug log once after intro completes to see state
        if (afterText && !_debugLoggedOnce)
        {
            _debugLoggedOnce = true;
         //   Debug.Log($"[PlayerMovement] After intro: afterText={afterText}, IsPanelClosed={isPanelClosed}, canMove={canMove}");
            if (!canMove)
            {
                Debug.LogWarning("[PlayerMovement] WARNING: CANNOT MOVE! Panel might not be properly closed.");
            }
        }

        // Movement is allowed only after intro text completed AND panel closed (GameStateManager)
        if (notGetForcesFromAmadeo && canMove)
        {
            HandleMovement();
        }
    }

    // Unified movement handler that works for both keyboard and Amadeo device input
    // For keyboard: applies idle upward speed when moving down or not moving up
    // For Amadeo: only applies idle speed when within tolerance (no input), otherwise uses exact sign direction
    public void ApplyMovement(float verticalInput, float verticalTolerance = 0.1f, bool isAmadeoInput = false)
    {
        // Track input source for regression analysis
        if (isAmadeoInput)
        {
            amadeoInputCount++;
        }
        else
        {
            keyboardInputCount++;
        }

        Vector3 horizontalVelocity = speed * transform.TransformDirection(Vector3.forward); // move along the z-axis (forward direction)

        // Calculate vertical movement speed based on input
        float verticalMovementSpeed;

        if (Mathf.Abs(verticalInput) < verticalTolerance)
        {
            // Within tolerance: apply idle upward speed
            verticalMovementSpeed = idleUpwardSpeed;
        }
        else
        {
            // Outside tolerance: apply movement based on input
            if (isAmadeoInput)
            {
                // Amadeo: use exact sign direction (sign = -1, 0, or +1)
                // This matches original behavior: Mathf.Sign(...) * verticalSpeed
                verticalMovementSpeed = Mathf.Sign(verticalInput) * verticalSpeed;
            }
            else
            {
                // Keyboard: multiply input by speed and add idle speed when moving down
                verticalMovementSpeed = verticalInput * verticalSpeed;
                if (verticalInput <= 0)
                {
                    verticalMovementSpeed += idleUpwardSpeed;
                }
            }
        }

        Vector3 verticalVelocity = verticalMovementSpeed * transform.TransformDirection(Vector3.up);
        // Apply target velocity to the Rigidbody
        rb.velocity = horizontalVelocity + verticalVelocity;
    }

    // Reset input tracking counters (call at start of each trial)
    public void ResetInputTracking()
    {
        amadeoInputCount = 0;
        keyboardInputCount = 0;
    }

    private void HandleMovement()
    {
        // Get keyboard input for vertical movement
        float upDownInput = Input.GetAxis("UpDown");   // Change in Edit -> Project Settings -> Input Manager -> Axes - UpDown

        // Use unified movement function
        ApplyMovement(upDownInput);
    }

    void OnCollisionStay(Collision collision)
    {
        // Check if the player collides with a wall
        if (collision.gameObject.CompareTag("Wall") || collision.gameObject.CompareTag("Cave"))
        {
            // Move the player upward
            rb.velocity = new Vector3(rb.velocity.x, verticalSpeed * GetIdleUpwardFactor(), rb.velocity.z);
        }
    }

    // when the player collides with a wall, ground or cave, the player is moved to the surface
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall") || collision.gameObject.CompareTag("Ground") || collision.gameObject.CompareTag("Cave"))
        {
           // Debug.Log("collision " + gameObject.name + " " + collision.gameObject.name);
        }
    }

    // Handle trigger events (for trial fish detection)
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("TrialFish"))
        {
            // Find PanelOpenUp and notify it
            var panelOpenUp = FindObjectOfType<PanelOpenUp>();
            if (panelOpenUp != null)
            {
                var health = FindObjectOfType<Health>();
                float finalOxygen = health != null ? health.GetOxygen() : 0f;
                bool completed = true; // If reached fish, it's completed

                panelOpenUp.OnTrialFishReached(finalOxygen, completed);
            }
        }
    }

    // Method to toggle player movement on or off
    public void ToggleMovement(bool move)
    {
        canMove = move;
    }
}
