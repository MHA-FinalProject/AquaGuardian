using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UIAssistant
{
    [HelpURL("https://sites.google.com/view/uiassistant/documentation/scaling?authuser=0#h.uec0osv6089i")]
    [RequireComponent(typeof(RectTransform)), AddComponentMenu("UI Assistant/Rect Transform Scaler"), DisallowMultipleComponent]
    public class RectTransformScaler : UIAssistantComponent, IScaleable
    {
        #region Variables
        [SerializeField] RectTransform RectTransform;

        public ScaleMode Mode;
        public bool ScaleX = true;
        public bool ScaleY = true;
        public bool CounterX = false;
        public bool CounterY = false;

        [SerializeField] Vector2 OriginalSizeDelta;
        [SerializeField] Vector2 OriginalRectSize;
        [SerializeField] Vector2 OriginalScale;
        #endregion

        #region Enums
        public enum ScaleMode
        {
            Scale = 0,
            Size = 1,
        }
        #endregion

        #region Function
        void OnEnable()
        {
            OriginalSizeDelta = RectTransform.sizeDelta;
            OriginalRectSize = RectTransform.rect.size;
            OriginalScale = RectTransform.localScale;

            ScaleSettings.Add(this);
            SetScale();
        }
        void OnDisable()
        {
            ScaleSettings.Remove(this);
        }

        /// <summary>
        /// Updates the Rect Transform's scale or size based on the Scale Settings.
        /// </summary>
        public void SetScale()
        {
            float multiplier = ScaleSettings.ScaleMultiplier;
            
            switch (Mode)
            {
                case ScaleMode.Scale:
                    float scaleX = ScaleX ? OriginalScale.x * multiplier : OriginalScale.x;
                    float scaleY = ScaleY ? OriginalScale.y * multiplier : OriginalScale.y;

                    RectTransform.localScale = new(scaleX, scaleY);

                    Vector2 targetSizeDelta = OriginalSizeDelta;

                    if (ScaleX && CounterX)
                    {
                        float targetRectSizeX = OriginalRectSize.x / multiplier;
                        float differenceX = targetRectSizeX - OriginalRectSize.x;
                        targetSizeDelta.x = OriginalSizeDelta.x + differenceX;
                    }

                    if (ScaleY && CounterY)
                    {
                        float targetRectSizeY = OriginalRectSize.y / multiplier;
                        float differenceY = targetRectSizeY - OriginalRectSize.y;
                        targetSizeDelta.y = OriginalSizeDelta.y + differenceY;
                    }

                    RectTransform.sizeDelta = targetSizeDelta;

                    break;
                case ScaleMode.Size:
                    float sizeX = ScaleX ? OriginalSizeDelta.x * multiplier : OriginalSizeDelta.x;
                    float sizeY = ScaleY ? OriginalSizeDelta.y * multiplier : OriginalSizeDelta.y;

                    RectTransform.sizeDelta = new(sizeX, sizeY);

                    break;
            }
        }
        #endregion

#if UNITY_EDITOR

        #region Function
        protected override void OnValidate()
        {
            base.OnValidate();

            if (RectTransform == null) RectTransform = GetComponent<RectTransform>();
        }
        #endregion

#endif
    }
}