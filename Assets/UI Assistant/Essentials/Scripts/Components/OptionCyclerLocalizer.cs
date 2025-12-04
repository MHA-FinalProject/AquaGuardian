using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UIAssistant
{
    [HelpURL("https://sites.google.com/view/uiassistant/documentation/text?authuser=0#h.j9t32xv68gy5")]
    [RequireComponent(typeof(OptionCycler)), AddComponentMenu("UI Assistant/Option Cycler Localizer"), DisallowMultipleComponent]
    public class OptionCyclerLocalizer : OptionLocalizer
    {
        #region Variables
        [SerializeField] OptionCycler OptionCycler;
        #endregion

        #region Function
        protected override void SetOptions()
        {
            if (OptionSyncer != null) return;
            OptionCycler.Options = GetOptions();
        }
        protected override void GetSelectable()
        {
            if (OptionCycler == null) OptionCycler = GetComponent<OptionCycler>();
        }
        #endregion

#if UNITY_EDITOR

        #region Function
        protected override void OnValidate()
        {
            base.OnValidate();

            GetSelectable();
            SetOptions();
        }
        #endregion

#endif
    }
}