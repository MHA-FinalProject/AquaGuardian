using UnityEngine;

/// <summary>
/// Centralized helper to start the next trial run without relying on UI object state.

/// </summary>
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

