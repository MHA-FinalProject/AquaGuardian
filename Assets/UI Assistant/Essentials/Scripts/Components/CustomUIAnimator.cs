using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UIAssistant
{
    [HelpURL("https://sites.google.com/view/uiassistant/documentation/animation?authuser=0#h.jgtiaktb8c9l")]
    [AddComponentMenu("UI Assistant/Custom UI Animator"), RequireComponent(typeof(Animator)), DisallowMultipleComponent]
    public class CustomUIAnimator : VisibilityStateHandler
    {
        #region Variables
        [SerializeField, HideInInspector] Animator Animator;
        [Tooltip("Name of the Animator Controller's parameter used to switch between hidden and visible states.")] public string ParameterName = "Visible";

        Coroutine WaitTimerCoroutine;
        #endregion

        #region Function
        protected override void Start()
        {
            SetParameter(!StartHidden, true);
        }
        public override void HideInstantly()
        {
            SetParameter(false, true);
        }
        public override void Show()
        {
            if (!CanStart || Visible) return;
            SetParameter(true, false);
            StartWaitTimer();
        }
        public override void Hide()
        {
            if (!CanStart || !Visible) return;
            SetParameter(false, false);
            StartWaitTimer();
        }

        void SetParameter(bool value, bool instantly)
        {
            Visible = value;
            Animator.SetBool(ParameterName, Visible);

            if (instantly)
            {
                Animator.Update(0);
                AnimatorStateInfo stateInfo = Animator.GetCurrentAnimatorStateInfo(0);
                Animator.Play(stateInfo.fullPathHash, 0, 1);
            }
        }
        void StartWaitTimer()
        {
            if (!gameObject.activeInHierarchy) return;

            StopWaitTimer();
            WaitTimerCoroutine = StartCoroutine(WaitTimer(Animator.GetCurrentAnimatorStateInfo(0).length));
        }
        IEnumerator WaitTimer(float waitTime)
        {
            while (waitTime > 0)
            {
                waitTime -= Time.deltaTime;
                yield return null;
            }

            if (Visible) OnShowComplete();
            else OnHideComplete();
            WaitTimerCoroutine = null;
        }
        void StopWaitTimer()
        {
            if (WaitTimerCoroutine != null)
            {
                StopCoroutine(WaitTimerCoroutine);
                WaitTimerCoroutine = null;
            }
        }
        #endregion

        #region Helpers
        protected override bool CanStart => !WaitForAnimation || WaitTimerCoroutine == null;
        #endregion

#if UNITY_EDITOR

        #region Function
        protected override void OnValidate()
        {
            base.OnValidate();

            if (Animator == null) Animator = GetComponent<Animator>();
        }
        #endregion

#endif
    }
}