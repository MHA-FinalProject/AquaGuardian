using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
public class ScenesManager : MonoBehaviour
{

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void GoToGameScene()
    {
        SceneManager.LoadScene("Scene_Ocean");
    }

    public void GoToGameSceneRight()
    {
        SceneManager.LoadScene("Scene_Ocean_Right");
    }

    public void GoToSettingScene()
    {
        SceneManager.LoadScene("Settings");
    }

    public void GoToMenuScene()
    {
        SceneManager.LoadScene("Menu");
    }

    public void GoToHowToPlay()
    {
        SceneManager.LoadScene("how_to_play");
    }

    // Quit the game and close the application
    public void QuitGame()
    {
        Application.Quit();
    }
}
