using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UIAssistant
{
    [HelpURL("https://sites.google.com/view/uiassistant/documentation/color?authuser=0#h.3wh3b1dc1j3")]
    public class ColorProfile : NamedAsset
    {
        #region Variables
        /// <summary>
        /// The time it takes to transition from one state to the next.
        /// </summary>
        public float FadeTime;

        /// <summary>
        /// Animation curve used to ease between states.
        /// </summary>
        public AnimationCurve FadeCurve;
        #endregion
    }
}