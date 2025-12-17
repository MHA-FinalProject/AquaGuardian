using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

/**
 * Utility to find and remove missing scripts in the scene and project.
 * 
 * Usage:
 * 1. In Unity Editor, go to: Tools → Find Missing Scripts → In Current Scene
 * 2. Check Console for list of GameObjects with missing scripts
 * 3. Select reported GameObjects in Hierarchy and remove missing components manually
 * 
 * Or use: Tools → Find Missing Scripts → Remove All Missing Scripts
 */
public class FindMissingScripts : EditorWindow
{
    private Vector2 scrollPosition;
    private List<GameObject> objectsWithMissingScripts = new List<GameObject>();
    private int missingCount = 0;

    [MenuItem("Tools/Find Missing Scripts/In Current Scene")]
    public static void FindMissingScriptsInScene()
    {
        FindMissingScripts window = GetWindow<FindMissingScripts>("Missing Scripts Finder");
        window.Show();
        window.ScanCurrentScene();
    }

    [MenuItem("Tools/Find Missing Scripts/Remove All Missing Scripts")]
    public static void RemoveAllMissingScripts()
    {
        if (EditorUtility.DisplayDialog("Remove Missing Scripts",
            "This will remove all missing script components from GameObjects in the current scene. Continue?",
            "Yes", "Cancel"))
        {
            RemoveMissingScriptsInScene();
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Missing Scripts Finder", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        if (GUILayout.Button("Scan Current Scene", GUILayout.Height(30)))
        {
            ScanCurrentScene();
        }

        if (GUILayout.Button("Remove All Missing Scripts", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog("Confirm Removal",
                $"Found {missingCount} missing scripts. Remove them all?",
                "Yes", "Cancel"))
            {
                RemoveMissingScriptsInScene();
                ScanCurrentScene(); // Rescan after removal
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"Found: {missingCount} missing scripts", EditorStyles.helpBox);
        EditorGUILayout.Space();

        if (objectsWithMissingScripts.Count > 0)
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            EditorGUILayout.LabelField("GameObjects with missing scripts:", EditorStyles.boldLabel);
            
            foreach (GameObject go in objectsWithMissingScripts)
            {
                if (go != null)
                {
                    EditorGUILayout.BeginHorizontal();
                    
                    if (GUILayout.Button("Select", GUILayout.Width(60)))
                    {
                        Selection.activeGameObject = go;
                        EditorGUIUtility.PingObject(go);
                    }
                    
                    EditorGUILayout.LabelField(GetGameObjectPath(go));
                    
                    EditorGUILayout.EndHorizontal();
                }
            }
            
            EditorGUILayout.EndScrollView();
        }
    }

    private void ScanCurrentScene()
    {
        objectsWithMissingScripts.Clear();
        missingCount = 0;

        Scene currentScene = SceneManager.GetActiveScene();
        GameObject[] allObjects = currentScene.GetRootGameObjects();

        Debug.Log($"=== Scanning scene: {currentScene.name} for missing scripts ===");

        foreach (GameObject root in allObjects)
        {
            ScanGameObject(root);
        }

        if (missingCount == 0)
        {
            Debug.Log("✓ No missing scripts found!");
        }
        else
        {
            Debug.LogWarning($"⚠ Found {missingCount} missing scripts in {objectsWithMissingScripts.Count} GameObjects");
        }

        Repaint();
    }

    private void ScanGameObject(GameObject go)
    {
        Component[] components = go.GetComponents<Component>();
        bool hasMissing = false;
        int missingInObject = 0;

        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] == null)
            {
                hasMissing = true;
                missingInObject++;
                missingCount++;
            }
        }

        if (hasMissing)
        {
            objectsWithMissingScripts.Add(go);
            Debug.LogWarning($"Missing script(s) on: {GetGameObjectPath(go)} ({missingInObject} missing)", go);
        }

        // Recursively scan children
        foreach (Transform child in go.transform)
        {
            ScanGameObject(child.gameObject);
        }
    }

    private static void RemoveMissingScriptsInScene()
    {
        Scene currentScene = SceneManager.GetActiveScene();
        GameObject[] allObjects = currentScene.GetRootGameObjects();
        int removedCount = 0;

        foreach (GameObject root in allObjects)
        {
            removedCount += RemoveMissingScriptsFromGameObject(root);
        }

        if (removedCount > 0)
        {
            EditorUtility.SetDirty(currentScene.GetRootGameObjects()[0]);
            Debug.Log($"✓ Removed {removedCount} missing script(s) from scene: {currentScene.name}");
            
            // Mark scene as dirty so user can save
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(currentScene);
        }
        else
        {
            Debug.Log("No missing scripts found to remove.");
        }
    }

    private static int RemoveMissingScriptsFromGameObject(GameObject go)
    {
        int removedCount = 0;
        Component[] components = go.GetComponents<Component>();

        // Use SerializedObject to remove missing scripts
        SerializedObject so = new SerializedObject(go);
        SerializedProperty prop = so.FindProperty("m_Component");

        int propertyCount = 0;
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] == null)
            {
                Debug.Log($"Removing missing script from: {GetGameObjectPath(go)}", go);
                removedCount++;
            }
            else
            {
                propertyCount++;
            }
        }

        // Remove null components
        if (removedCount > 0)
        {
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
        }

        // Recursively process children
        foreach (Transform child in go.transform)
        {
            removedCount += RemoveMissingScriptsFromGameObject(child.gameObject);
        }

        return removedCount;
    }

    private static string GetGameObjectPath(GameObject go)
    {
        string path = go.name;
        Transform parent = go.transform.parent;

        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }

        return path;
    }
}
#endif

