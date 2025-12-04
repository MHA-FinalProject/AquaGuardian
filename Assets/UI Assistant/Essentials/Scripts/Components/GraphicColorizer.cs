using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UIAssistant
{
    [HelpURL("https://sites.google.com/view/uiassistant/documentation/color?authuser=0#h.nhosb06d73xc")]
    [RequireComponent(typeof(Graphic)), AddComponentMenu("UI Assistant/Graphic Colorizer"), DisallowMultipleComponent]
    public class GraphicColorizer : UIAssistantComponent, IColorizable
    {
        #region Variables
        [SerializeField] Graphic Graphic;

        [Tooltip("Category defined in the Color Settings. Determines the Graphic's color.")] public ColorCategory Category;
        [Range(0, 1), Tooltip("Value used to multiply the alpha of the colors in the Category.")] public float AlphaMultiplier = 1;

        Color Color;
        Coroutine ColorFadeCoroutine;
        #endregion

        #region Function
        void OnEnable()
        {
            ColorSettings.Add(this);
            SetColor();
        }
        void OnDisable()
        {
            ColorSettings.Remove(this);
            StopColorFade();
        }

        /// <summary>
        /// Updates the Graphic's color with the selected Color Category's normal color.
        /// </summary>
        public void OnColorProfileChanged()
        {
            SetColor();
        }

        /// <summary>
        /// Updates the Graphic's color based on selection state.
        /// </summary>
        /// <param name="selectionState">Determines the Graphic's new color based on the selected Color Category.</param>
        /// <param name="instant">When set to true, the Graphic's color will change without animation.</param>
        public void OnColorProfileChanged(SelectionState selectionState, bool instant)
        {
            if (ColorFadeCoroutine != null)
            {
                StartColorFade(selectionState, instant);
            }
        }

        /// <summary>
        /// Updates the Graphic's color with the selected Color Category's normal color.
        /// </summary>
        public void SetColor()
        {
            if (Category == null) return;

            Color = Category.NormalColor;
            Graphic.color = AlphaAdjustedColor;
        }

        /// <summary>
        /// Updates the Graphic's color based on selection state.
        /// </summary>
        /// <param name="selectionState">Determines the Graphic's new color based on the selected Color Category.</param>
        /// <param name="instant">When set to true, the Graphic's color will change without animation.</param>
        public void StartColorFade(SelectionState selectionState, bool instant)
        {
            if (!gameObject.activeInHierarchy || Category.Colors.Count == 0) return;

            StopColorFade();

            Color = Category.GetColor(selectionState);
            Color targetColor = AlphaAdjustedColor;

            if (targetColor == Graphic.color) return;

            if (instant)
            {
                Graphic.color = targetColor;
            }
            else
            {
                float fadeTime = ColorSettings.ActiveColorProfile.FadeTime;
                ColorFadeCoroutine = StartCoroutine(ColorFade(Graphic.color, targetColor, fadeTime));
            }
        }
        IEnumerator ColorFade(Color startColor, Color targetColor, float fadeTime)
        {
            float progress = 0;

            while (progress < fadeTime)
            {
                progress += Time.deltaTime;
                float t = ColorSettings.ActiveColorProfile.FadeCurve.Evaluate(progress / fadeTime);
                Graphic.color = Color.Lerp(startColor, targetColor, t);

                yield return null;
            }

            ColorFadeCoroutine = null;
        }
        void StopColorFade()
        {
            if (ColorFadeCoroutine != null)
            {
                StopCoroutine(ColorFadeCoroutine);
                ColorFadeCoroutine = null;
            }
        }
        #endregion

        #region Helpers
        Color AlphaAdjustedColor
        {
            get
            {
                Color color = Color;
                if (AlphaMultiplier < 1) color.a *= AlphaMultiplier;
                return color;
            }
        }
        #endregion

#if UNITY_EDITOR

        #region Function
        protected override void OnValidate()
        {
            base.OnValidate();

            if (Graphic == null) Graphic = GetComponent<Graphic>();

            if (Category == null && ColorSettings.ColorCategories.Count > 0) Category = ColorSettings.ColorCategories[0];

            SetColor();
        }
        #endregion

#endif
    }
}