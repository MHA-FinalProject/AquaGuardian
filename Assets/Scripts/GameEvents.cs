using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameEvents : MonoBehaviour
{
   
   
    public static event System.Action OnGameStarted;
    // game paused
    public static event System.Action OnGamePaused;

    // game resumed
    public static event System.Action OnGameResumed;

    // game restarted
    public static event System.Action OnGameRestarted;
    // game over screen
    public static event System.Action OnGameOverScreen;

   


}
