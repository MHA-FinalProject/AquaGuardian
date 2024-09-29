using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;
using UnityEngine.UI;

/**
 * Keeps track of the player's oxygen level.
 * See also: Health
 */
public class PlayerLife : MonoBehaviour
{
    //    /*public int maxCollisions = 3; // Maximum number of collisions allowed
    //    private int currentCollisions = 0; // Current number of collisions*/
    //    private bool canCollide = true; // Flag to control collision timing
    //    private float waitTime = 2f;

    //    private bool PlayerisCollide = false;
    // /*   float PlayerPositionX;
    //    float PlayerPositionY;

    //    [SerializeField] public GameObject TopOfCave = null;
    //    [SerializeField] public GameObject BottomOfCave = null;*/

    //   /* float pivotToCaveCenter = 8f;*/

    //    /*[SerializeField] GameObject fish1;
    //    [SerializeField] GameObject fish2;
    //    [SerializeField] GameObject fish3;
    //    [SerializeField] GameObject fish4;
    //*/
    //    public AudioClip collisionSound; // Assign this in the inspector
    //    private AudioSource audioSource;

    //    [SerializeField] GameObject healthBarObject2;
    //    private Health healthBar2; // Reference to the HealthBar component

    //    private float removeHealthWithCollide;
    //    public TMP_InputField removeHealthWithCollide_inputField;

    //    private float timeBetweenCollides;
    //    public TMP_InputField timeBetweenCollides_inputField;

    //    private float healHealthPoint;
    //    public TMP_InputField healHealthPoints_inputField;

    //    public bool didntGetInputsYet = false;

    //    /*float distance = 0;*/

    //    public AudioClip collisionSoundOxygen; // Assign this in the inspector
    //    private AudioSource audioSourceOxygen;

    //    // Blood splatter image
    //    [SerializeField] private Image bloodSplatterImage;

    //    private float colorAlphaValue = 0.5f;

    //    private float timeUntilFadeOut = 3f;

    // ----- Collision Control -----
    private bool canCollide = true;  // Flag to control collision timing
    private float waitTime = 2f;  // Time to wait between collisions
    private bool PlayerisCollide = false;  // Flag to indicate if player is colliding

    // ----- Health Management -----
    [Header("Health Management")]
    [SerializeField] GameObject healthBarObject2;  // Reference to the health bar object
    private Health healthBar2;  // Reference to the HealthBar component
    private float removeHealthWithCollide;  // Health to be removed on collision
    public TMP_InputField removeHealthWithCollide_inputField;  // Input field for health removal
    private float timeBetweenCollides;  // Time to wait between collisions
    public TMP_InputField timeBetweenCollides_inputField;  // Input field for time between collisions
    private float healHealthPoint;  // Health to be healed
    public TMP_InputField healHealthPoints_inputField;  // Input field for healing health
    public bool didntGetInputsYet = false;  // Flag to indicate if inputs haven't been received yet

    // ----- Audio -----
    [Header("Audio")]
    public AudioClip collisionSound;  // Audio clip for collision sound, assign in the inspector
    private AudioSource audioSource;  // Audio source for collision sounds
    public AudioClip collisionSoundOxygen;  // Audio clip for oxygen collision sound, assign in the inspector
    private AudioSource audioSourceOxygen;  // Audio source for oxygen sounds

    // ----- Visual Effects -----
    [Header("Visual Effects")]
    [SerializeField] private Image bloodSplatterImage;  // Reference to the blood splatter image

    // ----- Fade Control -----
    private float colorAlphaValue = 0.5f;  // Alpha value for visual effects
    private float timeUntilFadeOut = 3f;  // Time until fade out occurs

    void Start()
    {
       /* PlayerPositionX = gameObject.transform.position.x;
        PlayerPositionY = (TopOfCave.transform.position.y + BottomOfCave.transform.position.y) / 2;*/

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.clip = collisionSound;

        // Get the HealthBar component
        if (healthBarObject2 != null)
        {
            healthBar2 = healthBarObject2.GetComponent<Health>();
        }

        audioSourceOxygen = gameObject.AddComponent<AudioSource>();
        audioSourceOxygen.clip = collisionSoundOxygen;

       /* distance = gameObject.transform.position.z - fish1.transform.position.z;*/

        // Ensure the blood splatter image is initially invisible
        if (bloodSplatterImage != null)
        {
            Color color = bloodSplatterImage.color;
            color.a = 0f; // Fully transparent
            bloodSplatterImage.color = color;

            // Disable Raycast Target to prevent blocking other UI elements
            bloodSplatterImage.raycastTarget = false;
        }
    }

    private void Update()
    {
        if (didntGetInputsYet)
        {
            ProcessUserInputs();
            didntGetInputsYet = false;
        }

        /*float currentDistance = gameObject.transform.position.z - fish1.transform.position.z;
        if (distance != currentDistance)
        {
            Vector3 newPosition1 = fish1.transform.position;
            Vector3 newPosition2 = fish2.transform.position;
            Vector3 newPosition3 = fish3.transform.position;
            Vector3 newPosition4 = fish4.transform.position;

            newPosition1.z = gameObject.transform.position.z - distance;
            newPosition2.z = gameObject.transform.position.z - distance;
            newPosition3.z = gameObject.transform.position.z - distance;
            newPosition4.z = gameObject.transform.position.z - distance;

            fish1.transform.position = newPosition1;
            fish2.transform.position = newPosition2;
            fish3.transform.position = newPosition3;
            fish4.transform.position = newPosition4;
        }*/
    }

    void ProcessUserInputs()
    {
        // Get user input values
        bool isRemoveHealthWithCollideValid = float.TryParse(removeHealthWithCollide_inputField.text, out removeHealthWithCollide);
        bool isTimeBetweenCollidesValid = float.TryParse(timeBetweenCollides_inputField.text, out timeBetweenCollides);
        bool isHealHealthPointValid = float.TryParse(healHealthPoints_inputField.text, out healHealthPoint);

        if (isRemoveHealthWithCollideValid && isTimeBetweenCollidesValid && isHealHealthPointValid)
        {
            removeHealthWithCollide = float.Parse(removeHealthWithCollide_inputField.text);
            timeBetweenCollides = float.Parse(timeBetweenCollides_inputField.text);
            healHealthPoint = float.Parse(healHealthPoints_inputField.text);
        }
        else
        {
            Debug.Log("error: " + removeHealthWithCollide_inputField.text);
            Debug.Log("error: " + timeBetweenCollides_inputField.text);
            Debug.Log("error: " + healHealthPoints_inputField.text);
        }

        Debug.Log("removeHealthWithCollide: " + removeHealthWithCollide + ", timeBetweenCollides: " + timeBetweenCollides + ", healHealthPoint: " + healHealthPoint);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (canCollide && collision.collider.CompareTag("Cave"))
        {
            PlayerisCollide = true;
            HandleCollision();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // Check if the player has collided with the oxygen object
        if (other.CompareTag("OxygenObject"))
        {
            Debug.Log("Collide with oxygen");

            // Play collision sound
            PlayCollisionSoundOxygen();

            // Disable the oxygen object
            other.gameObject.SetActive(false);

            // Add health points
            if (healthBar2 != null)
            {
                healthBar2.heal(healHealthPoint);
            }
        }
    }

    void PlayCollisionSoundOxygen()
    {
        if (audioSourceOxygen != null && collisionSoundOxygen != null)
        {
            audioSourceOxygen.Play();
        }
    }

    private void HandleCollision()
    {
        /*currentCollisions++; // Increment collision count*/
        Debug.Log("Collision detected");

        // Play collision sound
        PlayCollisionSound();

        StartCoroutine(ShowBloodSplatter()); // Show the blood splatter when damage is taken

        // Remove health points
        if (healthBar2 != null && canCollide)
        {
            healthBar2.damage(removeHealthWithCollide);
            StartCoroutine(Wait(timeBetweenCollides));
        }
    }

    IEnumerator ShowBloodSplatter()
    {
        // Make the blood splatter visible
        if (bloodSplatterImage != null)
        {
            Color color = bloodSplatterImage.color;
            color.a = colorAlphaValue; // visible amount
            bloodSplatterImage.color = color;

            // Fade out over 1 second
            for (float t = 0; t < timeUntilFadeOut; t += Time.deltaTime)
            {
                color.a = Mathf.Lerp(colorAlphaValue, 0f, t / timeUntilFadeOut); // Fade out
                bloodSplatterImage.color = color;
                yield return null;
            }

            // Ensure it's fully invisible at the end
            color.a = 0f;
            bloodSplatterImage.color = color;
        }
    }

    IEnumerator Wait(float number)
    {
        canCollide = false; // Disable collision temporarily
        yield return new WaitForSeconds(number); // Wait for the specified time
        canCollide = true; // Enable collision after delay
    }

    void GameOver()
    {
        // Load the Game Over scene
        SceneManager.LoadScene("Game_Over");
    }

    void PlayCollisionSound()
    {
        if (audioSource != null && collisionSound != null)
        {
            audioSource.Play();
        }
    }
}
