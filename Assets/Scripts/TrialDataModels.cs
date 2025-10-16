using UnityEngine;
using System;

/// <summary>
/// Shared data models for trial system
/// Centralized location for all trial-related data structures
/// Extracted from PanelOpenUp.cs for better code organization and reusability
/// </summary>
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
        public float downHealthPairSec;
        public float removeHealthWithCollide;
        public float timeBetweenCollides;
        public float healHealthPoint;
        public float factorForce;
        public float finalOxygenRemaining;
        public bool completed;
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
        public Vector2 speedRange = new Vector2(10f, 25f);
        public Vector2 verticalSpeedRange = new Vector2(15f, 40f);
        public Vector2 idleUpwardSpeedRange = new Vector2(0.5f, 2f);

        [Header("Health Parameters")]
        public Vector2 oxygenHealRange = new Vector2(3f, 15f);
        public Vector2 timeBetweenCollidesRange = new Vector2(1f, 5f);
        public Vector2 collisionDamageRange = new Vector2(5f, 15f);
        public Vector2 oxygenDropPerSecRange = new Vector2(0.5f, 2f);
        public Vector2 lifeTimeRange = new Vector2(0.8f, 3f);
    }
}

