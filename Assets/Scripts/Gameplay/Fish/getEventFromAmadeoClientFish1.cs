using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class getEventFromAmadeoClientFish1 : MonoBehaviour
{
    [SerializeField] private AmadeoClient amadeoClient;

    // Smoothing factor to control how quickly the object moves towards the target position
    private float smoothSpeed = 0.5f;

    [SerializeField] GameObject Panel;

    [SerializeField] GameObject player = null;

    private float minPositionY = 0;
    private float maxPositionY = 0;

    private float offsetBarrier = 1.1f;

    private void OnEnable()
    {
        if (amadeoClient != null)
        {
            amadeoClient.OnForcesUpdated += HandleForcesUpdated;
        }
    }

    private void OnDisable()
    {
        if (amadeoClient != null)
        {
            amadeoClient.OnForcesUpdated -= HandleForcesUpdated;
        }
    }

    private void HandleForcesUpdated(float[] forces)
    {
        minPositionY = player.transform.position.y - offsetBarrier;

        maxPositionY = player.transform.position.y + offsetBarrier;

        if (!Panel.activeSelf && forces != null && forces.Length > 0)
        {
            float forceValue = forces[0];

            if (transform.position.y + forceValue > minPositionY && transform.position.y + forceValue < maxPositionY)
            {
                Vector3 newPosition = new Vector3(transform.position.x, transform.position.y + (forceValue), transform.position.z);
                gameObject.transform.position = Vector3.Lerp(transform.position, newPosition, smoothSpeed * Time.deltaTime);
            }
            else if (transform.position.y + forceValue < minPositionY)
            {
                Vector3 newPosition = new Vector3(transform.position.x, minPositionY, transform.position.z);
                gameObject.transform.position = Vector3.Lerp(transform.position, newPosition, smoothSpeed * Time.deltaTime);
            }
            else if (transform.position.y + forceValue > maxPositionY)
            {
                Vector3 newPosition = new Vector3(transform.position.x, maxPositionY, transform.position.z);
                gameObject.transform.position = Vector3.Lerp(transform.position, newPosition, smoothSpeed * Time.deltaTime);
            }

            /*// Move the fish based on forces[0]
            Vector3 newPosition = new Vector3(transform.position.x,transform.position.y + (forceValue),transform.position.z);
            gameObject.transform.position = Vector3.Lerp(transform.position, newPosition, smoothSpeed * Time.deltaTime);*/
        }
    }

}
