using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UIAssistant
{
    [HelpURL("https://sites.google.com/view/uiassistant/documentation/popup-management?authuser=0#h.owx3yrkgfeoh")]
    public class PopupContext : MonoBehaviour
    {
        #region Variables
        [Tooltip("When set to true, Popup Manager will be notified when the Popup Context is enabled.")] public bool TriggerOnEnable;
        [Tooltip("When set to true, buttons' labels will require Localization Sets instead of strings.")] public bool Localized;
        [Tooltip("Text to be displayed at the top of the popup window.")] public TextSettings.StringOrLocalizationSet HeaderText;
        [Tooltip("Text that serves as the main content of the popup window.")] public TextSettings.StringOrLocalizationSet BodyText;
        [Tooltip("One or multiple buttons with on-click functions.")] public List<PopupButton> Buttons = new();
        #endregion

        #region Classes
        [System.Serializable]
        public class PopupButton
        {
            [Tooltip("Text to be displayed on the button.")] public TextSettings.StringOrLocalizationSet Label;
            [Tooltip("Unity Events to be invoked when the button is clicked.")] public Button.ButtonClickedEvent OnClick;
        }
        #endregion

        #region Function
        void OnEnable()
        {
            if (TriggerOnEnable) TriggerPopup();
        }

        /// <summary>
        /// Notifies the Popup Manager to show a popup with this Popup Context's parameters.
        /// </summary>
        public void TriggerPopup()
        {
            if (PopupManager.Instance != null) PopupManager.TriggerPopup(this);
            else Debug.LogWarning("Popup Context failed to trigger popup; please add a Popup Manager to your scene.");
        }
        #endregion

#if UNITY_EDITOR

        #region Function
        void OnValidate()
        {
            HeaderText.Localized = Localized;
            BodyText.Localized = Localized;
            for (int i = 0; i < Buttons.Count; i++)
            {
                Buttons[i].Label.Localized = Localized;
            }
        }
        #endregion

#endif
    }
}