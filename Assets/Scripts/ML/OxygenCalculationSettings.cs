using UnityEngine;
using System.Linq;

/// <summary>
/// Settings for how to calculate oxygen from CSV data
/// Attach this to a GameObject to configure oxygen calculation
/// </summary>
public class OxygenCalculationSettings : MonoBehaviour
{
    [Header("Oxygen Calculation Method")]
    [Tooltip("How to calculate final oxygen from multiple runs in CSV")]
    public OxygenCalculationMode calculationMode = OxygenCalculationMode.Average;
    
    [Header("Specific Column (if SpecificColumn mode)")]
    [Tooltip("Column name to use (e.g., 'o2_run7', 'o2_run1')")]
    public string specificColumnName = "o2_run7";
    
    [Header("Info")]
    [TextArea(3, 10)]
    public string info = 
        "Average: average of all runs\n" +
        "LastRun: last run only (o2_run7)\n" +
        "FirstRun: first run only (o2_run1)\n" +
        "Minimum: minimum oxygen (worst case)\n" +
        "Maximum: maximum oxygen (best case)\n" +
        "Median: median value\n" +
        "SpecificColumn: specific column by name (set in specificColumnName)";
    
    public static OxygenCalculationSettings Instance { get; private set; }
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    void Start()
    {
        Debug.Log($"[OxygenCalculation] Mode: {calculationMode}" + 
                  (calculationMode == OxygenCalculationMode.SpecificColumn ? $" ({specificColumnName})" : ""));
    }
    
    /// <summary>
    /// Calculate oxygen based on settings
    /// </summary>
    public float CalculateOxygen(float[] values, string[] columnNames = null)
    {
        if (values == null || values.Length == 0)
        {
            Debug.LogWarning("[OxygenCalculation] No values to calculate");
            return 0f;
        }
        
        switch (calculationMode)
        {
            case OxygenCalculationMode.Average:
                return values.Average();
            
            case OxygenCalculationMode.LastRun:
                return values[values.Length - 1];
            
            case OxygenCalculationMode.FirstRun:
                return values[0];
            
            case OxygenCalculationMode.Minimum:
                return values.Min();
            
            case OxygenCalculationMode.Maximum:
                return values.Max();
            
            case OxygenCalculationMode.Median:
                float[] sorted = new float[values.Length];
                System.Array.Copy(values, sorted, values.Length);
                System.Array.Sort(sorted);
                int mid = sorted.Length / 2;
                if (sorted.Length % 2 == 0)
                    return (sorted[mid - 1] + sorted[mid]) / 2f;
                return sorted[mid];
            
            case OxygenCalculationMode.SpecificColumn:
                if (columnNames != null && !string.IsNullOrEmpty(specificColumnName))
                {
                    for (int i = 0; i < columnNames.Length && i < values.Length; i++)
                    {
                        if (columnNames[i].Trim().Equals(specificColumnName, System.StringComparison.OrdinalIgnoreCase))
                        {
                            Debug.Log($"[OxygenCalculation] Using specific column: {specificColumnName} = {values[i]}");
                            return values[i];
                        }
                    }
                    Debug.LogWarning($"[OxygenCalculation] Column '{specificColumnName}' not found, using average");
                }
                return values.Average();
            
            default:
                return values.Average();
        }
    }
}

// Extension methods
public static class FloatArrayExtensions
{
    public static float Average(this float[] array)
    {
        if (array == null || array.Length == 0) return 0f;
        float sum = 0f;
        foreach (float f in array) sum += f;
        return sum / array.Length;
    }
    
    public static float Min(this float[] array)
    {
        if (array == null || array.Length == 0) return 0f;
        float min = array[0];
        for (int i = 1; i < array.Length; i++)
            if (array[i] < min) min = array[i];
        return min;
    }
    
    public static float Max(this float[] array)
    {
        if (array == null || array.Length == 0) return 0f;
        float max = array[0];
        for (int i = 1; i < array.Length; i++)
            if (array[i] > max) max = array[i];
        return max;
    }
}

