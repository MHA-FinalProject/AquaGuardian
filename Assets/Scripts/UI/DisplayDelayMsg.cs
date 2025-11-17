using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DisplayDelayMsg : MonoBehaviour
{

     [SerializeField] private float firstDelay;
    [SerializeField] private string message;

    [SerializeField] private string updateMessage;
    [SerializeField] private float updateDelayTime;

    private bool hasUpdatedText = false;

    void Start()
    {
    
        DisplayManager.DisplayMessage(message, firstDelay);
    }

    void Update()
    {
        // Update text after a certain delay
        if (!hasUpdatedText && Time.timeSinceLevelLoad >= updateDelayTime)
        {
            DisplayManager.UpdateMsgText(updateMessage, updateDelayTime);
            hasUpdatedText = true; // Set flag to prevent multiple updates
            
        }
    }



}
