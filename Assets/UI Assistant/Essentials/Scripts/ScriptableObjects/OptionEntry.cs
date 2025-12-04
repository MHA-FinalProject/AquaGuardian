using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UIAssistant
{
    [HelpURL("https://sites.google.com/view/uiassistant/documentation/options?authuser=0#h.ysyurzfv2qca")]
    public class OptionEntry : NamedAsset
    {
        #region Variables
        /// <summary>
        /// The type of the Option Entry. Determines which Selectables can control its value.
        /// </summary>
        public OptionType Type;

        /// <summary>
        /// Selectable associated with this Option Entry, used to set this Option Entry's value.
        /// </summary>
        public OptionSyncer Prefab;

        /// <summary>
        /// The format in which the Option Entry's value is displayed.
        /// </summary>
        public string ValueFormat;

        /// <summary>
        /// Determines whether the Option Entry has limits.
        /// </summary>
        public bool Clamp;

        /// <summary>
        /// The minimum value of the clamped Option Entry.
        /// </summary>
        public float ClampMin;

        /// <summary>
        /// The maximum value of the clamped Option Entry.
        /// </summary>
        public float ClampMax;

        /// <summary>
        /// A list of strings representing the Option Entry's possible values.
        /// </summary>
        public List<string> Options = new();

        /// <summary>
        /// A list of Localization Sets representing the Option Entry's possible values.
        /// </summary>
        public List<LocalizationSet> LocalizedOptions = new();

        public float DefaultFloatValue;
        public float FloatValue
        {
            get => _FloatValue;
            set
            {
                if (Clamp) value = Mathf.Clamp(value, ClampMin, ClampMax);
                if (_FloatValue != value)
                {
                    _FloatValue = value;
                    for (int i = 0; i < OptionListeners.Count; i++)
                        OptionListeners[i].OnOptionFloatValueChanged(value);

                    switch (Type)
                    {
                        case OptionType.ScaleInterpolation:
                            ScaleSettings.ScaleInterpolationValue = _FloatValue;
                            break;
                        case OptionType.FontSizeInterpolation:
                            ScaleSettings.FontSizeInterpolationValue = _FloatValue;
                            break;
                    }
                }
            }
        }
        [SerializeField, HideInInspector] float _FloatValue;

        public bool DefaultBoolValue;
        public bool BoolValue
        {
            get => _BoolValue;
            set
            {
                if (_BoolValue != value)
                {
                    _BoolValue = value;
                    for (int i = 0; i < OptionListeners.Count; i++)
                        OptionListeners[i].OnOptionBoolValueChanged(value);
                }
            }
        }
        [SerializeField, HideInInspector] bool _BoolValue;

        public int DefaultIntValue;
        public int IntValue
        {
            get => _IntValue;
            set
            {
                if (Clamp) value = Mathf.Clamp(value, (int)ClampMin, (int)ClampMax);
                if (_IntValue != value)
                {
                    _IntValue = value;
                    for (int i = 0; i < OptionListeners.Count; i++)
                        OptionListeners[i].OnOptionIntValueChanged(value);

                    switch (Type)
                    {
                        case OptionType.ColorProfile:
                            ColorSettings.SetColorProfile(_IntValue);
                            break;
                        case OptionType.ScaleProfile:
                            ScaleSettings.SetScaleProfile(_IntValue);
                            break;
                        case OptionType.Language:
                            TextSettings.SetLanguage(_IntValue);
                            break;
                    }
                }
            }
        }
        [SerializeField, HideInInspector] int _IntValue;

        public NamedAsset DefaultNamedAssetValue;

        readonly List<OptionSyncer> OptionSyncers = new();
        readonly List<IOptionListener> OptionListeners = new();
        #endregion

        #region Function
        /// <summary>
        /// Clamps all values.
        /// </summary>
        public void Validate()
        {

#if UNITY_EDITOR

            OnValidate();

#endif

            switch (Type)
            {
                case OptionType.Float:
                    FloatValue = _FloatValue;
                    break;
                case OptionType.Bool:
                    BoolValue = _BoolValue;
                    break;
                case OptionType.List:
                    IntValue = _IntValue;
                    break;
                case OptionType.ColorProfile:
                    IntValue = ColorSettings.ActiveColorProfileIndex;
                    break;
                case OptionType.ScaleProfile:
                    IntValue = ScaleSettings.ActiveScaleProfileIndex;
                    break;
                case OptionType.ScaleInterpolation:
                    FloatValue = _FloatValue;
                    break;
                case OptionType.FontSizeInterpolation:
                    FloatValue = _FloatValue;
                    break;
                case OptionType.Language:
                    IntValue = TextSettings.ActiveLanguageIndex;
                    break;
            }
        }

        /// <summary>
        /// Registers an Option Syncer to be updated when the Option Entry's value changes. Option Syncers automatically add themselves.
        /// </summary>
        /// <param name="optionSyncer">The Option Syncer to be added.</param>
        public void Add(OptionSyncer optionSyncer)
        {
            OptionSyncers.Add(optionSyncer);
        }

        /// <summary>
        /// Removes an Option Syncer, locking it out of updates when the Option Entry's value changes. Option Syncers automatically remove themselves.
        /// </summary>
        /// <param name="optionSyncer">The Option Syncer to be removed.</param>
        public void Remove(OptionSyncer optionSyncer)
        {
            OptionSyncers.Remove(optionSyncer);
        }

        /// <summary>
        /// If there are multiple Option Syncers working with the same Option Entry, they will be updated when the source Option Syncer's value changes.
        /// </summary>
        /// <param name="source">The invoking Option Syncer that will not be notified.</param>
        public void NotifyOptionSyncers(OptionSyncer source)
        {
            for (int i = 0; i < OptionSyncers.Count; i++)
            {
                if (OptionSyncers[i] != source) OptionSyncers[i].SyncSelectable();
            }
        }

        /// <summary>
        /// Registers an Option Listener to be updated when the Option Entry's value changes.
        /// </summary>
        /// <param name="optionListener">The Option Listener to be added.</param>
        public void Add(IOptionListener optionListener)
        {
            OptionListeners.Add(optionListener);
        }

        /// <summary>
        /// Removes an Option Listener, locking it out of updates when the Option Entry's value changes.
        /// </summary>
        /// <param name="optionListener">The Option Listener to be removed.</param>
        public void Remove(IOptionListener optionListener)
        {
            OptionListeners.Remove(optionListener);
        }

        /// <summary>
        /// Sets all values to their defaults.
        /// </summary>
        public void RestoreDefaults()
        {
            switch (Type)
            {
                case OptionType.Float:
                    FloatValue = DefaultFloatValue;
                    break;
                case OptionType.Bool:
                    BoolValue = DefaultBoolValue;
                    break;
                case OptionType.List:
                    IntValue = DefaultIntValue;
                    break;
                case OptionType.ColorProfile:
                    IntValue = ColorSettings.ColorProfiles.IndexOf(DefaultNamedAssetValue as ColorProfile);
                    break;
                case OptionType.ScaleProfile:
                    IntValue = ScaleSettings.ScaleProfiles.IndexOf(DefaultNamedAssetValue as ScaleProfile);
                    break;
                case OptionType.ScaleInterpolation:
                    FloatValue = DefaultFloatValue;
                    break;
                case OptionType.FontSizeInterpolation:
                    FloatValue = DefaultFloatValue;
                    break;
                case OptionType.Language:
                    IntValue = TextSettings.Languages.IndexOf(DefaultNamedAssetValue as Language);
                    break;
            }

            for (int i = 0; i < OptionSyncers.Count; i++)
            {
                OptionSyncers[i].SyncSelectable();
            }
        }
        #endregion

        #region Helpers
        /// <summary>
        /// Returns a list of localized or non-localized options as TMP Dropdown Option Data.
        /// </summary>
        public List<TMPro.TMP_Dropdown.OptionData> GetOptions()
        {
            List<TMPro.TMP_Dropdown.OptionData> options = new();

            if (OptionSettings.Localized)
            {
                switch (Type)
                {
                    case OptionType.List:
                        for (int i = 0; i < Options.Count; i++)
                        {
                            options.Add(new(LocalizedOptions[i].LocalizedText));
                        }
                        break;
                    case OptionType.ColorProfile:
                        for (int i = 0; i < ColorSettings.ColorProfiles.Count; i++)
                        {
                            options.Add(new(ColorSettings.ColorProfiles[i].GetLocalizedName()));
                        }
                        break;
                    case OptionType.ScaleProfile:
                        for (int i = 0; i < ScaleSettings.ScaleProfiles.Count; i++)
                        {
                            options.Add(new(ScaleSettings.ScaleProfiles[i].GetLocalizedName()));
                        }
                        break;
                    case OptionType.Language:
                        for (int i = 0; i < TextSettings.Languages.Count; i++)
                        {
                            options.Add(new(TextSettings.Languages[i].SimpleLocalizedName));
                        }
                        break;
                }
            }
            else
            {
                switch (Type)
                {
                    case OptionType.List:
                        for (int i = 0; i < Options.Count; i++)
                        {
                            options.Add(new(Options[i]));
                        }
                        break;
                    case OptionType.ColorProfile:
                        for (int i = 0; i < ColorSettings.ColorProfiles.Count; i++)
                        {
                            options.Add(new(ColorSettings.ColorProfiles[i].Name));
                        }
                        break;
                    case OptionType.ScaleProfile:
                        for (int i = 0; i < ScaleSettings.ScaleProfiles.Count; i++)
                        {
                            options.Add(new(ScaleSettings.ScaleProfiles[i].Name));
                        }
                        break;
                    case OptionType.Language:
                        for (int i = 0; i < TextSettings.Languages.Count; i++)
                        {
                            options.Add(new(TextSettings.Languages[i].Name));
                        }
                        break;
                }
            }

            return options;
        }
        #endregion
    }

    public interface IOptionListener
    {
        #region Function
        /// <summary>
        /// Notifies the Listener about changes made to the Option Entry's float value.
        /// </summary>
        /// <param name="value">The up-to-date value.</param>
        public void OnOptionFloatValueChanged(float value) { }

        /// <summary>
        /// Notifies the Listener about changes made to the Option Entry's bool value.
        /// </summary>
        /// <param name="value">The up-to-date value.</param>
        public void OnOptionBoolValueChanged(bool value) { }

        /// <summary>
        /// Notifies the Listener about changes made to the Option Entry's int value.
        /// </summary>
        /// <param name="value">The up-to-date value.</param>
        public void OnOptionIntValueChanged(int value) { }
        #endregion
    }
}