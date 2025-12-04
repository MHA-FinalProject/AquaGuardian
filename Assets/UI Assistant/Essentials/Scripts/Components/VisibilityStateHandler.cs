using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace UIAssistant
{
    public abstract class VisibilityStateHandler : UIAssistantComponent
    {
        #region Variables
        [Tooltip("If set to true, the hidden state will be applied instantly on Start.")] public bool StartHidden;
        [Tooltip("Uncheck this box to allow the Show/Hide methods to override animations in progress.")] public bool WaitForAnimation = true;
        [Tooltip("Unity Events to be invoked at the end of the Show animation.")] public UnityEvent OnShow;
        [Tooltip("Unity Events to be invoked at the end of the Hide animation.")] public UnityEvent OnHide;
        
        protected bool Visible = true;
        #endregion

        #region Function
        protected virtual void Start()
        {
            if (StartHidden) HideInstantly();
        }

        /// <summary>
        /// Applies the hidden state without animation.
        /// </summary>
        public virtual void HideInstantly()
        {
            Visible = false;
        }

        /// <summary>
        /// Animates into the visible state.
        /// </summary>
        public virtual void Show()
        {
            OnShowComplete();
        }
        protected virtual void OnShowComplete()
        {
            Visible = true;
            OnShow.Invoke();
        }

        /// <summary>
        /// Animates into the hidden state.
        /// </summary>
        public virtual void Hide()
        {
            OnHideComplete();
        }
        protected virtual void OnHideComplete()
        {
            Visible = false;
            OnHide.Invoke();
        }

        /// <summary>
        /// Animates between the visible and hidden states, based on current state.
        /// </summary>
        public virtual void Toggle()
        {
            if (Visible) Hide();
            else Show();
        }
        #endregion

        #region Helpers
        protected virtual bool CanStart => true;
        #endregion
    }
}