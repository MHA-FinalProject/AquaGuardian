using UnityEngine;

public class FishMover2 : MonoBehaviour
{
    [SerializeField] GameObject player;
    Vector3 lastPlayerPosition;

    Vector3 moveDirection;
    float moveSpeed;

    // Factor to reduce the speed
    private float speedFactor = 3f;

    // Smoothness factor for interpolation
    private float smoothness = 2f;

    // Start is called before the first frame update
    void Start()
    {
        // Initialize the last player position
        lastPlayerPosition = player.transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        // Calculate the movement direction and speed based on player's movement
        CalculateMovement();

        // Move the fish
        MoveFish();

        CheckVerticalMovementInput();
    }

    // Calculate the movement direction and speed based on player's movement
    void CalculateMovement()
    {
        // Calculate movement direction
        Vector3 currentPosition = player.transform.position;
        Vector3 playerMovement = currentPosition - lastPlayerPosition;
        float deltaX = playerMovement.x;
        float deltaY = playerMovement.y;
        float deltaZ = playerMovement.z;

        // Calculate movement speed (magnitude of the direction vector)
        moveSpeed = playerMovement.magnitude / Time.deltaTime * speedFactor;

        // Update the last player position
        lastPlayerPosition = currentPosition;
    }

    // Move the fish
    void MoveFish()
    {
        // Smoothly interpolate between the current position and the new position
        Vector3 targetPosition = new Vector3(transform.position.x, transform.position.y, player.transform.position.z) + moveDirection.normalized * moveSpeed * Time.deltaTime;
        transform.position = Vector3.Lerp(transform.position, targetPosition, smoothness * Time.deltaTime);
    }

    // Check for vertical movement input
    void CheckVerticalMovementInput()
    {
        // Check for 'G' key press to move down
        if (Input.GetKeyDown(KeyCode.H))
        {
            transform.Translate(Vector3.down);
        }

        // Check for 'Y' key press to move up
        if (Input.GetKeyDown(KeyCode.Y))
        {
            transform.Translate(Vector3.up);
        }
    }
}