using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

public class DisplayManager : MonoBehaviour
{
    [SerializeField] private Image bannerImage;
    [SerializeField] private TextMeshProUGUI displayTextComponent;
    [SerializeField] private float fadeDuration = 1f; // fade duration
    [SerializeField] private float backDelay = 5f;

    private float finishScale = 1f;
    private float currentScale = 0f;

    // Singleton pattern
    private static DisplayManager instance;

    public static DisplayManager Instance
    {
        get { if (instance == null) { Debug.Log("DisplayManager instance is null"); } return instance; }
    }
   



    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            instance = this;
        }
    }

    public static void DisplayMessage(string text, float delay)
    {

        if (instance != null)
        {
            instance.StartCoroutine(instance.DelayedMsgRoutine(text, delay));
        }
    }
    public static void UpdateMsgText(string newText, float delay)
    {
        if (instance != null)
        {
            instance.StopAllCoroutines();
            instance.StartCoroutine(instance.UpdateTextBack(newText, delay));
        }
    }


    private IEnumerator UpdateTextBack(string newText, float delay)
    {
        yield return new WaitForSeconds(delay);

        displayTextComponent.text = newText;
        displayTextComponent.color = new Color(0f, 0f, 0f, 1f); // Set text color to black

        yield return new WaitForSeconds(backDelay);

        yield return StartCoroutine(BackMsg());
    }

    private IEnumerator DelayedMsgRoutine(string text, float delay)
    {
        yield return new WaitForSeconds(delay);

        yield return StartCoroutine(MsgRoutine(text));

        yield return new WaitForSeconds(backDelay);

        yield return StartCoroutine(BackMsg());
    }

    private IEnumerator MsgRoutine(string text)
    {
        yield return StartCoroutine(DisplayTime(text));
    }


    private IEnumerator DisplayTime(string text)
    {
        displayTextComponent.text = text;

        displayTextComponent.color = new Color(0f, 0f, 0f, 1f);

        yield return null;
    }

    private IEnumerator BackMsg()
    {
        float elapsedTime = 0f;
        while (elapsedTime < fadeDuration)
        {
            currentScale = Mathf.Lerp(finishScale, 0f, elapsedTime / fadeDuration);
            bannerImage.transform.localScale = new Vector3(currentScale, 1f, 1f);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        bannerImage.transform.localScale = new Vector3(0f, 1f, 1f);
        displayTextComponent.text = "";

    }


}
