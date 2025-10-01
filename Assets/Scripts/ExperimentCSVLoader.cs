using UnityEngine;
using System.IO;

/**
 * Helper class to load experiment CSV files dynamically
 * Integrates with PanelOpenUp to support automated experiments
 */
public class ExperimentCSVLoader : MonoBehaviour
{
    [Header("CSV Loading Settings")]
    [SerializeField] private bool useExperimentCSV = false;
    [SerializeField] private string experimentCSVFileName = "Trial_5_runs_.csv";
    
    private PanelOpenUp panelOpenUp;
    
    void Start()
    {
        panelOpenUp = GetComponent<PanelOpenUp>();
        if (panelOpenUp == null)
        {
            Debug.LogError("ExperimentCSVLoader: PanelOpenUp component not found!");
        }
    }
    
    /// <summary>
    /// Load experiment CSV if it exists, otherwise use default
    /// </summary>
    public bool TryLoadExperimentCSV()
    {
        if (!useExperimentCSV) return false;
        
        string csvPath = Path.Combine(Application.dataPath, "Data", experimentCSVFileName);
        
        if (File.Exists(csvPath))
        {
            try
            {
                string csvContent = File.ReadAllText(csvPath);
                
                // Create a temporary TextAsset-like object
                if (panelOpenUp != null)
                {
                    // Use reflection to set the CSV content
                    var panelType = typeof(PanelOpenUp);
                    var linesField = panelType.GetField("lines", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var numOfLinesField = panelType.GetField("numOfLines", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (linesField != null && numOfLinesField != null)
                    {
                        string[] lines = csvContent.Split(new[] { "\r\n", "\r", "\n" }, System.StringSplitOptions.RemoveEmptyEntries);
                        linesField.SetValue(panelOpenUp, lines);
                        numOfLinesField.SetValue(panelOpenUp, lines.Length);
                        
                        Debug.Log($"ExperimentCSVLoader: Loaded experiment CSV with {lines.Length} lines from {csvPath}");
                        return true;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"ExperimentCSVLoader: Failed to load experiment CSV: {e.Message}");
            }
        }
        
        Debug.Log($"ExperimentCSVLoader: Experiment CSV not found at {csvPath}, using default");
        return false;
    }
    
    /// <summary>
    /// Enable experiment CSV loading
    /// </summary>
    public void EnableExperimentMode()
    {
        useExperimentCSV = true;
        Debug.Log("ExperimentCSVLoader: Experiment mode enabled");
    }
    
    /// <summary>
    /// Disable experiment CSV loading
    /// </summary>
    public void DisableExperimentMode()
    {
        useExperimentCSV = false;
        Debug.Log("ExperimentCSVLoader: Experiment mode disabled");
    }
    
    /// <summary>
    /// Check if experiment CSV exists
    /// </summary>
    public bool DoesExperimentCSVExist()
    {
        string csvPath = Path.Combine(Application.dataPath, "Data", experimentCSVFileName);
        return File.Exists(csvPath);
    }
    
    /// <summary>
    /// Get the path to experiment CSV
    /// </summary>
    public string GetExperimentCSVPath()
    {
        return Path.Combine(Application.dataPath, "Data", experimentCSVFileName);
    }
    
    /// <summary>
    /// Force reload of the CSV data
    /// </summary>
    public void ReloadCSV()
    {
        if (TryLoadExperimentCSV())
        {
            Debug.Log("ExperimentCSVLoader: Successfully reloaded experiment CSV");
        }
        else
        {
            Debug.Log("ExperimentCSVLoader: Using default CSV file");
        }
    }
}






