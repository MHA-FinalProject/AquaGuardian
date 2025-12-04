using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UIAssistant
{
    [HelpURL("https://sites.google.com/view/uiassistant/documentation/color?authuser=0#h.xiwgnmldomow")]
    public class ColorCategory : NamedAsset
    {
        #region Variables
        /// <summary>
        /// A list of Normal, Highlighted, Pressed, Selected, and Disabled colors for every Color Profile added to the Color Settings.
        /// </summary>
        public List<ProfileColors> Colors = new();

        /// <summary>
        /// The Normal color of the active Color Profile.
        /// </summary>
        public Color NormalColor => Colors.Count == 0 ? Color.white : Colors[ColorSettings.ActiveColorProfileIndex].NormalColor;
        #endregion

        #region Classes
        [System.Serializable]
        public class ProfileColors
        {
            public ColorProfile ColorProfile = null;
            public Color NormalColor = Color.white;
            public Color HighlightedColor = Color.white;
            public Color PressedColor = Color.white;
            public Color SelectedColor = Color.white;
            public Color DisabledColor = Color.white;
        }
        #endregion

        #region Function
        /// <summary>
        /// Updates Colors based on changes made to Color Profiles. Color Settings automatically handles validation.
        /// </summary>
        public void ValidateCategory()
        {
#if UNITY_EDITOR

            OnValidate();

#endif

            if (Colors.Count != ColorSettings.ColorProfiles.Count)
            {
                UpdateColors();
                return;
            }

            for (int i = 0; i < Colors.Count; i++)
            {
                if (Colors[i].ColorProfile != ColorSettings.ColorProfiles[i])
                {
                    UpdateColors();
                    return;
                }
            }
        }
        void UpdateColors()
        {
            List<ProfileColors> colors = new(Colors);
            Colors.Clear();

            for (int i = 0; i < ColorSettings.ColorProfiles.Count; i++)
            {
                int index = MatchingIndex(colors, ColorSettings.ColorProfiles[i]);

                if (index == -1)
                {
                    ProfileColors newColors = new()
                    {
                        ColorProfile = ColorSettings.ColorProfiles[i],
                        NormalColor = Color.white,
                        HighlightedColor = Color.white,
                        PressedColor = Color.white,
                        SelectedColor = Color.white,
                        DisabledColor = Color.white,
                    };

                    Colors.Add(newColors);
                }
                else
                {
                    Colors.Add(colors[index]);
                    colors.RemoveAt(index);
                }
            }
        }
        #endregion

        #region Helpers
        /// <summary>
        /// Returns the Normal, Highlighted, Pressed, Selected, or Disabled color set for the active Color Profile, based on the requested Selectable state.
        /// </summary>
        /// <param name="selectionState">The current Selection State of the Selectable.</param>
        public Color GetColor(SelectionState selectionState)
        {
            if (Colors.Count == 0) return Color.white;

            else
            {
                int activeIndex = ColorSettings.ActiveColorProfileIndex;

                return selectionState switch
                {
                    SelectionState.Normal => Colors[activeIndex].NormalColor,
                    SelectionState.Highlighted => Colors[activeIndex].HighlightedColor,
                    SelectionState.Pressed => Colors[activeIndex].PressedColor,
                    SelectionState.Selected => Colors[activeIndex].SelectedColor,
                    SelectionState.Disabled => Colors[activeIndex].DisabledColor,
                    _ => Colors[activeIndex].NormalColor,
                };
            }
        }
        int MatchingIndex(List<ProfileColors> colors, ColorProfile colorProfile)
        {
            int index = -1;

            for (int i = 0; i < colors.Count; i++)
            {
                if (colors[i].ColorProfile == colorProfile)
                {
                    index = i;
                    break;
                }
            }

            return index;
        }
        #endregion
    }
    public enum SelectionState
    {
        Normal = 0,
        Highlighted = 1,
        Pressed = 2,
        Selected = 3,
        Disabled = 4,
    }
}