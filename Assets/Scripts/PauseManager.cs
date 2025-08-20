using UnityEngine;
using UnityEngine.UI;

public class PauseManager : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Button pauseButton;    
    [SerializeField] private Button resumeButton;   
    [SerializeField] private GameObject pausePanel; 
    
    [Header("References")]
    [SerializeField] private AmadeoClient amadeoClient;

    private void Start()
    {
        InitializeComponents();
    }

    private void InitializeComponents()
    {
        // Validate required components
        if (pausePanel == null || pauseButton == null || resumeButton == null)
        {
            Debug.LogError("Missing required components in PauseManager!");
            enabled = false;
            return;
        }

        pausePanel.SetActive(false);
        pauseButton.onClick.AddListener(Pause);
        resumeButton.onClick.AddListener(Resume);
       
        // Find AmadeoClient if not assigned
        if (amadeoClient == null)
        {
            amadeoClient = FindObjectOfType<AmadeoClient>();
            if (amadeoClient == null)
            {
                Debug.LogWarning("AmadeoClient not found in scene!");
            }
        }
    }

    public void Pause()
    {
        if (Time.timeScale != 0f)
        {
            pausePanel.SetActive(true);
            amadeoClient?.StopReceiveData();
            Time.timeScale = 0f;
        }
    }

    public void Resume()
    {
        if (Time.timeScale == 0f)
        {
            pausePanel.SetActive(false);
            amadeoClient?.StartReceiveData();
            Time.timeScale = 1f;
        }
    }

    private void OnDestroy()
    {
        Resume();
        pauseButton.onClick.RemoveListener(Pause);
        resumeButton.onClick.RemoveListener(Resume);
    }
}