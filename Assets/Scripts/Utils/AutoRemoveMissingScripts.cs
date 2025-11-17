using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.SceneManagement;

/**
 * Automatically removes missing scripts when Unity starts.
 * Simple one-click solution for missing script cleanup.
 * 
 * Usage: Right-click in Hierarchy → Remove All Missing Scripts
 */
public class AutoRemoveMissingScripts
{
    [MenuItem("GameObject/Remove All Missing Scripts", false, 0)]
    private static void RemoveMissingScriptsFromSelected()
    {
        GameObject[] selected = Selection.gameObjects;
        
        if (selected.Length == 0)
        {
            // No selection - scan entire scene
            if (EditorUtility.DisplayDialog("Remove Missing Scripts",
                "No GameObjects selected. Remove missing scripts from entire scene?",
                "Yes, Entire Scene", "Cancel"))
            {
                RemoveMissingScriptsFromScene();
            }
        }
        else
        {
            // Process selected objects
            int totalRemoved = 0;
            foreach (GameObject go in selected)
            {
                totalRemoved += RemoveMissingScriptsRecursive(go);
            }
            
            if (totalRemoved > 0)
            {
                Debug.Log($"✓ Removed {totalRemoved} missing script(s) from selected GameObject(s)");
                EditorUtility.DisplayDialog("Success", 
                    $"Removed {totalRemoved} missing script(s)", "OK");
            }
            else
            {
                Debug.Log("No missing scripts found in selected GameObject(s)");
            }
        }
    }

    [MenuItem("Tools/Clean Up/Remove All Missing Scripts In Scene")]
    private static void RemoveMissingScriptsFromScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject[] rootObjects = scene.GetRootGameObjects();
        
        int totalRemoved = 0;
        int objectsScanned = 0;
        
        Debug.Log($"=== Scanning scene: {scene.name} ===");
        
        foreach (GameObject root in rootObjects)
        {
            int removed = RemoveMissingScriptsRecursive(root);
            totalRemoved += removed;
            objectsScanned++;
        }
        
        Debug.Log($"=== Scan complete ===");
        Debug.Log($"Objects scanned: {objectsScanned}");
        Debug.Log($"Missing scripts removed: {totalRemoved}");
        
        if (totalRemoved > 0)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            EditorUtility.DisplayDialog("Cleanup Complete", 
                $"Removed {totalRemoved} missing script(s) from {objectsScanned} object(s).\n\nDon't forget to save the scene!", 
                "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("All Clean!", 
                "No missing scripts found in the scene.", 
                "OK");
        }
    }

    private static int RemoveMissingScriptsRecursive(GameObject go)
    {
        int removedCount = 0;
        
        // Check this object
        Component[] components = go.GetComponents<Component>();
        
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] == null)
            {
                Debug.Log($"Removing missing script from: {GetGameObjectPath(go)}", go);
                removedCount++;
            }
        }
        
        // Remove missing scripts
        if (removedCount > 0)
        {
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
        }
        
        // Process children
        foreach (Transform child in go.transform)
        {
            removedCount += RemoveMissingScriptsRecursive(child.gameObject);
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

