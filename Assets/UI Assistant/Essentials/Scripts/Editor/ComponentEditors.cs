using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Animations;

namespace UIAssistant
{
    public class ComponentEditor : Editor
    {
        #region Function
        void OnEnable()
        {
            FindProperties();
        }
        protected virtual void FindProperties() { }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();

            EditorContent();

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(target);
                serializedObject.ApplyModifiedProperties();
            }

            SettingsButton();
        }
        protected virtual void EditorContent() { }
        protected virtual void SettingsButton() { }

        protected void ColorSettingsButton()
        {
            EditorGUILayout.Space();

            if (GUILayout.Button("Open Color Settings")) ColorSettingsWindow.OpenWindow();
        }
        protected void OptionSettingsButton()
        {
            EditorGUILayout.Space();

            if (GUILayout.Button("Open Option Settings")) OptionSettingsWindow.OpenWindow();
        }
        protected void ScaleSettingsButton()
        {
            EditorGUILayout.Space();

            if (GUILayout.Button("Open Scale Settings")) ScaleSettingsWindow.OpenWindow();
        }
        protected void TextSettingsButton()
        {
            EditorGUILayout.Space();

            if (GUILayout.Button("Open Text Settings")) TextSettingsWindow.OpenWindow();
        }
        protected void AnimationSettingsButton()
        {
            EditorGUILayout.Space();

            if (GUILayout.Button("Open Animation Settings")) AnimationSettingsWindow.OpenWindow();
        }
        #endregion
    }

    [CustomEditor(typeof(SelectableScaler)), CanEditMultipleObjects]
    public class SelectableScalerEditor : ComponentEditor
    {
        #region Variables
        SerializedProperty Selectable;
        SerializedProperty NormalScale;
        SerializedProperty HighlightedScale;
        SerializedProperty PressedScale;
        SerializedProperty SelectedScale;
        SerializedProperty DisabledScale;
        SerializedProperty TransitionTime;
        SerializedProperty TransitionCurve;
        #endregion

        #region Function
        protected override void FindProperties()
        {
            Selectable = serializedObject.FindProperty("Selectable");
            NormalScale = serializedObject.FindProperty("NormalScale");
            HighlightedScale = serializedObject.FindProperty("HighlightedScale");
            PressedScale = serializedObject.FindProperty("PressedScale");
            SelectedScale = serializedObject.FindProperty("SelectedScale");
            DisabledScale = serializedObject.FindProperty("DisabledScale");
            TransitionTime = serializedObject.FindProperty("TransitionTime");
            TransitionCurve = serializedObject.FindProperty("TransitionCurve");
        }
        protected override void EditorContent()
        {
            EditorGUILayout.PropertyField(Selectable);

            if (Selectable.objectReferenceValue != null)
            {
                if (Selectable.objectReferenceValue is not ISelectableScalerController)
                    EditorGUILayout.HelpBox("Can only be used with a Colored Selectable.", MessageType.Error);
            }

            EditorGUILayout.Space();

            GUILayout.Label("Scaling", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(NormalScale);
            EditorGUILayout.PropertyField(HighlightedScale);
            EditorGUILayout.PropertyField(PressedScale);
            EditorGUILayout.PropertyField(SelectedScale);
            EditorGUILayout.PropertyField(DisabledScale);

            EditorGUILayout.Space();

            EditorGUI.indentLevel--;
            GUILayout.Label("Animation", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(TransitionTime);
            EditorGUILayout.PropertyField(TransitionCurve);
        }
        #endregion
    }

    [CustomEditor(typeof(GraphicColorizer)), CanEditMultipleObjects]
    public class GraphicColorizerEditor : ComponentEditor
    {
        #region Variables
        SerializedProperty Category;
        SerializedProperty AlphaMultiplier;
        #endregion

        #region Function
        protected override void FindProperties()
        {
            Category = serializedObject.FindProperty("Category");
            AlphaMultiplier = serializedObject.FindProperty("AlphaMultiplier");
        }
        protected override void EditorContent()
        {
            EditorGUILayout.PropertyField(Category);
            EditorGUILayout.PropertyField(AlphaMultiplier);
        }
        protected override void SettingsButton()
        {
            ColorSettingsButton();
        }
        #endregion
    }

    [CustomEditor(typeof(GraphicColorizerGroup)), CanEditMultipleObjects()]
    public class GraphicColorizerGroupEditor : ComponentEditor
    {
        #region Variables
        readonly GUIContent SetTargetGraphicsContent = new("Set Target Graphics", "Adds all Graphic Colorizer children.");
        GraphicColorizerGroup GraphicColorizerGroup;
        Selectable Selectable;
        SerializedProperty ForceNormal;
        SerializedProperty TargetGraphics;
        #endregion

        #region Function
        protected override void FindProperties()
        {
            GraphicColorizerGroup = target as GraphicColorizerGroup;

            ForceNormal = serializedObject.FindProperty("ForceNormal");
            TargetGraphics = serializedObject.FindProperty("TargetGraphics");
        }
        protected override void EditorContent()
        {
            if (Selectable == null) Selectable = GraphicColorizerGroup.GetComponent<Selectable>();

            EditorGUILayout.PropertyField(ForceNormal);
            EditorGUILayout.PropertyField(TargetGraphics);

            if (GUILayout.Button(SetTargetGraphicsContent)) SetTargetGraphics();
        }
        protected override void SettingsButton()
        {
            ColorSettingsButton();
        }
        void SetTargetGraphics()
        {
            List<GraphicColorizer> coloredGraphics = new(GraphicColorizerGroup.GetComponentsInChildren<GraphicColorizer>());

            for (int i = GraphicColorizerGroup.TargetGraphics.Count - 1; i >= 0; i--)
            {
                if (GraphicColorizerGroup.TargetGraphics[i] == null)
                {
                    GraphicColorizerGroup.TargetGraphics.RemoveAt(i);
                }
            }

            for (int i = 0; i < coloredGraphics.Count; i++)
            {
                if (!GraphicColorizerGroup.TargetGraphics.Contains(coloredGraphics[i]))
                {
                    GraphicColorizerGroup.TargetGraphics.Add(coloredGraphics[i]);
                }
            }
        }
        #endregion
    }

    [CustomEditor(typeof(OptionSyncer)), CanEditMultipleObjects]
    public class OptionSyncerEditor : ComponentEditor
    {
        #region Variables
        OptionSyncer OptionSyncer;
        SerializedProperty OptionEntry;
        SerializedProperty LabelText;
        SerializedProperty ValueText;
        SerializedProperty CustomSelectable;
        SerializedProperty OnSync;
        SerializedProperty Dropdown;
        SerializedProperty OptyionCycler;
        #endregion

        #region Function
        protected override void FindProperties()
        {
            OptionSyncer = target as OptionSyncer;
            OptionEntry = serializedObject.FindProperty("OptionEntry");
            LabelText = serializedObject.FindProperty("LabelText");
            ValueText = serializedObject.FindProperty("ValueText");
            CustomSelectable = serializedObject.FindProperty("CustomSelectable");
            OnSync = serializedObject.FindProperty("OnSync");
            Dropdown = serializedObject.FindProperty("Dropdown");
            OptyionCycler = serializedObject.FindProperty("OptionCycler");
        }
        protected override void EditorContent()
        {
            EditorGUILayout.PropertyField(OptionEntry);
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(LabelText);

            if (OptionSyncer.OptionEntry != null)
            {
                OptionType optionType = OptionSyncer.OptionEntry.Type;
                if (optionType == OptionType.Float || optionType == OptionType.FontSizeInterpolation || optionType == OptionType.ScaleInterpolation)
                {
                    EditorGUILayout.PropertyField(ValueText);
                }
            }

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(CustomSelectable);

            if (OptionSyncer.CustomSelectable) EditorGUILayout.PropertyField(OnSync);
        }
        protected override void SettingsButton()
        {
            OptionSettingsButton();
        }
        #endregion
    }

    [CustomEditor(typeof(RectTransformScaler)), CanEditMultipleObjects]
    public class RectTransformScalerEditor : ComponentEditor
    {
        #region Variables
        readonly GUIContent ScalingModeContent = new("Scaling Mode", "Property whose selected axes will be multiplied based on the active Scale Profile.");
        readonly GUIContent RetainBoundsContent = new("Retain Bounds", "Keeps the Rect Transform's edges in their original position.");
     
        SerializedProperty Mode;
        SerializedProperty ScaleX;
        SerializedProperty ScaleY;
        SerializedProperty CounterX;
        SerializedProperty CounterY;
        #endregion

        #region Function
        protected override void FindProperties()
        {
            Mode = serializedObject.FindProperty("Mode");
            ScaleX = serializedObject.FindProperty("ScaleX");
            ScaleY = serializedObject.FindProperty("ScaleY");
            CounterX = serializedObject.FindProperty("CounterX");
            CounterY = serializedObject.FindProperty("CounterY");
        }
        protected override void EditorContent()
        {
            EditorGUILayout.PropertyField(Mode, ScalingModeContent);

            Rect rect = EditorGUILayout.GetControlRect();
            rect = EditorGUI.PrefixLabel(rect, -1, EditorGUIUtility.TrTextContent(ControlScaleText(Mode.intValue), "Determines which axes will be affected."));
            rect.width = Mathf.Max(50, (rect.width - 4) / 3);
            EditorGUIUtility.labelWidth = 50;
            ToggleLeft(rect, ScaleX, EditorGUIUtility.TrTextContent(ScaleXLabel(Mode.intValue)));
            rect.x += rect.width + 2;
            ToggleLeft(rect, ScaleY, EditorGUIUtility.TrTextContent(ScaleYLabel(Mode.intValue)));
            EditorGUIUtility.labelWidth = 0;

            if (Mode.enumValueIndex == 0)
            {
                rect = EditorGUILayout.GetControlRect();
                rect = EditorGUI.PrefixLabel(rect, -1, RetainBoundsContent);
                rect.width = Mathf.Max(50, (rect.width - 4) / 3);
                EditorGUIUtility.labelWidth = 50;
                ToggleLeft(rect, CounterX, EditorGUIUtility.TrTextContent(ScaleXLabel(Mode.intValue)));
                rect.x += rect.width + 2;
                ToggleLeft(rect, CounterY, EditorGUIUtility.TrTextContent(ScaleYLabel(Mode.intValue)));
                EditorGUIUtility.labelWidth = 0;
            }
        }
        protected override void SettingsButton()
        {
            ScaleSettingsButton();
        }
        void ToggleLeft(Rect position, SerializedProperty property, GUIContent label)
        {
            bool toggle = property.boolValue;
            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();
            int oldIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            _ = EditorGUI.ToggleLeft(position, label, toggle);
            EditorGUI.indentLevel = oldIndent;
            if (EditorGUI.EndChangeCheck())
            {
                property.boolValue = property.hasMultipleDifferentValues || !property.boolValue;
            }
            EditorGUI.EndProperty();
        }
        #endregion

        #region Helpers
        string ControlScaleText(int mode) => mode == 0 ? "Control Scale" : "Control Size";
        string ScaleXLabel(int mode) => new(mode == 0 ? "X" : "Width");
        string ScaleYLabel(int mode) => new(mode == 0 ? "Y" : "Height");
        #endregion
    }

    [CustomEditor(typeof(SlicedImageScaler)), CanEditMultipleObjects]
    public class SlicedImageScalerEditor : ComponentEditor
    {
        #region Variables
        Image Image;
        #endregion

        #region Function
        protected override void FindProperties()
        {
            Image = serializedObject.FindProperty("Image").objectReferenceValue as Image;
        }
        protected override void EditorContent()
        {
            bool scalingEnabled = Image.type == Image.Type.Sliced;
            EditorGUILayout.HelpBox(ScalingHelpMessage(scalingEnabled), scalingEnabled ? MessageType.None : MessageType.Error);
        }
        protected override void SettingsButton()
        {
            ScaleSettingsButton();
        }
        #endregion

        #region Helpers
        string ScalingHelpMessage(bool enabled) => enabled ? "Scaling enabled." : "Scaling disabled!\nImage Type must be set to 'Sliced' to enable scaling.";
        #endregion
    }

    [CustomEditor(typeof(StretchingGridLayoutGroup)), CanEditMultipleObjects]
    public class StretchingGridLayoutGroupEditor: ComponentEditor
    {
        #region Variables
        readonly GUIContent ConstraintContent = new("Constraint");
        readonly GUIContent ConstraintCountContent = new("Constraint Count");
        readonly GUIContent CellCountContent = new("Constraint Count", "The number of cells.");
        readonly GUIContent ColumnCountContent = new("Constraint Count", "The number of columnts.");
        readonly GUIContent RowCountContent = new("Constraint Count", "The number of rows.");
        readonly GUIContent MatchContent = new("Match", "Determines if the scaling is using the width or height as reference, or a mix in between.");
        readonly GUIContent WidthContent = new("Width");
        readonly GUIContent HeightContent = new("Height");
        readonly GUIContent PreferredCellWidthContent = new("Preferred Column Width", "The preferred width of each column the layout will try to match.");
        readonly GUIContent PreferredCellHeigthContent = new("Preferred Row Height", "The preferred height of each row the layout will try to match.");

        SerializedProperty Padding;
        SerializedProperty CellSize;
        SerializedProperty PreferredCellSize;
        SerializedProperty Spacing;
        SerializedProperty StartCorner;
        SerializedProperty StartAxis;
        SerializedProperty ChildAlignment;

        SerializedProperty EnableScaling;
        SerializedProperty StretchCells;
        SerializedProperty Constraint;
        SerializedProperty StretchingConstraint;
        SerializedProperty ConstraintCount;
        SerializedProperty ConstraintCountVector2;
        SerializedProperty MatchWidthOrHeight;
        #endregion

        #region Function
        protected override void FindProperties()
        {
            Padding = serializedObject.FindProperty("m_Padding");
            CellSize = serializedObject.FindProperty("m_CellSize");
            PreferredCellSize = serializedObject.FindProperty("PreferredCellSize");
            Spacing = serializedObject.FindProperty("m_Spacing");
            StartCorner = serializedObject.FindProperty("m_StartCorner");
            StartAxis = serializedObject.FindProperty("m_StartAxis");
            ChildAlignment = serializedObject.FindProperty("m_ChildAlignment");

            EnableScaling = serializedObject.FindProperty("EnableScaling");
            StretchCells = serializedObject.FindProperty("StretchCells");
            Constraint = serializedObject.FindProperty("m_Constraint");
            StretchingConstraint = serializedObject.FindProperty("StretchingConstraint");
            ConstraintCount = serializedObject.FindProperty("m_ConstraintCount");
            ConstraintCountVector2 = serializedObject.FindProperty("ConstraintCountVector2");
            MatchWidthOrHeight = serializedObject.FindProperty("MatchWidthOrHeight");
        }
        protected override void EditorContent()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(EnableScaling);

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(Padding, true);
            EditorGUILayout.PropertyField(Spacing, true);

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(StartCorner, true);
            EditorGUILayout.PropertyField(StartAxis, true);
            EditorGUILayout.PropertyField(ChildAlignment, true);

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(StretchCells);

            if (StretchCells.boolValue)
            {
                Constraint.enumValueIndex = 0;

                EditorGUILayout.PropertyField(StretchingConstraint, ConstraintContent, true);

                EditorGUI.indentLevel++;

                if (StretchingConstraint.enumValueIndex > 1 && StretchingConstraint.enumValueIndex < 5)
                {
                    if (StretchingConstraint.enumValueIndex == 2) EditorGUILayout.PropertyField(ConstraintCount, CellCountContent, true);
                    else if (StretchingConstraint.enumValueIndex == 3) EditorGUILayout.PropertyField(ConstraintCount, ColumnCountContent, true);
                    else if (StretchingConstraint.enumValueIndex == 4) EditorGUILayout.PropertyField(ConstraintCount, RowCountContent, true);
                }
                else if (StretchingConstraint.enumValueIndex == 5)
                {
                    EditorGUILayout.PropertyField(ConstraintCountVector2, ConstraintCountContent, true);
                }

                if (StretchingConstraint.enumValueIndex > 0 && StretchingConstraint.enumValueIndex < 3)
                {
                    Rect rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight + 12);
                    DualLabeledSlider(rect, MatchWidthOrHeight, MatchContent, WidthContent, HeightContent);
                }
                else if (StretchingConstraint.enumValueIndex == 0)
                {
                    if (StretchingConstraint.enumValueIndex < 3) EditorGUILayout.PropertyField(PreferredCellSize);
                }
                else if (StretchingConstraint.enumValueIndex == 3)
                {
                    float height = EditorGUILayout.FloatField(PreferredCellHeigthContent, PreferredCellSize.vector2Value.y);
                    PreferredCellSize.vector2Value = new(PreferredCellSize.vector2Value.x, height);
                }
                else if (StretchingConstraint.enumValueIndex == 4)
                {
                    float width = EditorGUILayout.FloatField(PreferredCellWidthContent, PreferredCellSize.vector2Value.x);
                    PreferredCellSize.vector2Value = new(width, PreferredCellSize.vector2Value.y);
                }

                EditorGUI.indentLevel--;

                EditorGUILayout.Space();

                GUI.enabled = false;
                EditorGUILayout.PropertyField(CellSize, true);
                GUI.enabled = true;
            }
            else
            {
                EditorGUILayout.PropertyField(Constraint, ConstraintContent, true);

                if (Constraint.enumValueIndex > 0)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(ConstraintCount, true);
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.Space();

                EditorGUILayout.PropertyField(CellSize, true);
            }

            serializedObject.ApplyModifiedProperties();
        }
        void DualLabeledSlider(Rect position, SerializedProperty property, GUIContent mainLabel, GUIContent labelLeft, GUIContent labelRight)
        {
            GUIStyle rightLabelStyle = new(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleRight
            };
            
            position.height = EditorGUIUtility.singleLineHeight;
            Rect rect = position;

            position.y += 12;
            position.xMin += EditorGUIUtility.labelWidth;
            position.xMax -= EditorGUIUtility.fieldWidth;

            GUI.Label(position, labelLeft);
            GUI.Label(position, labelRight, rightLabelStyle);

            EditorGUI.PropertyField(rect, property, mainLabel);
        }
        #endregion
    }

    [CustomEditor(typeof(TextStyler)), CanEditMultipleObjects]
    public class TextStylerEditor : ComponentEditor
    {
        #region Variables
        TextSettings TextSettings;

        SerializedProperty Style;
        SerializedProperty Scaling;
        SerializedProperty CustomSize;
        SerializedProperty Localization;
        SerializedProperty LocalizationSet;
        SerializedProperty Language;
        #endregion

        #region Function
        protected override void FindProperties()
        {
            TextSettings = ContentLibrary.GetTextSettings();

            Style = serializedObject.FindProperty("Style");
            Scaling = serializedObject.FindProperty("Scaling");
            CustomSize = serializedObject.FindProperty("CustomSize");
            Localization = serializedObject.FindProperty("Localization");
            LocalizationSet = serializedObject.FindProperty("LocalizationSet");
            Language = serializedObject.FindProperty("Language");
        }
        protected override void EditorContent()
        {
            EditorGUILayout.PropertyField(Style);

            EditorGUILayout.PropertyField(Scaling);

            EditorGUILayout.PropertyField(CustomSize);

            EditorGUILayout.PropertyField(Localization);

            if (Localization.enumValueIndex == 1)
            {
                if (TextSettings.LocalizationSets.Count == 0)
                {
                    EditorGUILayout.HelpBox("There are no Localization Sets to choose from.\nOpen Tools/UI Assistant/Text Settings to create one!", MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.PropertyField(LocalizationSet);
                }
            }
            else if (Localization.enumValueIndex == 2)
            {
                EditorGUILayout.PropertyField(Language);
            }
        }
        protected override void SettingsButton()
        {
            TextSettingsButton();
        }
        #endregion
    }

    [CustomEditor(typeof(DropdownLocalizer)), CanEditMultipleObjects]
    public class DropdownLocalizerEditor : ComponentEditor
    {
        #region Variables
        SerializedProperty Options;
        SerializedProperty OptionSyncer;
        #endregion

        #region Function
        protected override void FindProperties()
        {
            Options = serializedObject.FindProperty("_Options");
            OptionSyncer = serializedObject.FindProperty("OptionSyncer");
        }
        protected override void EditorContent()
        {
            if (OptionSyncer.objectReferenceValue == null) EditorGUILayout.PropertyField(Options);
            else EditorGUILayout.HelpBox("Options set by the Option Syncer.", MessageType.Info, true);
        }
        protected override void SettingsButton()
        {
            TextSettingsButton();
        }
        #endregion
    }

    [CustomEditor(typeof(OptionCyclerLocalizer)), CanEditMultipleObjects]
    public class OptionCyclerLocalizerEditor : ComponentEditor
    {
        #region Variables
        SerializedProperty Options;
        SerializedProperty OptionSyncer;
        #endregion

        #region Function
        protected override void FindProperties()
        {
            Options = serializedObject.FindProperty("_Options");
            OptionSyncer = serializedObject.FindProperty("OptionSyncer");
        }
        protected override void EditorContent()
        {
            if (OptionSyncer.objectReferenceValue == null) EditorGUILayout.PropertyField(Options);
            else EditorGUILayout.HelpBox("Options set by the Option Syncer.", MessageType.Info, true);
        }
        protected override void SettingsButton()
        {
            TextSettingsButton();
        }
        #endregion
    }

    [CustomEditor(typeof(OptionsBuilder)), CanEditMultipleObjects]
    public class OptionsBuilderEditor : ComponentEditor
    {
        #region Variables
        SerializedProperty OpenOnStart;
        SerializedProperty SubmenuTemplate;
        SerializedProperty HeaderText;
        SerializedProperty Spacer;
        SerializedProperty ButtonTemplate;
        SerializedProperty ButtonLabelText;
        SerializedProperty SubmenusAnimate;
        SerializedProperty AnimationProfile;
        SerializedProperty Submenus;
        SerializedProperty Localized;
        SerializedProperty MainHeader;
        SerializedProperty BackLabel;
        SerializedProperty DefaultsLabel;
        SerializedProperty OnOpen;
        SerializedProperty OnClose;
        #endregion

        #region Function
        protected override void FindProperties()
        {
            OpenOnStart = serializedObject.FindProperty("OpenOnStart");
            SubmenuTemplate = serializedObject.FindProperty("SubmenuTemplate");
            HeaderText = serializedObject.FindProperty("HeaderText");
            Spacer = serializedObject.FindProperty("Spacer");
            ButtonTemplate = serializedObject.FindProperty("ButtonTemplate");
            ButtonLabelText = serializedObject.FindProperty("ButtonLabelText");
            SubmenusAnimate = serializedObject.FindProperty("SubmenusAnimate");
            AnimationProfile = serializedObject.FindProperty("AnimationProfile");
            Submenus = serializedObject.FindProperty("Submenus");
            Localized = serializedObject.FindProperty("Localized");
            MainHeader = serializedObject.FindProperty("MainHeader");
            BackLabel = serializedObject.FindProperty("BackLabel");
            DefaultsLabel = serializedObject.FindProperty("DefaultsLabel");
            OnOpen = serializedObject.FindProperty("OnOpen");
            OnClose = serializedObject.FindProperty("OnClose");
        }
        protected override void EditorContent()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.PropertyField(OpenOnStart);

                EditorGUILayout.Space();

                EditorGUILayout.PropertyField(SubmenuTemplate);
                EditorGUILayout.PropertyField(HeaderText);
                EditorGUILayout.PropertyField(Spacer);
                EditorGUILayout.PropertyField(ButtonTemplate);
                EditorGUILayout.PropertyField(ButtonLabelText);

                EditorGUILayout.Space();

                EditorGUILayout.PropertyField(SubmenusAnimate);
                if (SubmenusAnimate.boolValue) EditorGUILayout.PropertyField(AnimationProfile);
                EditorGUILayout.PropertyField(Submenus);

                EditorGUILayout.Space();

                EditorGUILayout.PropertyField(Localized);
                EditorGUILayout.PropertyField(MainHeader);
                EditorGUILayout.PropertyField(BackLabel);
                EditorGUILayout.PropertyField(DefaultsLabel);

                EditorGUILayout.Space();
            }

            EditorGUILayout.PropertyField(OnOpen);
            EditorGUILayout.PropertyField(OnClose);
        }
        protected override void SettingsButton()
        {
            OptionSettingsButton();
        }
        #endregion
    }

    [CustomEditor(typeof(UIAnimator)), CanEditMultipleObjects]
    public class UIAnimatorEditor : ComponentEditor
    {
        #region Variables
        SerializedProperty AnimationProfile;
        SerializedProperty StartHidden;
        SerializedProperty WaitForAnimation;
        SerializedProperty IsDynamic;
        SerializedProperty OnShow;
        SerializedProperty OnHide;
        #endregion

        #region Function
        protected override void FindProperties()
        {
            AnimationProfile = serializedObject.FindProperty("AnimationProfile");
            StartHidden = serializedObject.FindProperty("StartHidden");
            WaitForAnimation = serializedObject.FindProperty("WaitForAnimation");
            IsDynamic = serializedObject.FindProperty("IsDynamic");
            OnShow = serializedObject.FindProperty("OnShow");
            OnHide = serializedObject.FindProperty("OnHide");
        }
        protected override void EditorContent()
        {
            if (AnimationProfile.objectReferenceValue != null)
            {
                EditorGUILayout.PropertyField(AnimationProfile);

                EditorGUILayout.PropertyField(StartHidden);

                EditorGUILayout.PropertyField(WaitForAnimation);

                EditorGUILayout.PropertyField(IsDynamic);

                if (!WaitForAnimation.boolValue && IsDynamic.boolValue)
                    EditorGUILayout.HelpBox("It is recommended to always wait for dynamic UI Animators' animations to finish.", MessageType.Warning);

                EditorGUILayout.Space();

                EditorGUILayout.PropertyField(OnShow);

                EditorGUILayout.PropertyField(OnHide);
            }
            else EditorGUILayout.HelpBox("An Animation Profile is required to use this component. You can create Animation Profiles under Tools/UI Assistant/Animation Settings.", MessageType.Error, true);
        }
        protected override void SettingsButton()
        {
            AnimationSettingsButton();
        }
        #endregion
    }

    [CustomEditor(typeof(CustomUIAnimator)), CanEditMultipleObjects]
    public class CustomUIAnimatorEditor : ComponentEditor
    {
        #region Variables
        SerializedProperty ParameterName;
        SerializedProperty StartHidden;
        SerializedProperty WaitForAnimation;
        SerializedProperty OnShow;
        SerializedProperty OnHide;
        Animator Animator;
        #endregion

        #region Function
        protected override void FindProperties()
        {
            ParameterName = serializedObject.FindProperty("ParameterName");
            StartHidden = serializedObject.FindProperty("StartHidden");
            WaitForAnimation = serializedObject.FindProperty("WaitForAnimation");
            OnShow = serializedObject.FindProperty("OnShow");
            OnHide = serializedObject.FindProperty("OnHide");
            Animator = serializedObject.FindProperty("Animator").objectReferenceValue as Animator;
        }
        protected override void EditorContent()
        {
            EditorGUILayout.PropertyField(StartHidden);
            EditorGUILayout.PropertyField(WaitForAnimation);
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(ParameterName);

            if (Animator != null && Animator.runtimeAnimatorController == null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(EditorGUIUtility.labelWidth);
                if (GUILayout.Button("Auto Generate Animation"))
                {
                    string directory = "Assets";
                    for (int i = 0; i < Selection.assetGUIDs.Length; i++)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(Selection.assetGUIDs[i]);
                        if (AssetDatabase.IsValidFolder(path))
                        {
                            directory = path;
                            break;
                        }
                    }
                    string selectedPath = EditorUtility.SaveFilePanel("Select Save Location", directory, serializedObject.targetObject.name, "controller");
                    if (!string.IsNullOrEmpty(selectedPath)) CreateAnimatorController(FileUtil.GetProjectRelativePath(selectedPath));
                }
                else GUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(OnShow);
            EditorGUILayout.PropertyField(OnHide);
        }
        void CreateAnimatorController(string path)
        {
            float transitionDuration = .15f;
            string visibleParameter = string.IsNullOrEmpty(ParameterName.stringValue) ? "Visible" : ParameterName.stringValue;

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);

            controller.AddParameter(visibleParameter, AnimatorControllerParameterType.Bool);

            AnimatorStateMachine rootStateMachine = controller.layers[0].stateMachine;
            AnimatorState nullState = rootStateMachine.AddState("Null");
            AnimatorState showState = rootStateMachine.AddState("Show");
            AnimatorState hideState = rootStateMachine.AddState("Hide");
            rootStateMachine.defaultState = nullState;

            AnimatorStateTransition nullToShow = nullState.AddTransition(showState);
            nullToShow.hasExitTime = false;
            nullToShow.duration = 0;
            nullToShow.AddCondition(AnimatorConditionMode.If, 0, visibleParameter);

            AnimatorStateTransition nullToHide = nullState.AddTransition(hideState);
            nullToHide.hasExitTime = false;
            nullToHide.duration = 0;
            nullToHide.AddCondition(AnimatorConditionMode.IfNot, 0, visibleParameter);

            AnimatorStateTransition showToHide = showState.AddTransition(hideState);
            showToHide.hasExitTime = false;
            showToHide.duration = transitionDuration;
            showToHide.AddCondition(AnimatorConditionMode.IfNot, 0, visibleParameter);

            AnimatorStateTransition hideToShow = hideState.AddTransition(showState);
            hideToShow.hasExitTime = false;
            hideToShow.duration = transitionDuration;
            hideToShow.AddCondition(AnimatorConditionMode.If, 0, visibleParameter);

            AnimationClip showClip = new();
            showClip.name = "Show";
            AssetDatabase.AddObjectToAsset(showClip, controller);
            showState.motion = showClip;

            AnimationClip hideClip = new();
            hideClip.name = "Hide";
            AssetDatabase.AddObjectToAsset(hideClip, controller);
            hideState.motion = hideClip;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (Animator != null) Animator.runtimeAnimatorController = controller;
        }
        #endregion
    }

    [CustomEditor(typeof(TextRevealer)), CanEditMultipleObjects]
    public class TextRevealerEditor : ComponentEditor
    {
        #region Variables
        SerializedProperty StartHidden;
        SerializedProperty OnRevealStart;
        SerializedProperty OnRevealEnd;
        #endregion

        #region Function
        protected override void FindProperties()
        {
            StartHidden = serializedObject.FindProperty("StartHidden");
            OnRevealStart = serializedObject.FindProperty("OnRevealStart");
            OnRevealEnd = serializedObject.FindProperty("OnRevealEnd");
        }
        protected override void EditorContent()
        {
            EditorGUILayout.PropertyField(StartHidden);

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(OnRevealStart);

            EditorGUILayout.PropertyField(OnRevealEnd);
        }
        protected override void SettingsButton()
        {
            AnimationSettingsButton();
        }
        #endregion
    }

    [CustomEditor(typeof(CreditsBuilder)), CanEditMultipleObjects]
    public class CreditsBuilderEditor : ComponentEditor
    {
        #region Variables
        SerializedProperty PlayOnStart;
        SerializedProperty Localized;
        SerializedProperty Speed;
        SerializedProperty Elements;
        SerializedProperty OnCreditsStart;
        SerializedProperty OnCreditsEnd;
        SerializedProperty ContentTransform;
        SerializedProperty SingleTemplate;
        SerializedProperty SingleText;
        SerializedProperty VerticalTemplate;
        SerializedProperty VerticalMainText;
        SerializedProperty VerticalListText;
        SerializedProperty HorizontalTemplate;
        SerializedProperty HorizontalMainText;
        SerializedProperty HorizontalListText;

        static bool EditReferences;
        #endregion

        #region Function
        protected override void FindProperties()
        {
            PlayOnStart = serializedObject.FindProperty("PlayOnStart");
            Localized = serializedObject.FindProperty("Localized");
            Speed = serializedObject.FindProperty("Speed");
            Elements = serializedObject.FindProperty("Elements");
            OnCreditsStart = serializedObject.FindProperty("OnCreditsStart");
            OnCreditsEnd = serializedObject.FindProperty("OnCreditsEnd");
            ContentTransform = serializedObject.FindProperty("ContentTransform");
            SingleTemplate = serializedObject.FindProperty("SingleTemplate");
            SingleText = serializedObject.FindProperty("SingleText");
            VerticalTemplate = serializedObject.FindProperty("VerticalTemplate");
            VerticalMainText = serializedObject.FindProperty("VerticalMainText");
            VerticalListText = serializedObject.FindProperty("VerticalListText");
            HorizontalTemplate = serializedObject.FindProperty("HorizontalTemplate");
            HorizontalMainText = serializedObject.FindProperty("HorizontalMainText");
            HorizontalListText = serializedObject.FindProperty("HorizontalListText");
        }
        protected override void EditorContent()
        {
            EditorGUILayout.PropertyField(PlayOnStart);
            EditorGUILayout.PropertyField(Speed);

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.PropertyField(Localized);
                EditorGUILayout.PropertyField(Elements);
               
                EditorGUILayout.Space();

                Rect toggleRect = EditorGUILayout.GetControlRect();
                toggleRect.xMin += EditorGUIUtility.labelWidth;
                GUIContent editReferences = new("Edit References", "Show fields containing all basic references used by the Credits Builder.");
                EditReferences = GUI.Toggle(toggleRect, EditReferences, editReferences, EditorStyles.miniButton);

                if (EditReferences)
                {
                    EditorGUILayout.PropertyField(ContentTransform);
                    EditorGUILayout.PropertyField(SingleTemplate);
                    EditorGUILayout.PropertyField(SingleText);
                    EditorGUILayout.PropertyField(VerticalTemplate);
                    EditorGUILayout.PropertyField(VerticalMainText);
                    EditorGUILayout.PropertyField(VerticalListText);
                    EditorGUILayout.PropertyField(HorizontalTemplate);
                    EditorGUILayout.PropertyField(HorizontalMainText);
                    EditorGUILayout.PropertyField(HorizontalListText);
                }
            }

            EditorGUILayout.PropertyField(OnCreditsStart);
            EditorGUILayout.PropertyField(OnCreditsEnd);
        }
        #endregion
    }
}