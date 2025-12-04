using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UIAssistant
{
    [HelpURL("https://sites.google.com/view/uiassistant/documentation/options?authuser=0#h.11kx1p5xlf6t")]
    public class OptionModifier : MonoBehaviour
    {
        #region Variables
        [Tooltip("The Option Entry whose value will be modified.")] public OptionEntry OptionEntry;
        #endregion

        #region Function
        /// <summary>
        /// Sets the Option Entry's float value.
        /// </summary>
        public void SetFloatValue(float value) { if (OptionEntry) OptionEntry.FloatValue = value; }

        /// <summary>
        /// Sets the Option Entry's bool value.
        /// </summary>
        public void SetBoolValue(bool value) { if (OptionEntry) OptionEntry.BoolValue = value; }

        /// <summary>
        /// Sets the Option Entry's int value.
        /// </summary>
        public void SetIntValue(int value) { if (OptionEntry) OptionEntry.IntValue = value; }
        #endregion
    }
}