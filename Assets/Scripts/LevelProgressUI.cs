using UnityEngine;
using UnityEngine.UI;

// 1. Split UI updates and progress calculations
// 2. Add input validation
// 3. Consider using events for progress updates
// 4. Add more documentation
public class LevelProgressUI : MonoBehaviour
{
    [Header("UI References:")]
    [SerializeField] private Image playerModelImage; // The Image component representing the player model
    [SerializeField] private RectTransform progressBar; // The RectTransform of the progress bar

    [Header("Player & Finish Line Settings:")]
    [SerializeField] private Transform player; // The player transform
    [SerializeField] private Slider slider;

    [Header("Smooth Settings:")]
    [SerializeField] private float smoothSpeed = 5f; 

    private Transform finishLine;
    private float maxDistance; 
    private float currentSliderValue;
    private Vector2 currentPlayerModelPosition;

    // Start is called before the first frame update
    void Start()
    {
        slider.value = 0; // Initialize the slider to 0
        currentSliderValue = slider.value; // Initialize the current slider value
        currentPlayerModelPosition = playerModelImage.rectTransform.anchoredPosition; // Initialize the current player model position
    }

    // Update is called once per frame
    void Update()
    {
        if (finishLine != null && player != null)
        {
            float distance = GetDistance();
            float targetProgress = 1 - (distance / maxDistance);
            targetProgress = Mathf.Clamp01(targetProgress);

            // Debug removed for cleaner gameplay

            // Smoothly update the slider value
            currentSliderValue = Mathf.Lerp(currentSliderValue, targetProgress, Time.deltaTime * smoothSpeed);
            SetProgress(currentSliderValue);

            // Smoothly update the player model position
            UpdatePlayerModelPosition(currentSliderValue);
        }
    }

    float GetDistance()
    {
        return Vector3.Distance(player.position, finishLine.position);
    }

    void SetProgress(float p)
    {
        slider.value = p;
    }

    void UpdatePlayerModelPosition(float progress)
    {
        if (playerModelImage != null && progressBar != null)
        {
            RectTransform playerRectTransform = playerModelImage.rectTransform;
            Vector2 targetPosition = new Vector2(Mathf.Lerp(0, progressBar.rect.width, progress), playerRectTransform.anchoredPosition.y);
            playerRectTransform.anchoredPosition = Vector2.Lerp(playerRectTransform.anchoredPosition, targetPosition, Time.deltaTime * smoothSpeed);
        }
    }

    public void SetFinishLine(Transform finishLineTransform)
    {
        if (finishLineTransform != null)
        {
            finishLine = finishLineTransform;
            maxDistance = GetDistance(); // Update maxDistance after setting the finish line
        }
        else
        {
            Debug.LogWarning("PROGRESS BAR: Finish line transform is null.");
        }
    }

}
