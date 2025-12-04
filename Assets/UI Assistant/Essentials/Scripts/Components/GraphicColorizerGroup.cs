using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UIAssistant
{
    [HelpURL("https://sites.google.com/view/uiassistant/documentation/color?authuser=0#h.hmc3io9yqk8c")]
    [AddComponentMenu("UI Assistant/Graphic Colorizer Group"), DisallowMultipleComponent]
    public class GraphicColorizerGroup : UIAssistantComponent, IColorizable
    {
        #region Variables
        [Tooltip("Target Graphics will retain their normal visuals as long as the Selectable is interactable.")] public bool ForceNormal = true;
        [Tooltip("Graphic Colorizers to update upon the Selectable's state change.")] public List<GraphicColorizer> TargetGraphics = new();

        SelectionState SelectionState;
        bool Instant;
        #endregion

        #region Function
        void OnEnable()
        {
            ColorSettings.Add(this);
        }
        void OnDisable()
        {
            ColorSettings.Remove(this);
        }

        /// <summary>
        /// Prompts all Graphic Colorizers in the list to update their Graphics' color.
        /// </summary>
        /// <param name="selectionState">Determines the new color set for the Graphic Colorizers' Graphics based on their selected Color Categories.</param>
        /// <param name="instant">When set to true, the color of the Graphic Colorizers' Graphics will change without animation.</param>
        public void DoStateTransition(SelectionState selectionState, bool instant)
        {
            if (!Application.isPlaying) return;

            SelectionState = selectionState;
            Instant = instant;

            for (int i = 0; i < TargetGraphics.Count; i++)
            {
                TargetGraphics[i].StartColorFade(SelectionState, Instant);
            }
        }

        /// <summary>
        /// Prompts all Graphic Colorizers in the list to update their Graphics' color.
        /// </summary>
        public void OnColorProfileChanged()
        {
            if (!Application.isPlaying) return;

            for (int i = 0; i < TargetGraphics.Count; i++)
            {
                TargetGraphics[i].OnColorProfileChanged(SelectionState, Instant);
            }
        }
        #endregion
    }
}