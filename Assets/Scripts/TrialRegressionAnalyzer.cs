using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Globalization;
using System;

/**


*/
public class TrialRegressionAnalyzer : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject regressionPanel;
    [SerializeField] private TMP_Text regressionResultsText;
    [SerializeField] private Button calculateRegressionButton;
    [SerializeField] private Button closeRegressionButton;
    [SerializeField] private Button saveResultsButton;
    
    [Header("Data Files")]
    [SerializeField] private TextAsset trialDataCSV;
    
    [Header("Save Settings")]
    [SerializeField] private bool autoSaveResults = true;
    [SerializeField] private string saveFolder = "RegressionResults";
    
    private string lastRegressionResults = "";
    
    [System.Serializable]
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
    
    private List<TrialData> allTrialData = new List<TrialData>();
    
    void Start()
    {
        if (calculateRegressionButton != null)
            calculateRegressionButton.onClick.AddListener(CalculateRegression);
        
        if (closeRegressionButton != null)
            closeRegressionButton.onClick.AddListener(CloseRegressionPanel);
        
        if (saveResultsButton != null)
            saveResultsButton.onClick.AddListener(SaveRegressionResults);
        
        if (regressionPanel != null)
            regressionPanel.SetActive(false);
    }
    
    public void CalculateRegression()
    {
        
        
        if (LoadTrialDataFromCSV())
        {
            AppendOxygenWideToMaster(allTrialData);
            
            string regressionResults = PerformRegressionAnalysis();
            lastRegressionResults = regressionResults;
            ShowRegressionResults(regressionResults);
            
            if (autoSaveResults)
                SaveRegressionResults();
        }
        else
        {
            ShowError("Failed to load trial data!");
        }
    }
    
    public void CalculateAndShowRegression() => CalculateRegression();
    
    private bool LoadTrialDataFromCSV()
    {
        try
        {
            allTrialData.Clear();
            
            if (trialDataCSV == null)
            {
                Debug.LogError("Trial data TextAsset not assigned!");
                return false;
            }
            
            string csvText = trialDataCSV.text;
            string[] lines = csvText.Split(new[] { "\r\n", "\r", "\n" }, System.StringSplitOptions.RemoveEmptyEntries);
            
            if (lines.Length <= 1) return false;
            
            for (int i = 1; i < lines.Length; i++)
            {
                string[] fields = lines[i].Split(',');
                if (fields.Length < 11) continue;
                
                if (string.IsNullOrEmpty(fields[10]) || !float.TryParse(fields[10], out float finalOxygen))
                    continue;
                
                var trialData = new TrialData
                {
                    trialId = int.Parse(fields[0]),
                    speed = float.Parse(fields[1]),
                    verticalSpeed = float.Parse(fields[2]),
                    idleUpwardSpeed = float.Parse(fields[3]),
                    lifeTime = float.Parse(fields[4]),
                    downHealthPairSec = float.Parse(fields[5]),
                    removeHealthWithCollide = float.Parse(fields[6]),
                    timeBetweenCollides = float.Parse(fields[7]),
                    healHealthPoint = float.Parse(fields[8]),
                    factorForce = float.Parse(fields[9]),
                    finalOxygenRemaining = finalOxygen,
                    completed = finalOxygen > 0
                };
                
                allTrialData.Add(trialData);
            }
            
            Debug.Log($"Loaded {allTrialData.Count} trials");
            return allTrialData.Count >= 2;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error loading CSV: {e.Message}");
            return false;
        }
    }
    
    private string PerformRegressionAnalysis()
    {
        float[] outputs = allTrialData.Select(t => t.finalOxygenRemaining).ToArray();
        
        var correlations = new Dictionary<string, float>();
        
        correlations["Speed"] = CalculateCorrelation(
            allTrialData.Select(t => t.speed).ToArray(), outputs);
        correlations["VerticalSpeed"] = CalculateCorrelation(
            allTrialData.Select(t => t.verticalSpeed).ToArray(), outputs);
        correlations["IdleUpwardSpeed"] = CalculateCorrelation(
            allTrialData.Select(t => t.idleUpwardSpeed).ToArray(), outputs);
        correlations["LifeTime"] = CalculateCorrelation(
            allTrialData.Select(t => t.lifeTime).ToArray(), outputs);
        correlations["O2DropPerSec"] = CalculateCorrelation(
            allTrialData.Select(t => t.downHealthPairSec).ToArray(), outputs);
        correlations["CollisionDamage"] = CalculateCorrelation(
            allTrialData.Select(t => t.removeHealthWithCollide).ToArray(), outputs);
        correlations["TimeBetweenCollides"] = CalculateCorrelation(
            allTrialData.Select(t => t.timeBetweenCollides).ToArray(), outputs);
        correlations["HealPoints"] = CalculateCorrelation(
            allTrialData.Select(t => t.healHealthPoint).ToArray(), outputs);
        correlations["FactorForce"] = CalculateCorrelation(
            allTrialData.Select(t => t.factorForce).ToArray(), outputs);
 
        string results = "REGRESSION ANALYSIS\n";
        
        float totalOxygen = 0f;
        int perfectTrials = 0;
        int failedTrials = 0;
        
        foreach (var trial in allTrialData)
        {
            totalOxygen += trial.finalOxygenRemaining;
            if (trial.finalOxygenRemaining <= 5f && trial.finalOxygenRemaining > 0f)
                perfectTrials++;
            if (trial.finalOxygenRemaining <= 0f)
                failedTrials++;
        }
        
        float avgOxygen = totalOxygen / allTrialData.Count;
        results += $"Trials:{allTrialData.Count} Avg:{avgOxygen:F1}% Perfect:{perfectTrials} Failed:{failedTrials}\n";
        results += "TOP CORRELATIONS:\n";
 
        var top3 = correlations.OrderByDescending(x => Mathf.Abs(x.Value)).Take(3);
        foreach (var corr in top3)
        {
            string sign = corr.Value > 0 ? "+" : "";
            results += $"{sign}{corr.Value:F2} {corr.Key}\n";
        }
        
        results += "\nRECOMMENDATIONS:\n";
        
        var mostPositive = correlations.OrderByDescending(x => x.Value).First();
        var mostNegative = correlations.OrderBy(x => x.Value).First();
        
        if (Mathf.Abs(mostPositive.Value) > 0.3f)
        {
            results += $"INCREASE {mostPositive.Key}\n";
        }
        if (Mathf.Abs(mostNegative.Value) > 0.3f)
        {
            results += $"DECREASE {mostNegative.Key}\n";
        }
        
        if (avgOxygen > 20f)
        {
            results += "\nStatus: TOO EASY - increase difficulty\n";
        }
        else if (failedTrials > allTrialData.Count / 2)
        {
            results += "\nStatus: TOO HARD - decrease difficulty\n";
        }
        else if (perfectTrials >= allTrialData.Count / 2)
        {
            results += "\nStatus: WELL CALIBRATED\n";
        }
        
        Debug.Log(results);
        return results;
    }
    
    private float CalculateCorrelation(float[] x, float[] y)
    {
        if (x.Length != y.Length || x.Length == 0) return 0f;
        
        int n = x.Length;
        float sumX = x.Sum();
        float sumY = y.Sum();
        float sumXY = 0f;
        float sumX2 = 0f;
        float sumY2 = 0f;
        
        for (int i = 0; i < n; i++)
        {
            sumXY += x[i] * y[i];
            sumX2 += x[i] * x[i];
            sumY2 += y[i] * y[i];
        }
        
        float numerator = n * sumXY - sumX * sumY;
        float denominator = Mathf.Sqrt((n * sumX2 - sumX * sumX) * (n * sumY2 - sumY * sumY));
        
        return denominator == 0f ? 0f : numerator / denominator;
    }
    
    private void ShowRegressionResults(string results)
    {
        if (regressionPanel != null)
        {
            regressionPanel.SetActive(true);
            // Ensure regression panel is on top
            regressionPanel.transform.SetAsLastSibling();
        }
        
        if (regressionResultsText != null)
            regressionResultsText.text = results;
        
        Debug.Log(results);
    }
    
    private void ShowError(string errorMessage)
    {
        if (regressionPanel != null)
            regressionPanel.SetActive(true);
        
        if (regressionResultsText != null)
            regressionResultsText.text = $"ERROR:\n{errorMessage}";
        
        Debug.LogError(errorMessage);
    }
    
    public void CloseRegressionPanel()
    {
        if (regressionPanel != null)
            regressionPanel.SetActive(false);
        
        var trialUIController = FindObjectOfType<TrialUIController>();
        if (trialUIController != null)
            trialUIController.OpenTrialControlPanel();
        
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    
    /// <summary>
    /// Force close regression panel without opening trial panel (used when starting new game/trial)
    /// </summary>
    public void ForceCloseRegressionPanel()
    {
        if (regressionPanel != null && regressionPanel.activeSelf)
        {
            regressionPanel.SetActive(false);
        }
    }
    
    public bool CanCalculateRegression()
    {
        return LoadTrialDataFromCSV() && allTrialData.Count >= 2;
    }
    
    public void SaveRegressionResults()
    {
        if (string.IsNullOrEmpty(lastRegressionResults))
        {
            Debug.LogWarning("No results to save!");
            return;
        }
        
        try
        {
            string dataPath = Path.Combine(Application.dataPath, "Data");
            string savePath = Path.Combine(dataPath, saveFolder);
            
            if (!Directory.Exists(savePath))
                Directory.CreateDirectory(savePath);
            
            string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string fileName = $"RegressionAnalysis_{timestamp}.txt";
            string fullPath = Path.Combine(savePath, fileName);
            
            string fileContent = "=====================================\n";
            fileContent += "REGRESSION ANALYSIS\n";
            fileContent += "=====================================\n";
            fileContent += $"Generated: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\n";
            fileContent += $"Trials analyzed: {allTrialData.Count}\n";
            fileContent += "=====================================\n\n";
            fileContent += lastRegressionResults;
            fileContent += "\n\n=====================================\n";
            fileContent += "RAW TRIAL DATA:\n";
            
            foreach (var trial in allTrialData)
            {
                fileContent += $"\nTrial {trial.trialId}:\n";
                fileContent += $"  Speed: {trial.speed:F2}\n";
                fileContent += $"  VerticalSpeed: {trial.verticalSpeed:F2}\n";
                fileContent += $"  IdleUpwardSpeed: {trial.idleUpwardSpeed:F2}\n";
                fileContent += $"  LifeTime: {trial.lifeTime:F2}\n";
                fileContent += $"  O2DropPerSec: {trial.downHealthPairSec:F2}\n";
                fileContent += $"  CollisionDamage: {trial.removeHealthWithCollide:F2}\n";
                fileContent += $"  TimeBetweenCollides: {trial.timeBetweenCollides:F2}\n";
                fileContent += $"  HealPoints: {trial.healHealthPoint:F2}\n";
                fileContent += $"  FactorForce: {trial.factorForce:F2}\n";
                fileContent += $"  FinalO2: {trial.finalOxygenRemaining:F1}%\n";
            }
            
            File.WriteAllText(fullPath, fileContent);
            Debug.Log($"Results saved: {fullPath}");
            
            if (regressionResultsText != null)
                regressionResultsText.text += $"\n\nSaved: {fileName}";
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Save failed: {e.Message}");
        }
    }
    
    private string GetWritableDataDir()
    {
        #if UNITY_EDITOR
        string dir = Path.Combine(Application.dataPath, "Data", "RegressionResults");
        #else
        string dir = Application.persistentDataPath;
        #endif
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        return dir;
    }
    
    private void AppendOxygenWideToMaster(List<TrialData> trials, string masterFileName = "O2_Wide_AllSets.csv")
    {
        if (trials == null || trials.Count == 0) return;
        
        string dir = GetWritableDataDir();
        string path = Path.Combine(dir, masterFileName);
        
        var values = trials
            .OrderBy(t => t.trialId)
            .Select(t => t.finalOxygenRemaining.ToString("0.###", CultureInfo.InvariantCulture))
            .ToList();
        
        var header = new List<string> { "timestamp" };
        for (int i = 0; i < values.Count; i++)
            header.Add($"o2_remaining_{i+1}");
        
        var newRow = new List<string>();
        newRow.Add(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        newRow.AddRange(values);
        
        if (!File.Exists(path))
        {
            File.WriteAllLines(path, new[]
            {
                string.Join(",", header),
                string.Join(",", newRow)
            });
            Debug.Log($"Created master wide file: {path}");
            return;
        }
        
        var allLines = File.ReadAllLines(path).ToList();
        if (allLines.Count == 0)
            allLines.Add(string.Join(",", header));
        
        var existingHeader = allLines[0].Split(',').ToList();
        int existingO2Cols = Math.Max(0, existingHeader.Count - 1);
        int neededCols = values.Count;
        
        if (existingO2Cols < neededCols)
        {
            for (int i = existingO2Cols; i < neededCols; i++)
                existingHeader.Add($"o2_remaining_{i+1}");
            
            for (int i = 1; i < allLines.Count; i++)
            {
                var parts = allLines[i].Split(',').ToList();
                while (parts.Count < existingHeader.Count)
                    parts.Add(string.Empty);
                allLines[i] = string.Join(",", parts);
            }
            
            allLines[0] = string.Join(",", existingHeader);
        }
        
        while (newRow.Count < existingHeader.Count)
            newRow.Add(string.Empty);
        
        allLines.Add(string.Join(",", newRow));
        File.WriteAllLines(path, allLines);
        
        //Debug.Log($"Appended set to master wide file: {path}");
    }
}

