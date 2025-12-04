using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace UIAssistant
{
    [HelpURL("https://sites.google.com/view/uiassistant/documentation/text?authuser=0#h.pih2182s8z9a")]
    [RequireComponent(typeof(TextMeshProUGUI)), AddComponentMenu("UI Assistant/Text Styler"), DisallowMultipleComponent]
    public class TextStyler : UIAssistantComponent, IScaleable, ILocalizable
    {
        #region Variables
        [SerializeField] TextMeshProUGUI Text;
        [Tooltip("Style defined in the Text Settings. Determines the text's font asset and font sizes.")] public TextStyle Style;
        [Tooltip("Determines which interpolation values of the active Scale Profile will be considered when setting the text’s font size.")] public ScalingBehaviour Scaling;
        [Tooltip("When set to true, the text's font size settings will not be overridden.")] public bool CustomSize;
        [Tooltip("Determines which Language's settings within the selected Text Style will be applied.")] public LocalizationType Localization;
        [Tooltip("Localization Set whose correct localization will be used to override the text.")] public LocalizationSet LocalizationSet;
        [Tooltip("The language of the Text Mesh Pro UGUI's text.")] public Language Language;

        [SerializeField] float OriginalFontSize;
        [SerializeField] float OriginalMinFontSize;
        [SerializeField] float OriginalMaxFontSize;
        #endregion

        #region Enums
        public enum ScalingBehaviour
        {
            None = 0,
            ScaleInterpolation = 1,
            FontSizeInterpolation = 2,
            Both = 3,
        }
        public enum LocalizationType
        {
            None = 0,
            Localized = 1,
            Static = 2,
        }
        #endregion

        #region Function
        void OnEnable()
        {
            if (Scaling != ScalingBehaviour.None)
            {
                if (Scaling == ScalingBehaviour.Both || Scaling == ScalingBehaviour.ScaleInterpolation) ScaleSettings.Add(this as IScaleable);
                if (Scaling == ScalingBehaviour.Both || Scaling == ScalingBehaviour.FontSizeInterpolation) ScaleSettings.Add(this as TextStyler);
            }

            TextSettings.Add(this);
            SetStyle();
            SetLocalization();
        }
        void OnDisable()
        {
            TextSettings.Remove(this);

            if (Scaling == ScalingBehaviour.Both || Scaling == ScalingBehaviour.ScaleInterpolation) ScaleSettings.Remove(this as IScaleable);
            if (Scaling == ScalingBehaviour.Both || Scaling == ScalingBehaviour.FontSizeInterpolation) ScaleSettings.Remove(this as TextStyler);
        }

        /// <summary>
        /// Updates the Text Mesh Pro UGUI's font size, minimum font size, maximum font size, word spacing, line spacing,
        /// and paragraph spacing values based on the selected Text Style, and sets its text based on the selected Localization Set.
        /// </summary>
        public void OnLanguageChanged()
        {
            SetStyle();
            SetLocalization();
        }
        void SetStyle()
        {
            if (Style == null) return;

            TextStyle.LanguageStyle style;
            if (Localization != LocalizationType.Static)
                style = Style.Styles[TextSettings.ActiveLanguageIndex];
            else
            {
                if (Language != null) style = Style.GetStyle(Language);
                else return;
            }

            Text.font = style.FontAsset;

            if (!CustomSize)
            {
                Text.fontSize = style.Size;
                Text.fontSizeMin = style.MinSize;
                Text.fontSizeMax = style.MaxSize;
            }

            OriginalFontSize = Text.fontSize;
            OriginalMinFontSize = Text.fontSizeMin;
            OriginalMaxFontSize = Text.fontSizeMax;

            if (Scaling != ScalingBehaviour.None) SetScale();
        }
        void SetLocalization()
        {
            if (Localization != LocalizationType.Localized) return;

            if (LocalizationSet == null) Text.text = TextSettings.MissingLocalizationSetWarning;
            else if (LocalizationSet.LocalizedText == "") Text.text = TextSettings.MissingLocalizationSetWarning;
            else Text.text = LocalizationSet.LocalizedText;
        }

        /// <summary>
        /// Updates the Text Mesh Pro UGUI's font size, minimum font size, and maximum font size values based on the Scale Settings.
        /// </summary>
        public void SetScale()
        {
            float multiplier = Scaling switch
            {
                ScalingBehaviour.None => 1,
                ScalingBehaviour.ScaleInterpolation => ScaleSettings.ScaleMultiplier,
                ScalingBehaviour.FontSizeInterpolation => ScaleSettings.FontSizeMultiplier,
                ScalingBehaviour.Both => ScaleSettings.ScaleMultiplier * ScaleSettings.FontSizeMultiplier,
                _ => 1,
            };

            float increase = Scaling switch
            {
                ScalingBehaviour.None => 0,
                ScalingBehaviour.ScaleInterpolation => 0,
                ScalingBehaviour.FontSizeInterpolation => ScaleSettings.FontSizeIncrease,
                ScalingBehaviour.Both => ScaleSettings.FontSizeIncrease,
                _ => 0,
            };

            Text.fontSize = (OriginalFontSize + increase) * multiplier;
            Text.fontSizeMin = (OriginalMinFontSize + increase) * multiplier;
            Text.fontSizeMax = (OriginalMaxFontSize + increase) * multiplier;
        }
        #endregion

#if UNITY_EDITOR

        #region Function
        protected override void OnValidate()
        {
            base.OnValidate();

            if (Text == null) Text = GetComponent<TextMeshProUGUI>();

            if (Style == null && TextSettings.TextStyles.Count > 0)
                Style = TextSettings.TextStyles[0];

            SetStyle();
            SetLocalization();
        }
        #endregion

#endif
    }
}