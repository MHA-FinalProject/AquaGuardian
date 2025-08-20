using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GoToEndGame : MonoBehaviour
{
    [SerializeField] string sceneName;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Player reached finish line - exporting session report...");
            StartCoroutine(ExportAndLoad());
        }
    }

    private IEnumerator ExportAndLoad()
    {
        // Export session CSV upon reaching the end
        var tracker = Object.FindObjectOfType<CaveTracker>();
        var panel = Object.FindObjectOfType<PanelOpenUp>();
        var health = Object.FindObjectOfType<Health>();
        var player = Object.FindObjectOfType<PlayerMovement>();
        
        Debug.Log($"Components found: Tracker={tracker != null}, Panel={panel != null}, Health={health != null}, Player={player != null}");
        
        ReportExporter.SaveSessionCsv(tracker, panel, health, player);

        // Wait one frame to ensure file writing completes
        yield return new WaitForEndOfFrame();
        
        Debug.Log("Loading next scene...");
        SceneManager.LoadScene(sceneName);
    }
}
