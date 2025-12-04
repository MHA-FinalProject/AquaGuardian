using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UIAssistant
{
    [HelpURL("https://sites.google.com/view/uiassistant/documentation/text?authuser=0#h.5go8s6qlcnm7")]
    public class TextSettings : ScriptableObject
    {
        #region Variables
        /// <summary>
        /// A list of Text Styles added to the Text Settings asset. This is handled by the Text Settings editor; please do not add or remove any Text Styles yourself.
        /// </summary>
        public List<TextStyle> TextStyles = new();

        /// <summary>
        /// A list of Languages added to the Text Settings asset. This is handled by the Text Settings editor; please do not add or remove any Languages yourself.
        /// </summary>
        public List<Language> Languages = new();

        /// <summary>
        /// A list of Localization Sets added to the Text Settings asset. This is handled by the Text Settings editor; please do not add or remove any Localization Sets yourself.
        /// </summary>
        public List<LocalizationSet> LocalizationSets = new();

        public readonly static string MissingLocalizationWarning = "Missing Localization";
        public readonly static string MissingLocalizationSetWarning = "Missing Localization Set";

        /// <summary>
        /// The active Language that will determine the Font Asset, Font Size, Min Font Size, and Max Font Size values set by Text Stylers. Active Language also determines which texts of your Localization Sets will be displayed.
        /// </summary>
        public Language ActiveLanguage
        {
            get
            {
                if (_ActiveLanguage == null && Languages.Count > 0)
                    SetLanguage(0);
                return _ActiveLanguage;
            }
            private set
            {
                _ActiveLanguage = value;
            }
        }
        [SerializeField] Language _ActiveLanguage;

        /// <summary>
        /// The index of the active Language, based on the order of Languages set within the Text Settings.
        /// </summary>
        public int ActiveLanguageIndex
        {
            get => _ActiveLanguageIndex;
            private set { _ActiveLanguageIndex = value; }
        }
        [SerializeField] int _ActiveLanguageIndex;

        /// <summary>
        /// Returns an array of Text Style names listed in the Text Settings.
        /// </summary>
        public string[] StyleNames
        {
            get
            {
                List<string> styleNames = new();

                for (int i = 0; i < TextStyles.Count; i++)
                {
                    styleNames.Add(TextStyles[i].Name);
                }

                return styleNames.ToArray();
            }
        }

        /// <summary>
        /// Returns an array of Language names listed in the Text Settings.
        /// </summary>
        public string[] LanguageNames
        {
            get
            {
                List<string> languageNames = new();

                for (int i = 0; i < Languages.Count; i++)
                {
                    languageNames.Add(Languages[i].Name);
                }

                return languageNames.ToArray();
            }
        }

        /// <summary>
        /// Returns an array of Localization Set names listed in the Text Settings.
        /// </summary>
        public string[] LocalizationSetNames
        {
            get
            {
                List<string> localizationKeys = new();

                for (int i = 0; i < LocalizationSets.Count; i++)
                {
                    localizationKeys.Add(LocalizationSets[i].Name);
                }

                return localizationKeys.ToArray();
            }
        }
        
        readonly List<ILocalizable> Localizables = new();

        /// <summary>
        /// URL to be used when importing Localization Sets.
        /// </summary>
        [HideInInspector] public string LocalizationURL;
        #endregion

        #region Classes
        [System.Serializable]
        public class StringOrLocalizationSet
        {
            public bool Localized;
            public string String;
            public LocalizationSet LocalizationSet;

            public string ProperString
            {
                get
                {
                    if (Localized)
                    {
                        if (LocalizationSet == null) return MissingLocalizationSetWarning;
                        else return LocalizationSet.LocalizedText;
                    }
                    else return String;
                }
            }
        }
        #endregion

        #region Function
        /// <summary>
        /// Registers a Localizable to be updated when the active Language changes. Localizables automatically add themselves.
        /// </summary>
        /// <param name="localizable">The Localizable to be added.</param>
        public void Add(ILocalizable localizable)
        {
            Localizables.Add(localizable);
        }

        /// <summary>
        /// Removes a Localizable, locking it out of updates when the active Language changes. Localizables automatically remove themselves.
        /// </summary>
        /// <param name="localizable">The Localizable to be removed.</param>
        public void Remove(ILocalizable localizable)
        {
            Localizables.Remove(localizable);
        }

        /// <summary>
        /// Updates all Localizables based on their selected Localization Set.
        /// </summary>
        /// <param name="language">A Language asset within the Text Settings asset.</param>
        public void SetLanguage(Language language)
        {
            ActiveLanguage = language;
            ActiveLanguageIndex = Languages.IndexOf(ActiveLanguage);

            SetLanguage();
        }

        /// <summary>
        /// Updates all Localizables based on their selected Localization Set.
        /// </summary>
        /// <param name="index">The index of a Language. The order of Languages can be modified under UI Assistant/Text Settings.</param>
        public void SetLanguage(int index)
        {
            ActiveLanguageIndex = Mathf.Clamp(index, 0, Languages.Count - 1);
            ActiveLanguage = Languages[ActiveLanguageIndex];

            SetLanguage();
        }
        void SetLanguage()
        {
            for (int i = 0; i < Localizables.Count; i++)
            {
                Localizables[i].OnLanguageChanged();
            }
        }

        /// <summary>
        /// Sets the first Language as the active Language if the active Language is null. This happens automatically when a Language is deleted from the Text Settings.
        /// </summary>
        public void OnLanguageRemoved()
        {
            if (Languages.Count > 0 && ActiveLanguage == null) SetLanguage(0);
        }

        /// <summary>
        /// Sets the first Language as the active Language if the active Language is null. This happens automatically when a Language is created within the Text Settings.
        /// </summary>
        public void OnLanguageAdded()
        {
            if (ActiveLanguage == null) SetLanguage(0);
        }
        #endregion

        #region Helpers
        /// <summary>
        /// Finds a Localization Set by name.
        /// </summary>
        /// <param name="name">The name of the Localization Set.</param>
        public LocalizationSet GetLocalizationSet(string name)
        {
            LocalizationSet localizationSet = null;

            for (int i = 0; i < LocalizationSets.Count; i++)
            {
                if (LocalizationSets[i].Name == name)
                {
                    localizationSet = LocalizationSets[i];
                    break;
                }
            }

            return localizationSet;
        }
        #endregion
    }

    public interface ILocalizable
    {
        #region Function
        public void OnLanguageChanged() { }
        #endregion
    }
}