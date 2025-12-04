using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UIAssistant
{
    [HelpURL("https://sites.google.com/view/uiassistant/documentation/animation?authuser=0#h.xo3ieust7gfy")]
    [AddComponentMenu("UI Assistant/UI Animator"), RequireComponent(typeof(CanvasGroup)), RequireComponent(typeof(RectTransform)), DisallowMultipleComponent]
    public class UIAnimator : VisibilityStateHandler
    {
        #region Variables
        [Tooltip("Determines the Game Object's position, rotation, scale, and alpha in its hidden state.")] public AnimationProfile AnimationProfile;
        [Tooltip("Check this box if the Game Object's Rect Transform and/or Canvas Group is manipulated by scripts other than this.")] public bool IsDynamic;

        [SerializeField] RectTransform RectTransform;
        [SerializeField] CanvasGroup CanvasGroup;

        [SerializeField] bool ToggleInteractability;
        [SerializeField] bool HorizontalStretch;
        [SerializeField] bool VerticalStretch;

        [SerializeField] Vector2 DefaultOffsetMin;
        [SerializeField] Vector2 DefaultOffsetMax;
        [SerializeField] float DefaultAngle;
        [SerializeField] Vector3 DefaultScale;
        [SerializeField] float DefaultAlpha;

        [SerializeField] Vector2 HiddenOffsetMin;
        [SerializeField] Vector2 HiddenOffsetMax;
        [SerializeField] float HiddenAngle;
        [SerializeField] Vector3 HiddenScale;
        [SerializeField] float HiddenAlpha;

        bool Animating;
        Coroutine StateTransitionCoroutine;
        #endregion

        #region Function
        protected override void Start()
        {
            Validate();
            base.Start();
        }
        void Validate()
        {
            if (RectTransform == null) RectTransform = GetComponent<RectTransform>();
            if (CanvasGroup == null) CanvasGroup = GetComponent<CanvasGroup>();

            if (AnimationProfile != null)
            {
                SetDefaultValues();
                CalculateHiddenValues();
            }
            else if (AnimationSettings.AnimationProfiles.Count > 0)
                AnimationProfile = AnimationSettings.AnimationProfiles[0];
        }
        void SetDefaultValues()
        {
            ToggleInteractability = CanvasGroup.interactable;

            HorizontalStretch = RectTransform.anchorMin.x != RectTransform.anchorMax.x;
            VerticalStretch = RectTransform.anchorMin.y != RectTransform.anchorMax.y;

            DefaultOffsetMin.x = RectTransform.offsetMin.x;
            DefaultOffsetMax.x = RectTransform.offsetMax.x;
            DefaultOffsetMin.y = RectTransform.offsetMin.y;
            DefaultOffsetMax.y = RectTransform.offsetMax.y;
            DefaultAngle = RectTransform.localEulerAngles.z;
            DefaultScale = RectTransform.localScale;
            DefaultAlpha = CanvasGroup.alpha;
        }
        void CalculateHiddenValues()
        {
            if (HorizontalStretch)
            {
                HiddenOffsetMin.x = RectTransform.offsetMin.x - AnimationProfile.HiddenPositionOffset.x;
                HiddenOffsetMax.x = RectTransform.offsetMax.x + AnimationProfile.HiddenPositionOffset.x;
            }
            else
            {
                float xMultiplier = 0;
                if (RectTransform.anchorMin.x < .5f) xMultiplier = -1;
                else if (RectTransform.anchorMin.x > .5f) xMultiplier = 1;
                float xOffset = AnimationProfile.HiddenPositionOffset.x * xMultiplier;
                HiddenOffsetMin.x = RectTransform.offsetMin.x + xOffset;
                HiddenOffsetMax.x = RectTransform.offsetMax.x + xOffset;
            }

            if (VerticalStretch)
            {
                HiddenOffsetMin.y = RectTransform.offsetMin.y - AnimationProfile.HiddenPositionOffset.y;
                HiddenOffsetMax.y = RectTransform.offsetMax.y + AnimationProfile.HiddenPositionOffset.y;
            }
            else
            {
                float yMultiplier = 0;
                if (RectTransform.anchorMin.y < .5f) yMultiplier = -1;
                else if (RectTransform.anchorMin.y > .5f) yMultiplier = 1;
                float yOffset = AnimationProfile.HiddenPositionOffset.y * yMultiplier;
                HiddenOffsetMin.y = RectTransform.offsetMin.y + yOffset;
                HiddenOffsetMax.y = RectTransform.offsetMax.y + yOffset;
            }

            HiddenAngle = DefaultAngle + AnimationProfile.HiddenAngleOffset;
            HiddenScale = new(DefaultScale.x * AnimationProfile.HiddenScale.x, DefaultScale.y * AnimationProfile.HiddenScale.y, 1);
            HiddenAlpha = AnimationProfile.HiddenAlpha;
        }

        public override void HideInstantly()
        {
            base.HideInstantly();

            RectTransform.offsetMin = HiddenOffsetMin;
            RectTransform.offsetMax = HiddenOffsetMax;
            RectTransform.localEulerAngles = new(0, 0, HiddenAngle);
            RectTransform.localScale = HiddenScale;
            CanvasGroup.alpha = HiddenAlpha;
            if (ToggleInteractability)
            {
                CanvasGroup.interactable = false;
                CanvasGroup.blocksRaycasts = false;
            }
        }
        public override void Show()
        {
            if (!CanStart || Visible) return;
            StartStateTransition();
        }
        protected override void OnShowComplete()
        {
            if (ToggleInteractability)
            {
                CanvasGroup.interactable = true;
                CanvasGroup.blocksRaycasts = true;
            }

            base.OnShowComplete();
        }
        public override void Hide()
        {
            if (!CanStart || !Visible) return;

            if (IsDynamic)
            {
                SetDefaultValues();
                CalculateHiddenValues();
            }

            if (ToggleInteractability)
            {
                CanvasGroup.interactable = false;
                CanvasGroup.blocksRaycasts = false;
            }

            StartStateTransition();
        }
        public override void Toggle()
        {
            if (!CanStart) return;
            if (Animating) Visible = !Visible;
            base.Toggle();
        }

        void StartStateTransition()
        {
            if (!gameObject.activeInHierarchy) return;

            StopStateTransition();
            StateTransitionCoroutine = StartCoroutine(DoStateTransition());
        }
        IEnumerator DoStateTransition()
        {
            Animating = true;

            Vector2 targetOffsetMin = Visible ? HiddenOffsetMin : DefaultOffsetMin;
            Vector2 targetOffsetMax = Visible ? HiddenOffsetMax : DefaultOffsetMax;
            float targetAngle = Visible ? HiddenAngle : DefaultAngle;
            Vector3 targetScale = Visible ? HiddenScale : DefaultScale;
            float targetAlpha = Visible ? HiddenAlpha : DefaultAlpha;

            Vector2 startOffsetMin = RectTransform.offsetMin;
            Vector2 startOffsetMax = RectTransform.offsetMax;
            float startAngle = RectTransform.localEulerAngles.z;
            Vector3 startScale = RectTransform.localScale;
            float startAlpha = CanvasGroup.alpha;

            float progress = 0;
            float transitionTime = AnimationProfile.TransitionTime;

            while (progress < transitionTime)
            {
                progress += Time.deltaTime;
                float t = AnimationProfile.TransitionCurve.Evaluate(progress / transitionTime);

                RectTransform.offsetMin = Vector2.Lerp(startOffsetMin, targetOffsetMin, t);
                RectTransform.offsetMax = Vector2.Lerp(startOffsetMax, targetOffsetMax, t);
                RectTransform.localEulerAngles = new(0, 0, Mathf.LerpAngle(startAngle, targetAngle, t));
                RectTransform.localScale = Vector3.Lerp(startScale, targetScale, t);
                CanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);

                yield return null;
            }

            StateTransitionCoroutine = null;
            Animating = false;

            if (Visible) OnHideComplete();
            else OnShowComplete();
        }
        void StopStateTransition()
        {
            if (StateTransitionCoroutine != null)
            {
                StopCoroutine(StateTransitionCoroutine);
                StateTransitionCoroutine = null;
            }
        }
        #endregion

        #region Helpers
        protected override bool CanStart => AnimationProfile != null && (WaitForAnimation == false || Animating == false);
        #endregion

#if UNITY_EDITOR

        #region Function
        protected override void OnValidate()
        {
            base.OnValidate();

            Validate();
        }
        #endregion

#endif
    }
}