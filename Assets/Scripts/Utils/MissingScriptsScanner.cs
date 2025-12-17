using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;

/**
 * Advanced Missing Scripts Scanner & Remover
 * Scans ALL scenes, prefabs, and assets in the project
 * 
 * Usage:
 * 1. Window → Missing Scripts Scanner
 * 2. Click "Scan Entire Project"
 * 3. Review results
 * 4. Click "Remove All Missing Scripts"
 */
public class MissingScriptsScanner : EditorWindow
{
    private Vector2 scrollPosition;
    private List<MissingScriptInfo> missingScripts = new List<MissingScriptInfo>();
    private bool isScanning = false;
    private string scanStatus = "Ready to scan";
    private int totalScanned = 0;
    private int totalMissing = 0;

    private class MissingScriptInfo
    {
        public string path;
        public string objectName;
        public int componentIndex;
        public GameObject gameObject;
        public bool isPrefab;
    }

    [MenuItem("Window/Missing Scripts Scanner")]
    public static void ShowWindow()
    {
        MissingScriptsScanner window = GetWindow<MissingScriptsScanner>("Missing Scripts Scanner");
        window.minSize = new Vector2(500, 400);
        window.Show();
    }

    [MenuItem("Tools/Scan Project/Find All Missing Scripts")]
    public static void QuickScan()
    {
        MissingScriptsScanner window = GetWindow<MissingScriptsScanner>("Missing Scripts Scanner");
        window.Show();
        window.ScanEntireProject();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Missing Scripts Scanner", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Scans scenes, prefabs, and assets for missing scripts", EditorStyles.miniLabel);
        EditorGUILayout.Space(10);

        // Scan buttons
        EditorGUILayout.BeginHorizontal();
        GUI.enabled = !isScanning;
        
        if (GUILayout.Button("Scan Current Scene", GUILayout.Height(35)))
        {
            ScanCurrentScene();
        }
        
        if (GUILayout.Button("Scan Entire Project", GUILayout.Height(35)))
        {
            ScanEntireProject();
        }
        
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // Remove button
        GUI.enabled = missingScripts.Count > 0 && !isScanning;
        if (GUILayout.Button("Remove All Missing Scripts", GUILayout.Height(40)))
        {
            if (EditorUtility.DisplayDialog("Confirm Removal",
                $"Remove {totalMissing} missing script(s) from {missingScripts.Count} object(s)?",
                "Yes, Remove All", "Cancel"))
            {
                RemoveAllMissingScripts();
            }
        }
        GUI.enabled = true;

        EditorGUILayout.Space(10);

        // Status
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Status:", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(scanStatus);
        EditorGUILayout.LabelField($"Objects Scanned: {totalScanned}");
        EditorGUILayout.LabelField($"Missing Scripts Found: {totalMissing}");
        EditorGUILayout.LabelField($"Objects with Issues: {missingScripts.Count}");
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);

        // Results list
        if (missingScripts.Count > 0)
        {
            EditorGUILayout.LabelField("Missing Scripts Found:", EditorStyles.boldLabel);
            
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            foreach (var info in missingScripts)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                
                // Icon
                GUILayout.Label(info.isPrefab ? "🔷" : "🔴", GUILayout.Width(20));
                
                // Info
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField(info.objectName, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(info.path, EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
                
                // Select button
                if (info.gameObject != null)
                {
                    if (GUILayout.Button("Select", GUILayout.Width(60)))
                    {
                        Selection.activeGameObject = info.gameObject;
                        EditorGUIUtility.PingObject(info.gameObject);
                    }
                }
                
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(3);
            }
            
            EditorGUILayout.EndScrollView();
        }
    }

    private void ScanCurrentScene()
    {
        missingScripts.Clear();
        totalScanned = 0;
        totalMissing = 0;
        isScanning = true;
        scanStatus = "Scanning current scene...";

        Scene scene = SceneManager.GetActiveScene();
        Debug.Log($"=== Scanning Scene: {scene.name} ===");

        GameObject[] rootObjects = scene.GetRootGameObjects();
        foreach (GameObject root in rootObjects)
        {
            ScanGameObject(root, scene.name, false);
        }

        isScanning = false;
        scanStatus = $"Scan complete: {scene.name}";
        
        Debug.Log($"=== Scan Complete ===");
        Debug.Log($"Objects scanned: {totalScanned}");
        Debug.Log($"Missing scripts: {totalMissing}");
        
        Repaint();
    }

    private void ScanEntireProject()
    {
        missingScripts.Clear();
        totalScanned = 0;
        totalMissing = 0;
        isScanning = true;

        Debug.Log("=== Starting Full Project Scan ===");

        // Scan current scene
        scanStatus = "Scanning current scene...";
        Repaint();
        Scene currentScene = SceneManager.GetActiveScene();
        GameObject[] rootObjects = currentScene.GetRootGameObjects();
        foreach (GameObject root in rootObjects)
        {
            ScanGameObject(root, currentScene.name, false);
        }

        // Scan all prefabs
        scanStatus = "Scanning prefabs...";
        Repaint();
        string[] prefabPaths = AssetDatabase.FindAssets("t:Prefab")
            .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
            .ToArray();

        foreach (string path in prefabPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                ScanGameObject(prefab, path, true);
            }
        }

        isScanning = false;
        scanStatus = "Full project scan complete";
        
        Debug.Log("=== Full Project Scan Complete ===");
        Debug.Log($"Total objects scanned: {totalScanned}");
        Debug.Log($"Total missing scripts: {totalMissing}");
        Debug.Log($"Objects with issues: {missingScripts.Count}");
        
        if (totalMissing > 0)
        {
            EditorUtility.DisplayDialog("Scan Complete",
                $"Found {totalMissing} missing script(s) in {missingScripts.Count} object(s).\n\n" +
                $"Review the results below and click 'Remove All Missing Scripts' to fix them.",
                "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("All Clean!",
                $"Scanned {totalScanned} objects.\nNo missing scripts found! ✓",
                "OK");
        }
        
        Repaint();
    }

    private void ScanGameObject(GameObject go, string scenePath, bool isPrefab)
    {
        totalScanned++;
        
        Component[] components = go.GetComponents<Component>();
        int missingCount = 0;
        
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] == null)
            {
                missingCount++;
                totalMissing++;
            }
        }
        
        if (missingCount > 0)
        {
            string path = isPrefab ? scenePath : GetGameObjectPath(go);
            
            missingScripts.Add(new MissingScriptInfo
            {
                path = $"{scenePath}/{path}",
                objectName = $"{go.name} ({missingCount} missing)",
                componentIndex = -1,
                gameObject = go,
                isPrefab = isPrefab
            });
            
            Debug.LogWarning($"Missing script(s) on: {path} ({missingCount} missing)", go);
        }
        
        // Scan children
        foreach (Transform child in go.transform)
        {
            ScanGameObject(child.gameObject, scenePath, isPrefab);
        }
    }

    private void RemoveAllMissingScripts()
    {
        int removed = 0;
        
        foreach (var info in missingScripts)
        {
            if (info.gameObject != null)
            {
                int count = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(info.gameObject);
                if (count > 0)
                {
                    removed += count;
                    Debug.Log($"Removed {count} missing script(s) from: {info.objectName}");
                    
                    if (info.isPrefab)
                    {
                        PrefabUtility.SavePrefabAsset(info.gameObject);
                    }
                }
            }
        }
        
        // Mark scene as dirty
        if (!missingScripts.All(m => m.isPrefab))
        {
            Scene scene = SceneManager.GetActiveScene();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        }
        
        Debug.Log($"=== Cleanup Complete ===");
        Debug.Log($"Removed {removed} missing script(s)");
        
        EditorUtility.DisplayDialog("Cleanup Complete",
            $"Successfully removed {removed} missing script(s).\n\nDon't forget to save!",
            "OK");
        
        // Rescan
        if (missingScripts.Any(m => !m.isPrefab))
        {
            ScanCurrentScene();
        }
        else
        {
            ScanEntireProject();
        }
    }

    private string GetGameObjectPath(GameObject go)
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

