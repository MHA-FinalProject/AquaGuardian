using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UIAssistant
{
    [HelpURL("https://sites.google.com/view/uiassistant/documentation/scaling?authuser=0#h.cytixrc0u6o1")]
    [AddComponentMenu("UI Assistant/Scaling Horizontal Layout Group"), DisallowMultipleComponent]
    public class ScalingHorizontalLayoutGroup : HorizontalLayoutGroup, IScaleable
    {
        #region Variables
        [SerializeField] ScaleSettings ScaleSettings;
        [SerializeField] float OriginalSpacing;
        [SerializeField] RectOffset OriginalPadding = new();
        #endregion

        #region Function
        protected override void OnEnable()
        {
            base.OnEnable();

            if (Application.isPlaying)
            {
                ScaleSettings.Add(this);
                SetScale();
            }
        }
        protected override void OnDisable()
        {
            base.OnDisable();

            if (Application.isPlaying) ScaleSettings.Remove(this);
        }

        /// <summary>
        /// Updates the Horizontal Layout Group's spacing and padding values based on the Scale Settings.
        /// </summary>
        public void SetScale()
        {
            spacing = OriginalSpacing * ScaleSettings.ScaleMultiplier;
            padding.left = Mathf.RoundToInt(OriginalPadding.left * ScaleSettings.ScaleMultiplier);
            padding.right = Mathf.RoundToInt(OriginalPadding.right * ScaleSettings.ScaleMultiplier);
            padding.top = Mathf.RoundToInt(OriginalPadding.top * ScaleSettings.ScaleMultiplier);
            padding.bottom = Mathf.RoundToInt(OriginalPadding.bottom * ScaleSettings.ScaleMultiplier);
        }
        #endregion

#if UNITY_EDITOR

        #region Function
        protected override void OnValidate()
        {
            base.OnValidate();

            if (ScaleSettings == null) ScaleSettings = ContentLibrary.GetScaleSettings();

            OriginalSpacing = spacing;
            OriginalPadding.left = padding.left;
            OriginalPadding.right = padding.right;
            OriginalPadding.top = padding.top;
            OriginalPadding.bottom = padding.bottom;
        }
        #endregion

#endif
    }
}