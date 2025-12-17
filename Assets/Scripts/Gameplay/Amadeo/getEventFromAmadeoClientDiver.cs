using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
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
   // [SerializeField] private float smoothSpeed = 1.5f;  // Smoothing factor for movement speed
    [SerializeField] float verticalTolerance = 0.1f;  // Tolerance for vertical movement to avoid unnecessary small adjustments
   //[SerializeField] bool invertForceDirection = false;  // Invert force direction (use if forces are negative)

    // === UI Elements ===
    [Header("UI Components")]
    [SerializeField] GameObject Panel;  // Reference to a UI panel
    public TMP_InputField factor_force_inputField;  // Input field to adjust the force multiplier

    // === Internal State ===
    private Rigidbody rb;  // Reference to the Rigidbody component
    private PlayerMovement pm;  // Reference to the PlayerMovement script
    private int indexForce = -1;  // Index of the selected finger (force to be used)

    void Start()
    {
        rb = GetComponent<Rigidbody>();  // Get the Rigidbody component
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
        // NOTE: this part runs only if Amadeo is connected.
        // If Amadeo is not connected, then the code in PlayerMovement.cs is run.

        // TODO: Test with real Amadeo.

        // TODO: Merge both functions to a single, easy-to-read function.

        Debug.Log(indexForce);

        // Check if the player can move and if the intro text has been shown
        if (pm.canMove && pm.afterText)
        {
            // Ensure the panel is not active and valid forces are received
            if (!Panel.activeSelf && forces != null && forces.Length > 0)
            {
                pm.notGetForcesFromAmadeo = false;  // Enable force reception from Amadeo

                Vector3 horizontalVelocity = pm.speed * transform.TransformDirection(Vector3.forward); // move along the z-axis (forward direction)

                Debug.Log("factor_force: " + float.Parse(factor_force_inputField.text));

                // Calculate the new vertical position based on finger force
                float newVerticalPosition = forces[indexForce] * float.Parse(factor_force_inputField.text);
                float currentVerticalPosition = transform.position.y;

                // Calculate vertical speed based on the difference between current and target vertical positions
                float verticalMovementSpeed = Mathf.Abs(newVerticalPosition - currentVerticalPosition) < verticalTolerance?
                    pm.idleUpwardSpeed:                                                             // Apply idle upward speed if within tolerance
                    Mathf.Sign(newVerticalPosition - currentVerticalPosition) * pm.verticalSpeed;   // Move up or down
                Vector3 verticalVelocity = verticalMovementSpeed * transform.TransformDirection(Vector3.up);

                rb.velocity = horizontalVelocity + verticalVelocity;
                pm.notGetForcesFromAmadeo = true;  // Disable force reception after applying movement
            }
        }
    }
    
/*

//previous code Receives forces from Amadeo or emulation , translates them into "vertical movement intent" (up / down / don't move), and passes the decision
// to PlayerMovement to perform the actual movement
    // Handles the forces received from the Amadeo device
    private void HandleForcesUpdated(float[] forces)
    {
        // NOTE: this part runs only if Amadeo is connected or in emulation mode.
        // If Amadeo is not connected, then the code in PlayerMovement.cs is run.

        Debug.Log(indexForce);

        // Check if the player can move and if the intro text has been shown
        if (pm.canMove && pm.afterText)
        {
            // Ensure the panel is not active and valid forces are received
            if (!Panel.activeSelf && forces != null && forces.Length > 0 && indexForce >= 0 && indexForce < forces.Length)
            {
                pm.notGetForcesFromAmadeo = false;  // Disable keyboard input - using Amadeo forces

                // Use factor_forces as default, override from input field if available
                float factorForce = factor_forces;
                if (factor_force_inputField != null && !string.IsNullOrEmpty(factor_force_inputField.text))
                {
                    if (float.TryParse(factor_force_inputField.text, out float parsedFactor))
                    {
                        factorForce = parsedFactor;
                    }
                }

                // Calculate the target vertical position based on finger force
                float fingerForce = forces[indexForce];
                if (invertForceDirection) fingerForce = -fingerForce;  // Invert if needed (for negative force data)
                
                Debug.Log($"[Amadeo] finger {indexForce}: force={fingerForce:F3}, factor={factorForce:F2}");
                
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

                Debug.Log($"[Amadeo] target={targetVerticalPosition:F2}, current={currentVerticalPosition:F2}, diff={positionDifference:F2}, input={verticalInput}");

                // Use unified movement function from PlayerMovement
                // Pass isAmadeoInput=true to use Amadeo behavior (exact sign direction, no idle speed when moving down)
                pm.ApplyMovement(verticalInput, verticalTolerance, isAmadeoInput: true);

                pm.notGetForcesFromAmadeo = true;  // Re-enable keyboard fallback after applying movement
            }
            else
            {
                // No valid finger selected or panel is active - allow keyboard input
                pm.notGetForcesFromAmadeo = true;
            }
        }
        else
        {
            // Player can't move yet - ensure keyboard fallback is available
            pm.notGetForcesFromAmadeo = true;
        }
    }
    */

    /*
     // Handles the forces received from the Amadeo device
    private void HandleForcesUpdated(float[] forces)
    {
        // NOTE: This part runs only if Amadeo is connected or in emulation mode.
        // If Amadeo is not connected, then the code in PlayerMovement.HandleMovement() is run.

        // Safety null checks
        if (pm == null || Panel == null) return;

        // CRITICAL FIX: Always ensure keyboard fallback is enabled when player can't move
        // This prevents keyboard from being stuck disabled between trials
        if (!pm.canMove || !pm.afterText)
        {
            pm.notGetForcesFromAmadeo = true;  // Enable keyboard fallback
            return;
        }

        // Player can move - process Amadeo/Emulation input
        // Ensure the panel is not active and valid forces are received
        if (!Panel.activeSelf && forces != null && forces.Length > 0 && indexForce >= 0 && indexForce < forces.Length)
        {
            // Valid Amadeo input - disable keyboard input for this frame
            pm.notGetForcesFromAmadeo = false;

            // Use cached factor force (synced from input field in Update)
            float factorForce = cachedFactorForce;

            // Calculate the target vertical position based on finger force
            float fingerForce = forces[indexForce];
            // Debug.Log($"[getEventFromAmadeoClientDiver] Using finger {indexForce}: force={fingerForce:F3}, factor={factorForce:F2}");
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
            // Invalid Amadeo input (panel active, etc.)
            // Allow keyboard input by ensuring notGetForcesFromAmadeo is true
            pm.notGetForcesFromAmadeo = true;
        }
    }
    */
}
