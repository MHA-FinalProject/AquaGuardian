using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UIAssistant
{
    [HelpURL("https://sites.google.com/view/uiassistant/documentation/scaling?authuser=0#h.rxvzmpg6sn3h")]
    [RequireComponent(typeof(RectTransform)), ExecuteAlways, AddComponentMenu("UI Assistant/Scaling Layout Element"), DisallowMultipleComponent]
    public class ScalingLayoutElement : LayoutElement, IScaleable
    {
        #region Variables
        [SerializeField] ScaleSettings ScaleSettings;
        [SerializeField] float OriginalMinWidth;
        [SerializeField] float OriginalMinHeight;
        [SerializeField] float OriginalPreferredWidth;
        [SerializeField] float OriginalPreferredHeight;
        #endregion

        #region Function
        protected override void OnEnable()
        {
            if (Application.isPlaying)
            {
                OriginalMinWidth = minWidth;
                OriginalMinHeight = minHeight;
                OriginalPreferredWidth = preferredWidth;
                OriginalPreferredHeight = preferredHeight;

                ScaleSettings.Add(this);
                SetScale();
            }

            base.OnEnable();
        }
        protected override void OnDisable()
        {
            if (Application.isPlaying) ScaleSettings.Remove(this);
         
            base.OnDisable();
        }

        /// <summary>
        /// Updates the Layout Element's minimum width, minimum height, preferred width, and preferred height values based on the Scale Settings.
        /// </summary>
        public void SetScale()
        {
            float multiplier = ScaleSettings.ScaleMultiplier;

            if (minWidth > 0) minWidth = OriginalMinWidth * multiplier;
            if (minHeight > 0) minHeight = OriginalMinHeight * multiplier;
            if (preferredWidth > 0) preferredWidth = OriginalPreferredWidth * multiplier;
            if (preferredHeight > 0) preferredHeight = OriginalPreferredHeight * multiplier;
        }
        #endregion

#if UNITY_EDITOR

        #region Function
        protected override void OnValidate()
        {
            base.OnValidate();

            if (ScaleSettings == null) ScaleSettings = ContentLibrary.GetScaleSettings();
        }
        #endregion

#endif
    }
}