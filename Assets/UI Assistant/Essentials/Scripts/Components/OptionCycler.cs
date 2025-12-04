using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using TMPro;

namespace UIAssistant
{
    [HelpURL("https://sites.google.com/view/uiassistant/documentation/options?authuser=0#h.4zfsu81ep0fv")]
    [AddComponentMenu("UI Assistant/Option Cycler"), RequireComponent(typeof(RectTransform)), DisallowMultipleComponent]
    public class OptionCycler : Selectable
    {
        #region Variables
        [Space]
        [Tooltip("When set to true, the Option Cycler will stop at both ends.")] public bool Clamp;
        [SerializeField, Tooltip("The orientation of the buttons responsible for changing options.")] CycleAxis Axis;
        [Tooltip("Text Mesh Pro UGUI that will display the selected option's text.")] public TextMeshProUGUI CaptionText;
        [Tooltip("Image that will display the selected option's image.")] public Image CaptionImage;
        [Space]
        [Tooltip("The area to be clicked to set the previous option.")] public RectTransform PreviousArea;
        [Tooltip("The area to be clicked to set the next option.")] public RectTransform NextArea;
        [Tooltip("The set of options the Option Cycler will display.")] public List<TMP_Dropdown.OptionData> Options
        {
            get => _Options.options;
            set
            {
                _Options.options = value;
                RefreshShownValue();
            }
        }

        [Space]

        [SerializeField] TMP_Dropdown.OptionDataList _Options = new();

        [Space]

        /// <summary>
        /// Unity Events to be invoked when the Option Cycler's Value changes.
        /// </summary>
        public OptionCyclerEvent OnValueChanged;

        /// <summary>
        /// The index of the currently selected option.
        /// </summary>
        public int Value
        {
            get => _Value;
            set
            {
                if (Clamp)
                    value = Mathf.Clamp(value, 0, _Options.options.Count - 1);
                else
                {
                    if (value < 0) value = _Options.options.Count - 1;
                    else if (value >= _Options.options.Count) value = 0;
                }

                if (value != _Value)
                {
                    _Value = value;
                    RefreshShownValue();
                    OnValueChanged.Invoke(value);
                }
            }
        }
        int _Value;
        #endregion

        #region Enums
        protected enum CycleAxis
        {
            Horizontal = 0,
            Vertical = 1,
        }
        #endregion

        #region Classes
        /// <summary>
        /// UnityEvent callback for Value changes.
        /// </summary>
        [System.Serializable]
        public class OptionCyclerEvent : UnityEvent<int> { }
        #endregion

        #region Function
        /// <summary>
        /// Adds a single new option to the end of the list.
        /// </summary>
        /// <param name="option">The TMP Dropdown Option Data to be added.</param>
        public void AddOption(TMP_Dropdown.OptionData option)
        {
            _Options.options.Add(option);
            RefreshShownValue();
        }

        /// <summary>
        /// Adds a list of option to the end of the list.
        /// </summary>
        /// <param name="options">The list of TMP Dropdown Option Data to be added.</param>
        public void AddOptions(List<TMP_Dropdown.OptionData> options)
        {
            _Options.options.AddRange(options);
            RefreshShownValue();
        }

        /// <summary>
        /// Removes all options from the list.
        /// </summary>
        public void ClearOptions()
        {
            _Options.options.Clear();
            RefreshShownValue();
        }

        /// <summary>
        /// Sets the next option. Unless Clamp is set to true, the first option will be set instead of going out of range.
        /// </summary>
        public void NextOption() { Value += 1; }

        /// <summary>
        /// Sets the previous option. Unless Clamp is set to true, the last option will be set instead of going out of range.
        /// </summary>
        public void PreviousOption() { Value -= 1; }

        /// <summary>
        /// Sets a specific option by index without invoking OnValueChanged events.
        /// </summary>
        public void SetValueWithoutNotify(int value)
        {
            _Value = value;
            RefreshShownValue();
        }

        void RefreshShownValue()
        {
            if (Options.Count == 0)
            {
                if (CaptionText) CaptionText.text = "";
                if (CaptionImage) CaptionImage.sprite = null;
                return;
            }
            else
            {
                TMP_Dropdown.OptionData data = Options[Value];

                if (CaptionText) CaptionText.text = data.text;
                if (CaptionImage) CaptionImage.sprite = data.image;
            }
        }
        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);

            if (PreviousAreaEvent(eventData)) PreviousOption();

            if (NextAreaEvent(eventData)) NextOption();
        }
        public override void OnMove(AxisEventData eventData)
        {
            if (!IsActive() || !IsInteractable())
            {
                base.OnMove(eventData);
                return;
            }

            switch (eventData.moveDir)
            {
                case MoveDirection.Left:
                    if (Axis == CycleAxis.Horizontal) PreviousOption();
                    else base.OnMove(eventData);
                    break;
                case MoveDirection.Up:
                    if (Axis == CycleAxis.Vertical) NextOption();
                    else base.OnMove(eventData);
                    break;
                case MoveDirection.Right:
                    if (Axis == CycleAxis.Horizontal) NextOption();
                    else base.OnMove(eventData);
                    break;
                case MoveDirection.Down:
                    if (Axis == CycleAxis.Vertical) PreviousOption();
                    else base.OnMove(eventData);
                    break;
            }
        }
        #endregion

        #region Helpers
        bool PreviousAreaEvent(PointerEventData eventData) => RectTransformUtility.RectangleContainsScreenPoint(PreviousArea, eventData.position);
        bool NextAreaEvent(PointerEventData eventData) => RectTransformUtility.RectangleContainsScreenPoint(NextArea, eventData.position);
        #endregion

#if UNITY_EDITOR

        #region Function
        protected override void OnValidate()
        {
            base.OnValidate();
            RefreshShownValue();
        }
        #endregion

#endif
    }
}