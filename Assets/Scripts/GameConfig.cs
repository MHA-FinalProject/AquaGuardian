using UnityEngine;

[CreateAssetMenu(fileName="GameConfig", menuName="AquaGuardian/Game Config")]
public class GameConfig : ScriptableObject
{
    // Singleton instance for easy access
    //TODO: connect this with health.cs and etc..
    public static GameConfig Instance { get; private set; }
    
    private void OnEnable()
    {
        Instance = this;
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
    [Tooltip("Relative path under Assets/ to Trial parameters CSV")] public string trialParametersPath = "Data/Trial_5_runs_.csv";
    [Tooltip("Load caves per trial via path pattern (e.g. Data/Cave_{n}.csv)")] public bool useCaveFilePathPattern = true;
    [Tooltip("Relative path pattern under Assets/ for caves per trial")] public string caveFilePathPattern = "Data/caves{n}.csv";

    [Header("Trial Fish Defaults")]
    public bool animateTrialFish = false;
    public Vector3 trialFishScale = new Vector3(2f, 0.8f, 1f);
    public bool debugFishPosition = true;
    public bool addGoToEndGameToFish = true;
   
}
