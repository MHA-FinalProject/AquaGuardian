using System;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public static class ReportExporter
{
    // Exports a CSV report under Assets/SessionReports/
    public static void SaveSessionCsv(CaveTracker tracker, PanelOpenUp panel, Health health, PlayerMovement player)
    {
        try
        {
            Debug.Log("ReportExporter: Starting CSV export...");
            
            if (tracker == null || panel == null)
            {
                Debug.LogWarning("ReportExporter: Missing tracker or panel - aborting CSV export.");
                return;
            }

            
            string rootDir =
#if UNITY_EDITOR
                Path.Combine(Application.dataPath, "Subject");
#else
                Path.Combine(Application.persistentDataPath, "Subject");
#endif
            try
            {
                Debug.Log($"ReportExporter: Creating directory at: {rootDir}");
                Directory.CreateDirectory(rootDir);
            }
            catch (Exception dirEx)
            {
                Debug.LogError($"ReportExporter: Failed to create directory: {dirEx.Message}");
                return;
            }

            string filename = $"session_report_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            string path = Path.Combine(rootDir, filename);
            
            Debug.Log($"ReportExporter: Full file path: {path}");
            Debug.Log($"ReportExporter: Directory exists: {Directory.Exists(rootDir)}");

            using (var writer = new StreamWriter(path, false))
            {
                Debug.Log($"ReportExporter: StreamWriter created successfully");
                
                // Header: include cave geometry + difficulty + timing details and inter-cave timing + average speed
                writer.WriteLine("CaveIndex,Diameter,Height,Length,Difficulty,ExactTime(s),EstimatedTime(s),InterCaveActual(s),InterCaveEstimated(s),Collisions,AvgSpeed(m_s),ReactionTime(s),EntryTime,ExitTime");
                Debug.Log($"ReportExporter: Header written");

                int count = panel.caveInfos != null ? panel.caveInfos.Count : 0;
                Debug.Log($"ReportExporter: Cave count: {count}");
                float totalTime = 0f;
                int totalCollisions = 0;
                float lastExitTime = -1f; // for computing time between caves
                float configuredSpeed = player != null ? Mathf.Max(0.01f, player.speed) : 0.01f;

                for (int i = 0; i < count; i++)
                {
                    int idx = panel.caveInfos[i].index;
                    var info = panel.caveInfos[i];
                    var stats = tracker.GetStats(idx);
                    if (stats == null)
                    {
                        Debug.LogWarning($"ReportExporter: no stats for cave {idx}");
                        writer.WriteLine($"{idx},{info.diameter:F2},{info.height:F2},{info.length:F2},{info.minZ:F2},{info.maxZ:F2},{info.distanceFromPrevious:F2},{info.difficulty:F2},0,0,0,0,0,-1,-1,NOT_COMPLETED");
                        continue;
                    }

                    // Calculate exact time from entry/exit timestamps
                    float exactTime = (stats.exitTime > 0 && stats.entryTime > 0) ? (stats.exitTime - stats.entryTime) : stats.timeSpent;
                    float estimatedTime = stats.theoreticalTime > 0 ? stats.theoreticalTime : 0f;
                    int collisions = stats.collisions;
                    float avgSpeed = stats.avgForwardSpeed;
                    // Use actual time-based reaction time if available, otherwise distance-based
                    float reactionTime = stats.reactionTimeActual > 0 ? stats.reactionTimeActual : stats.reactionTime;
                    
                    // Format entry/exit times (Time.time is seconds since game start)
                    string entryTimeStr = stats.entryTime > 0 ? $"T+{stats.entryTime:F1}s" : "N/A";
                    string exitTimeStr = stats.exitTime > 0 ? $"T+{stats.exitTime:F1}s" : "N/A";

                    // Inter-cave timing (actual and estimated)
                    float interActual = (lastExitTime > 0 && stats.entryTime > 0) ? Mathf.Max(0f, stats.entryTime - lastExitTime) : 0f;
                    float interEstimated = info != null ? (configuredSpeed > 0f ? info.distanceFromPrevious / configuredSpeed : 0f) : 0f;
                    
                    // Only sum up caves that were actually visited
                    if (exactTime > 0)
                    {
                        totalTime += exactTime;
                        totalCollisions += collisions;
                    }
                    
                    float diameter = info != null ? info.diameter : 0f;
                    float height = info != null ? info.height : 0f;
                    float length = info != null ? info.length : Mathf.Abs((info != null ? info.maxZ - info.minZ : 0f));
                    float difficulty = info != null ? info.difficulty : 0f;

                    Debug.Log($"ReportExporter: Cave {idx} - geom(d={diameter:F2},h={height:F2},l={length:F2}), diff={difficulty:F2}, exactTime={exactTime:F2}s, estimatedTime={estimatedTime:F2}s, interActual={interActual:F2}s, interEstimated={interEstimated:F2}s, collisions={collisions}, avgSpeed={avgSpeed:F2}, reactionTime={reactionTime:F2}");
                    writer.WriteLine($"{idx},{diameter:F2},{height:F2},{length:F2},{difficulty:F2},{exactTime:F2},{estimatedTime:F2},{interActual:F2},{interEstimated:F2},{collisions},{avgSpeed:F2},{reactionTime:F2},{entryTimeStr},{exitTimeStr}");

                    // update last exit time for next cave's inter-cave computation
                    if (stats.exitTime > 0) lastExitTime = stats.exitTime;
                }

                // Summary
                writer.WriteLine();
                writer.WriteLine("Summary");
                writer.WriteLine($"Total_TimeInCaves,{totalTime:F2}");
                writer.WriteLine($"Total_Collisions_InCaves,{totalCollisions}");
                
                // Get overall collision count from PlayerLife
                var playerLife = UnityEngine.Object.FindObjectOfType<PlayerLife>();
                int overallCollisions = playerLife != null ? playerLife.GetCollisionCount() : -1;
                writer.WriteLine($"Total_Collisions_Overall,{overallCollisions}");
                // If CaveTracker exposes outside collisions, include it too
                try
                {
                    var outsideField = typeof(CaveTracker).GetField("outsideCollisions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (outsideField != null)
                    {
                        int outside = (int)outsideField.GetValue(tracker);
                        writer.WriteLine($"Outside_Collisions,{outside}");
                        writer.WriteLine($"Tracker_Total_IncludingOutside,{tracker.GetTotalCollisions() + outside}");
                    }
                }
                catch {}

                // Add final oxygen back to summary
                float finalOxygen = health != null ? SafeGetOxygen(health) : -1f;
                if (finalOxygen >= 0f) writer.WriteLine($"Final_Oxygen_Percent,{finalOxygen:F0}");
            }

            try
            {
                var size = new FileInfo(path).Length;
                Debug.Log($"Session CSV exported to: {path} (size {size} bytes)");
            }
            catch (Exception sizeEx)
            {
                Debug.LogWarning($"ReportExporter: Could not stat file size: {sizeEx.Message}");
            }

// AssetDatabase.Refresh() removed - to avoid interrupting user workflow
        }
        catch (Exception e)
        {
            Debug.LogError($"ReportExporter: Failed to export CSV: {e.Message}");
        }
    }

    // Exports a concise TXT summary alongside the CSV
    public static void SaveSessionTxt(CaveTracker tracker, PanelOpenUp panel, Health health, PlayerMovement player)
    {
        try
        {
            if (tracker == null)
            {
                Debug.LogWarning("ReportExporter: Missing tracker - aborting TXT export.");
                return;
            }

            string rootDir =
#if UNITY_EDITOR
                Path.Combine(Application.dataPath, "Subject");
#else
                Path.Combine(Application.persistentDataPath, "Subject");
#endif
            Directory.CreateDirectory(rootDir);

            string filename = $"session_report_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            string path = Path.Combine(rootDir, filename);

            using (var writer = new StreamWriter(path, false))
            {
                writer.WriteLine($"Report Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                writer.WriteLine("CaveIndex, Time(s), Collisions, ReactionTime(s)");

                float totalTime = 0f;
                int totalCollisions = 0;
                int cavesWithReaction = 0;

                // Determine cave order
                System.Collections.Generic.List<int> caveOrder = new System.Collections.Generic.List<int>();
                if (panel != null && panel.caveInfos != null && panel.caveInfos.Count > 0)
                {
                    for (int i = 0; i < panel.caveInfos.Count; i++) caveOrder.Add(panel.caveInfos[i].index);
                }
                else
                {
                    foreach (var kv in tracker.GetType().GetField("statsByIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(tracker) as System.Collections.IDictionary)
                    {
                        // Fallback: skip reflection-heavy usage; will be handled below in try-catch
                    }
                    // If reflection is blocked, fallback to 1..N
                    int maxCaves = panel != null && panel.caveInfos != null ? panel.caveInfos.Count : 0;
                    for (int i = 1; i <= maxCaves; i++) caveOrder.Add(i);
                }

                foreach (int idx in caveOrder)
                {
                    var s = tracker.GetStats(idx);
                    if (s == null) continue;
                    
                    // Only count caves that were actually visited
                    if (s.timeSpent > 0)
                    {
                        totalTime += s.timeSpent;
                        totalCollisions += s.collisions;
                    }

                    // Use actual time-based reaction time if available, otherwise distance-based
                    float reactionTime = s.reactionTimeActual > 0 ? s.reactionTimeActual : s.reactionTime;
                    string reactionStr = reactionTime > 0 ? $"{reactionTime:F2}" : "N/A";
                    if (reactionTime > 0) cavesWithReaction++;

                    writer.WriteLine($"{idx}, {s.timeSpent:F2}, {s.collisions}, {reactionStr}");
                }

                writer.WriteLine();
                writer.WriteLine("Summary");
                writer.WriteLine($"Total_TimeInCaves,{totalTime:F2}");
                writer.WriteLine($"Total_Collisions_InCaves,{totalCollisions}");
                
                // Get overall collision count from PlayerLife
                var playerLife = UnityEngine.Object.FindObjectOfType<PlayerLife>();
                int overallCollisions = playerLife != null ? playerLife.GetCollisionCount() : -1;
                writer.WriteLine($"Total_Collisions_Overall,{overallCollisions}");
                writer.WriteLine($"Reactions_Captured,{cavesWithReaction}/{(panel != null && panel.caveInfos != null ? panel.caveInfos.Count : 0)}");
                float finalOxygenTxt = health != null ? SafeGetOxygen(health) : -1f;
                if (finalOxygenTxt >= 0f) writer.WriteLine($"Final_Oxygen_Percent,{finalOxygenTxt:F0}");
            }

            Debug.Log($"Session TXT exported to: {path}");
        }
        catch (Exception e)
        {
            Debug.LogError($"ReportExporter: Failed to export TXT: {e.Message}");
        }
    }

    private static float SafeGetOxygen(Health h)
    {
        try 
        { 
            return h.GetOxygen(); 
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"ReportExporter: GetOxygen failed: {ex.Message}");
            return -1f;
        }
    }

#if UNITY_EDITOR
    [MenuItem("Tools/Open Subject Folder")]
    public static void OpenSubjectFolder()
    {
        string dir = Path.Combine(Application.dataPath, "Subject");
        Directory.CreateDirectory(dir);
        EditorUtility.RevealInFinder(dir);
        Debug.Log($"Opened Subject folder: {dir}");
    }

    [MenuItem("Tools/Show persistentDataPath")]
    public static void ShowPersistentDataPath()
    {
        string persistentPath = Path.Combine(Application.persistentDataPath, "Subject");
        Directory.CreateDirectory(persistentPath);
        Debug.Log($"persistentDataPath: {Application.persistentDataPath}");
        Debug.Log($"Subject folder in persistent: {persistentPath}");
        EditorUtility.RevealInFinder(persistentPath);
    }
#endif
}


