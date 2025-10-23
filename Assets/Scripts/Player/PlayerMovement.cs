using UnityEngine;
using TMPro;

// 3. Split ProcessUserInputsInInitialForm into smaller methods
// 4. Consider using events for state changes


public class PlayerMovement : MonoBehaviour
{

    // ----- Movement Settings -----
    [Header("Movement Settings")]
    public float speed;  // Speed of the player
    public TMP_InputField speed_inputField;  // Input field for speed
    public float verticalSpeed;  // Speed for upward and downward movement
    public TMP_InputField vertical_speed_inputField;  // Input field for vertical speed
    public float idleUpwardSpeed;  // Speed for upward movement when no input is detected
    public TMP_InputField idle_upward_speed_inputField;  // Input field for idle upward speed
    private float idleUpwardFactor = 0.5f;  // Factor for idle upward movement

    private Rigidbody rb;  // Reference to the Rigidbody component


    // ----- UI References -----
    [Header("UI References")]
    public GameObject Panel;  // Reference to the UI panel


    // ----- Game State -----
    [Header("Game State")]
    public bool canMove = true;  // Flag to control if the player can move
    public float collisionDelay = 2f;  // Delay between collisions
    public bool afterText = false;  // Flag to check if the intro text has been shown
    private bool _debugLoggedOnce = false;  // Flag to log state once after intro


    // ----- Scene References -----
    [Header("Scene References")]
    [SerializeField] string sceneName;  // Name of the scene
    [SerializeField] GameObject surface;  // Reference to the surface object
    [SerializeField] GameObject ground;  // Reference to the ground object


    // ----- Amadeo Device Connection -----
    [Header("Amadeo Device Connection")]
    public bool notGetForcesFromAmadeo = true;  // Flag to check if Amadeo device is connected or using keyboard


    private GameObject caveTracker;


    void Start()
    {
        rb = GetComponent<Rigidbody>();
        // Make sure to set the Rigidbody's collision detection mode to Continuous for accurate collision handling
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Ensure the player has the correct tag for cave trigger detection
        if (gameObject.tag != "Player")
        {
            Debug.LogWarning("PlayerMovement: Setting gameObject tag to 'Player' for trigger detection");
            gameObject.tag = "Player";
        }



        // Find cave tracker without direct type reference
        var behaviours = FindObjectsOfType<MonoBehaviour>();
        foreach (var mb in behaviours)
        {
            if (mb != null && mb.GetType().Name == "CaveTracker")
            {
                caveTracker = mb.gameObject;
                break;
            }
        }
    }

    void Update()
    {
        // Use GameStateManager for reliable state checking
        // Only allow movement after panel is closed AND intro text is complete
        if (GameStateManager.Instance != null)
        {
            canMove = GameStateManager.Instance.IsPanelClosed && afterText;
        }
        else
        {
            // Fallback: Check if the panel is active and afterText is complete
            if (Panel != null)
            {
                canMove = !Panel.activeSelf && afterText;
            }
        }

        // Debug log once after intro completes to see state
        if (afterText && !_debugLoggedOnce)
        {
            _debugLoggedOnce = true;
            bool isPanelClosed = GameStateManager.Instance != null ? GameStateManager.Instance.IsPanelClosed : !Panel.activeSelf;
            Debug.Log($"[PlayerMovement] After intro: afterText={afterText}, IsPanelClosed={isPanelClosed}, canMove={canMove}");
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

    private void HandleMovement()
    {
        // TODO: Merge both functions to a single, easy-to-read function.
        Vector3 horizontalVelocity = speed * transform.TransformDirection(Vector3.forward); // move along the z-axis (forward direction)

        float upDownInput = Input.GetAxis("UpDown");   // Change in Edit -> Project Settings -> Input Manager -> Axes - UpDown
        // TODO: move to the new input system - use InputAction.

        float verticalMovementSpeed = upDownInput * verticalSpeed;
        // Apply idle upward speed if no input is given
        if (upDownInput <= 0)
        {
            verticalMovementSpeed += idleUpwardSpeed;
        }

        Vector3 verticalVelocity = verticalMovementSpeed * transform.TransformDirection(Vector3.up);
        // Apply target velocity to the Rigidbody
        rb.velocity = horizontalVelocity + verticalVelocity;
    }



    void OnCollisionStay(Collision collision)
    {
        // Check if the player collides with a wall
        if (collision.gameObject.CompareTag("Wall") || collision.gameObject.CompareTag("Cave"))
        {
            // Move the player upward
            rb.velocity = new Vector3(rb.velocity.x, verticalSpeed * idleUpwardFactor, rb.velocity.z);
        }
    }

    // when the player collides with a wall, ground or cave, the player is moved to the surface
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall") || collision.gameObject.CompareTag("Ground") || collision.gameObject.CompareTag("Cave"))
        {
            Debug.Log("collision " + gameObject.name + " " + collision.gameObject.name);

            // Record all collisions (cave, wall, ground) in CaveTracker
            if (caveTracker != null)
            {
                caveTracker.SendMessage("RegisterCollision", SendMessageOptions.DontRequireReceiver);
            }
        }
    }

    // Handle trigger events (for trial fish detection)
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("TrialFish"))
        {
            // Debug.Log("Player reached trial fish!");

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



    #region Public API

    /// <summary>
    /// Trigger performance summary
    /// </summary>
    public void LogPerformanceSummary()
    {
        if (caveTracker != null)
        {
            caveTracker.SendMessage("PrintResults", SendMessageOptions.DontRequireReceiver);
        }
    }

    #endregion
}
