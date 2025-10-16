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

    [Header("UI Components")]
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private Image healthBar;
    [SerializeField] private GameObject Panel;

    [Header("Health Settings")]
    private float health = 100f;
    private float maxHealth = 100f;
    private float lerpSpeed;
    private float factorLerpSpeed = 3f;

    [Header("Lifetime & Damage Settings")]
    private float lifeTime;
    public TMP_InputField lifeTime_inputField;
    private float downHealthPairSec;
    public TMP_InputField downHealthPairSec_inputField;

    [Header("Internal States")]
    private bool moveOxygen = false;
    public bool didntGetInputsYet = false;

    void Start()
    {
        health = maxHealth;

        if (Panel != null && !Panel.activeSelf)
        {
            moveOxygen = true;
            StartCoroutine(DisappearHealthPoints());
        }
    }

    void Update()
    {
        if (didntGetInputsYet)
        {
            ProcessUserInputs();
            didntGetInputsYet = false;
        }

        healthText.text = "Oxygen: " + health + "%";

        healthBarFiller();
        colorChanger();

        bool panelClosed = Panel != null ? !Panel.activeSelf : true;
        if (GameStateManager.Instance != null)
        {
            panelClosed = GameStateManager.Instance.IsPanelClosed;
        }
        if (panelClosed && !moveOxygen)
        {
            moveOxygen = true;
            StartCoroutine(DisappearHealthPoints());
        }

        if (health > maxHealth)
        {
            health = maxHealth;
        }

        lerpSpeed = factorLerpSpeed * Time.deltaTime;
    }

    public void ProcessUserInputs()
    {
        bool isLifeTimeValid = float.TryParse(lifeTime_inputField.text, out lifeTime);
        bool isDownHealthPairSecValid = float.TryParse(downHealthPairSec_inputField.text, out downHealthPairSec);
        if (isLifeTimeValid && isDownHealthPairSecValid)
        {
            lifeTime = float.Parse(lifeTime_inputField.text);
            downHealthPairSec = float.Parse(downHealthPairSec_inputField.text);
        }
        else
        {
            Debug.LogError($"Input parsing failed: lifeTime={lifeTime_inputField.text}, downHealthPairSec={downHealthPairSec_inputField.text}");
        }

        StopAllCoroutines();
        moveOxygen = false;
    }

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

    public void damage(float damagePoint)
    {
        if (health > damagePoint)
        {
            health -= damagePoint;
        }
        else
        {
            health = 0;
            gameOver();
        }
    }

    public void heal(float healingPoint)
    {
        if (health < maxHealth)
        {
            health += healingPoint;
        }
    }

    IEnumerator DisappearHealthPoints()
    {
        for (float i = health; i > 0; i--)
        {
            yield return new WaitForSeconds(lifeTime);

            damage(downHealthPairSec);

            if (i <= 0)
            {
                gameOver();
            }
        }
    }

    void gameOver()
    {
        bool trialsActive = IsTrialModeActive();
        
        Debug.Log($"[Health] gameOver() called - Trials Active: {trialsActive}, Health: {health}");
        
        if (trialsActive)
        {
            // In trials: Open trial panel and notify
            Debug.Log("[Health] Trial failed - Opening trial panel");
            
            // Stop game time
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            
            // Find and open trial UI panel
            var trialUIController = FindObjectOfType<TrialUIController>();
            if (trialUIController != null)
            {
                trialUIController.OpenTrialControlPanel();
                Debug.Log("[Health] Trial panel opened");
            }
            else
            {
                Debug.LogError("[Health] TrialUIController not found!");
            }
            
            // Notify the system
            GameStateManager.NotifyGameEnded(health, false);
            return; 
        }

        // In normal game: Load the Game_Over scene
        Debug.Log("[Health] Loading Game_Over scene (not in trials)");
        GameStateManager.NotifyGameEnded(health, false);
        SceneManager.LoadScene("Game_Over");
    }

  
    bool IsTrialModeActive()
    {
        // Check if the trial mode is active via GameStateManager.AreTrialsActive (static property)
        if (GameStateManager.AreTrialsActive)
        {
            Debug.Log("[Health] Trials detected via GameStateManager.AreTrialsActive");
            return true;
        }

        // Check if the TrialSystemManager exists and is in trials mode
        var trialSystem = FindObjectOfType<TrialSystemManager>();
        if (trialSystem != null && trialSystem.TrialsMode)
        {
            Debug.Log("[Health] Trials detected via TrialSystemManager.TrialsMode");
            return true;
        }

       
        // Redundant check - already checked AreTrialsActive above
        // if (GameStateManager.Instance != null)
        // {
        //     bool trials = GameStateManager.AreTrialsActive;
        //     if (trials)
        //     {
        //         Debug.Log("[Health] Trials detected via GameStateManager.Instance");
        //         return true;
        //     }
        // }

        // No active trials
        Debug.Log("[Health] No trials detected - normal game mode");
        return false;
    }

    public float GetOxygen()
    {
        return health;
    }
}