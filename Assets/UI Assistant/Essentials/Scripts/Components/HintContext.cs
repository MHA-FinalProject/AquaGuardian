using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UIAssistant
{
    [HelpURL("https://sites.google.com/view/uiassistant/documentation/hint-management?authuser=0#h.m377sqh4t1c4")]
    public class HintContext : MonoBehaviour
    {
        #region Variables
        [Tooltip("When set to true, Hint Manager will be notified when the Hint Context is enabled.")] public bool ShowOnEnable;
        [Tooltip("When set to true, entries' labels will require Localization Sets instead of strings.")] public bool Localized;
        [Tooltip("One or multiple hint entries to be displayed as Hint Snippets.")] public List<HintEntry> Entries = new();
        #endregion

        #region Classes
        [System.Serializable]
        public class HintEntry
        {
            [Tooltip("Text to be displayed by the Hint Manager.")] public TextSettings.StringOrLocalizationSet Label;
            [Tooltip("Check this box if the prompted function requires multiple buttons to be pressed.")] public bool ButtonCombinatiton;
            [Tooltip("Icons used to refer to specific keyboard or controller buttons.")] public List<Sprite> ButtonSprites = new();
        }
        #endregion

        #region Function
        void OnEnable()
        {
            if (ShowOnEnable) Show();
        }
        void OnDisable()
        {
            if (ShowOnEnable) Hide();
        }

        /// <summary>
        /// Notifies the Hint Manager to show this Hint Context's entries.
        /// </summary>
        public void Show()
        {
            if (HintManager.Instance != null) HintManager.ShowHints(this);
            else Debug.LogWarning("Hint Context failed to display hints; please add a Hint Manager to your scene.");
        }

        /// <summary>
        /// Notifies the Hint Manager that this Hint Context's entries should no longer be shown.
        /// </summary>
        public void Hide()
        {
            if (HintManager.Instance != null) HintManager.RemoveHints(this);
        }
        #endregion

#if UNITY_EDITOR

        #region Function
        void OnValidate()
        {
            for (int i = 0; i < Entries.Count; i++)
            {
                Entries[i].Label.Localized = Localized;
            }
        }
        #endregion

#endif
    }
}