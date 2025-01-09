using UnityEngine;
using UnityEngine.UI;


// Add this line if PauseMenu is in a custom namespace
// using YourNamespace;  

public class PauseButton : MonoBehaviour
{
    [SerializeField] private PauseManager pauseMenu;
    private Button button;

    private void Start()
    {
        button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(OnPauseButtonClick);
        }
    }

    private void OnPauseButtonClick()
    {
        if (pauseMenu != null)
        {
            pauseMenu.TogglePause();
        }
    }
}
