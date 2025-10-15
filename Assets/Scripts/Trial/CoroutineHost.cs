using UnityEngine;

/// Global, always-active coroutine runner. Attach once in the bootstrap scene.
/// Ensures coroutines can start even if UI objects are inactive.

public class CoroutineHost : MonoBehaviour
{
    private static CoroutineHost _instance;
    public static CoroutineHost Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("CoroutineHost");
                _instance = go.AddComponent<CoroutineHost>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }
}



