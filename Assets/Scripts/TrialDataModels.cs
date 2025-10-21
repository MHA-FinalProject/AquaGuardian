using UnityEngine;
using System;


/**  TrialDataModels
  * This static class contains data models used in the trial system,
  * including TrialData, CaveInfo, and ParameterRanges.
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
        public float downHealthPairSec;
        public float removeHealthWithCollide;
        public float timeBetweenCollides;
        public float healHealthPoint;
        public float factorForce;
        public float finalOxygenRemaining;
        public bool completed;
        public bool isRandomParameters; // true if loaded from random_trial.csv
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
        public Vector2 verticalSpeedRange = new Vector2(15f, 45f);
        public Vector2 idleUpwardSpeedRange = new Vector2(0.01f, 2f);

        [Header("Health Parameters")]
        public Vector2 healHealthPointRange = new Vector2(3f, 15f);
        public Vector2 timeBetweenCollidesRange = new Vector2(1f, 5f);
        public Vector2 removeHealthWithCollideRange = new Vector2(5f, 15f);
        public Vector2 downHealthPairSecRange = new Vector2(0.5f, 3.5f);
        public Vector2 lifeTimeRange = new Vector2(0.8f, 3f);
    }
}

