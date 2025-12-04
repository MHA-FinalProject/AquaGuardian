using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace UIAssistant
{
    [HelpURL("https://sites.google.com/view/uiassistant/documentation/hint-management?authuser=0#h.k66dkew91mh4")]
    public class HintSnippet : MonoBehaviour
    {
        #region Variables
        [Tooltip("Image that will be instantiated to display the Hint Context entry's button sprites.")] public Image ButtonTemplate;
        [Tooltip("Graphic that will appear between icons if the Hint Context entry prompts a button combination.")] public GameObject AndSeparator;
        [Tooltip("Graphic that will appear between icons when the Hint Context entry lists multiple buttons.")] public GameObject OrSeparator;
        [Tooltip("The Text Mesh Pro UGUI that will display the Hint Context entry's label.")] public TextMeshProUGUI LabelText;
        #endregion

        #region Function
        /// <summary>
        /// Sets the Hint Snippet's parameters based on a Hint Context entry. This is handled by the Hint Manager.
        /// </summary>
        /// <param name="index">The index of the Hint Context entry, stored in the Hint Manager.</param>
        public void SetSnippet(int index)
        {
            HintContext.HintEntry hintEntry = HintManager.ActiveHintContext.Entries[index];

            LabelText.text = hintEntry.Label.ProperString;

            Transform parent = ButtonTemplate.transform.parent;
            GameObject separator = hintEntry.ButtonCombinatiton ? AndSeparator : OrSeparator;

            if (hintEntry.ButtonSprites.Count == 0) Destroy(ButtonTemplate.transform.parent.gameObject);
            else
            {
                for (int i = 0; i < hintEntry.ButtonSprites.Count; i++)
                {
                    if (i > 0 && separator != null) Instantiate(separator, parent);
                    Image buttonTemplate = Instantiate(ButtonTemplate, parent);
                    buttonTemplate.sprite = hintEntry.ButtonSprites[i];
                }
            }

            Destroy(ButtonTemplate.gameObject);
            Destroy(AndSeparator);
            Destroy(OrSeparator);
        }
        #endregion
    }
}