using UnityEngine;

/**
 * Static class to manage trial progression by interacting with TrialSystemManager.cs
 * This class provides a method to start the next trial by invoking the appropriate method on TrialSystemManager.
 * It assumes that the PanelOpenUp component is on the same GameObject as TrialSystemManager.
 */
public static class TrialRunManager
{
    public static void StartNextTrial(PanelOpenUp panel)
    {
        if (panel == null) return;
        
        // Find TrialSystemManager (should be on same GameObject as PanelOpenUp)
        var trialSystemManager = panel.GetComponent<TrialSystemManager>();
        if (trialSystemManager != null)
        {
            trialSystemManager.ContinueToNextTrial();
        }
        else
        {
            Debug.LogError("TrialSystemManager not found on PanelOpenUp GameObject!");
        }
    }
}

