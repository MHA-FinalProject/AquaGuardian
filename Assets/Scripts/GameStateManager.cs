using UnityEngine;
using System;

/**
 * Game State Manager that manages the game state and triggers events when the game state changes
 */
public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance { get; private set; }
    public static event Action OnPanelClosed;
    public static event Action OnIntroComplete;
    public static event Action OnGameStart;
    public static event Action<float, bool> OnGameEnded; // finalOxygen, completed
    
    [Header("Game State")]
    [SerializeField] private bool panelClosed = false;
    [SerializeField] private bool introComplete = false;
    [SerializeField] private bool trialsActive = false;
    public bool IsPanelClosed => panelClosed;
    public bool IsIntroComplete => introComplete;
    public bool IsGameReady => panelClosed && introComplete;
    public static bool AreTrialsActive => Instance != null && Instance.trialsActive;
    
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    
  
    public void NotifyPanelClosed()
    {
        if (panelClosed) return; // already closed
        
        panelClosed = true;
        Debug.Log("GameStateManager: Panel closed");
        OnPanelClosed?.Invoke();
        
        CheckGameStart();
    }
    public void NotifyPanelOpened()
    {
        panelClosed = false;
        Debug.Log("GameStateManager: Panel opened (reset)");
    }

    
   
    public void NotifyIntroComplete()
    {
        if (introComplete) return; 
        
        introComplete = true;
        Debug.Log("GameStateManager: Intro complete");
        OnIntroComplete?.Invoke();
        
        CheckGameStart();
    }
    
   
    private void CheckGameStart()
    {
        if (IsGameReady)
        {
            Debug.Log("GameStateManager: Game started!");
            OnGameStart?.Invoke();
        }
    }
    
        
  
    public static void NotifyGameEnded(float finalOxygen, bool completed)
    {
        Debug.Log($"GameStateManager: Game ended - O2: {finalOxygen:F2}, Completed: {completed}");
        OnGameEnded?.Invoke(finalOxygen, completed);
    }
    
    
    // Set trials active
    public static void SetTrialsActive(bool active)
    {
        if (Instance != null)
        {
            Instance.trialsActive = active;
            Debug.Log($"GameStateManager: Trials active = {active}");
        }
    }
    
 
 
    public void ResetState()
    {
        panelClosed = false;
        introComplete = false;
    }
}


