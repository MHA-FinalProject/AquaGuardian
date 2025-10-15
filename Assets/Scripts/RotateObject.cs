using UnityEngine;
/**
 * This script rotates the attached GameObject around its axes at a constant speed.
 * The rotation speed is defined by the rotationSpeed vector, which specifies degrees per second for each axis.
 * The rotation is applied in the Update method to ensure smooth and continuous rotation over time.
 * Attach this script on oxygen objects.
 */

public class RotateObject : MonoBehaviour
{
    // Rotation speed around each axis
    private Vector3 rotationSpeed = new Vector3(0, 100, 0);

    void Update()
    {
        // Rotate the object based on the rotation speed
        gameObject.transform.Rotate(rotationSpeed * Time.deltaTime);
    }
}