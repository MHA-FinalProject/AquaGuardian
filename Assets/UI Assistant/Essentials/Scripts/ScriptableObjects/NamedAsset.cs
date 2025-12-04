using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UIAssistant
{
    public abstract class NamedAsset : ScriptableObject
    {
        #region Variables
        /// <summary>
        /// The name of the asset.
        /// </summary>
        public string Name = "";

        /// <summary>
        /// Localization Set that contains the name of the asset in all Languages.
        /// </summary>
        public LocalizationSet LocalizedName;

        [SerializeField] protected AnimationSettings AnimationSettings;
        [SerializeField] protected ColorSettings ColorSettings;
        [SerializeField] protected OptionSettings OptionSettings;
        [SerializeField] protected ScaleSettings ScaleSettings;
        [SerializeField] protected TextSettings TextSettings;
        #endregion

        #region Helpers
        /// <summary>
        /// Returns the asset's name in the active Language.
        /// </summary>
        public string GetLocalizedName()
        {
            if (LocalizedName == null) return TextSettings.MissingLocalizationSetWarning;
            else return LocalizedName.LocalizedText;
       }
        #endregion

#if UNITY_EDITOR

        #region Function
        protected virtual void OnValidate()
        {
            if (AnimationSettings == null) AnimationSettings = ContentLibrary.GetAnimationSettings();
            if (ColorSettings == null) ColorSettings = ContentLibrary.GetColorSettings();
            if (OptionSettings == null) OptionSettings = ContentLibrary.GetOptionSettings();
            if (ScaleSettings == null) ScaleSettings = ContentLibrary.GetScaleSettings();
            if (TextSettings == null) TextSettings = ContentLibrary.GetTextSettings();
        }
        #endregion

#endif
    }
}