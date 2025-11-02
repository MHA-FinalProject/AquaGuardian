using UnityEngine;
using System.Linq;

/// <summary>
/// Calculation modes for combining multiple oxygen runs
/// </summary>
public enum OxygenCalculationMode
{
    Average,         // Average of all runs
    LastRun,         // Use only the last run
    FirstRun,        // Use only the first run
    Minimum,         // Take minimum value
    Maximum,         // Take maximum value
    Median,          // Take median value
    SpecificColumn   // Use a specific column name
}

/// <summary>
/// Settings for how to calculate final oxygen from multiple CSV runs
/// </summary>
public class OxygenCalculationSettings : MonoBehaviour
{
    [Header("Oxygen Calculation Method")]
    [Tooltip("How to calculate final oxygen from multiple runs in CSV")]
    public OxygenCalculationMode calculationMode = OxygenCalculationMode.Average;
    
    [Header("Specific Column (if SpecificColumn mode)")]
    [Tooltip("Column name to use (e.g., 'o2_run7', 'o2_run1')")]
    public string specificColumnName = "o2_run7";
    
    public static OxygenCalculationSettings Instance { get; private set; }
    
    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }
    
    public float CalculateOxygen(float[] values, string[] columnNames = null)
    {
        if (values == null || values.Length == 0) return 0f;
        
        switch (calculationMode)
        {
            case OxygenCalculationMode.LastRun: return values[values.Length - 1];
            case OxygenCalculationMode.FirstRun: return values[0];
            case OxygenCalculationMode.Minimum: return values.Min();
            case OxygenCalculationMode.Maximum: return values.Max();
            case OxygenCalculationMode.Median:
                var sorted = values.OrderBy(v => v).ToArray();
                int mid = sorted.Length / 2;
                return sorted.Length % 2 == 0 ? (sorted[mid - 1] + sorted[mid]) / 2f : sorted[mid];
            case OxygenCalculationMode.SpecificColumn:
                if (columnNames != null && !string.IsNullOrEmpty(specificColumnName))
                {
                    for (int i = 0; i < columnNames.Length && i < values.Length; i++)
                        if (columnNames[i].Trim().Equals(specificColumnName, System.StringComparison.OrdinalIgnoreCase))
                            return values[i];
                    Debug.LogWarning($"Column '{specificColumnName}' not found, using average");
                }
                return values.Average();
            default: return values.Average();
        }
    }
}