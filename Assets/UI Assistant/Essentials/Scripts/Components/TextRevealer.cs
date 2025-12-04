using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

namespace UIAssistant
{
    [HelpURL("https://sites.google.com/view/uiassistant/documentation/animation?authuser=0#h.1ke6iqjtqkb0")]
    [AddComponentMenu("UI Assistant/Text Revealer"), RequireComponent(typeof(TextMeshProUGUI))]
    public class TextRevealer : UIAssistantComponent, ILocalizable
    {
        #region Variables
        [SerializeField] TextMeshProUGUI Text;
        [Tooltip("If set to true, the Text Mesh Pro UGUI will not display any text on Start.")] public bool StartHidden;
        [Tooltip("Unity Events to be invoked when the Text Revealer animation starts.")] public UnityEvent OnRevealStart;
        [Tooltip("Unity Events to be invoked when the Text Revealer animation ends.")] public UnityEvent OnRevealEnd;

        readonly List<RevealSection> RevealSections = new();
        Coroutine RevealCoroutine;
        int CurrentSectionIndex;
        float CurrentRevealTime;
        float TargetRevealTime;
        #endregion

        #region Classes & Enums
        class RevealSection
        {
            public int StartIndex;
            public int EndIndex;
            public float StartTime;
            public float EndTime;
            public CharType EndCharType;

            public RevealSection(int startIndex, int endIndex, CharType endCharType)
            {
                StartIndex = startIndex;
                EndIndex = endIndex;
                EndCharType = endCharType;
            }
        }
        enum CharType
        {
            Regular = 0,
            Space = 1,
            Pause = 2,
            Ellipsis = 3,
        }
        #endregion

        #region Function
        void Start()
        {
            if (StartHidden) Text.maxVisibleCharacters = 0;
        }

        void OnEnable()
        {
            TextSettings.Add(this);
        }
        void OnDisable()
        {
            StopReveal();
            TextSettings.Remove(this);
        }

        /// <summary>
        /// Stops revealing and displays the Text Mesh Pro UGUI's entire text on the next frame.
        /// </summary>
        public void OnLanguageChanged()
        {
            StartCoroutine(DelayedOnLanguageChanged());
        }
        IEnumerator DelayedOnLanguageChanged()
        {
            if (IsRevealing) SkipToEnd(false);
            yield return null;
            Text.maxVisibleCharacters = Text.textInfo.characterCount;
        }

        /// <summary>
        /// Starts the reveal animation of the Text Mesh Pro UGUI's text.
        /// </summary>
        public void RevealText()
        {
            RevealText(Text.text);
        }

        /// <summary>
        /// Starts the reveal animation of a string of your choice.
        /// </summary>
        /// <param name="stringToReveal">The string (including rich text) that will be displayed by the Text Mesh Pro UGUI.</param>
        public void RevealText(string stringToReveal)
        {
            if (IsRevealing || stringToReveal.Length == 0) return;

            Text.text = stringToReveal.Replace("…", "...");

            CreateRevealSections();

            IsRevealing = true;
            Text.maxVisibleCharacters = 0;

            StartReveal();
        }

        /// <summary>
        /// Stops the text's reveal animation and immediately skips to the end.
        /// </summary>
        /// <param name="invokeOnRevealComplete">When set to true, the OnRevealEnd Unity Events will be invoked.</param>
        public void SkipToEnd(bool invokeOnRevealComplete)
        {
            StopReveal();
            CurrentRevealTime = TargetRevealTime;
            SetTextVisibility();
            if (invokeOnRevealComplete) OnRevealComplete();
        }

        void CreateRevealSections()
        {
            Text.ForceMeshUpdate(true);
            TMP_TextInfo textInfo = Text.textInfo;

            RevealSections.Clear();
            int startIndex = 0;

            for (int i = 0; i < textInfo.characterInfo.Length; i++)
            {
                TMP_CharacterInfo characterInfo = textInfo.characterInfo[i];

                CharType charType = GetCharType(i);

                if (charType == CharType.Space)
                {
                    RevealSections.Add(new(startIndex, i - 1, CharType.Regular));
                    startIndex = i;
                }
                if (charType != CharType.Regular)
                {
                    if (startIndex == i && charType == CharType.Pause && RevealSections[^1].EndCharType == CharType.Pause)
                        RevealSections[^1].EndCharType = CharType.Regular;

                    RevealSections.Add(new(startIndex, i, charType));
                    startIndex = i + 1;
                }
            }

            CharType prevCharType = CharType.Regular;
            float prevEndTime = 0;

            for (int i = 0; i < RevealSections.Count; i++)
            {
                RevealSection section = RevealSections[i];
                int length = section.EndIndex - section.StartIndex + 1;

                float delay = prevCharType switch
                {
                    CharType.Regular => 0,
                    CharType.Space => 0,
                    CharType.Pause => AnimationSettings.TextRevealerRegularPauseTime,
                    CharType.Ellipsis => AnimationSettings.TextRevealerEllipsisPauseTime,
                    _ => 0,
                };

                section.StartTime = prevEndTime + delay;
                section.EndTime = section.EndCharType == CharType.Space ? section.StartTime :
                    section.StartTime + length * AnimationSettings.TextRevealerTimePerCharacter;

                prevCharType = section.EndCharType;
                prevEndTime = section.EndTime;
            }

            TargetRevealTime = RevealSections[^1].EndTime;
        }

        void StartReveal()
        {
            if (!gameObject.activeInHierarchy) return;

            OnRevealStart.Invoke();
            StopReveal();
            RevealCoroutine = StartCoroutine(DoReveal());
        }
        IEnumerator DoReveal()
        {
            CurrentRevealTime = 0;
            CurrentSectionIndex = 0;
            
            while (CurrentRevealTime < TargetRevealTime)
            {
                CurrentRevealTime += Time.deltaTime;
                SetTextVisibility();
                yield return null;
            }

            CurrentRevealTime = TargetRevealTime;
            RevealCoroutine = null;
            OnRevealComplete();
        }
        void StopReveal()
        {
            if (RevealCoroutine != null)
            {
                StopCoroutine(RevealCoroutine);
                RevealCoroutine = null;
                IsRevealing = false;
            }
        }
        void OnRevealComplete()
        {
            IsRevealing = false;
            OnRevealEnd.Invoke();
        }

        void SetTextVisibility()
        {
            if (CurrentRevealTime >= RevealSections[CurrentSectionIndex].EndTime)
            {
                for (int i = RevealSections.Count - 1; i >= CurrentSectionIndex; i--)
                {
                    if (CurrentRevealTime > RevealSections[i].StartTime)
                    {
                        CurrentSectionIndex = i;
                        break;
                    }
                }
            }

            RevealSection currentSection = RevealSections[CurrentSectionIndex];
            float t = Mathf.InverseLerp(currentSection.StartTime, currentSection.EndTime, CurrentRevealTime);
            int index = Mathf.RoundToInt(Mathf.Lerp(currentSection.StartIndex, currentSection.EndIndex, t));

            Text.maxVisibleCharacters = index + 1;
        }
        #endregion

        #region Helpers
        /// <summary>
        /// Returns true if the text is currently animating.
        /// </summary>
        public bool IsRevealing { get; private set; }

        /// <summary>
        /// Returns a value between 0 and 1 based on current and target animation times.
        /// </summary>
        public float RevealProgress => CurrentRevealTime / TargetRevealTime;

        CharType GetCharType(int index)
        {
            char character = Text.textInfo.characterInfo[index].character;

            if (character == ' ' && AnimationSettings.TextRevealerSkipSpaces) return CharType.Space;
            if (IsInSequence(index) && AnimationSettings.TextRevealerEllipsisCharacters.Contains(character)) return CharType.Ellipsis;
            else if (AnimationSettings.TextRevealerPauseCharacters.Contains(character)) return CharType.Pause;
            else return CharType.Regular;
        }
        bool IsInSequence(int index)
        {
            TMP_TextInfo textInfo = Text.textInfo;

            bool sequence = false;
            if (index > 0 && textInfo.characterInfo[index].character == textInfo.characterInfo[index - 1].character) sequence = true;
            else if (index < textInfo.characterInfo.Length - 1 && textInfo.characterInfo[index].character == textInfo.characterInfo[index + 1].character) sequence = true;

            return sequence;
        }
        #endregion

#if UNITY_EDITOR

        #region Function
        protected override void OnValidate()
        {
            base.OnValidate();

            if (Text == null) Text = GetComponent<TextMeshProUGUI>();
        }
        #endregion

#endif
    }
}