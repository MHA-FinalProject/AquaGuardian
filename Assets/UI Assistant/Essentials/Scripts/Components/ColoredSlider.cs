using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UIAssistant
{
    [HelpURL("https://sites.google.com/view/uiassistant/documentation/color?authuser=0#h.opnv8e9evao9")]
    [AddComponentMenu("UI Assistant/Colored Slider"), RequireComponent(typeof(GraphicColorizerGroup)), DisallowMultipleComponent]
    public class ColoredSlider : Slider, ISelectableScalerController
    {
        #region Variables
        [SerializeField, HideInInspector] GraphicColorizerGroup GraphicColorizerGroup;
        readonly List<SelectableScaler> SelectableScalers = new();
        #endregion

        #region Function
        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);

            UIAssistant.SelectionState convertedState;
            if (state == SelectionState.Disabled && interactable)
                convertedState = UIAssistant.SelectionState.Normal;
            else convertedState = (UIAssistant.SelectionState)state;

            for (int i = 0; i < SelectableScalers.Count; i++)
                SelectableScalers[i].DoStateTransition(convertedState, instant);

            if (GraphicColorizerGroup != null)
                GraphicColorizerGroup.DoStateTransition(convertedState, instant);
        }

        /// <summary>
        /// Registers a Selectable Scaler to be updated on state change. Selectable Scalers automatically add themselves.
        /// </summary>
        public void AddSelectableScaler(SelectableScaler selectableScaler)
        {
            SelectableScalers.Add(selectableScaler);
        }

        /// <summary>
        /// Removes a Selectable Scaler, locking it out of updates on state change. Selectable Scalers automatically remove themselves.
        /// </summary>
        public void RemoveSelectableScaler(SelectableScaler selectableScaler)
        {
            SelectableScalers.Remove(selectableScaler);
        }
        #endregion

#if UNITY_EDITOR

        #region Function
        protected override void OnValidate()
        {
            base.OnValidate();

            if (GraphicColorizerGroup == null) GraphicColorizerGroup = GetComponent<GraphicColorizerGroup>();
        }
        #endregion

#endif
    }
}