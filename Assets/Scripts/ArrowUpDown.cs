using UnityEngine;

public class ArrowUpDown : MonoBehaviour
{
    // Speed of the movement
    public float speed = 2.0f;

    // The distance the arrow will move up and down
    public float distance = 2f;

    // Initial position of the arrow
    private Vector3 startPosition;

    void Start()
    {
        // Store the starting position of the arrow
        startPosition = gameObject.transform.position;
    }

    void Update()
    {
        // Calculate the new position
        float newY = startPosition.y + Mathf.Sin(Time.time * speed) * distance;
        transform.position = new Vector3(startPosition.x, newY, startPosition.z);
    }
}