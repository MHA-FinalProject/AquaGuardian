using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using Unity.Mathematics;
using System.IO;

public class PanelOpenUp : MonoBehaviour
{
    // === Amadeo Client and UI References ===
    [Header("Amadeo Client and UI Components")]
    [SerializeField] private AmadeoClient _client;  // Reference to the AmadeoClient
    public GameObject Panel;  // Reference to a UI panel
    [SerializeField] public TextMeshProUGUI num_of_caves_Text = null;  // Reference to the TextMeshPro component for number of caves
    [SerializeField] public Slider slider;  // Reference to the UI slider

    // === Game Object References ===
    [Header("Game Objects")]
    [SerializeField] public GameObject objectToScale = null;  // Reference to the object being scaled
    [SerializeField] public GameObject oxygenObject = null;  // Reference to the oxygen object
    [SerializeField] public GameObject wall = null;  // Reference to the wall object
    [SerializeField] public GameObject arrows = null;  // Reference to the arrows object
    [SerializeField] public GameObject chest = null;  // Reference to the chest object

    // === Game Settings ===
    [Header("Game Settings")]
    [SerializeField] public float maxSliderAmount = 5.0f;  // Maximum amount for the slider
    public float num_caves_from_user = 0;  // Number of caves specified by the user
    private int numOfLines = 0;  // Internal counter for lines in the CSV

    // === Pivot and Position Settings ===
    [Header("Pivot and Position Settings")]
    private int pivotPlace = 120;  // Pivot place for general objects
    private int pivotChest = 75;  // Pivot place for the chest
    private int pivotOxygen = 100;  // Pivot place for oxygen objects
    private float chestX = 291.774f;  // X position for the chest
    private float chestY = 20.002f;  // Y position for the chest
    private float generalPivot = 50f;  // General pivot for object placement
    private float pivotCavePlace = 70;  // Pivot place for caves
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

    /* public void num_of_caves(float value)
     {
         num_caves_from_user = 0;
         *//*Debug.Log("before update: " + num_caves_from_user);*//*

         // Round down the value to the nearest integer
         int intValue = Mathf.FloorToInt(value);

         num_of_caves_Text.text = intValue.ToString("0");

         num_caves_from_user = intValue;

         *//*Debug.Log("num_caves_from_user after update: " + numOfLines);*//*
     }
 */

    // The function handles closing a panel and creates objects according to the data read from a CSV file.
    // The function places the objects in the game world and updates their size and position based on the data in the file.
    public void ClosePanel()
    {
        if (Panel != null)
        {
            Panel.SetActive(false);
            /*Debug.Log("num_caves_from_user in ClosePanel: " + numOfLines);*/
            Vector3 currentPosition = objectToScale.transform.position;
            Vector3 currentPositionOxygen = oxygenObject.transform.position;
            Vector3 currentPositionWall = wall.transform.position;
            Vector3 currentPositionArrows = arrows.transform.position;

            Vector3 newPosition = new Vector3(currentPosition.x,currentPosition.y,currentPosition.z);
            Vector3 newOxygenPosition = new Vector3(currentPositionOxygen.x, currentPositionOxygen.y, currentPositionOxygen.z);
            Vector3 newWallPosition = new Vector3(currentPositionWall.x, currentPositionWall.y, currentPositionWall.z);
            Vector3 newArrowsPosition = new Vector3(currentPositionArrows.x, currentPositionArrows.y, currentPositionArrows.z);

            Vector3 currentScale = objectToScale.transform.localScale;
            Vector3 newScale = new Vector3(currentScale.x, currentScale.y, currentScale.z);

            //For each row
            for (int i = 1; i < numOfLines; i++)
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

                float valueZnext = valueZ;

                if (i < numOfLines - 1) {
                    string[] fieldsNext = lines[i+1].Split(',');
                    valueZnext = float.Parse(fieldsNext[3]);
                }


                // In these lines, the current position of the objects that are added to the game world is updated,
                // and this position is calculated based on their previous position and data from the file.

                newScale = new Vector3(newScale.x, valueY, valueZ);

                /*Debug.Log("current cave position: " + currentPosition.x + " " + currentPosition.y + " " + currentPosition.z);*/
                
                newPosition = new Vector3(currentPosition.x, currentPosition.y + posY, currentPositionWall.z - pivotCavePlace);

                currentPosition = new Vector3(currentPosition.x,currentPosition.y,newPosition.z);

                /*Debug.Log( i +" current cave position: " + currentPosition.x + " " + currentPosition.y + " " + currentPosition.z);
*/
                //instantiate objects
                GameObject newObject = Instantiate(objectToScale, newPosition, Quaternion.identity);
                newObject.transform.localScale = newScale;

                //Objects position

                newOxygenPosition = new Vector3(currentPositionOxygen.x, currentPositionOxygen.y, currentPosition.z - generalPivot);

                newWallPosition = new Vector3(currentPositionWall.x, currentPositionWall.y, currentPosition.z - generalPivot);

                currentPositionWall = new Vector3(currentPositionWall.x , currentPositionWall.y , newWallPosition.z);

                newArrowsPosition = new Vector3(currentPositionArrows.x, currentPositionWall.y + pivotArrowsToWall, currentPosition.z - generalPivot);

                // Instantiate Oxygen, Wall and Arrows
                if (i != numOfLines - 1)
                {
                    GameObject newOxygenObject = Instantiate(oxygenObject, newOxygenPosition, Quaternion.identity);
                    GameObject newWallObject = Instantiate(wall, newWallPosition, Quaternion.identity);
                }
                GameObject newArrowsObject = Instantiate(arrows, newArrowsPosition, Quaternion.identity);

                if (_client == null )
                {
                    Debug.LogWarning("Amadeo Client is null");
                    return;
                }
                _client.StartReceiveData();
            }

            // Instantiate chest
            Vector3 newPosition_chest = new Vector3(chestX, currentPosition.y, newPosition.z - (pivotChest));

            GameObject newObject_chest = Instantiate(chest, newPosition_chest, Quaternion.identity);

            // Get the Transform component of the newly instantiated chest
            Transform chestTransform = newObject_chest.transform;

            // Set the finish line in LevelProgressUI
            if (levelProgressUI != null)
            {
                levelProgressUI.SetFinishLine(chestTransform);
            }

            // Boolean to initialize variables after panel been close

            playerLife.didntGetInputsYet = true;

            health.didntGetInputsYet = true;


        }
    }

   /* public void num_of_caves(float value)
    {
        num_caves_from_user = 0;
        Debug.Log("before update: " + num_caves_from_user);

        // Round down the value to the nearest integer
        int intValue = Mathf.FloorToInt(value);

        num_of_caves_Text.text = intValue.ToString("0");

        num_caves_from_user = intValue;

        Debug.Log("num_caves_from_user after update: " + num_caves_from_user);
    }


    public void ClosePanel()
    {
        if (Panel != null)
        {
            Panel.SetActive(false);
            Debug.Log("num_caves_from_user in ClosePanel: " + num_caves_from_user);
            Vector3 currentPosition = objectToScale.transform.position;
            Vector3 currentPositionOxygen = oxygenObject.transform.position;
            Vector3 newPosition = new Vector3(currentPosition.x, currentPosition.y, currentPosition.z);
            Vector3 newOxygenPosition = new Vector3(currentPositionOxygen.x, currentPositionOxygen.y, currentPositionOxygen.z);

            for (int i = 1; i <= num_caves_from_user; i++)
            {
                newPosition = new Vector3(currentPosition.x, currentPosition.y, currentPosition.z - (pivotPlace * i));
                newOxygenPosition = new Vector3(currentPositionOxygen.x, currentPositionOxygen.y, currentPositionOxygen.z - (pivotPlace * i));
                GameObject newObject = Instantiate(objectToScale, newPosition, Quaternion.identity);
                GameObject newOxygenObject = Instantiate(oxygenObject, newOxygenPosition, Quaternion.identity);
            }


            Vector3 newPosition_chest = new Vector3(chestX, chestY, newPosition.z - (pivotPlace));

            GameObject newObject_chest = Instantiate(chest, newPosition_chest, Quaternion.identity);

        }
    }*/

}