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
    private float lerpSpeed;

    [Header("Lifetime & Damage Settings")]
    private float lifeTime;
    public TMP_InputField lifeTime_inputField;
    private float RemoveHealthEveryLifeTime;  // Health removed every lifeTime cycle
    public TMP_InputField RemoveHealthEveryLifeTime_inputField;

    [Header("Game Data (Optional - leave empty to use singleton)")]
    [SerializeField] private GameDataSO gameDataOverride;

    [Header("Internal States")]
    private bool moveOxygen = false;
    public bool didntGetInputsYet = false;


    void Start()
    {
        health = GetMaxHealth();

        if (Panel != null && !Panel.activeSelf)
        {
            moveOxygen = true;
            StartCoroutine(DisappearHealthPoints());
        }
    }

    // Get max health from GameDataSO (fallback to 100f if not available)
    private float GetMaxHealth()
    {
        GameDataSO gameData = gameDataOverride != null ? gameDataOverride : GameDataSO.Instance;
        if (gameData != null)
            return gameData.maxHealth;
        return 100f;
    }

    // Get factor lerp speed from GameDataSO (fallback to 3f if not available)
    private float GetFactorLerpSpeed()
    {
        GameDataSO gameData = gameDataOverride != null ? gameDataOverride : GameDataSO.Instance;
        if (gameData != null)
            return gameData.factorLerpSpeed;
        return 3f;
    }

    void Update()
    {
        if (didntGetInputsYet)
        {
            ProcessUserInputs();
            didntGetInputsYet = false;
        }

        // Smart formatting: shows up to 3 decimals, removes trailing zeros
        // 100.000 -> "100", 97.8 -> "97.8", 97.8975666 -> "97.898"
        healthText.text = "Oxygen: " + health.ToString("0.###") + "%";

        HealthBarFiller();
        ColorChanger();

        bool panelClosed = Panel == null || !Panel.activeSelf;
        if (GameStateManager.Instance != null)
        {
            panelClosed = GameStateManager.Instance.IsPanelClosed;
        }
        if (panelClosed && !moveOxygen)
        {
            moveOxygen = true;
            StartCoroutine(DisappearHealthPoints());
        }

        float maxHealth = GetMaxHealth();
        if (health > maxHealth)
        {
            health = maxHealth;
        }

        lerpSpeed = GetFactorLerpSpeed() * Time.deltaTime;
    }

    public void ProcessUserInputs()
    {
        bool isLifeTimeValid = float.TryParse(lifeTime_inputField.text, out lifeTime);
        bool isRemoveHealthValid = float.TryParse(RemoveHealthEveryLifeTime_inputField.text, out RemoveHealthEveryLifeTime);
        if (isLifeTimeValid && isRemoveHealthValid)
        {
            lifeTime = float.Parse(lifeTime_inputField.text);
            RemoveHealthEveryLifeTime = float.Parse(RemoveHealthEveryLifeTime_inputField.text);
        }
        else
        {
            Debug.LogError($"Input parsing failed: lifeTime={lifeTime_inputField.text}, RemoveHealthEveryLifeTime={RemoveHealthEveryLifeTime_inputField.text}");
        }

        StopAllCoroutines();
        moveOxygen = false;
    }

    void HealthBarFiller()
    {
        float maxHealth = GetMaxHealth();
        healthBar.fillAmount = Mathf.Lerp(healthBar.fillAmount, health / maxHealth, lerpSpeed);
    }

    // Changes the color of the health bar based on the current health percentage
    void ColorChanger()
    {
        float maxHealth = GetMaxHealth();
        Color healthColor = Color.Lerp(Color.red, Color.green, (health / maxHealth));
        healthBar.color = healthColor;
    }

    public void Damage(float damagePoint)
    {
        if (health > damagePoint)
        {
            health -= damagePoint;
        }
        else
        {
            health = 0;
            GameOver();
        }
    }

    public void Heal(float healingPoint)
    {
        float maxHealth = GetMaxHealth();
        health = Mathf.Clamp(health + healingPoint, 0, maxHealth);
    }

    IEnumerator DisappearHealthPoints()
    {
        for (float i = health; i > 0; i--)
        {
            yield return new WaitForSeconds(lifeTime);

            Damage(RemoveHealthEveryLifeTime);

            if (i <= 0)
            {
                GameOver();
            }
        }
    }

    void GameOver()
    {
        bool trialsActive = IsTrialModeActive();

        Debug.Log($"[Health] GameOver() called - Trials Active: {trialsActive}, Health: {health}");

        if (trialsActive)
        {
            // In trials: Open trial panel and notify
            //Debug.Log("[Health] Trial failed - Opening trial panel");

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

        // No active trials
        Debug.Log("[Health] No trials detected - normal game mode");
        return false;
    }

    public float GetOxygen()
    {
        return health;
    }
}