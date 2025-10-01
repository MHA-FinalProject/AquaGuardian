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
            ProcessUserInputsInInitialForm();
            StartCoroutine(ShowInfoTextAndKeys());
            show = false;
        }
    }

    void ProcessUserInputsInInitialForm()
    {
        bool isSpeedValid = float.TryParse(playerMovement.speed_inputField.text, out float speed);
        bool isSpeedVerticalValid = float.TryParse(playerMovement.vertical_speed_inputField.text, out float verticalSpeed);
        bool isIdleUpwardSpeedValid = float.TryParse(playerMovement.idle_upward_speed_inputField.text, out float idleUpwardSpeed);

        if (isSpeedValid && isSpeedVerticalValid && isIdleUpwardSpeedValid)
        {
            playerMovement.speed = speed;
            playerMovement.verticalSpeed = verticalSpeed;
            playerMovement.idleUpwardSpeed = idleUpwardSpeed;
        }
        else
        {
            Debug.Log($"error: {playerMovement.speed_inputField.text}");
            Debug.Log(playerMovement.vertical_speed_inputField.text);
            Debug.Log(playerMovement.idle_upward_speed_inputField.text);
        }
        Debug.Log($"speed: {speed}, vertical speed: {verticalSpeed}, idleUpwardSpeed: {idleUpwardSpeed}");
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

        Debug.Log("PlayerIntro reset - intro text will play again for next trial");
    }

    private IEnumerator ShowInfoTextAndKeys()
    {
        if (infoText6 != null && infoText7 != null)
        {
            infoText6.gameObject.SetActive(true);
            yield return WaitForSecondsOrSkip(1f);
            infoText6.gameObject.SetActive(false);

            yield return WaitForSecondsOrSkip(1f);

            infoText7.gameObject.SetActive(true);
            yield return WaitForSecondsOrSkip(1f);
            infoText7.gameObject.SetActive(false);
            playerMovement.afterText = true;

            // Notify GameStateManager that intro is complete
            GameStateManager.Instance?.NotifyIntroComplete();
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
                yield break;
            }
            yield return null;
        }
    }
}