using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UIAssistant
{
    [HelpURL("https://sites.google.com/view/uiassistant/documentation/scaling?authuser=0#h.bcyxa2stprzf")]
    [RequireComponent(typeof(Image)), AddComponentMenu("UI Assistant/Sliced Image Scaler"), DisallowMultipleComponent]
    public class SlicedImageScaler : UIAssistantComponent, IScaleable
    {
        #region Variables
        [SerializeField] Image Image;
        [SerializeField] float OriginalMultiplier;
        #endregion

        #region Function
        void OnEnable()
        {
            if (Image.type != Image.Type.Sliced) return;

            OriginalMultiplier = Image.pixelsPerUnitMultiplier;
            ScaleSettings.Add(this);
            SetScale();
        }
        void OnDisable()
        {
            if (Image.type != Image.Type.Sliced) return;

            ScaleSettings.Remove(this);
        }

        /// <summary>
        /// Updates the Image's pixels per unit multiplier based on the Scale Settings. Image type must be set to sliced.
        /// </summary>
        public void SetScale()
        {
            if (Image.type != Image.Type.Sliced) return;

            float multiplier = 1 / ScaleSettings.ScaleMultiplier;
            Image.pixelsPerUnitMultiplier = OriginalMultiplier * multiplier;
        }
        #endregion

#if UNITY_EDITOR

        #region Function
        protected override void OnValidate()
        {
            base.OnValidate();

            if (Image == null) Image = GetComponent<Image>();
        }
        #endregion

#endif
    }
}