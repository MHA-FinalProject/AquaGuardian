using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UIAssistant
{
    [HelpURL("https://sites.google.com/view/uiassistant/documentation/animation?authuser=0#h.s6f2p4rr4g01")]
    public class AnimationProfile : NamedAsset
    {
        #region Variables
        /// <summary>
        /// The time it takes to transition between the hidden and visible states.
        /// </summary>
        public float TransitionTime;

        /// <summary>
        /// Animation curve used to ease between the states.
        /// </summary>
        public AnimationCurve TransitionCurve;

        /// <summary>
        /// Values used to move the Game Object into its hidden state. Positive values move the Game Object away from its parent, while negative values move it toward it.
        /// </summary>
        public Vector2 HiddenPositionOffset;

        /// <summary>
        /// Value used to rotate the Game Object into its hidden state, based on its anchored position. A positive value will rotate the Game Object clockwise if it’s anchored to the right, or, counterclockwise if it’s anchored to the left.
        /// </summary>
        public float HiddenAngleOffset;

        /// <summary>
        /// Values used to multiply the X and Y values of the Game Object’s scale when transitioning into the hidden state.
        /// </summary>
        public Vector2 HiddenScale;

        /// <summary>
        /// The Canvas Group’s alpha value in the Game Object’s hidden state.
        /// </summary>
        public float HiddenAlpha;
        #endregion
    }
}