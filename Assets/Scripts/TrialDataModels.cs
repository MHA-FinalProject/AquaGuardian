using UnityEngine;
using System;

/**
 * TrialDataModels - Core data structures for AquaGuardian
 * 
 * This static class contains all primary data models used across the game:
 * 
 * Models:
 *   - TrialData: Trial parameters, results, and metadata
 *   - RegressionResult: Machine learning regression analysis results
 *   - CaveInfo: Cave geometry and positioning data (used in BOTH trial and regular modes)
 *   - ParameterRanges: Valid parameter ranges for trial generation and optimization
 */
public static class TrialDataModels
{
    /**
     * Feature names for regression analysis (single source of truth)
     * Must match TrialData field names and ParameterHelper indices!
     * 
     * 10 FEATURES:
     * 1. speed - Horizontal forward speed
     * 2. verticalSpeed - Vertical movement speed
     * 3. idleUpwardSpeed - Passive upward drift
     * 4. lifeTime - Oxygen tank lifetime (seconds)
     * 5. RemoveHealthEveryLifeTime - Health removed per lifeTime cycle
     * 6. removeHealthWithCollide - Health removal per collision
     * 7. timeBetweenCollides - Collision cooldown (seconds)
     * 8. healHealthPoint - Oxygen restored by health packs
     * 9. factorForce - Amadeo device force multiplier (0 for keyboard)
     * 10. EffectiveDrainRate - Derived: RemoveHealthEveryLifeTime / lifeTime
     */
    public static readonly string[] FeatureNames =
    {
        "speed", "verticalSpeed", "idleUpwardSpeed", "lifeTime",
        "RemoveHealthEveryLifeTime", "removeHealthWithCollide",
        "timeBetweenCollides", "healHealthPoint", "factorForce", "EffectiveDrainRate"
    };

    public static int FeatureCount => FeatureNames.Length;

    /// <summary>
    /// TrialData - Complete trial information including parameters and results
    /// Used in: Trial system, Regression analysis, CSV export/import
    /// </summary>
    [Serializable]
    public class TrialData
    {
        public int trialId;
        public float speed;
        public float verticalSpeed;
        public float idleUpwardSpeed;
        public float lifeTime;
        public float RemoveHealthEveryLifeTime;  // Health removed every lifeTime cycle (not per second!)
        public float removeHealthWithCollide;
        public float timeBetweenCollides;
        public float healHealthPoint;
        public float factorForce;
        public float IsAmadeoMode; // 1 if Amadeo/Emulation mode, 0 if keyboard mode
        public float finalOxygenRemaining;
        public bool completed;
        public bool isRandomParameters; // true if loaded from random_trial.csv
        public float trialDuration; // Duration in seconds
        public int attemptNumber = 1; // Number of attempts for this trial (1 = first attempt, 2 = retry after failure, etc.)

        // Derived property: Effective drain rate (RemoveHealthEveryLifeTime / lifeTime)
        public float EffectiveDrainRate => RemoveHealthEveryLifeTime / Mathf.Max(0.1f, lifeTime);
        
        // Derived property: Effective collision damage rate (removeHealthWithCollide / timeBetweenCollides)
        public float EffectiveCollisionDamageRate => removeHealthWithCollide / Mathf.Max(0.1f, timeBetweenCollides);
    }

    /// <summary>
    /// RegressionResult - Machine learning regression analysis output
    /// Contains predictions, optimized parameters, and detailed reports
    /// Used in: TrialRegressionAlgorithm, TrialRegressionUI, TrialReportGenerator
    /// </summary>
    [Serializable]
    public class RegressionResult
    {
        public string summaryText;
        public string fullDetailsText;
        public System.Collections.Generic.Dictionary<string, float> correlations;
        public float averageOxygen;
        public TrialData optimizedSolution;  // Optimized parameters for target oxygen level
        public float optimizedSolutionError;  // Prediction error of optimized solution (%)
    }

    /// <summary>
    /// CaveInfo - Cave geometry and spatial data
    /// SHARED STRUCTURE: Used in both regular gameplay and trial mode
    /// Used in: CaveBuilder, CaveTracker, PanelOpenUp, TrialSystemManager
    /// </summary>
    [Serializable]
    public class CaveInfo
    {
        public int index;
        public float minZ;
        public float maxZ;
        public float diameter;
        public float height;
        public float length;
        public float distanceFromPrevious;
    }

    /// <summary>
    /// ParameterRanges - Valid ranges for game parameters
    /// Used for: Trial generation, Random parameter creation, Optimization constraints
    /// Used in: TrialParameterManager, DifficultyParameterSolver, FeatureExtractor
    /// </summary>
    [Serializable]
    public class ParameterRanges
    {
        [Header("Movement Parameters")]
        
        public Vector2 speedRange = new Vector2(10f, 40f);
        public Vector2 verticalSpeedRange = new Vector2(15f, 45f);
        public Vector2 idleUpwardSpeedRange = new Vector2(0.01f, 3f);  // Changed: 5->3 (more realistic)

        [Header("Health Parameters")]
        public Vector2 healHealthPointRange = new Vector2(3f, 15f);
        public Vector2 timeBetweenCollidesRange = new Vector2(1f, 5f);
        public Vector2 removeHealthWithCollideRange = new Vector2(5f, 20f);  // Changed: 15->20 (more damage for difficulty)
        public Vector2 RemoveHealthEveryLifeTimeRange = new Vector2(0.5f, 7f);  // Health removed every lifeTime cycle
        public Vector2 lifeTimeRange = new Vector2(0.5f, 4f);
        
        [Header("Force Multiplication")]
        [Tooltip("Factor for Amadeo device force multiplication (0 for keyboard)")]
        public Vector2 factorForceRange = new Vector2(0.5f, 5f);  // Factor for force multiplication
    }
}

