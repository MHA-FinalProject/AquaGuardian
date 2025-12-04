using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UIAssistant
{
    public class UIAssistantComponent : MonoBehaviour
    {
        #region Variables
        [SerializeField, HideInInspector] protected AnimationSettings AnimationSettings;
        [SerializeField, HideInInspector] protected ColorSettings ColorSettings;
        [SerializeField, HideInInspector] protected OptionSettings OptionSettings;
        [SerializeField, HideInInspector] protected ScaleSettings ScaleSettings;
        [SerializeField, HideInInspector] protected TextSettings TextSettings;
        #endregion

#if UNITY_EDITOR

        #region Function
        protected virtual void OnValidate()
        {
            if (AnimationSettings == null) AnimationSettings = ContentLibrary.GetAnimationSettings();
            if (ColorSettings == null) ColorSettings = ContentLibrary.GetColorSettings();
            if (OptionSettings == null) OptionSettings = ContentLibrary.GetOptionSettings();
            if (ScaleSettings == null) ScaleSettings = ContentLibrary.GetScaleSettings();
            if (TextSettings == null) TextSettings = ContentLibrary.GetTextSettings();
        }
        #endregion

#endif
    }
}