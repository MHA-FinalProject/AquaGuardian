using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

namespace UIAssistant
{
    [HelpURL("https://sites.google.com/view/uiassistant/documentation/popup-management?authuser=0#h.gxf22c4yf87p")]
    public class PopupManager : MonoBehaviour
    {
        #region Variables
        [Tooltip("The Text Mesh Pro UGUI that will display the Popup Context's header text.")] public TextMeshProUGUI HeaderText;
        [Tooltip("The Text Mesh Pro UGUI that will display the Popup Context's body text.")] public TextMeshProUGUI BodyText;
        [Space]
        [Tooltip("Template that will be instantiated to create the Popup Context's buttons.")] public Button TemplateButton;
        [Tooltip("A Text Mesh Pro UGUI child of the template button that serves as its label.")] public TextMeshProUGUI TemplateLabelText;
        [Space]
        [Tooltip("Unity Events to be invoked when the popup window is shown.")] public UnityEvent OnShow;
        [Tooltip("Unity Events to be invoked when the popup window is closed.")] public UnityEvent OnHide;

        public static PopupManager Instance;
        readonly List<GameObject> Buttons = new();
        #endregion

        #region Function
        void Start()
        {
            TemplateButton.gameObject.SetActive(false);
        }
        void OnEnable()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else Destroy(gameObject);
        }

        /// <summary>
        /// Shows the popup window with context-based parameters.
        /// </summary>
        /// <param name="context">The Popup Context whose parameters are applied to the popup.</param>
        public static void TriggerPopup(PopupContext context)
        {
            Instance.SetWindowParameters(context);
            Instance.Show();
        }
        void SetWindowParameters(PopupContext context)
        {
            HeaderText.text = context.HeaderText.ProperString;
            BodyText.text = context.BodyText.ProperString;

            for (int i = 0; i < Buttons.Count; i++)
            {
                Destroy(Buttons[i]);
            }
            Buttons.Clear();

            for (int i = 0; i < context.Buttons.Count; i++)
            {
                TemplateLabelText.text = context.Buttons[i].Label.ProperString;
                Button button = Instantiate(TemplateButton, TemplateButton.transform.parent);
                button.onClick = context.Buttons[i].OnClick;
                button.onClick.AddListener(Hide);
                button.gameObject.SetActive(true);
                Buttons.Add(button.gameObject);
            }
        }
        void Show()
        {
            OnShow.Invoke();
        }
        public void Hide()
        {
            OnHide.Invoke();
        }
        #endregion
    }
}