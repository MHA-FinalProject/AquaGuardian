using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections.Generic;
using System.Globalization;

/**
 *  The panel that opens up when we start the game. 
 *  It allows the physician to set all the parameters of the game, e.g. force factor, fingers, etc.
 *  
 *  Note: Detailed difficulty analysis and CSV export are handled by CaveTracker and ReportExporter.
 */
public partial class PanelOpenUp : MonoBehaviour
{

    [Header("Amadeo Client and UI Components")]
    [SerializeField] private AmadeoClient _client;  // Reference to the AmadeoClient
    public GameObject Panel;  // Reference to a UI panel
                              

    [Header("Game Objects")]
    public GameObject caveObject = null;     // Reference to the cave object, that is being scaled
    public GameObject oxygenObject = null;   // Reference to the oxygen object
    public GameObject wall = null;           // Reference to the wall object
    public GameObject arrows = null;         // Reference to the arrows object
    public GameObject chest = null;          // Reference to the chest object

    // === Game Settings ===
    [Header("Game Settings")]

    private int numOfLines = 0;  // Internal counter for lines in the CSV

    [SerializeField] private TextAsset csvFile; // Will show file selection in inspector
    private string[] lines = null;  // Array for storing lines from the CSV file


    [Header("Component References")]
    [SerializeField] private LevelProgressUI levelProgressUI;  // Reference to the LevelProgressUI component
    [SerializeField] private PlayerLife playerLife;  // Reference to the PlayerLife component for handling player health
    [SerializeField] private Health health;  // Reference to the Health component to manage player health
    
    // === Cave Bounds for Tracking ===
    [System.Serializable]
    public class CaveInfo
    {
        public int index;
        public float minZ;
        public float maxZ;
        // Optional geometry for reporting/analysis
        public float diameter;
        public float height;
        public float length;
        public float difficulty;
        public float distanceFromPrevious;
    }

    // Populated during ClosePanel() alongside cave instantiation
    public List<CaveInfo> caveInfos = new List<CaveInfo>();

    // === Simple Performance Tracking ===
    [Header("Performance Tracking")]
   // [SerializeField] private bool enablePerformanceTracking = true;
    [SerializeField] private bool enableDifficultyAnalysis = true;
    
    // Difficulty tracking
    private float levelDifficultyScore = 0f;
    
    // Cave difficulty data for CaveTracker
    [System.Serializable]
    public class CaveDifficultyData
    {
        public int caveIndex;
        public float difficultyScore;
        public float distanceFromPrevious;
    }
    
    public List<CaveDifficultyData> caveDifficultyList = new List<CaveDifficultyData>();

    [Header("Pivot and Position Settings")]
    private const int pivotChest = 75;        // Distance (in minus z direction) from last cave to treasure chest
    private const float chestX = 291.774f;    // X position for the chest (note: Y position for the chest is the Y position of the last cave).
    private const float generalPivot = 50f;   // Distance (in minus z direction) from current cave to next wall / oxygen / arrows.
    private const float pivotCavePlace = 70;  // Distance (in minus z direction) from current wall to next cave.
    private const float pivotArrowsToWall = 45f;  // Pivot distance between arrows and walls


    void Start()
    {
        if (csvFile != null)
        {
            ReadCSVFromTextAsset();
        }
        else
        {
            Debug.LogError("No CSV file assigned! Please assign a CSV file in the inspector.");
        }
    }

    void ReadCSVFromTextAsset()
    {
        try
        {
            string csvText = csvFile.text;
            lines = csvText.Split(new[] { "\r\n", "\r", "\n" }, System.StringSplitOptions.RemoveEmptyEntries);
            numOfLines = lines.Length;

            Debug.Log("=== CSV FILE LOADED ===");
            Debug.Log($"Number of caves from file: {numOfLines}");

            foreach (string line in lines)
            {
                /*Debug.Log(line);*/ // Prints each line of the CSV file

                // Split the line into fields based on the comma delimiter
                string[] fields = line.Split(',');

                // Process the fields as needed
                foreach (string field in fields)
                {
                    /*Debug.Log(field);*/ // Prints each field in the current line
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error reading CSV file: {e.Message}");
        }
    }

    // The function handles closing a panel and creates objects according to the data read from a CSV file.
    // The function places the objects in the game world and updates their size and position based on the data in the file.
    public void ClosePanel()
    {
        if (Panel != null)
        {
            Panel.SetActive(false);
            /*Debug.Log("num_caves_from_user in ClosePanel: " + numOfLines);*/
            Vector3 currentPositionCave = caveObject.transform.position;
            Vector3 currentPositionOxygen = oxygenObject.transform.position;
            Vector3 currentPositionWall = wall.transform.position;
            Vector3 currentPositionArrows = arrows.transform.position;

            Vector3 newCavePosition = new Vector3(currentPositionCave.x, currentPositionCave.y, currentPositionCave.z);
            Vector3 newOxygenPosition;
            Vector3 newWallPosition;
            Vector3 newArrowsPosition;

            Vector3 currentCaveScale = caveObject.transform.localScale;
            Vector3 newCaveScale = new Vector3(currentCaveScale.x, currentCaveScale.y, currentCaveScale.z);

            // Add the first cave (index 0) which already exists in the scene
            if (numOfLines > 0)
            {
                // Get bounds of the existing cave object
                float minZ = currentPositionCave.z;
                float maxZ = currentPositionCave.z;
                var existingRend = caveObject.GetComponentInChildren<Renderer>();
                if (existingRend != null)
                {
                    var b = existingRend.bounds;
                    minZ = b.min.z;
                    maxZ = b.max.z;
                }
                else
                {
                    var existingCol = caveObject.GetComponentInChildren<Collider>();
                    if (existingCol != null)
                    {
                        var b = existingCol.bounds;
                        minZ = b.min.z;
                        maxZ = b.max.z;
                    }
                    else
                    {
                        // Fallback: approximate by scale length on Z
                        float half = caveObject.transform.localScale.z * 0.5f;
                        minZ = currentPositionCave.z - half;
                        maxZ = currentPositionCave.z + half;
                    }
                }
                
                // Populate geometry for the first (existing) cave from localScale directly
                float firstDiameter = currentCaveScale.x; // X = diameter
                float firstHeightOffset = currentCaveScale.y; // Y = height
                float firstLength = currentCaveScale.z; // Z = length

                float firstDifficulty = CalculateCaveDifficulty(firstDiameter, firstHeightOffset, firstLength);

                caveInfos.Add(new CaveInfo {
                    index = 1,
                    minZ = minZ,
                    maxZ = maxZ,
                    diameter = firstDiameter,
                    height = firstHeightOffset,
                    length = firstLength,
                    difficulty = firstDifficulty,
                    distanceFromPrevious = 0f
                });
                caveDifficultyList.Add(new CaveDifficultyData { caveIndex = 1, difficultyScore = firstDifficulty, distanceFromPrevious = 0f });
                levelDifficultyScore += firstDifficulty;
                Debug.Log($"Cave 1 (existing) bounds: Z[{minZ:F1},{maxZ:F1}] at position {currentPositionCave.z:F1}, geom(d={firstDiameter:F2},h={firstHeightOffset:F2},l={firstLength:F2}), diff={firstDifficulty:F2}");
            }

            //For each row
            for (int i = 1; i < numOfLines; i++)   // numOfLines = number of caves defined in the CSV file.
            {
                string[] fields = lines[i].Split(',');

                // Diameter
                float valueY = float.Parse(fields[1]);
                /*Debug.Log("Y of cave " + i +" from file: " + valueY);*/

                // Height
                float posY = float.Parse(fields[2]);
                /*Debug.Log("posY of cave " + i +" from file: " + posY);*/
                
                // Length
                float valueZ = float.Parse(fields[3]);
                /*Debug.Log("Z of cave " + i +" from file: " + valueZ);*/

                /*
                float valueZnext = valueZ;
                if (i < numOfLines - 1) {
                    string[] fieldsNext = lines[i+1].Split(',');
                    valueZnext = float.Parse(fieldsNext[3]);
                }
                */


                // In these lines, the current position of the objects that are added to the game world is updated,
                // and this position is calculated based on their previous position and data from the file.

                newCaveScale = new Vector3(newCaveScale.x, valueY, valueZ);

                /*Debug.Log("current cave position: " + currentPosition.x + " " + currentPosition.y + " " + currentPosition.z);*/
                
                newCavePosition = new Vector3(currentPositionCave.x, currentPositionCave.y + posY, currentPositionWall.z - pivotCavePlace);

                currentPositionCave = new Vector3(currentPositionCave.x, currentPositionCave.y, newCavePosition.z);

                /*Debug.Log( i +" current cave position: " + currentPosition.x + " " + currentPosition.y + " " + currentPosition.z);
*/
                // Instantiate objects
                GameObject newCaveObject = Instantiate(caveObject, newCavePosition, Quaternion.identity);
                newCaveObject.transform.localScale = newCaveScale;

                // Compute actual Z bounds of the cave for tracking (renderer/collider fallback)
                float minZ = newCavePosition.z;
                float maxZ = newCavePosition.z;
                var rend = newCaveObject.GetComponentInChildren<Renderer>();
                if (rend != null)
                {
                    var b = rend.bounds;
                    minZ = b.min.z;
                    maxZ = b.max.z;
                }
                else
                {
                    var col = newCaveObject.GetComponentInChildren<Collider>();
                    if (col != null)
                    {
                        var b = col.bounds;
                        minZ = b.min.z;
                        maxZ = b.max.z;
                    }
                    else
                    {
                        // Fallback: approximate by scale length on Z
                        float half = newCaveObject.transform.localScale.z * 0.5f;
                        minZ = newCavePosition.z - half;
                        maxZ = newCavePosition.z + half;
                    }
                }

                // Keep original indexing logic (i + 1) and compute structural difficulty
                float structuralDifficulty = CalculateCaveDifficulty(valueY, posY, valueZ);
                float distanceFromPrev = 0f;
                if (caveInfos.Count >= 1)
                {
                    var prev = caveInfos[caveInfos.Count - 1];
                    distanceFromPrev = Mathf.Abs(minZ - prev.maxZ);
                }
                caveInfos.Add(new CaveInfo {
                    index = i + 1,
                    minZ = minZ,
                    maxZ = maxZ,
                    diameter = valueY,
                    height = posY,
                    length = valueZ,
                    difficulty = structuralDifficulty,
                    distanceFromPrevious = distanceFromPrev
                });
                caveDifficultyList.Add(new CaveDifficultyData { caveIndex = i + 1, difficultyScore = structuralDifficulty, distanceFromPrevious = distanceFromPrev });
                levelDifficultyScore += structuralDifficulty;
                Debug.Log($"Cave {i + 1} bounds: Z[{minZ:F1},{maxZ:F1}] at position {newCavePosition.z:F1}");

                // Objects position

                newOxygenPosition = new Vector3(currentPositionOxygen.x, currentPositionOxygen.y, currentPositionCave.z - generalPivot);
                newWallPosition = new Vector3(currentPositionWall.x, currentPositionWall.y, currentPositionCave.z - generalPivot);
                currentPositionWall = new Vector3(currentPositionWall.x, currentPositionWall.y, newWallPosition.z);
                newArrowsPosition = new Vector3(currentPositionArrows.x, currentPositionWall.y + pivotArrowsToWall, currentPositionCave.z - generalPivot);

                // Instantiate Oxygen, Wall and Arrows
                if (i != numOfLines - 1)
                {
                    Instantiate(oxygenObject, newOxygenPosition, Quaternion.identity);
                    Instantiate(wall, newWallPosition, Quaternion.identity);
                }
                Instantiate(arrows, newArrowsPosition, Quaternion.identity);
                
                // Original behavior: Start receiving data per iteration
                if (_client == null)
                {
                    Debug.LogWarning("Amadeo Client is null");
                    return;
                }
                _client.StartReceiveData();
            }

            // Instantiate chest
            Vector3 newPosition_chest = new Vector3(chestX, currentPositionCave.y, newCavePosition.z - (pivotChest));
            GameObject newObject_chest = Instantiate(chest, newPosition_chest, Quaternion.identity);

            // Set the finish line in the progress bar according to the chest position.
            Transform chestTransform = newObject_chest.transform;
            if (levelProgressUI != null)
            {
                levelProgressUI.SetFinishLine(chestTransform);
            }

            // Boolean to initialize variables after panel been close
            playerLife.didntGetInputsYet = true;   // the PlayerLife component has to read the input data from the panel only once. After it reads the data, it sets this flag to false.
            // playerLife.ProcessUserInputs(...)
            health.didntGetInputsYet = true;       // the Health component has to read the input data from the panel only once. After it reads the data, it sets this flag to false.
            // health.ProcessUserInputs(...)
            
            if (enableDifficultyAnalysis)
            {
                Debug.Log("=== LEVEL DIFFICULTY ANALYSIS ===");
                Debug.Log($"Total caves: {caveDifficultyList.Count}");
                Debug.Log($"Average difficulty: {GetAverageDifficulty():F2}");
            }
         
        }
    }
    
    
    
    /// <summary>
    /// Calculate difficulty of a single cave based on its parameters
    /// </summary>
    private float CalculateCaveDifficulty(float diameter, float height, float length)
    {
        // Difficulty based on opening size (smaller = harder)
        float diameterScore = 1f - Mathf.Clamp01((diameter - 0.2f) / 0.6f);
        
        // Difficulty based on height (higher = harder)
        float heightScore = Mathf.Clamp01(height / 25f);
        
        // Difficulty based on length (longer = harder)
        float lengthScore = Mathf.Clamp01((length - 0.2f) / 0.8f);
        
        // Final score (weighted average)
        return (diameterScore * 0.5f) + (heightScore * 0.3f) + (lengthScore * 0.2f);
    }
    
    // Note: Expected time inside cave is computed in CaveTracker using cave length and therapist speed.
    
    /// <summary>
    /// Get difficulty data for a specific cave (used by CaveTracker)
    /// </summary>
    public CaveDifficultyData GetCaveDifficultyData(int caveIndex)
    {
        foreach (var data in caveDifficultyList)
        {
            if (data.caveIndex == caveIndex)
                return data;
        }
        return null;
    }
    
    /// <summary>
    /// Get level difficulty score
    /// </summary>
    public float GetLevelDifficultyScore()
    {
        return levelDifficultyScore;
    }
    
    /// <summary>
    /// Get average difficulty
    /// </summary>
    public float GetAverageDifficulty()
    {
        return caveDifficultyList.Count > 0 ? levelDifficultyScore / caveDifficultyList.Count : 0f;
    }



}