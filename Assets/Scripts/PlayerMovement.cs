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

    [Header("Collision Control")]
    private bool canCollide = true;  // Flag to control collision timing
    public float collisionDelay = 2f;  // Delay between collisions

    [Header("Game State")]
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
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    void Update()
    {
        if (notGetForcesFromAmadeo && canMove && afterText)
        {
            Vector3 horizontalVelocity = speed * transform.TransformDirection(Vector3.forward);

            float upDownInput = Input.GetAxis("UpDown");
            float verticalMovementSpeed = upDownInput * verticalSpeed;
            if (upDownInput <= 0)
            {
                verticalMovementSpeed += idleUpwardSpeed;
            }

            Vector3 verticalVelocity = verticalMovementSpeed * transform.TransformDirection(Vector3.up);
            rb.velocity = horizontalVelocity + verticalVelocity;
        }
    }

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

}
