using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UIAssistant
{
    [HelpURL("https://sites.google.com/view/uiassistant/documentation/options?authuser=0#h.t42gynfyfw9d")]
    public class OptionSettings : ScriptableObject
    {
        #region Variables
        /// <summary>
        /// A list of Option Entries added to the Option Settings asset. This is handled by the Option Settings editor; please do not add or remove any Option Entries yourself.
        /// </summary>
        public List<OptionEntry> OptionEntries = new();

        /// <summary>
        /// When set to true, Option Syncers' label texts will be set based on Option Entries' localized names.
        /// </summary>
        public bool Localized;

        /// <summary>
        /// Returns an array of Option Entry names listed in the Option Settings.
        /// </summary>
        public string[] EntryNames
        {
            get
            {
                List<string> entryNames = new();

                for (int i = 0; i < OptionEntries.Count; i++)
                {
                    entryNames.Add(OptionEntries[i].Name);
                }

                return entryNames.ToArray();
            }
        }
        #endregion
    }

    #region Enums
    public enum OptionType
    {
        Float = 0,
        Bool = 1,
        List = 2,
        ColorProfile = 3,
        ScaleProfile = 4,
        ScaleInterpolation = 5,
        FontSizeInterpolation = 6,
        Language = 7,
    }
    #endregion
}