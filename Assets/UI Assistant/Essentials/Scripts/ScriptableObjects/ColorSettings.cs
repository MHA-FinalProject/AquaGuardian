using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UIAssistant
{
    [HelpURL("https://sites.google.com/view/uiassistant/documentation/color?authuser=0#h.cfmv4xtefp5")]
    public class ColorSettings : ScriptableObject
    {
        #region Variables
        /// <summary>
        /// A list of Color Categories added to the Color Settings asset. This is handled by the Color Settings editor; please do not add or remove any Color Categories yourself.
        /// </summary>
        public List<ColorCategory> ColorCategories = new();

        /// <summary>
        /// A list of Color Profiles added to the Color Settings asset. This is handled by the Color Settings editor; please do not add or remove any Color Profiles yourself.
        /// </summary>
        public List<ColorProfile> ColorProfiles = new();

        /// <summary>
        /// The active Color Profile whose values will determine Graphic Colorizers' colors.
        /// </summary>
        public ColorProfile ActiveColorProfile
        {
            get
            {
                if (_ActiveColorProfile == null && ColorProfiles.Count > 0)
                    SetColorProfile(0);
                return _ActiveColorProfile;
            }
            private set { _ActiveColorProfile = value; }
        }
        [SerializeField] ColorProfile _ActiveColorProfile;

        /// <summary>
        /// The index of the active Color Profile, based on the order of Color Profiles set within the Color Settings.
        /// </summary>
        public int ActiveColorProfileIndex
        {
            get => _ActiveColorProfileIndex;
            private set { _ActiveColorProfileIndex = value; }
        }
        [SerializeField] int _ActiveColorProfileIndex;

        /// <summary>
        /// Returns an array of Color Category names listed in the Color Settings.
        /// </summary>
        public string[] CategoryNames
        {
            get
            {
                List<string> categoryNames = new();

                for (int i = 0; i < ColorCategories.Count; i++)
                {
                    categoryNames.Add(ColorCategories[i].Name);
                }

                return categoryNames.ToArray();
            }
        }

        /// <summary>
        /// Returns an array of Color Profile names listed in the Color Settings.
        /// </summary>
        public string[] ProfileNames
        {
            get
            {
                List<string> profileNames = new();

                for (int i = 0; i < ColorProfiles.Count; i++)
                {
                    profileNames.Add(ColorProfiles[i].Name);
                }

                return profileNames.ToArray();
            }
        }
        
        readonly List<IColorizable> Colorizables = new();
        #endregion

        #region Function
        /// <summary>
        /// Registers a Colorizable to be updated when the active Color Profile changes. Colorizables automatically add themselves.
        /// </summary>
        public void Add(IColorizable colorizable)
        {
            Colorizables.Add(colorizable);
        }

        /// <summary>
        /// Removes a Colorizable, locking it out of updates when the active Color Profile changes. Colorizables automatically remove themselves.
        /// </summary>
        public void Remove(IColorizable colorizable)
        {
            Colorizables.Remove(colorizable);
        }

        /// <summary>
        /// Sets the designated Color Profile as the active Color Profile, and notifies all Colorizables.
        /// </summary>
        /// <param name="colorProfile">A Color Profile asset within the Color Settings asset.</param>
        public void SetColorProfile(ColorProfile colorProfile)
        {
            ActiveColorProfile = colorProfile;
            ActiveColorProfileIndex = ColorProfiles.IndexOf(ActiveColorProfile);

            SetColorProfile();
        }

        /// <summary>
        /// Sets the designated Color Profile as the active Color Profile, and notifies all Colorizables.
        /// </summary>
        /// <param name="index">The index of a Color Profile. The order of Color Profiles can be modified under UI Assistant/Color Settings.</param>
        public void SetColorProfile(int index)
        {
            ActiveColorProfileIndex = Mathf.Clamp(index, 0, ColorProfiles.Count - 1);
            ActiveColorProfile = ColorProfiles[ActiveColorProfileIndex];

            SetColorProfile();
        }
        void SetColorProfile()
        {
            for (int i = 0; i < Colorizables.Count; i++)
            {
                Colorizables[i].OnColorProfileChanged();
            }
        }

        /// <summary>
        /// Sets the first Color Profile as the active Color Profile if the active Color Profile is null. This happens automatically when a Color Profile is deleted from the Color Settings.
        /// </summary>
        public void OnColorProfileRemoved()
        {
            if (ColorProfiles.Count > 0 && ActiveColorProfile == null) SetColorProfile(0);
        }

        /// <summary>
        /// Sets the first Color Profile as the active Color Profile if the active Color Profile is null. This happens automatically when a Color Profile is created within the Color Settings.
        /// </summary>
        public void OnColorProfileAdded()
        {
            if (ActiveColorProfile == null) SetColorProfile(0);
        }
        #endregion
    }

    public interface IColorizable
    {
        #region Function
        public void OnColorProfileChanged() { }
        #endregion
    }
}