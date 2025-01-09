using UnityEngine;
using TMPro;
using System.Collections;

public class  GameStartUI : MonoBehaviour
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
        if (show && !panel.activeSelf)
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