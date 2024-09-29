using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.VisualScripting;

public class getEventFromAmadeoClientDiver : MonoBehaviour
{
    // === Amadeo Client & Movement Parameters ===
    [Header("Amadeo Client")]
    [SerializeField] private AmadeoClient amadeoClient;  // Reference to the AmadeoClient script
    [SerializeField] float factor_forces = 10f;  // Multiplier for forces received from the Amadeo device

    [Header("Movement Settings")]
    [SerializeField] private float smoothSpeed = 1.5f;  // Smoothing factor for movement speed
    [SerializeField] float verticalTolerance = 0.1f;  // Tolerance for vertical movement to avoid unnecessary small adjustments

    // === UI Elements ===
    [Header("UI Components")]
    [SerializeField] GameObject Panel;  // Reference to a UI panel
    public TMP_InputField factor_force_inputField;  // Input field to adjust the force multiplier

    // === Internal State ===
    private Rigidbody rb;  // Rigidbody component for physics-based movement
    private PlayerMovement pm;  // Reference to the PlayerMovement script
    private int indexForce = -1;  // Index of the selected finger (force to be used)

    void Start()
    {
        rb = GetComponent<Rigidbody>();  // Get the Rigidbody component attached to the GameObject
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;  // Set collision detection mode to Continuous for better accuracy

        pm = GetComponent<PlayerMovement>();  // Get the PlayerMovement script component
    }

    // Subscribe to the OnForcesUpdated event when the object is enabled
    private void OnEnable()
    {
        if (amadeoClient != null)
        {
            amadeoClient.OnForcesUpdated += HandleForcesUpdated;
        }
    }

    // Unsubscribe from the OnForcesUpdated event when the object is disabled
    private void OnDisable()
    {
        if (amadeoClient != null)
        {
            amadeoClient.OnForcesUpdated -= HandleForcesUpdated;
        }
    }

    // Method to select which finger's force will control the movement
    public void SelectFinger(int fingerIndex)
    {
        indexForce = fingerIndex;
    }

    // Handles the forces received from the Amadeo device
    private void HandleForcesUpdated(float[] forces)
    {
        Debug.Log(indexForce);

        // Check if the player can move and if the intro text has been shown
        if (pm.canMove && pm.afterText)
        {
            // Ensure the panel is not active and valid forces are received
            if (!Panel.activeSelf && forces != null && forces.Length > 0)
            {
                pm.notGetForcesFromAmadeo = false;  // Enable force reception from Amadeo

                Vector3 movementDirection = Vector3.forward;  // Define movement along the z-axis
                Vector3 targetVelocity = pm.speed * transform.TransformDirection(movementDirection);  // Calculate target velocity

                Debug.Log("factor_force: " + float.Parse(factor_force_inputField.text));

                // Calculate the new vertical position based on finger force
                float newVerticalPosition = forces[indexForce] * float.Parse(factor_force_inputField.text);
                float currentVerticalPosition = transform.position.y;
                float verticalMovementSpeed;

                // Adjust vertical speed based on the difference between current and target vertical positions
                if (Mathf.Abs(newVerticalPosition - currentVerticalPosition) < verticalTolerance)
                {
                    verticalMovementSpeed = pm.idleUpwardSpeed;  // Apply idle upward speed if within tolerance
                }
                else
                {
                    verticalMovementSpeed = Mathf.Sign(newVerticalPosition - currentVerticalPosition) * pm.verticalSpeed;  // Move up or down
                }

                // Add the calculated vertical movement to the target velocity
                targetVelocity += verticalMovementSpeed * transform.TransformDirection(Vector3.up);
                Debug.Log("targetVelocity = " + targetVelocity);

                rb.velocity = targetVelocity;  // Apply the target velocity to the Rigidbody

                pm.notGetForcesFromAmadeo = true;  // Disable force reception after applying movement
            }
        }
    }
}
