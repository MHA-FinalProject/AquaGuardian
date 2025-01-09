using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PauseManager : MonoBehaviour
{

    [SerializeField] private GameObject pauseMenuPanel;
    [SerializeField] private Button resumeButton;
    [SerializeField] private AmadeoClient amadeoClient;


    private bool isPaused = false;

    private void Start()
    {
        // Ensure the pause menu is hidden at start
        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(false);
            Debug.Log("Pause menu panel is hidden at start.");
        }
        else
        {
            Debug.LogError("Pause menu panel is not assigned.");
        }

        // Add listener to resume button
        if (resumeButton != null)
        {
            resumeButton.onClick.AddListener(ResumeGame);
            Debug.Log("Resume button listener added.");
        }
        else
        {
            Debug.LogError("Resume button is not assigned.");
        }

        // Find AmadeoClient if not assigned
        if (amadeoClient == null)
        {
            amadeoClient = FindObjectOfType<AmadeoClient>();
            
        }
    }

    public void TogglePause()
    {
        isPaused = !isPaused;

        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(isPaused);
            Debug.Log("Pause menu panel set to " + isPaused);
        }
        else
        {
            Debug.LogError("Pause menu panel is not assigned.");
        }

        // Handle Amadeo data reception
        if (amadeoClient != null)
        {
            if (isPaused)
            {
                amadeoClient.StopReceiveData();
                Debug.Log("AmadeoClient stopped receiving data.");
            }
            else
            {
                amadeoClient.StartReceiveData();
                Debug.Log("AmadeoClient started receiving data.");
            }
        }

        // Pause/unpause the game
        Time.timeScale = isPaused ? 0f : 1f;
        Debug.Log("Time scale set to " + Time.timeScale);
    }

    public void ResumeGame()
    {
        isPaused = false;
        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(false);
            Debug.Log("Pause menu panel hidden.");
        }
        else
        {
            Debug.LogError("Pause menu panel is not assigned.");
        }

        // Resume Amadeo data reception
        if (amadeoClient != null)
        {
            amadeoClient.StartReceiveData();
            Debug.Log("AmadeoClient started receiving data.");
        }

        Time.timeScale = 1f;
        Debug.Log("Time scale set to 1.");
    }

    private void OnDestroy()
    {
        // Stop Amadeo data reception and ensure time scale is reset
        if (amadeoClient != null)
        {
            amadeoClient.StopReceiveData();
            Debug.Log("AmadeoClient stopped receiving data.");
        }
        Time.timeScale = 1f;
        Debug.Log("Time scale reset to 1.");
    }
}
