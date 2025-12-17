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

    [Header("Collision Tracking")]
    private int collisionCount = 0; // helper for debug 
    private bool canCollide = true;

    // ----- Health Management -----
    [Header("Health Management")]
    [SerializeField] GameObject healthBarObject2;  // Reference to the health bar object
    private Health healthBar2;  // Reference to the HealthBar component
    private float removeHealthWithCollide;  // Health to be removed on collision
    public TMP_InputField removeHealthWithCollide_inputField;  // Input field for health removal
    private float timeBetweenCollides;  
    public TMP_InputField timeBetweenCollides_inputField;  // Input field for time between collisions
    private float healHealthPoint; 
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

    // ----- Game Data -----
    [Header("Game Data (Optional - leave empty to use singleton)")]
    [SerializeField] private GameDataSO gameDataOverride;

    // ----- Fade Control -----
    // Values are now loaded from GameDataSO (override or singleton)

    void Start()
    {
        // Subscribe to GameStateManager events
        GameStateManager.OnPanelClosed += OnPanelClosed;

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.clip = collisionSound;

        // Get the HealthBar component
        if (healthBarObject2 != null)
        {
            healthBar2 = healthBarObject2.GetComponent<Health>();
        }

        audioSourceOxygen = gameObject.AddComponent<AudioSource>();
        audioSourceOxygen.clip = collisionSoundOxygen;

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

    void OnDestroy()
    {
        // Unsubscribe from events to prevent memory leaks
        GameStateManager.OnPanelClosed -= OnPanelClosed;
    }

    // Called when the panel is closed via GameStateManager
    private void OnPanelClosed()
    {
        ProcessUserInputs();
    }

    private void Update()
    {
        if (didntGetInputsYet)
        {
            ProcessUserInputs(); 
            didntGetInputsYet = false;
        }
    }

    public void ProcessUserInputs()
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
            Debug.LogError($"Input parsing failed: collision={removeHealthWithCollide_inputField.text}, time={timeBetweenCollides_inputField.text}, heal={healHealthPoints_inputField.text}");
        }

        StopAllCoroutines();
        canCollide = true;
    }
    
    private float GetWaitTime()
    {
        GameDataSO gameData = gameDataOverride != null ? gameDataOverride : GameDataSO.Instance;
         return gameData != null ? gameData.playerLifeWaitTime : 2f;
    }
    
    private float GetColorAlphaValue()
    {
        GameDataSO gameData = gameDataOverride != null ? gameDataOverride : GameDataSO.Instance;
        return gameData != null ? gameData.colorAlphaValue : 0.5f;
    }
    
    private float GetTimeUntilFadeOut()
    {
        GameDataSO gameData = gameDataOverride != null ? gameDataOverride : GameDataSO.Instance;
        return gameData != null ? gameData.timeUntilFadeOut : 3f;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (canCollide && collision.collider.CompareTag("Cave"))
        {
            collisionCount++;
            Debug.Log("collisionCount: " + collisionCount);
            HandleCollision();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("OxygenObject"))
        {
            PlayCollisionSoundOxygen();
            other.gameObject.SetActive(false);

            if (healthBar2 != null)
                healthBar2.Heal(healHealthPoint);
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
        PlayCollisionSound();
        StartCoroutine(ShowBloodSplatter());

        if (healthBar2 != null && canCollide)
        {
            healthBar2.Damage(removeHealthWithCollide);
            StartCoroutine(Wait(timeBetweenCollides));
        }
    }

    IEnumerator ShowBloodSplatter()
    {
        if (bloodSplatterImage == null) yield break;
        
        // Get values from GameDataSO.Instance
        float alphaValue = GetColorAlphaValue();
        float fadeTime = GetTimeUntilFadeOut();
        
        Color color = bloodSplatterImage.color;
        color.a = alphaValue;
        bloodSplatterImage.color = color;

        for (float t = 0; t < fadeTime; t += Time.deltaTime)
        {
            color.a = Mathf.Lerp(alphaValue, 0f, t / fadeTime);
            bloodSplatterImage.color = color;
            yield return null;
        }

        color.a = 0f;
        bloodSplatterImage.color = color;
    }

    IEnumerator Wait(float number)
    {
        canCollide = false;
        yield return new WaitForSeconds(number);
        canCollide = true;
    }

    void PlayCollisionSound()
    {
        if (audioSource != null && collisionSound != null)
        {
            audioSource.Play();
        }
    }

    // Public method to get collision count
    public int GetCollisionCount()
    {
        return collisionCount;
    }

    public void ResetBloodSplatter()
    {
        StopAllCoroutines();
        
        if (bloodSplatterImage != null)
        {
            Color color = bloodSplatterImage.color;
            color.a = 0f;
            bloodSplatterImage.color = color;
        }
        
        canCollide = true;
    }
}
