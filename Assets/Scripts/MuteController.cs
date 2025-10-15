using UnityEngine;

/**
 * MuteController is a script that is used to mute all audio sources in the game object.
 */

public class MuteController : MonoBehaviour
{
    private AudioSource[] audioSources;
    
    void Start()
    {
        // Find all AudioSource components in this GameObject and all children
        audioSources = GetComponentsInChildren<AudioSource>();
        
        if (audioSources.Length == 0)
        {
            Debug.LogWarning("No AudioSource components found on this GameObject or its children");
        }
        else
        {
            Debug.Log($"Found {audioSources.Length} AudioSource components (including children)");
        }
    }
    
    public void MuteAndUnMute() 
    {
        if (audioSources != null && audioSources.Length > 0)
        {
            // Toggle mute state for all audio sources
            bool newMuteState = !audioSources[0].mute;
            
            foreach (var audioSource in audioSources)
            {
                if (audioSource != null)
                {
                    audioSource.mute = newMuteState;
                }
            }
            
            Debug.Log($"All audio sources {(newMuteState ? "muted" : "unmuted")}");
        }
    }
    
    public void UnMute() 
    { 
        if (audioSources != null && audioSources.Length > 0)
        {
            // Unmute all audio sources
            foreach (var audioSource in audioSources)
            {
                if (audioSource != null)
                {
                    audioSource.mute = false;
                }
            }
            
            Debug.Log("All audio sources unmuted");
        }
    }
    
    public void Mute()
    {
        if (audioSources != null && audioSources.Length > 0)
        {
            // Mute all audio sources
            foreach (var audioSource in audioSources)
            {
                if (audioSource != null)
                {
                    audioSource.mute = true;
                }
            }
            
           // Debug.Log("All audio sources muted");
        }
    }
}