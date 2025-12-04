using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

namespace UIAssistant
{
    [HelpURL("https://sites.google.com/view/uiassistant/documentation/options?authuser=0#h.9wbgxx1nw2la")]
    [RequireComponent(typeof(Selectable)), AddComponentMenu("UI Assistant/Option Syncer"), DisallowMultipleComponent]
    public class OptionSyncer : UIAssistantComponent, ILocalizable
    {
        #region Variables
        [Tooltip("The Option Entry updated via the Option Syncer.")] public OptionEntry OptionEntry;
        [Tooltip("The Text Mesh Pro UGUI that will display the Option Entry's name.")] public TextMeshProUGUI LabelText;
        [Tooltip("The Text Mesh Pro UGUI that will display the Option Entry's current value.")] public TextMeshProUGUI ValueText;
        [Tooltip("Set this to true if the Selectable used with this Option Syncer is not a Slider, Toggle, Dropdown, or Option Cycler.")] public bool CustomSelectable;
        [Tooltip("Unity Events to be invoked when the Option Entry's value changes.")] public UnityEvent OnSync;

        [SerializeField, HideInInspector] Slider Slider;
        [SerializeField, HideInInspector] Toggle Toggle;
        [SerializeField, HideInInspector] TMP_Dropdown Dropdown;
        [SerializeField, HideInInspector] OptionCycler OptionCycler;
        #endregion

        #region Function
        void OnEnable()
        {
            if (OptionEntry == null)
            {
                enabled = false;
                gameObject.SetActive(false);
                Debug.LogWarning($"The Option Syncer on '{name}' is missing its Option Entry reference; '{name}' has been disabled.", this);
                return;
            }

            TextSettings.Add(this);
            FindSelectable();
            ValidateOptionEntry();
            SetOptions();
            AddValueChangedEvent();
            SyncSelectable();
            SetLabelText();
            OptionEntry.Add(this);
        }
        void OnDisable()
        {
            if (OptionEntry == null) return;

            TextSettings.Remove(this);
            RemoveValueChangedEvent();
            OptionEntry.Remove(this);
        }

        void FindSelectable()
        {
            if (CustomSelectable || OptionEntry == null) return;

            if (IsFloatSubtype)
            {
                if (Slider == null) Slider = GetComponent<Slider>();
            }
            else if (OptionEntry.Type == OptionType.Bool)
            {
                if (Toggle == null) Toggle = GetComponent<Toggle>();
            }
            else
            {
                if (Dropdown == null) Dropdown = GetComponent<TMP_Dropdown>();
                if (OptionCycler == null) OptionCycler = GetComponent<OptionCycler>();
            }
        }
        void AddValueChangedEvent()
        {
            if (OptionEntry == null) return;
            
            if (IsFloatSubtype)
            {
                if (Slider != null) Slider.onValueChanged.AddListener(OnSelectableValueChanged);
            }
            else if (OptionEntry.Type == OptionType.Bool)
            {
                if (Toggle != null) Toggle.onValueChanged.AddListener(OnSelectableValueChanged);
            }
            else
            {
                if (OptionCycler != null) OptionCycler.OnValueChanged.AddListener(OnSelectableValueChanged);
                else if (Dropdown != null) Dropdown.onValueChanged.AddListener(OnSelectableValueChanged);
            }
        }
        void RemoveValueChangedEvent()
        {
            if (OptionEntry == null) return;

            if (IsFloatSubtype)
            {
                if (Slider != null) Slider.onValueChanged.RemoveListener(OnSelectableValueChanged);
            }
            else if (OptionEntry.Type == OptionType.Bool)
            {
                if (Toggle != null) Toggle.onValueChanged.RemoveListener(OnSelectableValueChanged);
            }
            else
            {
                if (OptionCycler != null) OptionCycler.OnValueChanged.RemoveListener(OnSelectableValueChanged);
                else if (Dropdown != null) Dropdown.onValueChanged.RemoveListener(OnSelectableValueChanged);
            }
        }

        /// <summary>
        /// Sets the value of the Dropdown, Option Cycler, Slider, or Toggle without invoking On Value Changed events.
        /// </summary>
        public void SyncSelectable()
        {
            if (IsFloatSubtype)
            {
                if (Slider != null) Slider.SetValueWithoutNotify(OptionEntry.FloatValue);
            }
            else if (OptionEntry.Type == OptionType.Bool)
            {
                if (Toggle != null) Toggle.SetIsOnWithoutNotify(OptionEntry.BoolValue);
            }
            else
            {
                if (OptionCycler != null) OptionCycler.SetValueWithoutNotify(OptionEntry.IntValue);
                else if (Dropdown != null) Dropdown.SetValueWithoutNotify(OptionEntry.IntValue);
            }

            SetValueText();
        }

        void OnSelectableValueChanged(float value)
        {
            OptionEntry.FloatValue = value;
            OptionEntry.NotifyOptionSyncers(this);
            SetValueText();
        }
        void OnSelectableValueChanged(bool value)
        {
            OptionEntry.BoolValue = value;
            OptionEntry.NotifyOptionSyncers(this);
        }
        void OnSelectableValueChanged(int value)
        {
            OptionEntry.IntValue = value;
            OptionEntry.NotifyOptionSyncers(this);
        }

        /// <summary>
        /// Updates options with the Option Entry's Localization Sets when the active Language changes.
        /// </summary>
        public void OnLanguageChanged()
        {
            SetOptions();
            SetLabelText();
        }
     
        void ValidateOptionEntry()
        {
            if (OptionEntry) OptionEntry.Validate();
        }
        void SetOptions()
        {
            if (OptionEntry.Type == OptionType.Float || OptionEntry.Type == OptionType.Bool || CustomSelectable) return;

            if (OptionCycler != null) OptionCycler.Options = OptionEntry.GetOptions();
            else if (Dropdown != null) Dropdown.options = OptionEntry.GetOptions();
        }
        void SetLabelText()
        {
            if (LabelText == null) return;

            string label = OptionSettings.Localized ? OptionEntry.GetLocalizedName() : OptionEntry.Name;
            if (IsFloatSubtype && ValueText != null) label += ":";
            LabelText.text = label;
        }
        void SetValueText()
        {
            if (ValueText != null && IsFloatSubtype)
                ValueText.text = OptionEntry.FloatValue.ToString(OptionEntry.ValueFormat);
        }
        #endregion

        #region Helpers
        bool IsFloatSubtype => OptionEntry != null && (OptionEntry.Type == OptionType.Float || OptionEntry.Type == OptionType.FontSizeInterpolation || OptionEntry.Type == OptionType.ScaleInterpolation);
        #endregion

#if UNITY_EDITOR

        #region Functions
        protected override void OnValidate()
        {
            base.OnValidate();
            FindSelectable();
            ValidateOptionEntry();
        }
        #endregion

#endif
    }
}