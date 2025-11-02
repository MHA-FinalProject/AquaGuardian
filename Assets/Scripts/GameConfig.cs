using UnityEngine;

[CreateAssetMenu(fileName="GameConfig", menuName="AquaGuardian/Game Config")]
public class GameConfig : ScriptableObject
{
    // Singleton instance for easy access
    //TODO: connect this with health.cs and etc..
    public static GameConfig Instance 
    { 
        get 
        {
            if (_instance == null)
            {
                // Try to load from Resources folder
                _instance = Resources.Load<GameConfig>("GameConfig");
                
                // If not found in Resources, try to find in Assets (Editor only)
                #if UNITY_EDITOR
                if (_instance == null)
                {
                    string[] guids = UnityEditor.AssetDatabase.FindAssets("t:GameConfig");
                    if (guids.Length > 0)
                    {
                        string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                        _instance = UnityEditor.AssetDatabase.LoadAssetAtPath<GameConfig>(path);
                    }
                }
                #endif
                
                if (_instance == null)
                {
                    Debug.LogWarning("GameConfig asset not found! Please create one via Assets > Create > AquaGuardian > Game Config and place it in a Resources folder.");
                }
            }
            return _instance;
        }
        private set
        {
            _instance = value;
        }
    }
    
    private static GameConfig _instance;
    
    private void OnEnable()
    {
        if (_instance == null)
        {
            _instance = this;
        }
    }
    [Header("Defaults")]
    public float oxygenPerBalloon = 5f;
    public float oxygenDropPerSec = 1.0f;
    public float oxygenDropOnCollision = 8f;

    [Header("Positioning Constants (from PanelOpenUp.cs)")]
    [Tooltip("constants for positioning the chest, arrows, etc.")]
    public int pivotChest = 75; // Distance (in minus z direction) from last cave to treasure chest
    public float chestX = 291.774f; // X position for the chest (note: Y position for the chest is the Y position of the last cave).1
    public float generalPivot = 50f; // Distance (in minus z direction) from current cave to next wall / oxygen / arrows.
    public float pivotCavePlace = 70f; // Distance (in minus z direction) from current wall to next cave.
    public float pivotArrowsToWall = 45f; // Pivot distance between arrows and walls
    
    [Header("System Constants (from code)")]
   
    public float maxHealth = 100f;                  // Health.cs
    public float factorLerpSpeed = 3f;              // Health.cs
    public float idleUpwardFactor = 0.5f;           // PlayerMovement.cs
    public float playerCollisionDelay = 2f;         // PlayerMovement.cs
    public float playerLifeWaitTime = 2f;           // PlayerLife.cs
    public float colorAlphaValue = 0.5f;            // PlayerLife.cs
    public float timeUntilFadeOut = 3f;             // PlayerLife.cs

    [Header("Trial System Config")]
    [Tooltip("Number of trials to run in trial mode")] public int totalTrials = 5;
    [Tooltip("If true, use random parameters instead of CSV")] public bool useRandomParameters = false;
    [Tooltip("Relative path under Assets/ to Trial parameters CSV")] public string trialParametersPath = "Data/Trials/Trial_5_runs_.csv";
    
    [Header("Cave Files for Trials")]
    [Tooltip("Array of cave CSV files for each trial (index 0 = trial 1, etc.)")] public TextAsset[] caveFiles = new TextAsset[5];
    [Tooltip("Load caves per trial via path pattern (e.g. Data/Cave_{n}.csv)")] public bool useCaveFilePathPattern = true;
    [Tooltip("Relative path pattern under Assets/ for caves per trial")] public string caveFilePathPattern = "Data/Cave{n}.csv";

    [Header("Trial Fish Defaults")]
   
    public Vector3 trialFishScale = new Vector3(2f, 0.8f, 1f);
    public bool debugFishPosition = true;
    public bool addGoToEndGameToFish = true;
   
}
