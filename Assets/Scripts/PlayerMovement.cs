using System.Collections;
using UnityEngine;
using TMPro;
using System;
using UnityEngine.SceneManagement;

public class PlayerMovement : MonoBehaviour
{
    public float speed;
    public float rotationSpeed;
    public float verticalSpeed; // Adjust this for the speed of upward and downward movement
    public float idleUpwardSpeed; // Adjust this for the speed of upward movement when no input is detected

    private Rigidbody rb;
    public GameObject Panel;
    private bool canMove = true; // Set to true by default

    [SerializeField] public TextMeshProUGUI infoText1; // Reference to the text object
    [SerializeField] public TextMeshProUGUI infoText2; // Reference to the text object
    [SerializeField] public TextMeshProUGUI infoText3; // Reference to the text object
    [SerializeField] public TextMeshProUGUI infoText4; // Reference to the text object
    [SerializeField] public TextMeshProUGUI infoText5; // Reference to the text object
    [SerializeField] public TextMeshProUGUI infoText6; // Reference to the text object
    [SerializeField] public TextMeshProUGUI infoText7; // Reference to the text object

    /*[SerializeField] public GameObject key1; // Reference to the key object
    [SerializeField] public GameObject key2; // Reference to the key object
    [SerializeField] public GameObject key3; // Reference to the key object*/

    private bool show = true;
    private bool afterText = false;


    [SerializeField] GameObject blue;
    [SerializeField] GameObject green;
    [SerializeField] GameObject red;
    [SerializeField] GameObject purple;

    private int counterFish = 0;

    private bool canCollide = true;
    public float collisionDelay = 2f;

    [SerializeField] string sceneName;

    [SerializeField] GameObject healthBarObject;
    private HealthBar healthBar; // Reference to the HealthBar component

    public AudioClip collisionSound; // Assign this in the inspector
    private AudioSource audioSource;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        // Make sure to set the Rigidbody's collision detection mode to Continuous for accurate collision handling
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        if (infoText1 != null && infoText2 != null && infoText3 != null && infoText4 != null && infoText5 != null && infoText6 != null && infoText7 != null)
        {
            infoText1.gameObject.SetActive(false); // Hide the text initially
            infoText2.gameObject.SetActive(false); // Hide the text initially
            infoText3.gameObject.SetActive(false); // Hide the text initially
            infoText4.gameObject.SetActive(false); // Hide the text initially
            infoText5.gameObject.SetActive(false); // Hide the text initially
            infoText6.gameObject.SetActive(false); // Hide the text initially
            infoText7.gameObject.SetActive(false); // Hide the text initially
        }

        /*if (key1 != null && key2 != null && key3 != null)
        {
            key1.gameObject.SetActive(false);
            key2.gameObject.SetActive(false);
            key3.gameObject.SetActive(false);
        }*/

        // Get the HealthBar component
        if (healthBarObject != null)
        {
            healthBar = healthBarObject.GetComponent<HealthBar>();
        }

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.clip = collisionSound;
    }

    void Update()
    {
        // Check if the panel is active and set canMove accordingly
        if (Panel != null)
        {
            canMove = !Panel.activeSelf;
        }

        if (!blue.gameObject.activeInHierarchy && !green.gameObject.activeInHierarchy && !red.gameObject.activeInHierarchy && !purple.gameObject.activeInHierarchy)
        {
            SceneManager.LoadScene(sceneName);
        }

        if (show && !Panel.activeSelf)
        {
            // Show the info text for 4 seconds
            StartCoroutine(ShowInfoTextAndKeys());

            show = false;

        }

        if (canMove && afterText)
        {
            // Always move the player forward
            Vector3 movementDirection = Vector3.forward; // Move along the z-axis (forward direction)
            movementDirection.Normalize();

            // Apply movement
            Vector3 move = transform.TransformDirection(movementDirection) * speed * Time.deltaTime;
            rb.MovePosition(rb.position + move);

            // Apply vertical movement directly
            float upDownInput = Input.GetAxis("UpDown");
            float verticalMovement = upDownInput * verticalSpeed * Time.deltaTime;

            // If no down input, apply idle upward movement
            if (upDownInput <= 0)
            {
                verticalMovement += idleUpwardSpeed * Time.deltaTime;
            }

            rb.MovePosition(rb.position + Vector3.up * verticalMovement);

            // Rotate towards forward direction
            if (movementDirection != Vector3.zero)
            {
                Quaternion toRotation = Quaternion.LookRotation(movementDirection, Vector3.up);
                rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, toRotation, rotationSpeed * Time.deltaTime));
            }
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

        if (infoText1 != null && infoText2 != null && infoText3 != null && infoText4 != null && infoText5 != null && infoText6 != null && infoText7 != null)
        {
            /*infoText1.gameObject.SetActive(true); // Show the text
            yield return WaitForSecondsOrSkip(3f); // Wait for 3 seconds or skip if Enter is pressed
            infoText1.gameObject.SetActive(false); // Hide the text after 3 seconds

            yield return WaitForSecondsOrSkip(1f); // Wait for 1 second or skip if Enter is pressed*/

            /*infoText2.gameObject.SetActive(true); // Show the text
            yield return WaitForSecondsOrSkip(4f); // Wait for 4 seconds or skip if Enter is pressed
            infoText2.gameObject.SetActive(false); // Hide the text after 4 seconds

            yield return WaitForSecondsOrSkip(1f); // Wait for 1 second or skip if Enter is pressed*/

            /*infoText3.gameObject.SetActive(true); // Show the text
            yield return WaitForSecondsOrSkip(4f); // Wait for 4 seconds or skip if Enter is pressed
            infoText3.gameObject.SetActive(false); // Hide the text after 4 seconds

            yield return WaitForSecondsOrSkip(1f); // Wait for 1 second or skip if Enter is pressed

            infoText4.gameObject.SetActive(true); // Show the text
            yield return WaitForSecondsOrSkip(4f); // Wait for 4 seconds or skip if Enter is pressed
            infoText4.gameObject.SetActive(false); // Hide the text after 4 seconds

            yield return WaitForSecondsOrSkip(1f); // Wait for 1 second or skip if Enter is pressed

            infoText5.gameObject.SetActive(true); // Show the text
            yield return WaitForSecondsOrSkip(4f); // Wait for 4 seconds or skip if Enter is pressed
            infoText5.gameObject.SetActive(false); // Hide the text after 4 seconds

            yield return WaitForSecondsOrSkip(1f); // Wait for 1 second or skip if Enter is pressed*/

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

    void OnTriggerEnter(Collider other)
    {
        // Check if the player has collided with the oxygen object
        if (other.CompareTag("OxygenObject"))
        {
            Debug.Log("Collide with oxygen");

            // Play collision sound
            PlayCollisionSound();

            // Disable the oxygen object
            other.gameObject.SetActive(false);

            // Add 2 health points
            if (healthBar != null)
            {
                healthBar.AddHealthPoints(2);
            }
        }
    }

    void PlayCollisionSound()
    {
        if (audioSource != null && collisionSound != null)
        {
            audioSource.Play();
        }
    }
}
