using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace UIAssistant
{
    [HelpURL("https://sites.google.com/view/uiassistant/documentation/options?authuser=0#h.q59e2crb8ai9")]
    public class OptionsBuilder : MonoBehaviour
    {
        #region Variables
        [Tooltip("When set to true, the main submenu will be visible by default.")] public bool OpenOnStart;
        [Tooltip("Layout that will serve as the parent of a submenu's elements.")] public RectTransform SubmenuTemplate;
        [Tooltip("The Text Mesh Pro UGUI within the submenu template that will display the submenu's name.")] public TextMeshProUGUI HeaderText;
        [Tooltip("Object within the submenu template that will be instantiated after the last Option Syncer.")] public GameObject Spacer;
        [Tooltip("The button that will be instantiated for functions and submenu access.")] public Button ButtonTemplate;
        [Tooltip("The Text Mesh Pro UGUI within the button template that will display the button's label.")] public TextMeshProUGUI ButtonLabelText;
        [Tooltip("Check this box if the template uses the UI Animator component.")] public bool SubmenusAnimate;
        [Tooltip("Animation Profile used by all submenus.")] public AnimationProfile AnimationProfile;
        [Tooltip("Submenus that can be reached via the main submenu.")] public List<Submenu> Submenus = new();
        [Tooltip("When set to true, submenus' names will require Localization Sets instead of strings.")] public bool Localized;
        [Tooltip("Text to be displayed as the main submenu's header.")] public TextSettings.StringOrLocalizationSet MainHeader;
        [Tooltip("Text to be displayed on the button that returns to, or closes the main submenu.")] public TextSettings.StringOrLocalizationSet BackLabel;
        [Tooltip("Text to be displayed on the button that reverts to Option Entries' default values.")] public TextSettings.StringOrLocalizationSet DefaultsLabel;
        [Tooltip("Unity Events to be invoked when Open() is called.")] public Button.ButtonClickedEvent OnOpen;
        [Tooltip("Unity Events to be invoked when Close() is called.")] public Button.ButtonClickedEvent OnClose;

        TextStyler HeaderStyler;
        readonly List<RectTransform> SubmenuTransforms = new();
        readonly List<UIAnimator> UIAnimators = new();
        int ActiveSubmenuIndex = 0;
        #endregion

        #region Classes & Enums
        [System.Serializable]
        public class Submenu
        {
            [Tooltip("Text to be displayed on the submenu's button in the main submenu, as well as in its header.")] public TextSettings.StringOrLocalizationSet Name;
            [Tooltip("Option Entries listed in the submenu.")] public List<OptionEntry> OptionEntries = new();
            [Tooltip("Check this box to add a button to set Option Entries' default values.")] public bool HasDefaultsButton;
        }
        #endregion

        #region Function
        void Start()
        {
            Initialize();
            CreateSubmenus();

            if (OpenOnStart)
            {
                SubmenuTransforms[0].gameObject.SetActive(true);
                Open();
            }
        }

        /// <summary>
        /// Opens the main submenu.
        /// </summary>
        public void Open()
        {
            if (ActiveSubmenuIndex != 0) CloseSubmenu(ActiveSubmenuIndex);
            ActiveSubmenuIndex = 0;
            OpenSubmenu(ActiveSubmenuIndex);
            OnOpen.Invoke();
        }

        /// <summary>
        /// Closes the active submenu.
        /// </summary>
        public void Close()
        {
            CloseSubmenu(ActiveSubmenuIndex);
            OnClose.Invoke();
        }

        void Initialize()
        {
            if (Localized)
            {
                HeaderStyler = HeaderText.GetComponent<TextStyler>();
                if (HeaderStyler == null) HeaderStyler = HeaderText.gameObject.AddComponent<TextStyler>();
                HeaderStyler.Localization = TextStyler.LocalizationType.Localized;
            }

            SubmenuTemplate.gameObject.SetActive(false);
            UIAnimator submenuAnimator = SubmenuTemplate.GetComponent<UIAnimator>();
            if (submenuAnimator == null) submenuAnimator = SubmenuTemplate.gameObject.AddComponent<UIAnimator>();
            submenuAnimator.AnimationProfile = AnimationProfile;

            Spacer.SetActive(false);
            ButtonTemplate.gameObject.SetActive(true);
        }
        void CreateSubmenus()
        {
            ButtonTemplate.transform.SetParent(null);
            for (int i = -1; i < Submenus.Count; i++) CreateSubmenu(i);
            Destroy(SubmenuTemplate.gameObject);
            Destroy(ButtonTemplate.gameObject);
        }
        void CreateSubmenu(int index)
        {
            bool main = index == -1;

            if (Localized) HeaderStyler.LocalizationSet = main ? MainHeader.LocalizationSet : Submenus[index].Name.LocalizationSet;
            else HeaderText.text = main ? MainHeader.String : Submenus[index].Name.String;

            RectTransform parent = Instantiate(SubmenuTemplate, SubmenuTemplate.parent);
            parent.name = main ? "MainSubmenu" : $"Submenu{index + 1}";
            SubmenuTransforms.Add(parent);
            UIAnimator animator = parent.GetComponent<UIAnimator>();
            UIAnimators.Add(animator);

            if (main)
            {
                for (int i = 0; i < Submenus.Count; i++)
                {
                    int submenuIndex = i + 1;
                    ButtonLabelText.text = Submenus[i].Name.ProperString;
                    Button button = Instantiate(ButtonTemplate, parent.transform);
                    button.onClick.AddListener(() => SwitchSubmenus(submenuIndex));
                    button.name = $"Submenu{submenuIndex}";
                    button.gameObject.SetActive(true);
                }
            }
            else
            {
                for (int i = 0; i < Submenus[index].OptionEntries.Count; i++)
                {
                    OptionSyncer optionSyncer = Instantiate(Submenus[index].OptionEntries[i].Prefab, parent);
                    optionSyncer.OptionEntry = Submenus[index].OptionEntries[i];
                    optionSyncer.name = optionSyncer.OptionEntry.Name;
                }
            }

            if (Spacer != null)
            {
                GameObject spacer = Instantiate(Spacer, parent);
                spacer.SetActive(true);
            }

            if (!main && Submenus[index].HasDefaultsButton)
            {
                ButtonLabelText.text = DefaultsLabel.ProperString;
                Button defaultsButton = Instantiate(ButtonTemplate, parent);
                defaultsButton.name = "DefaultsButton";
                defaultsButton.onClick.AddListener(RestoreDefaults);
            }

            ButtonLabelText.text = BackLabel.ProperString;
            Button backButton = Instantiate(ButtonTemplate, parent);
            backButton.name = "BackButton";
            backButton.onClick.AddListener(main ? Close : CloseSubmenu);

            if (SubmenusAnimate)
            {
                parent.gameObject.SetActive(true);
                animator.StartHidden = true;
            }
        }

        void OpenSubmenu(int index)
        {
            if (SubmenusAnimate) UIAnimators[index].Show();
            else SubmenuTransforms[index].gameObject.SetActive(true);
        }
        void CloseSubmenu(int index)
        {
            if (SubmenusAnimate) UIAnimators[index].Hide();
            else SubmenuTransforms[index].gameObject.SetActive(false);
        }
        void CloseSubmenu() { SwitchSubmenus(0); }
        void SwitchSubmenus(int index)
        {
            CloseSubmenu(ActiveSubmenuIndex);
            ActiveSubmenuIndex = index;
            OpenSubmenu(ActiveSubmenuIndex);
        }

        void RestoreDefaults()
        {
            int index = ActiveSubmenuIndex - 1;
            for (int i = 0; i < Submenus[index].OptionEntries.Count; i++)
            {
                Submenus[index].OptionEntries[i].RestoreDefaults();
            }
        }
        #endregion

#if UNITY_EDITOR

        #region Function
        void OnValidate()
        {
            for (int i = 0; i < Submenus.Count; i++)
            {
                Submenus[i].Name.Localized = Localized;
            }
            MainHeader.Localized = Localized;
            BackLabel.Localized = Localized;
            DefaultsLabel.Localized = Localized;
        }
        #endregion

#endif
    }
}