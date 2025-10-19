using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/**
 * GoToEndGame is a script that is used to load the end game scene when the
 * player reaches the end of the game
 */
public class GoToEndGame : MonoBehaviour
{
    [SerializeField] string sceneName;
    [SerializeField] bool oneShot = true;
    private bool _consumed = false;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

    
        if (oneShot && _consumed) return;
        _consumed = true;

        // Disable collider to prevent multiple triggers
        var col = GetComponent<Collider>();
        if (col) col.enabled = false;

        StartCoroutine(ExportAndLoad());
    }

    private IEnumerator ExportAndLoad()
    {
        // Export session CSV upon reaching the end
        var tracker = Object.FindObjectOfType<CaveTracker>();
        var panel = Object.FindObjectOfType<PanelOpenUp>();
        var health = Object.FindObjectOfType<Health>();
        var player = Object.FindObjectOfType<PlayerMovement>();


        ReportExporter.SaveSessionCsv(tracker, panel, health, player);

        float finalOxygen = health != null ? health.GetOxygen() : 0f;

        // During trials (or when attached to the temporary TrialFish), do NOT load a scene
        bool trials = GameStateManager.AreTrialsActive;
        if (!trials && GameStateManager.Instance == null)
        {
            // Try to recover instance and re-check
            trials = GameStateManager.AreTrialsActive;
        }
        if (trials || gameObject.CompareTag("TrialFish"))
        {
            GameStateManager.NotifyGameEnded(finalOxygen, true);
            if (panel != null)
            {
                panel.OnTrialFishReached(finalOxygen, true);
            }
            yield break;
        }


        GameStateManager.NotifyGameEnded(finalOxygen, true);
        yield return new WaitForEndOfFrame();
        SceneManager.LoadScene(sceneName);
        
    }
}
