using UnityEngine;
using System;


/** 
    * TrialDataModels
    * 
    * This file contains data models for trial data, cave information, and parameter ranges.
    */
public static class TrialDataModels
{

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

        // Derived property: Effective drain rate (RemoveHealthEveryLifeTime / lifeTime)
        // if is higher 
        public float EffectiveDrainRate => RemoveHealthEveryLifeTime / Mathf.Max(0.1f, lifeTime);
        
    }

 
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


    [Serializable]
    public class ParameterRanges
    {
        [Header("Movement Parameters")]
        public Vector2 speedRange = new Vector2(10f, 40f);
        public Vector2 verticalSpeedRange = new Vector2(15f, 45f);
        public Vector2 idleUpwardSpeedRange = new Vector2(0.01f, 3f);  // Changed: 5→3 (more realistic)

        [Header("Health Parameters")]
        public Vector2 healHealthPointRange = new Vector2(3f, 15f);
        public Vector2 timeBetweenCollidesRange = new Vector2(1f, 5f);
        public Vector2 removeHealthWithCollideRange = new Vector2(5f, 20f);  // Changed: 15→20 (more damage for difficulty)
        public Vector2 RemoveHealthEveryLifeTimeRange = new Vector2(0.5f, 7f);  // Health removed every lifeTime cycle
        public Vector2 lifeTimeRange = new Vector2(0.5f, 4f);
        
        [Header("Force Multiplication")]
        public Vector2 factorForceRange = new Vector2(0.5f, 5f);  // Factor for force multiplication
    }
}

