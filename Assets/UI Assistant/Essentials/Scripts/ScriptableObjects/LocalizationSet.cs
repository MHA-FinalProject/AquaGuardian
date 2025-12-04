using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UIAssistant
{
    [HelpURL("https://sites.google.com/view/uiassistant/documentation/text?authuser=0#h.g6rg9p7ruh6x")]
    public class LocalizationSet : NamedAsset
    {
        #region Variables
        /// <summary>
        /// A list of localizations that each correspond to a Language.
        /// </summary>
        public List<Localization> Localizations;

        /// <summary>
        /// Returns true if there is at least one localization missing from this Localization Set.
        /// </summary>
        public bool MissingLocalization { get; private set; }

        /// <summary>
        /// Returns the localization that matches the active Language.
        /// </summary>
        public string LocalizedText => Localizations[TextSettings.ActiveLanguageIndex].Text;
        #endregion

        #region Classes
        [System.Serializable]
        public class Localization
        {
            public Language Language;
            public string Text = "";
        }
        #endregion

#if UNITY_EDITOR

        #region Function
        /// <summary>
        /// Updates Localizations based on changes made to Languages. Text Settings automatically handles validation.
        /// </summary>
        public void ValidateSet()
        {
            OnValidate();

            if (Localizations.Count != TextSettings.Languages.Count)
            {
                UpdateLocalizations();
                return;
            }

            for (int i = 0; i < Localizations.Count; i++)
            {
                if (Localizations[i].Language != TextSettings.Languages[i])
                {
                    UpdateLocalizations();
                    return;
                }
            }

            UpdateMissingLocalizationFlag();
        }
        void UpdateLocalizations()
        {
            List<Localization> localizations = new(Localizations);
            Localizations.Clear();

            for (int i = 0; i < TextSettings.Languages.Count; i++)
            {
                int index = MatchingIndex(localizations, TextSettings.Languages[i]);

                if (index == -1)
                {
                    Localization localization = new()
                    {
                        Language = TextSettings.Languages[i],
                        Text = "",
                    };

                    Localizations.Add(localization);
                }
                else
                {
                    Localizations.Add(localizations[index]);
                    localizations.RemoveAt(index);
                }
            }

            UpdateMissingLocalizationFlag();
        }
        void UpdateMissingLocalizationFlag()
        {
            MissingLocalization = false;

            for (int i = 0; i < Localizations.Count; i++)
            {
                if (Localizations[i].Text == "")
                {
                    MissingLocalization = true;
                    break;
                }
            }
        }
        #endregion

        #region Helpers
        int MatchingIndex(List<Localization> localizations, Language language)
        {
            int index = -1;

            for (int i = 0; i < localizations.Count; i++)
            {
                if (localizations[i].Language == language)
                {
                    index = i;
                    break;
                }
            }

            return index;
        }
        #endregion

#endif
    }
}