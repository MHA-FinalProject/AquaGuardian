using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
//TODO remove this script
public class SliderController : MonoBehaviour
{
    // Sliders

//    [SerializeField] private TextMeshProUGUI sliderText_scale_Y = null;
//    [SerializeField] private TextMeshProUGUI sliderText_pos_Y = null;
// //   [SerializeField] private TextMeshProUGUI sliderText_pos_Z = null;
//    [SerializeField] private TextMeshProUGUI sliderText_scale_Z = null;
//    [SerializeField] public GameObject objectToScale = null; // Reference to the object you want to scale
//    [SerializeField] private float maxSliderAmount = 100.0f;
//    [SerializeField] private float maxYScale; // Maximum scale of the object in the Y-axis
//    [SerializeField] private float maxZScale; // Maximum scale of the object in the Y-axis
//    [SerializeField] private float maxPositionY; // Maximum position of the object in the Y-axis
//    [SerializeField] private float maxPositionZ;
//    [SerializeField] public GameObject waterSurface = null;
//    // [SerializeField] private float startPosition_Z = 400f; // Starting position of the object in the Z-axis
//    // [SerializeField] private float maxPosition_Z = 1100f; // Maximum position of the object in the Z-axis

//    private float lerpA_scaleY = 0.35f;
//    private float lerpA_posY = -53.82798f;
//    private float lerpA_scaleZ = 0.5f;



//    public void SliderChange_scale_y(float value)
//    {
//        float scaledValue = value * maxSliderAmount;
//        sliderText_scale_Y.text = scaledValue.ToString("0");

//        // Calculate the new scale based on the slider value
//        float newYScale = Mathf.Lerp(lerpA_scaleY, maxYScale, (scaledValue / maxSliderAmount)*0.01f);

//        // Get the current scale of the object
//        Vector3 currentScale = objectToScale.transform.localScale;

//        // Modify only the Y component of the scale
//        Vector3 newScale = new Vector3(currentScale.x, newYScale, currentScale.z);

//        // Apply the new scale to the object
//        objectToScale.transform.localScale = newScale;
//    }

//    public void SliderChange_pos_y(float value)
//    {
//        float scaledValue = value * maxSliderAmount;
//        sliderText_pos_Y.text = scaledValue.ToString("0");

//        // Calculate the new Y position based on the slider value
//        float newYPosition = Mathf.Lerp(lerpA_posY, maxPositionY, scaledValue / maxSliderAmount);

//        // Get the current position of the object
//        Vector3 currentPosition = objectToScale.transform.position;

//        // Modify only the Y component of the position
//        Vector3 newPosition = new Vector3(currentPosition.x, newYPosition, currentPosition.z);

//        // Apply the new position to the object
//        objectToScale.transform.position = newPosition;
//    }

///*    public void SliderChange_pos_z(float value)
//    {
//        float scaledValue = value * maxSliderAmount;
//        sliderText_pos_Z.text = scaledValue.ToString("0");

//        // Calculate the new Z position based on the slider value
//        float newZPosition = Mathf.Lerp(startPosition_Z, maxPositionZ, scaledValue / maxSliderAmount);

//        // Get the current position of the object
//        Vector3 currentPosition = objectToScale.transform.position;

//        // Modify only the Z component of the position
//        Vector3 newPosition = new Vector3(currentPosition.x, currentPosition.y, newZPosition);

//        // Apply the new position to the object
//        objectToScale.transform.position = newPosition;
//    }*/

//    public void SliderChange_scale_z(float value)
//    {
//        float scaledValue = value * maxSliderAmount;
//        sliderText_scale_Z.text = scaledValue.ToString("0");

//        // Calculate the new scale based on the slider value
//        float newZScale = Mathf.Lerp(lerpA_scaleZ, maxZScale, scaledValue / maxSliderAmount);

//        // Get the current scale of the object
//        Vector3 currentScale = objectToScale.transform.localScale;

//        // Modify only the Y component of the scale
//        Vector3 newScale = new Vector3(currentScale.x, currentScale.y, newZScale);

//        // Apply the new scale to the object
//        objectToScale.transform.localScale = newScale;

//    }


}
