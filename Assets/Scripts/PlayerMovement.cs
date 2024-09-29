using System.Collections;
using UnityEngine;
using TMPro;
using System;
using UnityEngine.SceneManagement;


public class PlayerMovement : MonoBehaviour
{
    
    // ----- Movement Settings -----
    [Header("Movement Settings")]
    [SerializeField] public float speed;  // Speed of the player
    public TMP_InputField speed_inputField;  // Input field for speed

    public float verticalSpeed;  // Speed for upward and downward movement
    public TMP_InputField vertical_speed_inputField;  // Input field for vertical speed

    public float idleUpwardSpeed;  // Speed for upward movement when no input is detected
    public TMP_InputField idle_upward_speed_inputField;  // Input field for idle upward speed

    private Rigidbody rb;  // Reference to the Rigidbody component
    public bool canMove = true;  // Flag to control if the player can move

    // ----- UI References -----
    [Header("UI References")]
    public GameObject Panel;  // Reference to the UI panel
    [SerializeField] public TextMeshProUGUI infoText6;  // Text for "Get Ready"
    [SerializeField] public TextMeshProUGUI infoText7;  // Text for "Go"

    // ----- Collision Control -----
    [Header("Collision Control")]
    private bool canCollide = true;  // Flag to control collision timing
    public float collisionDelay = 2f;  // Delay between collisions

    // ----- Game State -----
    [Header("Game State")]
    private bool show = true;  // Flag to control display of text
    public bool afterText = false;  // Flag to check if the intro text has been shown

    // ----- Scene References -----
    [Header("Scene References")]
    [SerializeField] string sceneName;  // Name of the scene
    [SerializeField] GameObject surface;  // Reference to the surface object
    [SerializeField] GameObject ground;  // Reference to the ground object

    // ----- Upward Movement Factor -----
    private float idleUpwardFactor = 0.5f;  // Factor for idle upward movement

    // ----- Amadeo Device Connection -----
    [Header("Amadeo Device Connection")]
    public bool notGetForcesFromAmadeo = true;  // Flag to check if Amadeo device is connected or using keyboard

    void Start()
    {

        rb = GetComponent<Rigidbody>();
        // Make sure to set the Rigidbody's collision detection mode to Continuous for accurate collision handling
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        if (infoText6 != null && infoText7 != null)
        {
            infoText6.gameObject.SetActive(false); // Hide the text initially
            infoText7.gameObject.SetActive(false); // Hide the text initially
        }

        
    }

    void Update()
    {
        // Check if the panel is active and set canMove accordingly
        if (Panel != null)
        {
            canMove = !Panel.activeSelf;
        }

        /*if (!blue.gameObject.activeInHierarchy && !green.gameObject.activeInHierarchy && !red.gameObject.activeInHierarchy && !purple.gameObject.activeInHierarchy)
        {
            SceneManager.LoadScene(sceneName);
        }*/

        if (show && !Panel.activeSelf)
        {
            ProcessUserInputs();
            // Show the info text for 4 seconds
            StartCoroutine(ShowInfoTextAndKeys());

            show = false;
        }

        if (notGetForcesFromAmadeo && canMove && afterText)
        {
            Vector3 movementDirection = Vector3.forward; // Move along the z-axis (forward direction)
            Vector3 targetVelocity = speed * transform.TransformDirection(movementDirection);
            float upDownInput = Input.GetAxis("UpDown");
            float verticalMovementSpeed = upDownInput * verticalSpeed;

            // Apply idle upward speed if no input is given
            if (upDownInput <= 0)
            {
                verticalMovementSpeed += idleUpwardSpeed;
            }

            // Add vertical movement to the target velocity
            targetVelocity += verticalMovementSpeed * transform.TransformDirection(Vector3.up);

            // Apply target velocity to the Rigidbody
            rb.velocity = targetVelocity;


        }
    }

    void ProcessUserInputs()
    {
        // make speed. vertical speed and idle upward speed from user
        bool isSpeedValid = float.TryParse(speed_inputField.text, out speed);
        bool isSpeedVerticalValid = float.TryParse(vertical_speed_inputField.text, out verticalSpeed);
        bool isIdleUpwardSpeedValid = float.TryParse(idle_upward_speed_inputField.text, out idleUpwardSpeed);
        if (isSpeedValid && isSpeedVerticalValid && isIdleUpwardSpeedValid)
        {
            speed = float.Parse(speed_inputField.text);
            verticalSpeed = float.Parse(vertical_speed_inputField.text);
            idleUpwardSpeed = float.Parse(idle_upward_speed_inputField.text);
        }
        else
        {
            Debug.Log("error: " + speed_inputField.text);
            Debug.Log(vertical_speed_inputField.text);
            Debug.Log(idle_upward_speed_inputField.text);
        }
        Debug.Log("speed: " + speed + ", vertical speed: " + verticalSpeed + ", idleUpwardSpeed: " + idleUpwardSpeed);
    }

    /*void FixedUpdate()
    {

        if (canMove && afterText)
        {
            Vector3 movementDirection = Vector3.forward; // Move along the z-axis (forward direction)
            Vector3 targetVelocity = speed * transform.TransformDirection(movementDirection);
            float upDownInput = Input.GetAxis("UpDown");
            float verticalMovementSpeed = upDownInput * verticalSpeed;

            // Apply idle upward speed if no input is given
            if (upDownInput <= 0)
            {
                verticalMovementSpeed += idleUpwardSpeed;
            }

            // Add vertical movement to the target velocity
            targetVelocity += verticalMovementSpeed * transform.TransformDirection(Vector3.up);

            // Apply target velocity to the Rigidbody
            rb.velocity = targetVelocity;


        }
    }*/


    void OnCollisionStay(Collision collision)
    {
        // Check if the player collides with a wall
        if (collision.gameObject.CompareTag("Wall") || collision.gameObject.CompareTag("Cave"))
        {
            // Move the player upward
            rb.velocity = new Vector3(rb.velocity.x, verticalSpeed*idleUpwardFactor, rb.velocity.z);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall") || collision.gameObject.CompareTag("Ground") || collision.gameObject.CompareTag("Cave"))
        {

            Debug.Log("collision " + gameObject.name + " " +collision.gameObject.name);
            
        }
    }



    // Method to toggle player movement on or off
    public void ToggleMovement(bool move)
    {
        canMove = move;
    }

    private IEnumerator ShowInfoTextAndKeys()
    {

        /*if (key1 != null && key2 != null && key3 != null)
        {
            key1.gameObject.SetActive(true);
            key2.gameObject.SetActive(true);
            key3.gameObject.SetActive(true);
        }*/

        if (infoText6 != null && infoText7 != null)
        {
            
            infoText6.gameObject.SetActive(true); // Show the text
            yield return WaitForSecondsOrSkip(1f); // Wait for 1 second or skip if Enter is pressed
            infoText6.gameObject.SetActive(false); // Hide the text after 1 second

            yield return WaitForSecondsOrSkip(1f); // Wait for 1 second or skip if Enter is pressed

            infoText7.gameObject.SetActive(true); // Show the text
            yield return WaitForSecondsOrSkip(1f); // Wait for 1 second or skip if Enter is pressed
            infoText7.gameObject.SetActive(false); // Hide the text after 1 second
            afterText = true;
        }

    }

    IEnumerator WaitForSecondsOrSkip(float seconds)
    {
        float elapsedTime = 0f;
        while (elapsedTime < seconds)
        {
            elapsedTime += Time.deltaTime;
            if (Input.GetKeyDown(KeyCode.Return))
            {
                yield break; // Skip if Enter key is pressed
            }
            yield return null;
        }
    }
}
