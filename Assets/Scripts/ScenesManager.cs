using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
public class ScenesManager : MonoBehaviour
{
    public void GoToGameScene()
    {
        ResetGameState();
        SceneManager.LoadScene("Scene_Ocean");
    }

    public void GoToSettingScene()
    {
        SceneManager.LoadScene("Settings");
    }  

    public void GoToMenuScene()
    {
        ResetGameState();
        SceneManager.LoadScene("Menu");
    }

    public void GoToHowToPlay()
    {
        SceneManager.LoadScene("how_to_play");
    }
    
    /// <summary>
    /// Restart the current scene with full reset
    /// </summary>
    public void RestartGame()
    {
        Debug.Log("=== RESTARTING GAME ===");
        
        // Reset game state
        ResetGameState();
        
        // Stop time
        Time.timeScale = 1f;
        
        // Reload current scene
        string currentScene = SceneManager.GetActiveScene().name;
        SceneManager.LoadScene(currentScene);
        
        Debug.Log($"Restarted scene: {currentScene}");
    }
    
    /// <summary>
    /// Reset GameStateManager and other persistent systems
    /// </summary>
    private void ResetGameState()
    {
        // Reset GameStateManager
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ResetState();
            GameStateManager.SetTrialsActive(false);
            Debug.Log("GameStateManager reset");
        }
        
        // Clear trial cache if it exists
        if (TrialDataCache.Instance != null)
        {
            // Don't clear cache - we want to keep history!
            Debug.Log(" TrialDataCache preserved (history intact)");
        }
        
        // Reset time scale
        Time.timeScale = 1f;
        
        Debug.Log(" Game state reset complete");
    }

    // Quit the game and close the application
    public void QuitGame()
    {
        Application.Quit();
    }
}
