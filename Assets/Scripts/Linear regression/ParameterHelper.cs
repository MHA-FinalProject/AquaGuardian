using UnityEngine;

/// <summary>
/// Unified access to TrialData parameters by index
/// Centralizes all get/set/range/validation operations
/// </summary>
public static class ParameterHelper
{
    /// <summary>
    /// Get parameter value from TrialData by index
    /// </summary>
    public static float Get(TrialDataModels.TrialData d, int j) => j switch
    {
        0 => d.speed,
        1 => d.verticalSpeed,
        2 => d.idleUpwardSpeed,
        3 => d.lifeTime,
        4 => d.RemoveHealthEveryLifeTime,
        5 => d.removeHealthWithCollide,
        6 => d.timeBetweenCollides,
        7 => d.healHealthPoint,
        8 => d.factorForce,
        9 => d.EffectiveDrainRate,
        _ => 0f
    };

    /// <summary>
    /// Set parameter value in TrialData by index
    /// Note: EffectiveDrainRate (index 9) is derived and cannot be set
    /// </summary>
    public static void Set(ref TrialDataModels.TrialData d, int j, float v)
    {
        switch (j)
        {
            case 0: d.speed = v; break;
            case 1: d.verticalSpeed = v; break;
            case 2: d.idleUpwardSpeed = v; break;
            case 3: d.lifeTime = v; break;
            case 4: d.RemoveHealthEveryLifeTime = v; break;
            case 5: d.removeHealthWithCollide = v; break;
            case 6: d.timeBetweenCollides = v; break;
            case 7: d.healHealthPoint = v; break;
            case 8: d.factorForce = v; break;
            case 9: break; // EffectiveDrainRate is derived
        }
    }

    /// <summary>
    /// Get valid range for parameter by index
    /// </summary>
    public static (float min, float max) Range(TrialDataModels.ParameterRanges r, int i) => i switch
    {
        0 => (r.speedRange.x, r.speedRange.y),
        1 => (r.verticalSpeedRange.x, r.verticalSpeedRange.y),
        2 => (r.idleUpwardSpeedRange.x, r.idleUpwardSpeedRange.y),
        3 => (r.lifeTimeRange.x, r.lifeTimeRange.y),
        4 => (r.RemoveHealthEveryLifeTimeRange.x, r.RemoveHealthEveryLifeTimeRange.y),
        5 => (r.removeHealthWithCollideRange.x, r.removeHealthWithCollideRange.y),
        6 => (r.timeBetweenCollidesRange.x, r.timeBetweenCollidesRange.y),
        7 => (r.healHealthPointRange.x, r.healHealthPointRange.y),
        8 => (r.factorForceRange.x, r.factorForceRange.y),
        9 => (r.RemoveHealthEveryLifeTimeRange.x / r.lifeTimeRange.y, 
              r.RemoveHealthEveryLifeTimeRange.y / r.lifeTimeRange.x), // EffectiveDrainRate range
        _ => (0f, 100f)
    };

    /// <summary>
    /// Check if parameter has room to move within its range (not at boundaries)
    /// </summary>
    public static bool HasHeadroom(int idx, TrialDataModels.ParameterRanges r, TrialDataModels.TrialData p, float threshold = 0.1f)
    {
        var (min, max) = Range(r, idx);
        float v = Get(p, idx);
        float range = max - min;
        float marginMin = min + range * threshold;
        float marginMax = max - range * threshold;
        return v > marginMin && v < marginMax;
    }

    /// <summary>
    /// Clamp parameter value to valid range
    /// </summary>
    public static float Clamp(int j, float raw, Vector2[] bounds)
    {
        return Mathf.Clamp(raw, bounds[j].x, bounds[j].y);
    }

    /// <summary>
    /// Deep clone TrialData parameters
    /// </summary>
    public static TrialDataModels.TrialData Clone(TrialDataModels.TrialData s) => new()
    {
        speed = s.speed,
        verticalSpeed = s.verticalSpeed,
        idleUpwardSpeed = s.idleUpwardSpeed,
        lifeTime = s.lifeTime,
        RemoveHealthEveryLifeTime = s.RemoveHealthEveryLifeTime,
        removeHealthWithCollide = s.removeHealthWithCollide,
        timeBetweenCollides = s.timeBetweenCollides,
        healHealthPoint = s.healHealthPoint,
        factorForce = s.factorForce,
        IsAmadeoMode = s.IsAmadeoMode
    };
}

