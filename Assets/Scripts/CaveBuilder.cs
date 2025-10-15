using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/**
 * CaveBuilder - Responsible for building caves and related objects from CSV data
 */
public class CaveBuilder : MonoBehaviour
{
    [Header("Prefab References")]
    [SerializeField] private GameObject cavePrefab;
    [SerializeField] private GameObject oxygenPrefab;
    [SerializeField] private GameObject wallPrefab;
    [SerializeField] private GameObject arrowsPrefab;

    [Header("CSV Data")]
    [SerializeField] private TextAsset csvFile;
    private string[] csvLines = null;
    private int numOfLines = 0;

    [Header("Configuration")]
    [SerializeField] private GameConfig gameConfig;

    [Header("Tracking")]
    private List<TrialDataModels.CaveInfo> caveInfos = new List<TrialDataModels.CaveInfo>();
    private List<GameObject> spawnedObjects = new List<GameObject>();

    // Public access to cave info
    public List<TrialDataModels.CaveInfo> CaveInfos => caveInfos;
    public int CaveCount => numOfLines;

    public void SetCSVFile(TextAsset file)
    {
        csvFile = file;
        LoadCSVFromTextAsset();
    }

    
    public void LoadCSVFromTextAsset()
    {
        try
        {
            if (csvFile == null)
            {
                Debug.LogError("CSV file is null - cannot load caves!");
                return;
            }

            string csvText = csvFile.text;
            csvLines = csvText.Split(new[] { "\r\n", "\r", "\n" }, System.StringSplitOptions.RemoveEmptyEntries);
            numOfLines = csvLines.Length;
            Debug.Log($"CSV loaded: {numOfLines} cave definitions from {csvFile.name}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error reading CSV: {e.Message}");
        }
    }

    
    public void LoadCSVFromPath(string absolutePath)
    {
        try
        {
            if (!System.IO.File.Exists(absolutePath))
            {
                Debug.LogError($"Cave CSV not found at: {absolutePath}");
                return;
            }

            string[] fileLines = System.IO.File.ReadAllLines(absolutePath);
            csvLines = fileLines.Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
            numOfLines = csvLines.Length;
            Debug.Log($"Loaded {numOfLines} caves from path: {absolutePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error reading CSV from path: {e.Message}");
        }
    }

    

       public Vector3 BuildAllCaves(GameObject firstCave, GameObject firstOxygen, GameObject firstWall, GameObject firstArrows)
    {
        // Clear previous data
        caveInfos.Clear();
        DestroySpawnedObjects();

        if (csvLines == null || numOfLines == 0)
        {
            Debug.LogError("No CSV data loaded - cannot build caves!");
            return Vector3.zero;
        }

        if (gameConfig == null)
        {
            Debug.LogError("GameConfig not assigned - cannot build caves!");
            return Vector3.zero;
        }

        // Store prefabs from scene objects
        cavePrefab = firstCave;
        oxygenPrefab = firstOxygen;
        wallPrefab = firstWall;
        arrowsPrefab = firstArrows;

        // Get initial positions
        Vector3 currentPositionCave = firstCave.transform.position;
        Vector3 currentPositionOxygen = firstOxygen.transform.position;
        Vector3 currentPositionWall = firstWall.transform.position;
        Vector3 currentPositionArrows = firstArrows.transform.position;

        Vector3 newCavePosition = currentPositionCave;
        Vector3 currentCaveScale = firstCave.transform.localScale;
        Vector3 newCaveScale = currentCaveScale;

        // Add first cave (already in scene)
        AddFirstCaveBounds(currentPositionCave, currentCaveScale, firstCave);

        // Build remaining caves from CSV
        for (int i = 1; i < numOfLines; i++)
        {
            string[] fields = csvLines[i].Split(',');

            float diameter = float.Parse(fields[1]);
            float heightOffset = float.Parse(fields[2]);
            float length = float.Parse(fields[3]);

            // Calculate cave position and scale
            newCaveScale = new Vector3(newCaveScale.x, diameter, length);
            newCavePosition = new Vector3(
                currentPositionCave.x,
                currentPositionCave.y + heightOffset,
                currentPositionWall.z - gameConfig.pivotCavePlace
            );
            currentPositionCave = new Vector3(currentPositionCave.x, currentPositionCave.y, newCavePosition.z);

            // Create cave
            GameObject newCaveObject = Instantiate(cavePrefab, newCavePosition, Quaternion.identity);
            TrackSpawned(newCaveObject);
            newCaveObject.transform.localScale = newCaveScale;

            // Add cave bounds to tracking
            AddCaveBounds(newCaveObject, i + 1, newCavePosition, diameter, heightOffset, length);

            // Calculate positions for oxygen, wall, arrows
            Vector3 newOxygenPosition = new Vector3(
                currentPositionOxygen.x,
                currentPositionOxygen.y,
                currentPositionCave.z - gameConfig.generalPivot
            );

            Vector3 newWallPosition = new Vector3(
                currentPositionWall.x,
                currentPositionWall.y,
                currentPositionCave.z - gameConfig.generalPivot
            );
            currentPositionWall = new Vector3(currentPositionWall.x, currentPositionWall.y, newWallPosition.z);

            Vector3 newArrowsPosition = new Vector3(
                currentPositionArrows.x,
                currentPositionWall.y + gameConfig.pivotArrowsToWall,
                currentPositionCave.z - gameConfig.generalPivot
            );

            // Create oxygen, wall, arrows (except for last cave)
            if (i != numOfLines - 1)
            {
                CreateOxygenTank(newOxygenPosition, i);
                CreateWall(newWallPosition);
            }

            CreateArrows(newArrowsPosition);
        }

        // Return the last cave position for end object placement
        return newCavePosition;
    }

    public Vector3 GetEndObjectPosition(Vector3 lastCavePosition)
    {
        if (caveInfos.Count > 0)
        {
            var lastCave = caveInfos[caveInfos.Count - 1];
            float lastCaveEndZ = lastCave.maxZ;

            return new Vector3(
                gameConfig.chestX,
                lastCavePosition.y,
                lastCaveEndZ - gameConfig.pivotChest
            );
        }
        else
        {
            // Fallback
            return new Vector3(
                gameConfig.chestX,
                lastCavePosition.y,
                lastCavePosition.z - gameConfig.pivotChest
            );
        }
    }



    private void AddFirstCaveBounds(Vector3 position, Vector3 scale, GameObject cave)
    {
        float minZ = position.z;
        float maxZ = position.z;

        var existingRend = cave.GetComponentInChildren<Renderer>();
        if (existingRend != null)
        {
            minZ = existingRend.bounds.min.z;
            maxZ = existingRend.bounds.max.z;
        }
        else
        {
            var existingCol = cave.GetComponentInChildren<Collider>();
            if (existingCol != null)
            {
                minZ = existingCol.bounds.min.z;
                maxZ = existingCol.bounds.max.z;
            }
            else
            {
                float half = scale.z * 0.5f;
                minZ = position.z - half;
                maxZ = position.z + half;
            }
        }

        caveInfos.Add(new TrialDataModels.CaveInfo
        {
            index = 1,
            minZ = minZ,
            maxZ = maxZ,
            diameter = scale.x,
            height = scale.y,
            length = scale.z,
            distanceFromPrevious = 0f
        });
    }

    private void AddCaveBounds(GameObject cave, int index, Vector3 position, float diameter, float height, float length)
    {
        float minZ = position.z;
        float maxZ = position.z;

        var rend = cave.GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            minZ = rend.bounds.min.z;
            maxZ = rend.bounds.max.z;
        }
        else
        {
            var col = cave.GetComponentInChildren<Collider>();
            if (col != null)
            {
                minZ = col.bounds.min.z;
                maxZ = col.bounds.max.z;
            }
            else
            {
                float half = length * 0.5f;
                minZ = position.z - half;
                maxZ = position.z + half;
            }
        }

        // Calculate distance from previous cave
        float distanceFromPrev = 0f;
        if (caveInfos.Count >= 1)
        {
            var prev = caveInfos[caveInfos.Count - 1];
            distanceFromPrev = Mathf.Abs(minZ - prev.maxZ);
        }

        caveInfos.Add(new TrialDataModels.CaveInfo
        {
            index = index,
            minZ = minZ,
            maxZ = maxZ,
            diameter = diameter,
            height = height,
            length = length,
            distanceFromPrevious = distanceFromPrev
        });
    }

    private void CreateOxygenTank(Vector3 position, int index)
    {
        if (oxygenPrefab != null)
        {
            var oxy = Instantiate(oxygenPrefab, position, Quaternion.identity);
            oxy.name = $"tank_{index + 1}";
            oxy.SetActive(true);

            if (oxy.tag != "OxygenObject")
            {
                oxy.tag = "OxygenObject";
            }

            TrackSpawned(oxy);
            Debug.Log($" Created tank_{index + 1} at {position} with tag: {oxy.tag}, active: {oxy.activeSelf}");
        }
        else
        {
            Debug.LogError($"oxygenPrefab is NULL! Cannot create tank_{index + 1}");
        }
    }

    private void CreateWall(Vector3 position)
    {
        if (wallPrefab != null)
        {
            var wallObj = Instantiate(wallPrefab, position, Quaternion.identity);
            TrackSpawned(wallObj);
        }
    }

    private void CreateArrows(Vector3 position)
    {
        if (arrowsPrefab != null)
        {
            var arrowsObj = Instantiate(arrowsPrefab, position, Quaternion.identity);
            TrackSpawned(arrowsObj);
        }
    }

    private void TrackSpawned(GameObject go)
    {
        if (go != null)
        {
            spawnedObjects.Add(go);
        }
    }


    public void DestroySpawnedObjects()
    {
        foreach (var obj in spawnedObjects)
        {
            if (obj != null)
            {
                Destroy(obj);
            }
        }
        spawnedObjects.Clear();
    }

 
    public List<GameObject> GetSpawnedObjects()
    {
        return new List<GameObject>(spawnedObjects);
    }
}

