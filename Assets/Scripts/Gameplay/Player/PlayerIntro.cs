using UnityEngine;
using TMPro;
using System.Collections;

/**
 * PlayerIntro is a script that is used to show the intro text and keys to the player
 See also: PlayerMovement
 */
public class PlayerIntro : MonoBehaviour
{
    [Header("UI References")]
    public GameObject panel;
    [SerializeField] private TextMeshProUGUI infoText6;  // Text for "Get Ready"
    [SerializeField] private TextMeshProUGUI infoText7;  // Text for "Go"

    [Header("Player References")]
    [SerializeField] private PlayerMovement playerMovement;

    private bool show = true;

    void Start()
    {
        if (infoText6 != null && infoText7 != null)
        {
            infoText6.gameObject.SetActive(false);
            infoText7.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        // Check both panel state and GameStateManager for better reliability
        bool panelClosed = !panel.activeSelf;

        if (GameStateManager.Instance != null)
        {
            panelClosed = GameStateManager.Instance.IsPanelClosed;
        }

        if (show && panelClosed)
        {
           // Debug.Log($"[PlayerIntro] Starting intro (show={show}, panelClosed={panelClosed})");
            ProcessUserInputsInInitialForm();
            StartCoroutine(ShowInfoTextAndKeys());
            show = false;
        }
    }

    void ProcessUserInputsInInitialForm()
    {
        // Null checks to prevent errors
        if (playerMovement == null)
        {
            //Debug.LogWarning("[PlayerIntro] playerMovement is null, skipping input processing");
            return;
        }
        
        if (playerMovement.speed_inputField == null || 
            playerMovement.vertical_speed_inputField == null || 
            playerMovement.idle_upward_speed_inputField == null)
        {
            Debug.LogWarning("[PlayerIntro] Input fields not assigned, using default values");
            return;
        }

        bool isSpeedValid = float.TryParse(playerMovement.speed_inputField.text, out float speed);
        bool isSpeedVerticalValid = float.TryParse(playerMovement.vertical_speed_inputField.text, out float verticalSpeed);
        bool isIdleUpwardSpeedValid = float.TryParse(playerMovement.idle_upward_speed_inputField.text, out float idleUpwardSpeed);

        if (isSpeedValid && isSpeedVerticalValid && isIdleUpwardSpeedValid)
        {
            playerMovement.speed = speed;
            playerMovement.verticalSpeed = verticalSpeed;
            playerMovement.idleUpwardSpeed = idleUpwardSpeed;
            Debug.Log($"[PlayerIntro] Speed values set: speed={speed}, verticalSpeed={verticalSpeed}, idleUpwardSpeed={idleUpwardSpeed}");
        }
        else
        {
            Debug.LogWarning($"[PlayerIntro] Invalid input - speed: {playerMovement.speed_inputField.text}, " +
                           $"verticalSpeed: {playerMovement.vertical_speed_inputField.text}, " +
                           $"idleUpward: {playerMovement.idle_upward_speed_inputField.text}");
        }
    }

    public void ResetIntro()
    {
        StopAllCoroutines();

        if (infoText6 != null) infoText6.gameObject.SetActive(false);
        if (infoText7 != null) infoText7.gameObject.SetActive(false);

        show = true;

        if (playerMovement != null)
        {
            playerMovement.afterText = false;
        }

        //Debug.Log("PlayerIntro reset - intro text will play again for next trial");
    }

    private IEnumerator ShowInfoTextAndKeys()
    {
        //Debug.Log("[PlayerIntro] ShowInfoTextAndKeys started");
        
        if (infoText6 != null && infoText7 != null)
        {
         //   Debug.Log("[PlayerIntro] Showing READY text");
            infoText6.gameObject.SetActive(true);
            yield return WaitForSecondsOrSkip(1f);
            infoText6.gameObject.SetActive(false);

            yield return WaitForSecondsOrSkip(1f);

            //Debug.Log("[PlayerIntro] Showing GO text");
            infoText7.gameObject.SetActive(true);
            yield return WaitForSecondsOrSkip(1f);
            infoText7.gameObject.SetActive(false);
            
            if (playerMovement != null)
            {
                playerMovement.afterText = true;
                //Debug.Log("[PlayerIntro] Set playerMovement.afterText = true");
            }

            // Notify GameStateManager that intro is complete
            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.NotifyIntroComplete();
            }
           // Debug.Log("[PlayerIntro] Intro complete, notified GameStateManager");
        }
        else
        {
            Debug.LogWarning("[PlayerIntro] infoText6 or infoText7 is null, skipping intro animation");
        }
    }

    IEnumerator WaitForSecondsOrSkip(float seconds)
    {
        float elapsedTime = 0f;
        while (elapsedTime < seconds)
        {
            // Use unscaledDeltaTime to work even when Time.timeScale = 0
            elapsedTime += Time.unscaledDeltaTime;
            if (Input.GetKeyDown(KeyCode.Return))
            {
               // Debug.Log("[PlayerIntro] Skipping intro (Enter pressed)");
                yield break;
            }
            yield return null;
        }
       // Debug.Log($"[PlayerIntro] Wait completed ({seconds}s)");
    }
}