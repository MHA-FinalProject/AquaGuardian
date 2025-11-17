using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/**
 * Scans and fixes missing scripts in ALL prefabs
 * Perfect for runtime missing script errors
 * 
 * Usage: Tools → Fix Prefabs → Scan All Prefabs for Missing Scripts
 */
public class PrefabMissingScriptsFixer : EditorWindow
{
    private Vector2 scrollPosition;
    private List<PrefabInfo> problematicPrefabs = new List<PrefabInfo>();
    private bool isScanning = false;
    private int totalPrefabs = 0;
    private int prefabsWithIssues = 0;

    private class PrefabInfo
    {
        public string path;
        public string name;
        public GameObject prefab;
        public int missingCount;
    }

    [MenuItem("Tools/Fix Prefabs/Scan All Prefabs for Missing Scripts")]
    public static void ScanAllPrefabs()
    {
        PrefabMissingScriptsFixer window = GetWindow<PrefabMissingScriptsFixer>("Prefab Scanner");
        window.minSize = new Vector2(600, 400);
        window.Show();
        window.StartScan();
    }

    [MenuItem("Tools/Fix Prefabs/Remove Missing Scripts from All Prefabs")]
    public static void QuickFix()
    {
        if (EditorUtility.DisplayDialog("Fix All Prefabs",
            "This will scan ALL prefabs and remove missing scripts.\nThis cannot be undone!\n\nContinue?",
            "Yes, Fix All", "Cancel"))
        {
            PrefabMissingScriptsFixer window = GetWindow<PrefabMissingScriptsFixer>("Prefab Scanner");
            window.Show();
            window.StartScan();
            window.RemoveAllMissingScriptsFromPrefabs();
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Prefab Missing Scripts Scanner", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Scans ALL prefabs in the project for missing scripts", EditorStyles.miniLabel);
        EditorGUILayout.Space(10);

        // Scan button
        GUI.enabled = !isScanning;
        if (GUILayout.Button("Scan All Prefabs", GUILayout.Height(40)))
        {
            StartScan();
        }
        GUI.enabled = true;

        EditorGUILayout.Space(5);

        // Fix button
        GUI.enabled = prefabsWithIssues > 0 && !isScanning;
        if (GUILayout.Button($"Remove Missing Scripts from {prefabsWithIssues} Prefabs", GUILayout.Height(40)))
        {
            if (EditorUtility.DisplayDialog("Confirm Fix",
                $"Remove missing scripts from {prefabsWithIssues} prefab(s)?\nThis will modify the prefab files!",
                "Yes, Fix Them", "Cancel"))
            {
                RemoveAllMissingScriptsFromPrefabs();
            }
        }
        GUI.enabled = true;

        EditorGUILayout.Space(10);

        // Status
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField($"Total Prefabs Scanned: {totalPrefabs}");
        EditorGUILayout.LabelField($"Prefabs with Missing Scripts: {prefabsWithIssues}", 
            prefabsWithIssues > 0 ? EditorStyles.boldLabel : EditorStyles.label);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);

        // Results
        if (problematicPrefabs.Count > 0)
        {
            EditorGUILayout.LabelField("Prefabs with Missing Scripts:", EditorStyles.boldLabel);
            
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            foreach (var info in problematicPrefabs)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                
                // Name
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField(info.name, EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Missing: {info.missingCount} script(s)", EditorStyles.miniLabel);
                EditorGUILayout.LabelField(info.path, EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
                
                // Select button
                if (GUILayout.Button("Select", GUILayout.Width(70)))
                {
                    Selection.activeObject = info.prefab;
                    EditorGUIUtility.PingObject(info.prefab);
                }
                
                // Fix button
                if (GUILayout.Button("Fix This", GUILayout.Width(70)))
                {
                    FixPrefab(info);
                }
                
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(3);
            }
            
            EditorGUILayout.EndScrollView();
        }
        else if (totalPrefabs > 0)
        {
            EditorGUILayout.HelpBox("✓ All prefabs are clean! No missing scripts found.", MessageType.Info);
        }
    }

    private void StartScan()
    {
        problematicPrefabs.Clear();
        totalPrefabs = 0;
        prefabsWithIssues = 0;
        isScanning = true;

        Debug.Log("=== Scanning ALL Prefabs for Missing Scripts ===");

        // Find all prefabs
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        totalPrefabs = prefabGuids.Length;

        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab != null)
            {
                int missingCount = CountMissingScripts(prefab);
                
                if (missingCount > 0)
                {
                    problematicPrefabs.Add(new PrefabInfo
                    {
                        path = path,
                        name = prefab.name,
                        prefab = prefab,
                        missingCount = missingCount
                    });
                    
                    prefabsWithIssues++;
                    Debug.LogWarning($"Found {missingCount} missing script(s) in prefab: {path}", prefab);
                }
            }
        }

        isScanning = false;

        Debug.Log($"=== Scan Complete ===");
        Debug.Log($"Total prefabs scanned: {totalPrefabs}");
        Debug.Log($"Prefabs with missing scripts: {prefabsWithIssues}");

        if (prefabsWithIssues > 0)
        {
            EditorUtility.DisplayDialog("Missing Scripts Found!",
                $"Found {prefabsWithIssues} prefab(s) with missing scripts.\n\n" +
                $"These prefabs cause runtime errors when instantiated.\n\n" +
                $"Click 'Remove Missing Scripts' to fix them.",
                "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("All Clean!",
                $"Scanned {totalPrefabs} prefabs.\n\nNo missing scripts found! ✓",
                "OK");
        }

        Repaint();
    }

    private int CountMissingScripts(GameObject go)
    {
        int count = 0;

        // Check this object
        Component[] components = go.GetComponents<Component>();
        foreach (Component comp in components)
        {
            if (comp == null)
            {
                count++;
            }
        }

        // Check children recursively
        foreach (Transform child in go.transform)
        {
            count += CountMissingScripts(child.gameObject);
        }

        return count;
    }

    private void FixPrefab(PrefabInfo info)
    {
        if (info.prefab == null)
        {
            Debug.LogError($"Prefab is null: {info.path}");
            return;
        }

        int removed = RemoveMissingScriptsRecursive(info.prefab);
        
        if (removed > 0)
        {
            // Save the prefab
            PrefabUtility.SavePrefabAsset(info.prefab);
            
            Debug.Log($"✓ Removed {removed} missing script(s) from: {info.name}");
            
            EditorUtility.DisplayDialog("Fixed!",
                $"Removed {removed} missing script(s) from:\n{info.name}",
                "OK");
            
            // Rescan
            StartScan();
        }
    }

    private void RemoveAllMissingScriptsFromPrefabs()
    {
        int totalRemoved = 0;
        int prefabsFixed = 0;

        foreach (var info in problematicPrefabs.ToList())
        {
            if (info.prefab != null)
            {
                int removed = RemoveMissingScriptsRecursive(info.prefab);
                
                if (removed > 0)
                {
                    // Save the prefab
                    PrefabUtility.SavePrefabAsset(info.prefab);
                    totalRemoved += removed;
                    prefabsFixed++;
                    
                    Debug.Log($"✓ Fixed: {info.name} - Removed {removed} missing script(s)");
                }
            }
        }

        Debug.Log($"=== Fix Complete ===");
        Debug.Log($"Prefabs fixed: {prefabsFixed}");
        Debug.Log($"Total missing scripts removed: {totalRemoved}");

        EditorUtility.DisplayDialog("Fix Complete!",
            $"Successfully fixed {prefabsFixed} prefab(s).\n" +
            $"Removed {totalRemoved} missing script(s).\n\n" +
            $"Your prefabs are now clean! ✓",
            "OK");

        // Rescan to verify
        StartScan();
    }

    private int RemoveMissingScriptsRecursive(GameObject go)
    {
        int removed = 0;

        // Remove from this object
        int count = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
        removed += count;

        // Remove from children
        foreach (Transform child in go.transform)
        {
            removed += RemoveMissingScriptsRecursive(child.gameObject);
        }

        return removed;
    }
}
#endif

