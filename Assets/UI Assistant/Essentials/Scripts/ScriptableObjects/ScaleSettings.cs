using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UIAssistant
{
    [HelpURL("https://sites.google.com/view/uiassistant/documentation/scaling?authuser=0#h.wwxgt0gmwn8x")]
    public class ScaleSettings : ScriptableObject
    {
        #region Variables
        /// <summary>
        /// A list of Scale Profiles added to the Scale Settings asset. This is handled by the Scale Settings editor; please do not add or remove any Scale Profiles yourself.
        /// </summary>
        public List<ScaleProfile> ScaleProfiles = new();

        /// <summary>
        /// The active Scale Profile whose values will determine Scaleables' and Text Stylers' SetScale results.
        /// </summary>
        public ScaleProfile ActiveScaleProfile
        {
            get
            {
                if (_ActiveScaleProfile == null && ScaleProfiles.Count > 0)
                    SetScaleProfile(0);
                return _ActiveScaleProfile;
            }
            private set
            {
                _ActiveScaleProfile = value;
            }
        }
        [SerializeField] ScaleProfile _ActiveScaleProfile;

        /// <summary>
        /// The index of the active Scale Profile, based on the order of Scale Profiles set within the Scale Settings.
        /// </summary>
        public int ActiveScaleProfileIndex
        {
            get => _ActiveScaleProfileIndex;
            private set { _ActiveScaleProfileIndex = value; }
        }
        [SerializeField] int _ActiveScaleProfileIndex;

        /// <summary>
        /// The interpolation value between the active Scale Profile's Min and Max Scale Multiplier values.
        /// Determines the values used to update Scaleables, based on the active Scale Profile's minimum and maximum values.
        /// </summary>
        public float ScaleInterpolationValue
        {
            get => _ScaleInterpolationValue;
            set
            {
                value = Mathf.Clamp(value, 0, 1);

                if (_ScaleInterpolationValue != value)
                {
                    _ScaleInterpolationValue = value;
                    UpdateScaleMultiplier();
                }
            }
        }
        [SerializeField] float _ScaleInterpolationValue;

        /// <summary>
        /// The interpolation value between the active Scale Profile's Min and Max Font Size Increase, as well as Min and Max Font Size Multiplier values.
        /// Determines the values used to update Text Stylers, based on the active Scale Profile's minimum and maximum values.
        /// </summary>
        public float FontSizeInterpolationValue
        {
            get => _FontSizeInterpolationValue;
            set
            {
                value = Mathf.Clamp(value, 0, 1);

                if (_FontSizeInterpolationValue != value)
                {
                    _FontSizeInterpolationValue = value;
                    UpdateFontSizeValues();
                }
            }
        }
        [SerializeField] float _FontSizeInterpolationValue;

        /// <summary>
        /// The value used to multiply Scaleables' values.
        /// </summary>
        public float ScaleMultiplier
        {
            get => _ScaleMultiplier;
            private set { _ScaleMultiplier = value; }
        }
        [SerializeField] float _ScaleMultiplier;

        /// <summary>
        /// The value used to multiply Text Stylers' font sizes.
        /// </summary>
        public float FontSizeMultiplier
        {
            get => _FontSizeMultiplier;
            private set { _FontSizeMultiplier = value; }
        }
        [SerializeField] float _FontSizeMultiplier;

        /// <summary>
        /// The value used to increase Text Stylers' font sizes.
        /// </summary>
        public float FontSizeIncrease
        {
            get => _FontSizeIncrease;
            private set { _FontSizeIncrease = value; }
        }
        [SerializeField] float _FontSizeIncrease;
        /// <summary>
        /// Returns an array of Scale Profile names listed in the Scale Settings.
        /// </summary>
        public string[] ProfileNames
        {
            get
            {
                List<string> profileNames = new();

                for (int i = 0; i < ScaleProfiles.Count; i++)
                {
                    profileNames.Add(ScaleProfiles[i].Name);
                }

                return profileNames.ToArray();
            }
        }

        readonly List<IScaleable> Scaleables = new();
        readonly List<TextStyler> TextStylers = new();
        #endregion

        #region Function
        /// <summary>
        /// Registers a Scaleable to be updated when interpolation values or the active Scale Profile changes. Scaleables automatically add themselves.
        /// </summary>
        /// <param name="scaleable">The Scaleable to be registered for updates.</param>
        public void Add(IScaleable scaleable)
        {
            Scaleables.Add(scaleable);
        }

        /// <summary>
        /// Removes a Scaleable, locking it out of updates when interpolation values or the active Scale Profile changes. Scaleables automatically remove themselves.
        /// </summary>
        /// <param name="scaleable">The Scaleable to be locked out of updates.</param>
        public void Remove(IScaleable scaleable)
        {
            Scaleables.Remove(scaleable);
        }

        /// <summary>
        /// Registers a Text Styler to be updated when interpolation values or the active Scale Profile changes. Text Stylers automatically add themselves.
        /// </summary>
        /// <param name="textStyler">The Text Styler to be registered for updates.</param>
        public void Add(TextStyler textStyler)
        {
            TextStylers.Add(textStyler);
        }

        /// <summary>
        /// Removes a Text Styler, locking it out of updates when interpolation values or the active Scale Profile changes. Text Stylers automatically remove themselves.
        /// </summary>
        /// <param name="textStyler">The Text Styler to be locked out of updates.</param>
        public void Remove(TextStyler textStyler)
        {
            TextStylers.Remove(textStyler);
        }

        /// <summary>
        /// Sets the designated Scale Profile as the active Scale Profile, and updates all Scaleables and Text Stylers based on interpolation values, in combination with the new active Scale Profile's minimum and maximum values.
        /// </summary>
        /// <param name="scaleProfile">A Scale Profile asset within the Scale Settings asset.</param>
        public void SetScaleProfile(ScaleProfile scaleProfile)
        {
            ActiveScaleProfile = scaleProfile;
            ActiveScaleProfileIndex = ScaleProfiles.IndexOf(ActiveScaleProfile);

            UpdateScaleMultiplier();
            UpdateFontSizeValues();
        }

        /// <summary>
        /// Sets the designated Scale Profile as the active Scale Profile, and updates all Scaleables and Text Stylers based on interpolation values, in combination with the new active Scale Profile's minimum and maximum values.
        /// </summary>
        /// <param name="index">The index of the Scale Profile. The order of Scale Profiles can be modified under UI Assistant/Scale Settings.</param>
        public void SetScaleProfile(int index)
        {
            ActiveScaleProfileIndex = Mathf.Clamp(index, 0, ScaleProfiles.Count - 1);
            ActiveScaleProfile = ScaleProfiles[ActiveScaleProfileIndex];

            UpdateScaleMultiplier();
            UpdateFontSizeValues();
        }

        void UpdateScaleMultiplier()
        {
            ScaleMultiplier = Mathf.Lerp(ActiveScaleProfile.MinScaleMultiplier, ActiveScaleProfile.MaxScaleMultiplier, ScaleInterpolationValue);

            for (int i = 0; i < Scaleables.Count; i++)
            {
                Scaleables[i].SetScale();
            }
        }
        void UpdateFontSizeValues()
        {
            FontSizeMultiplier = Mathf.Lerp(ActiveScaleProfile.MinFontSizeMultiplier, ActiveScaleProfile.MaxFontSizeMultiplier, FontSizeInterpolationValue);
            FontSizeIncrease = Mathf.Lerp(ActiveScaleProfile.MinFontSizeIncrease, ActiveScaleProfile.MaxFontSizeIncrease, FontSizeInterpolationValue);

            for (int i = 0; i < TextStylers.Count; i++)
            {
                TextStylers[i].SetScale();
            }
        }

        /// <summary>
        /// Sets the first Scale Profile as the active Scale Profile if the active Scale Profile is null. This happens automatically when a Scale Profile is deleted from the Scale Settings.
        /// </summary>
        public void OnScaleProfileRemoved()
        {
            if (ScaleProfiles.Count == 0) return;

            if (ActiveScaleProfile == null) SetScaleProfile(0);
        }

        /// <summary>
        /// Sets the first Scale Profile as the active Scale Profile if the active Scale Profile is null. This happens automatically when a Scale Profile is created within the Scale Settings.
        /// </summary>
        public void OnScaleProfileAdded()
        {
            if (ActiveScaleProfile == null) SetScaleProfile(0);
        }
        #endregion
    }

    public interface IScaleable
    {
        #region Function
        public void SetScale() { }
        #endregion
    }
}