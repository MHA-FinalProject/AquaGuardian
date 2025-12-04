using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UIAssistant
{
    [HelpURL("https://sites.google.com/view/uiassistant/documentation/animation?authuser=0#h.cx1n7m614aey")]
    public class AnimationSettings : ScriptableObject
    {
        #region Variables
        /// <summary>
        /// A list of Animation Profiles added to the Animation Settings asset. This is handled by the Animation Settings editor; please do not add or remove any Animation Profiles yourself.
        /// </summary>
        public List<AnimationProfile> AnimationProfiles = new();

        /// <summary>
        /// The number of seconds the Text Revealer waits between revealing two adjacent characters.
        /// </summary>
        public float TextRevealerTimePerCharacter;

        /// <summary>
        /// If set to true, the Text Revealer will skip spaces during its animation.
        /// </summary>
        public bool TextRevealerSkipSpaces;

        /// <summary>
        /// Characters (without spaces) that cause the Text Revealer to pause.
        /// </summary>
        public string TextRevealerPauseCharacters;

        /// <summary>
        /// The number of seconds the Text Revealer waits after detecting a pause character.
        /// </summary>
        public float TextRevealerRegularPauseTime;

        /// <summary>
        /// Characters (without spaces) that cause the Text Revealer to pause when the character repeats.
        /// </summary>
        public string TextRevealerEllipsisCharacters;

        /// <summary>
        /// The number of seconds the Text Revealer waits after detecting an ellipsis character in a sequence. Overrides regular pause time in those cases.
        /// </summary>
        public float TextRevealerEllipsisPauseTime;

        /// <summary>
        /// Returns an array of Animation Profile names listed in the Animation Settings.
        /// </summary>
        public string[] ProfileNames
        {
            get
            {
                List<string> profileNames = new();

                for (int i = 0; i < AnimationProfiles.Count; i++)
                {
                    profileNames.Add(AnimationProfiles[i].Name);
                }

                return profileNames.ToArray();
            }
        }
        #endregion
    }
}