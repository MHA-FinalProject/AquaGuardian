using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace UIAssistant
{
    [HelpURL("https://sites.google.com/view/uiassistant/documentation/text?authuser=0#h.j9t32xv68gy5")]
    [RequireComponent(typeof(TMP_Dropdown)), AddComponentMenu("UI Assistant/Dropdown Localizer"), DisallowMultipleComponent]
    public class DropdownLocalizer : OptionLocalizer
    {
        #region Variables
        [SerializeField] TMP_Dropdown Dropdown;
        #endregion

        #region Function
        protected override void SetOptions()
        {
            if (OptionSyncer != null) return;
            Dropdown.options = GetOptions();
        }
        protected override void GetSelectable()
        {
            if (Dropdown == null) Dropdown = GetComponent<TMP_Dropdown>();
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