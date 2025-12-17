using UnityEngine;

[CreateAssetMenu(fileName = "GameDataSO", menuName = "AquaGuardian/Game Data SO")]
public class GameDataSO : ScriptableObject
{
    // Singleton instance for easy access across all game scripts
    //  Already connected with Health.cs, PlayerMovement.cs, PlayerLife.cs, CaveBuilder.cs, PanelOpenUp.cs
    public static GameDataSO Instance
    {
        get
        {
            if (_instance == null)
            {
                // Load from Resources folder
                _instance = Resources.Load<GameDataSO>("GameDataSO");
                
                if (_instance == null)
                {
                    Debug.LogWarning("GameDataSO asset not found! Please create one via Assets > Create > AquaGuardian > Game Data SO and place it in Assets/Resources/ folder.");
                }
            }
            return _instance;
        }
    }

    private static GameDataSO _instance;

    // Note: OnEnable removed to prevent issues with multiple GameDataSO assets
    // Instance is loaded only via Resources.Load in the Instance getter

    [Header("Setting of difficulty")]
    public float oxygenPerBalloon = 5f;
    public float oxygenDropPerSec = 1.0f;
    public float oxygenDropOnCollision = 8f;
    public float factorLerpSpeed = 3f;              // Health.cs 
    public float idleUpwardFactor = 0.5f;           // PlayerMovement.cs
    public float playerCollisionDelay = 2f;         // PlayerMovement.cs

    [Header("Cave Generation Settings (CaveBuilder.cs)")]
    public int pivotChest = 75; // Distance (in minus z direction) from last cave to treasure chest
    public float chestX = 291.774f; // X position for the chest (note: Y position for the chest is the Y position of the last cave).1
    public float generalPivot = 50f; // Distance (in minus z direction) from current cave to next wall / oxygen / arrows.
    public float pivotCavePlace = 70f; // Distance (in minus z direction) from current wall to next cave.
    public float pivotArrowsToWall = 45f; // Pivot distance between arrows and walls

    [Header("System Constants (from code)")]
    public float maxHealth = 100f;                  // Health.cs
    public float playerLifeWaitTime = 2f;           // PlayerLife.cs
    public float colorAlphaValue = 0.5f;            // PlayerLife.cs
    public float timeUntilFadeOut = 3f;             // PlayerLife.cs

    [Header("Trial System Config")]
    public int totalTrials = 5;
    public bool useRandomParameters = false;
    public string trialParametersPath = "Data/Trials/Trial_5_runs_.csv";

    [Header("Cave Files for Trials")]
    public TextAsset[] caveFiles = new TextAsset[5];
    public bool useCaveFilePathPattern = true;
    public string caveFilePathPattern = "Data/Cave{n}.csv";

    [Header("Trial Fish Defaults")]
    public Vector3 trialFishScale = new Vector3(2f, 0.8f, 1f);
    public bool debugFishPosition = true;
    public bool addGoToEndGameToFish = true;

}
