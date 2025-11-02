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
    
    public void RestartGame()
    {
        ResetGameState();
        Time.timeScale = 1f;
        
        string currentScene = SceneManager.GetActiveScene().name;
        SceneManager.LoadScene(currentScene);
        
        Debug.Log($"Restarted scene: {currentScene}");
    }
    
    private void ResetGameState()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ResetState();
            GameStateManager.SetTrialsActive(false);
            Debug.Log("GameStateManager reset");
        }
        
        if (TrialDataCache.Instance != null)
        {
            Debug.Log(" TrialDataCache preserved (history intact)");
        }
        
        Time.timeScale = 1f;
        Debug.Log(" Game state reset complete");
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
