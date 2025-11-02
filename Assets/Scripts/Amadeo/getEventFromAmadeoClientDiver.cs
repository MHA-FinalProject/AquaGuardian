using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;


[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(Rigidbody))]
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
    [SerializeField] GameObject Panel;  
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
        Debug.Log($"[getEventFromAmadeoClientDiver] Selected finger index: {fingerIndex}");
    }

    // Handles the forces received from the Amadeo device
    private void HandleForcesUpdated(float[] forces)
    {
        // NOTE: This part runs only if Amadeo is connected or in emulation mode.
        // If Amadeo is not connected, then the code in PlayerMovement.HandleMovement() is run.

        // Check if the player can move and if the intro text has been shown
        if (pm.canMove && pm.afterText)
        {
            // Ensure the panel is not active and valid forces are received
            if (!Panel.activeSelf && forces != null && forces.Length > 0 && indexForce >= 0 && indexForce < forces.Length)
            {
                // Valid Amadeo input - disable keyboard input for this frame
                pm.notGetForcesFromAmadeo = false;

                // Parse factor force from input field
                float factorForce = 10f; // Default value
                if (factor_force_inputField != null && !string.IsNullOrEmpty(factor_force_inputField.text))
                {
                    if (float.TryParse(factor_force_inputField.text, out float parsedFactor))
                    {
                        factorForce = parsedFactor;
                    }
                }

                // Calculate the target vertical position based on finger force
                float fingerForce = forces[indexForce];
                Debug.Log($"[getEventFromAmadeoClientDiver] Using finger {indexForce}: force={fingerForce:F3}, factor={factorForce:F2}");
                float targetVerticalPosition = fingerForce * factorForce;
                float currentVerticalPosition = transform.position.y;
                float positionDifference = targetVerticalPosition - currentVerticalPosition;
                
                // Calculate vertical input based on position difference
                // If difference is within tolerance, use 0 (will apply idle speed)
                // Otherwise, use sign to indicate direction (-1 down, +1 up)
                float verticalInput = 0f;
                if (Mathf.Abs(positionDifference) >= verticalTolerance)
                {
                    verticalInput = Mathf.Sign(positionDifference); // -1, 0, or +1
                }

                // Use unified movement function from PlayerMovement
                // Pass isAmadeoInput=true to preserve original Amadeo behavior (no idle speed when moving down)
                pm.ApplyMovement(verticalInput, verticalTolerance, isAmadeoInput: true);
                
                // Re-enable keyboard input after processing Amadeo input
                pm.notGetForcesFromAmadeo = true;
            }
            else
            {
                // Invalid Amadeo input (no indexForce selected, panel active, etc.)
                // Allow keyboard input by ensuring notGetForcesFromAmadeo is true
                pm.notGetForcesFromAmadeo = true;
            }
        }
    }
}
