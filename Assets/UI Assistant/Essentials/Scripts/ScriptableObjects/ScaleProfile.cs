using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UIAssistant
{
    [HelpURL("https://sites.google.com/view/uiassistant/documentation/scaling?authuser=0#h.lj1bx9zx9lp")]
    public class ScaleProfile : NamedAsset
    {
        #region Variables
        /// <summary>
        /// The value used to multiply scales when the interpolation value is set to 0. Cannot be lower than 0.01.
        /// </summary>
        public float MinScaleMultiplier;

        /// <summary>
        /// The value used to multiply scales when the interpolation value is set to 1. This value can exceed 1.
        /// </summary>
        public float MaxScaleMultiplier;

        /// <summary>
        /// The value used to increase font sizes when the interpolation value is set to 0.
        /// </summary>
        public float MinFontSizeIncrease;

        /// <summary>
        /// The value used to increase font sizes when the interpolation value is set to 1.
        /// </summary>
        public float MaxFontSizeIncrease;

        /// <summary>
        /// The value used to multiply font sizes when the interpolation value is set to 0. Cannot be lower than 0.01.
        /// </summary>
        public float MinFontSizeMultiplier;

        /// <summary>
        /// The value used to multiply font sizes when the interpolation value is set to 1. This value can exceed 1.
        /// </summary>
        public float MaxFontSizeMultiplier;
        #endregion
    }
}