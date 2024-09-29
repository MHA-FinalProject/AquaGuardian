using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/**
 * Keeps track of the player's oxygen level.
 * See also: PlayerLife
 */
public class Health : MonoBehaviour
{
    // === UI Elements ===
    [Header("UI Components")]
    [SerializeField] private TextMeshProUGUI healthText;  // Reference to the TextMeshPro component displaying the health
    [SerializeField] private Image healthBar;  // Reference to the UI Image representing the health bar
    [SerializeField] private GameObject Panel;  // Reference to a UI panel

    // === Health Settings ===
    [Header("Health Settings")]
    private float health = 100f;  // Current health value
    private float maxHealth = 100f;  // Maximum health value
    private float lerpSpeed;  // Speed for interpolating the health bar fill amount
    private float factorLerpSpeed = 3f;  // Multiplier for the lerp speed

    // === Lifetime and Damage Settings ===
    [Header("Lifetime & Damage Settings")]
    private float lifeTime;  // Time to wait before applying automatic damage
    public TMP_InputField lifeTime_inputField;  // Input field for user-defined lifetime
    private float downHealthPairSec;  // Damage applied every life time interval
    public TMP_InputField downHealthPairSec_inputField;  // Input field for user-defined damage per interval

    // === Internal States ===
    [Header("Internal States")]
    private bool moveOxygen = false;  // Flag to start oxygen decrease coroutine, initially set to false
    public bool didntGetInputsYet = false;  // Flag to check if inputs have been received yet

    // Start is called before the first frame update
    void Start()
    {
        health = maxHealth;  // Initialize health to maximum value

        // Check if the panel is initially closed and start the coroutine if it is
        if (Panel != null && !Panel.activeSelf)
        {
            moveOxygen = true;
            StartCoroutine(DisappearHealthPoints());
        }
    }

    // Update is called once per frame
    void Update()
    {
        // Get user inputs for lifeTime and downHealthPairSec
        if (didntGetInputsYet)
        {
            ProcessUserInputs();
            didntGetInputsYet = false;  // Inputs have now been received
        }

        // Update health text with the current health percentage
        healthText.text = "Oxygen: " + health + "%";

        healthBarFiller();  // Update the health bar fill amount

        colorChanger();  // Update the health bar color based on the current health

        // Start the coroutine when the panel is closed
        if (Panel != null && !Panel.activeSelf && !moveOxygen)
        {
            moveOxygen = true;
            StartCoroutine(DisappearHealthPoints());
        }

        // Ensure health does not exceed the maximum value
        if (health > maxHealth)
        {
            health = maxHealth;
        }

        // Update the lerp speed based on the time delta
        lerpSpeed = factorLerpSpeed * Time.deltaTime;
    }

    void ProcessUserInputs()
    {
        // Parse user inputs for lifeTime and downHealthPairSec
        bool isLifeTimeValid = float.TryParse(lifeTime_inputField.text, out lifeTime);
        bool isDownHealthPairSecValid = float.TryParse(downHealthPairSec_inputField.text, out downHealthPairSec);
        if (isLifeTimeValid && isDownHealthPairSecValid)
        {
            lifeTime = float.Parse(lifeTime_inputField.text);  // Convert input to float
            downHealthPairSec = float.Parse(downHealthPairSec_inputField.text);  // Convert input to float
        }
        else
        {
            // Log errors if input parsing fails
            Debug.Log("error: " + lifeTime_inputField.text);
            Debug.Log("error: " + downHealthPairSec_inputField.text);
        }

        Debug.Log("lifeTime: " + lifeTime + ", downHealthPairSec: " + downHealthPairSec);
    }

    // Smoothly updates the health bar fill amount
    void healthBarFiller()
    {
        healthBar.fillAmount = Mathf.Lerp(healthBar.fillAmount, health / maxHealth, lerpSpeed);
    }

    // Changes the color of the health bar based on the current health percentage
    void colorChanger()
    {
        Color healthColor = Color.Lerp(Color.red, Color.green, (health / maxHealth));
        healthBar.color = healthColor;
    }

    // Applies damage to the player's health
    public void damage(float damagePoint)
    {
        if (health > damagePoint)
        {
            health -= damagePoint;  // Subtract the damage from health
        }
        else
        {
            health = 0;  // Set health to 0 if damage exceeds current health
            gameOver();  // Trigger game over
        }
    }

    // Heals the player's health
    public void heal(float healingPoint)
    {
        if (health < maxHealth)
        {
            health += healingPoint;  // Add the healing points to the current health
        }
    }

    // Coroutine that decreases health over time
    IEnumerator DisappearHealthPoints()
    {
        for (float i = health; i > 0; i--)
        {
            yield return new WaitForSeconds(lifeTime); // Wait for the specified lifetime

            damage(downHealthPairSec);  // Apply damage after each interval

            // Check if all health points have disappeared
            if (i <= 0)
            {
                gameOver();  // Trigger game over if health reaches 0
            }
        }
    }

    // Loads the game over scene when the player runs out of health
    void gameOver()
    {
        SceneManager.LoadScene("Game_Over");
    }
}
