using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

namespace UIAssistant
{
    public class CreditsBuilder : MonoBehaviour
    {
        #region Variables
        [Tooltip("When set to true, credits will play automatically.")] public bool PlayOnStart;
        [Tooltip("Determines how fast the elements move up.")] public float Speed;
        [Tooltip("When set to true, main texts will require Localization Sets instead of strings.")] public bool Localized;
        [Tooltip("Credits content that will be displayed using templates or custom elements.")] public List<CreditsElement> Elements = new();
        [Space]
        [Tooltip("Unity Events to be invoked when Play() is called.")] public UnityEvent OnCreditsStart;
        [Tooltip("Unity Events to be invoked when End() is called.")] public UnityEvent OnCreditsEnd;
        [Space]
        [Tooltip("Layout that will serve as the parent of all credits elements.")] public RectTransform ContentTransform;
        [Tooltip("Object that will be instantiated for Single-type elements.")] public GameObject SingleTemplate;
        [Tooltip("The Text Mesh Pro UGUI within the Single template that will display Single-tye elements' texts.")] public TextMeshProUGUI SingleText;
        [Tooltip("Object that will be instantiated for Vertical-type elements.")] public GameObject VerticalTemplate;
        [Tooltip("The Text Mesh Pro UGUI within the Vertical template that will display Vertical-type elements' main texts.")] public TextMeshProUGUI VerticalMainText;
        [Tooltip("The Text Mesh Pro UGUI within the Vertical template that will display Vertical-type elements' list texts.")] public TextMeshProUGUI VerticalListText;
        [Tooltip("Object that will be instantiated for Horizontal-type elements.")] public GameObject HorizontalTemplate;
        [Tooltip("The Text Mesh Pro UGUI within the Horizontal template that will display Horizontal-type elements' main texts.")] public TextMeshProUGUI HorizontalMainText;
        [Tooltip("The Text Mesh Pro UGUI within the Horizontal template that will display Horizontal-type elements' list texts.")] public TextMeshProUGUI HorizontalListText;

        TextStyler SingleStyler;
        TextStyler VerticalMainStyler;
        TextStyler HorizontalMainStyler;
        bool CanScroll;
        #endregion

        #region Classes & Enums
        public enum CreditsElementType
        {
            Single = 0,
            Vertical = 1,
            Horizontal = 2,
            Custom = 3,
        }

        [System.Serializable]
        public class CreditsElement
        {
            [Tooltip("Determines content, as well as the template to be used.")] public CreditsElementType Type;
            [Tooltip("Single-line text that defines the element.")] public TextSettings.StringOrLocalizationSet MainString;
            [Tooltip("Multi-line text to list names."), TextArea] public string ListString;
            [Tooltip("Object that will be instantiated for the Custom-type element.")] public GameObject CustomTemplate;
        }
        #endregion

        #region Function
        void Start()
        {
            Initialize();
            CreateElements();
            if (PlayOnStart) Play();
        }

        void Initialize()
        {
            if (Localized)
            {
                SingleStyler = SingleText.GetComponent<TextStyler>();
                if (SingleStyler == null) SingleStyler = SingleText.gameObject.AddComponent<TextStyler>();
                SingleStyler.Localization = TextStyler.LocalizationType.Localized;

                VerticalMainStyler = VerticalMainText.GetComponent<TextStyler>();
                if (VerticalMainStyler == null) VerticalMainStyler = VerticalMainText.gameObject.AddComponent<TextStyler>();
                VerticalMainStyler.Localization = TextStyler.LocalizationType.Localized;

                HorizontalMainStyler = HorizontalMainText.GetComponent<TextStyler>();
                if (HorizontalMainStyler == null) HorizontalMainStyler = HorizontalMainText.gameObject.AddComponent<TextStyler>();
                HorizontalMainStyler.Localization = TextStyler.LocalizationType.Localized;
            }
        }

        void CreateElements()
        {
            List<GameObject> objectsToDestroy = new();
            for (int i = 0; i < ContentTransform.childCount; i++)
            {
                objectsToDestroy.Add(ContentTransform.GetChild(i).gameObject);
            }

            for (int i = 0; i < Elements.Count; i++)
            {
                GameObject objectToInstantiate = null;

                if (Elements[i].Type != CreditsElementType.Custom)
                {
                    LocalizationSet mainLocalizationSet = Elements[i].MainString.LocalizationSet;
                    string mainString = Elements[i].MainString.ProperString;
                    string listString = Elements[i].ListString;

                    switch (Elements[i].Type)
                    {
                        case CreditsElementType.Single:
                            if (Localized) SingleStyler.LocalizationSet = mainLocalizationSet;
                            SingleText.text = mainString;
                            objectToInstantiate = SingleTemplate;
                            break;
                        case CreditsElementType.Vertical:
                            if (Localized) VerticalMainStyler.LocalizationSet = mainLocalizationSet;
                            VerticalMainText.text = mainString;
                            VerticalListText.text = listString;
                            objectToInstantiate = VerticalTemplate;
                            break;
                        case CreditsElementType.Horizontal:
                            if (Localized) HorizontalMainStyler.LocalizationSet = mainLocalizationSet;
                            HorizontalMainText.text = mainString;
                            HorizontalListText.text = listString;
                            objectToInstantiate = HorizontalTemplate;
                            break;
                    }
                }
                else objectToInstantiate = Elements[i].CustomTemplate;

                Instantiate(objectToInstantiate, ContentTransform);
            }

            for (int i = 0; i < objectsToDestroy.Count; i++)
            {
                Destroy(objectsToDestroy[i]);
            }

            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(ContentTransform);
        }

        /// <summary>
        /// Invokes OnCreditsStart events, and starts scrolling.
        /// </summary>
        public void Play()
        {
            OnCreditsStart.Invoke();
            CanScroll = true;
        }

        /// <summary>
        /// Invokes OnCreditsEnd events, and optionally stops scrolling.
        /// </summary>
        public void End(bool stopScrolling)
        {
            OnCreditsEnd.Invoke();
            if (stopScrolling) CanScroll = false;
        }

        /// <summary>
        /// Stops scrolling.
        /// </summary>
        public void Pause() { CanScroll = false; }

        /// <summary>
        /// Starts scrolling.
        /// </summary>
        public void Resume() { CanScroll = true; }

        void Update()
        {
            if (CanScroll)
            {
                Vector2 position = ContentTransform.anchoredPosition;
                position.y += Speed * Time.deltaTime;

                if (position.y >= ContentTransform.rect.height)
                {
                    position.y = ContentTransform.rect.height;
                    End(true);
                }

                ContentTransform.anchoredPosition = position;
            }
        }
        #endregion

#if UNITY_EDITOR

        #region Function
        void OnValidate()
        {
            for (int i = 0; i < Elements.Count; i++)
            {
                Elements[i].MainString.Localized = Localized;
            }
        }
        #endregion

#endif
    }
}