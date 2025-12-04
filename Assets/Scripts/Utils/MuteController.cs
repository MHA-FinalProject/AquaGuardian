using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

/**
 * MuteController is a script that is used to mute all audio sources in the game object.
 */

public class MuteController : MonoBehaviour
{
    [SerializeField] private Sprite soundOnSprite;
    [SerializeField] private Sprite soundOffSprite;
    [SerializeField] private Button button;

    private AudioSource[] audioSources;
    
    void Start()
    {
        // Find all AudioSource components in this GameObject and all children
        audioSources = GetComponentsInChildren<AudioSource>();
        
        // If button is not assigned, try to get it from this GameObject
        if (button == null)
        {
            button = GetComponent<Button>();
        }
        
        if (audioSources.Length == 0)
        {
            Debug.LogWarning("No AudioSource components found on this GameObject or its children");
        }
        else
        {
            Debug.Log($"Found {audioSources.Length} AudioSource components (including children)");
        }
        
        // Initialize button image based on current mute state
        UpdateButtonImage();
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
        
        // Update button image
        UpdateButtonImage();
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
        
        // Update button image
        UpdateButtonImage();
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
        
        // Update button image
        UpdateButtonImage();
    }
    
    private void UpdateButtonImage()
    {
        if (button == null || button.image == null) return;
        
        // Determine current mute state
        bool isMuted = audioSources != null && audioSources.Length > 0 && audioSources[0].mute;
        
        // Update sprite based on mute state
        if (isMuted)
        {
            button.image.sprite = soundOffSprite;
        }
        else
        {
            button.image.sprite = soundOnSprite;
        }
    }
}