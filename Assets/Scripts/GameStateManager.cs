using UnityEngine;
using System;

/**
 * Game State Manager that manages the game state and triggers events when the game state changes
 * such as panel closed, intro complete, game start, and game end. 
 *
 */
public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance { get; private set; }
    public static event Action OnPanelClosed;
    public static event Action OnIntroComplete;
    public static event Action OnGameStart;
    public static event Action<float, bool> OnGameEnded; // finalOxygen, completed
    
    // Static trials state (not tied to Instance)
    private static bool _trialsActive = false;
    
    [Header("Game State")]
    [SerializeField] private bool panelClosed = false;
    [SerializeField] private bool introComplete = false;
    
    public bool IsPanelClosed => panelClosed;
    public bool IsIntroComplete => introComplete;
    public bool IsGameReady => panelClosed && introComplete;
    
    public static bool AreTrialsActive => _trialsActive;
    
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate GameStateManager detected - destroying duplicate");
            Destroy(gameObject);
            return;
        }
        Instance = this;
       DontDestroyOnLoad(gameObject);
    }
    
  
    public void NotifyPanelClosed()
    {
        if (panelClosed) return; // already closed
        
        panelClosed = true;
       // Debug.Log("GameStateManager: Panel closed");
        OnPanelClosed?.Invoke();
        
        CheckGameStart();
    }
    public void NotifyPanelOpened()
    {
        panelClosed = false;
        //Debug.Log("GameStateManager: Panel opened (reset)");
    }
   
    public void NotifyIntroComplete()
    {
        if (introComplete) return; 
        
        introComplete = true;
        //Debug.Log("GameStateManager: Intro complete");
        OnIntroComplete?.Invoke();
        
        CheckGameStart();
    }
    
   
    private void CheckGameStart()
    {
        if (IsGameReady)
        {
            // Debug.Log("GameStateManager: Game started!");
            OnGameStart?.Invoke();
        }
    }
    
    
    public static void SetTrialsActive(bool active)
    {
        _trialsActive = active;
        Debug.Log($"[GameStateManager] Trials set to: {active}");
    }
    
    public static void NotifyGameEnded(float oxygen, bool completed)
    {
        Debug.Log($"[GameStateManager] Game ended - Oxygen: {oxygen}, Completed: {completed}, TrialsActive: {_trialsActive}");
        OnGameEnded?.Invoke(oxygen, completed);
    }
    
 
 
    public void ResetState()
    {
        //Debug.Log($"[GameStateManager] Resetting state (panelClosed: {panelClosed} → false, introComplete: {introComplete} → false)");
        panelClosed = false;
        introComplete = false;
    }
}


