using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;

/**
 *  The panel that opens up when we start the game. 
 *  It allows the physician to set all the parameters of the game, e.g. force factor, fingers, etc.
 */
public class PanelOpenUp : MonoBehaviour
{
    // === Amadeo Client and UI References ===
    [Header("Amadeo Client and UI Components")]
    [SerializeField] private AmadeoClient _client;  // Reference to the AmadeoClient
    public GameObject Panel;  // Reference to a UI panel
    // [SerializeField] public TextMeshProUGUI num_of_caves_Text = null;  // Reference to the TextMeshPro component for number of caves
    ///[SerializeField] public Slider slider;  // Reference to the UI slider

    // === Game Object References ===
    [Header("Game Objects")]
    [SerializeField] public GameObject caveObject = null;     // Reference to the cave object, that is being scaled
    [SerializeField] public GameObject oxygenObject = null;   // Reference to the oxygen object
    [SerializeField] public GameObject wall = null;           // Reference to the wall object
    [SerializeField] public GameObject arrows = null;         // Reference to the arrows object
    [SerializeField] public GameObject chest = null;          // Reference to the chest object

    // === Game Settings ===
    [Header("Game Settings")]
    // [SerializeField] public float maxSliderAmount = 5.0f;     // Maximum amount for the slider
    // public float num_caves_from_user = 0;  // Number of caves specified by the user
    private int numOfLines = 0;  // Internal counter for lines in the CSV

    // === Pivot and Position Settings ===
    [Header("Pivot and Position Settings")]
    private int pivotChest = 75;        // Distance (in minus z direction) from last cave to treasure chest
    private float chestX = 291.774f;    // X position for the chest (note: Y position for the chest is the Y position of the last cave).
    private float generalPivot = 50f;   // Distance (in minus z direction) from current cave to next wall / oxygen / arrows.
    private float pivotCavePlace = 70;  // Distance (in minus z direction) from current wall to next cave.
    private float pivotArrowsToWall = 45f;  // Pivot distance between arrows and walls

    // === File Settings ===
    [Header("File Settings")]
    public string filePath = "Assets\\Data\\caves.csv";  // Path to the CSV file containing cave data
    string[] lines = null;  // Array for storing lines from the CSV file

    // === Component References ===
    [Header("Component References")]
    [SerializeField] private LevelProgressUI levelProgressUI;  // Reference to the LevelProgressUI component
    [SerializeField] private PlayerLife playerLife;  // Reference to the PlayerLife component for handling player health
    [SerializeField] private Health health;  // Reference to the Health component to manage player health


    void Start()
    {
        ReadCSVFile(filePath);
    }

    void ReadCSVFile(string path)
    {
        try
        {
            lines = File.ReadAllLines(path);

            numOfLines = lines.Length;

            /*Debug.Log("num of caves from file: " + numOfLines);*/

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
        catch (IOException e)
        {
            Debug.LogError($"Error reading file: {e.Message}");
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
            Vector3 currentPositionCave   = caveObject.transform.position;
            Vector3 currentPositionOxygen = oxygenObject.transform.position;
            Vector3 currentPositionWall   = wall.transform.position;
            Vector3 currentPositionArrows = arrows.transform.position;

            Vector3 newCavePosition = new Vector3(currentPositionCave.x,currentPositionCave.y,currentPositionCave.z);
            Vector3 newOxygenPosition;
            Vector3 newWallPosition;
            Vector3 newArrowsPosition;

            Vector3 currentCaveScale = caveObject.transform.localScale;
            Vector3 newCaveScale = new Vector3(currentCaveScale.x, currentCaveScale.y, currentCaveScale.z);

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

                currentPositionCave = new Vector3(currentPositionCave.x,currentPositionCave.y,newCavePosition.z);

                /*Debug.Log( i +" current cave position: " + currentPosition.x + " " + currentPosition.y + " " + currentPosition.z);
*/
                //instantiate objects
                GameObject newCaveObject = Instantiate(caveObject, newCavePosition, Quaternion.identity);
                newCaveObject.transform.localScale = newCaveScale;

                //Objects position

                newOxygenPosition = new Vector3(currentPositionOxygen.x, currentPositionOxygen.y, currentPositionCave.z - generalPivot);
                newWallPosition = new Vector3(currentPositionWall.x, currentPositionWall.y, currentPositionCave.z - generalPivot);
                currentPositionWall = new Vector3(currentPositionWall.x , currentPositionWall.y , newWallPosition.z);
                newArrowsPosition = new Vector3(currentPositionArrows.x, currentPositionWall.y + pivotArrowsToWall, currentPositionCave.z - generalPivot);

                // Instantiate Oxygen, Wall and Arrows
                if (i != numOfLines - 1)
                {
                    Instantiate(oxygenObject, newOxygenPosition, Quaternion.identity);
                    Instantiate(wall, newWallPosition, Quaternion.identity);
                }
                Instantiate(arrows, newArrowsPosition, Quaternion.identity);

                if (_client == null )
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
        }
    }

}