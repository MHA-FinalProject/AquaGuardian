using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace UIAssistant
{
    [HelpURL("https://sites.google.com/view/uiassistant/documentation/text?authuser=0#h.j9t32xv68gy5")]
    public abstract class OptionLocalizer : UIAssistantComponent, ILocalizable
    {
        #region Variables
        /// <summary>
        /// Localization Sets whose correct localizations will override options.
        /// </summary>
        public List<LocalizationSet> Options
        {
            get => _Options;
            set { if (value != _Options) _Options = value; SetOptions(); }
        }
        [Tooltip("Localization Sets whose correct localizations will override options."), SerializeField, HideInInspector] List<LocalizationSet> _Options = new();

        [SerializeField, HideInInspector] protected OptionSyncer OptionSyncer;
        #endregion

        #region Function
        void OnEnable()
        {
            GetSelectable();
            TextSettings.Add(this);
            TryGetOptionSyncer();
            if (OptionSyncer == null) SetOptions();
        }
        void OnDisable()
        {
            TextSettings.Remove(this);
        }
        void TryGetOptionSyncer()
        {
            if (OptionSyncer == null) OptionSyncer = GetComponent<OptionSyncer>();
        }
        protected virtual void GetSelectable() { }

        /// <summary>
        /// Adds a Localization Set to the options.
        /// </summary>
        /// <param name="localizationSet">The Localization Set to be added.</param>
        public void AddOption(LocalizationSet localizationSet) { _Options.Add(localizationSet); SetOptions(); }

        /// <summary>
        /// Adds a list of Localization Sets to the options.
        /// </summary>
        /// <param name="localizationSets">The list of Localization Sets to be added.</param>
        public void AddOptions(List<LocalizationSet> localizationSets) { _Options.AddRange(localizationSets); SetOptions(); }

        /// <summary>
        /// Removes a Localization Set from the options.
        /// </summary>
        /// <param name="localizationSet">The Localization Set to be removed.</param>
        public void RemoveOption(LocalizationSet localizationSet) { _Options.Remove(localizationSet); SetOptions(); }
        
        /// <summary>
        /// Clears all options.
        /// </summary>
        public void ClearOptions() { _Options.Clear(); SetOptions(); }

        protected virtual void SetOptions() { }

        /// <summary>
        /// Updates options with the Localization Sets when the active Language changes.
        /// </summary>
        public void OnLanguageChanged() { SetOptions(); }
        #endregion

        #region Helpers
        protected List<TMP_Dropdown.OptionData> GetOptions()
        {
            List<TMP_Dropdown.OptionData> options = new();

            for (int i = 0; i < Options.Count; i++)
            {
                string option = Options[i] == null ?
                    TextSettings.MissingLocalizationSetWarning : Options[i].Localizations[TextSettings.ActiveLanguageIndex].Text;

                options.Add(new(option));
            }

            return options;
        }
        #endregion

#if UNITY_EDITOR

        #region Function
        protected override void OnValidate()
        {
            base.OnValidate();
            TryGetOptionSyncer();
        }
        #endregion

#endif

    }
}