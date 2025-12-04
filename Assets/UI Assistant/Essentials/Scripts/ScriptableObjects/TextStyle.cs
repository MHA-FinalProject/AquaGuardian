using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace UIAssistant
{
    [HelpURL("https://sites.google.com/view/uiassistant/documentation/text?authuser=0#h.8vgxi8hlowno")]
    public class TextStyle : NamedAsset
    {
        #region Variables
        /// <summary>
        /// A list of Font Assets, Sizes, Min Sizes, Max Sizes for every Language added to the Text Settings.
        /// </summary>
        public List<LanguageStyle> Styles;
        #endregion

        #region Classes
        [System.Serializable]
        public class LanguageStyle
        {
            public Language Language = null;
            public TMP_FontAsset FontAsset = null;
            public float Size = 22;
            public float MinSize = 20;
            public float MaxSize = 24;
        }
        #endregion

        #region Helpers
        /// <summary>
        /// Returns the Style that corresponds to a specific Language.
        /// </summary>
        /// <param name="language">A Language asset within the Text Settings asset.</param>
        public LanguageStyle GetStyle(Language language) => Styles[MatchingIndex(Styles, language)];
        int MatchingIndex(List<LanguageStyle> styles, Language language)
        {
            int index = -1;

            for (int i = 0; i < styles.Count; i++)
            {
                if (styles[i].Language == language)
                {
                    index = i;
                    break;
                }
            }

            return index;
        }
        #endregion

#if UNITY_EDITOR

        #region Function
        /// <summary>
        /// Updates Styles based on changes made to Languages. Text Settings automatically handles validation.
        /// </summary>
        public void ValidateStyle()
        {
            OnValidate();

            if (Styles.Count != TextSettings.Languages.Count)
            {
                UpdateStyles();
                return;
            }

            for (int i = 0; i < Styles.Count; i++)
            {
                if (Styles[i].Language != TextSettings.Languages[i])
                {
                    UpdateStyles();
                    return;
                }
            }
        }
        void UpdateStyles()
        {
            List<LanguageStyle> styles = new(Styles);
            Styles.Clear();

            for (int i = 0; i < TextSettings.Languages.Count; i++)
            {
                int index = MatchingIndex(styles, TextSettings.Languages[i]);

                if (index == -1)
                {
                    LanguageStyle style = new()
                    {
                        Language = TextSettings.Languages[i],
                        FontAsset = null,
                        Size = 22,
                        MinSize = 20,
                        MaxSize = 24,
                    };

                    Styles.Add(style);
                }
                else
                {
                    Styles.Add(styles[index]);
                    styles.RemoveAt(index);
                }
            }
        }
        #endregion

#endif
    }
}