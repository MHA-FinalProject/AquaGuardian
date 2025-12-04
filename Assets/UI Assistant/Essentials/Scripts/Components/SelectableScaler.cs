using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UIAssistant
{
    [HelpURL("https://sites.google.com/view/uiassistant/documentation/animation?authuser=0#h.6i6du8i7ux0a")]
    public class SelectableScaler : MonoBehaviour
    {
        #region Variables
        [Tooltip("The Colored Selectable whose state will be matched.")] public Selectable Selectable;
        [Tooltip("The local scale of the Transform in the Normal state.")] public float NormalScale = 1;
        [Tooltip("The local scale of the Transform in the Highlighted state.")] public float HighlightedScale = 1.1f;
        [Tooltip("The local scale of the Transform in the Pressed state.")] public float PressedScale = 1.05f;
        [Tooltip("The local scale of the Transform in the Selected state.")] public float SelectedScale = 1.1f;
        [Tooltip("The local scale of the Transform in the Disabled state.")] public float DisabledScale = 1;
        [Tooltip("The time it takes to transition from one state to the next.")] public float TransitionTime = 0.1f;
        [Tooltip("Animation curve used to ease between states.")] public AnimationCurve TransitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [SerializeField, HideInInspector] Transform TargetTransform;
        Coroutine AnimationCoroutine;
        #endregion

        #region Function
        void OnEnable()
        {
            if (Selectable != null && Selectable is ISelectableScalerController controller)
                controller.AddSelectableScaler(this);
        }
        void OnDisable()
        {
            if (Selectable != null && Selectable is ISelectableScalerController controller)
                controller.RemoveSelectableScaler(this);
        }

        /// <summary>
        /// Prompts the Selectable Scaler to update its Transform's local scale.
        /// </summary>
        /// <param name="selectionState">Determines which scale value will be used.</param>
        /// <param name="instant">When set to true, the local scale of the Transform will change without animation.</param>
        public void DoStateTransition(SelectionState selectionState, bool instant)
        {
            if (!Application.isPlaying) return;

            if (instant) TargetTransform.localScale = GetTargetScale(selectionState);
            else
            {
                if (AnimationCoroutine != null) StopCoroutine(AnimationCoroutine);
                AnimationCoroutine = StartCoroutine(DoStateTransition(GetTargetScale(selectionState)));
            }
        }
        IEnumerator DoStateTransition(Vector3 targetScale)
        {
            Vector3 startScale = TargetTransform.localScale;
            float currentTime = 0;

            while (currentTime < TransitionTime)
            {
                currentTime += Time.deltaTime;
                float t = TransitionCurve.Evaluate(currentTime / TransitionTime);
                TargetTransform.localScale = Vector3.Lerp(startScale, targetScale, t);

                yield return null;
            }

            AnimationCoroutine = null;
        }
        #endregion

        #region Helpers
        Vector3 GetTargetScale(SelectionState selectionState)
        {
            float scale = selectionState switch
            {
                SelectionState.Normal => NormalScale,
                SelectionState.Highlighted => HighlightedScale,
                SelectionState.Pressed => PressedScale,
                SelectionState.Selected => SelectedScale,
                SelectionState.Disabled => DisabledScale,
                _ => NormalScale,
            };
            return new(scale, scale, scale);
        }
        #endregion

#if UNITY_EDITOR

        #region Function
        void OnValidate()
        {
            if (TargetTransform == null) TargetTransform = GetComponent<Transform>();
        }
        #endregion

#endif

    }

    public interface ISelectableScalerController
    {
        public void AddSelectableScaler(SelectableScaler selectableScaler) { }
        public void RemoveSelectableScaler(SelectableScaler selectableScaler) { }
    }
}