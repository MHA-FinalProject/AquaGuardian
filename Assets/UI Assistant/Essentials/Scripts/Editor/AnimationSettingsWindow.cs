using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace UIAssistant
{
    public class AnimationSettingsWindow : SettingsWindow
    {
        #region Variables
        readonly GUIContent NewAnimationProfileContent = new("New Animation Profile");
        readonly GUIContent TransitionTimeContent = new("Transition Time", "The time it takes to transition between the hidden and visible states.");
        readonly GUIContent TransitionCurveContent = new("Transition Curve", "Animation curve used to ease between the states.");
        readonly GUIContent HiddenPositionOffsetContent = new("Position Offset", "Values used to move the Game Object into its hidden state. Positive values move the Game Object away from its parent, while negative values move it toward it.");
        readonly GUIContent HiddenAngleOffsetContent = new("Angle Offset", "Value used to rotate the Game Object into its hidden state, based on its anchored position. A positive value will rotate the Game Object clockwise if it’s anchored to the right, or, counterclockwise if it’s anchored to the left.");
        readonly GUIContent HiddenScaleContent = new("Scale", "Values used to multiply the X and Y values of the Game Object’s scale when transitioning into the hidden state.");
        readonly GUIContent HiddenAlphaContent = new("Alpha", "The Canvas Group’s alpha value in the Game Object’s hidden state.");
        readonly GUIContent TimePerCharacterContent = new("Time Per Character", "The number of seconds the Text Revealer waits between revealing two adjacent characters.");
        readonly GUIContent SkipSpacesContent = new("Skip Spaces", "If set to true, the Text Revealer will skip spaces during its animation.");
        readonly GUIContent PauseCharactersContent = new("Pause Characters", "Characters (without spaces) that cause the Text Revealer to pause.");
        readonly GUIContent RegularPauseTimeContent = new("Regular Pause Time", "The number of seconds the Text Revealer waits after detecting a pause character.");
        readonly GUIContent EllipsisCharactersContent = new("Ellipsis Characters", "Characters (without spaces) that cause the Text Revealer to pause when the character repeats.");
        readonly GUIContent EllipsisPauseTimeContent = new("Ellipsis Pause Time", "The number of seconds the Text Revealer waits after detecting an ellipsis character in a sequence. Overrides regular pause time in those cases.");

        readonly string[] AnimationSettingsTabNames = { "UI Animator", "Text Revealer" };
        
        Tab SelectedTab;

        int EditedAnimationProfileIndex = -1;

        string EditedName;
        float EditedTransitionTime;
        AnimationCurve EditedTransitionCurve;
        Vector2 EditedHiddenPositionOffset;
        float EditedHiddenAngleOffset;
        Vector2 EditedHiddenScale;
        float EditedHiddenAlpha;

        string InitialEditedName;
        public float InitialEditedTransitionTime;
        AnimationCurve InitialEditedTransitionCurve;
        Vector2 InitialEditedHiddenPositionOffset;
        float InitialEditedHiddenAngleOffset;
        Vector2 InitialEditedHiddenScale;
        float InitialEditedHiddenAlpha;
        #endregion

        #region Enums
        enum Tab
        {
            UIAnimator = 0,
            TextRevealer = 1,
        }
        #endregion

        #region Function
        [MenuItem("Tools/UI Assistant/Animation Settings", priority = 0)]
        public static void OpenWindow()
        {
            GetWindow<AnimationSettingsWindow>("Animation Settings");
        }
        protected override void OnGUI()
        {
            base.OnGUI();

            EditorGUI.BeginChangeCheck();

            if (EditedAnimationProfileIndex > -1)
            {
                AnimationProfileEditor();
            }
            else
            {
                SelectedTab = (Tab)GUILayout.Toolbar((int)SelectedTab, AnimationSettingsTabNames);

                switch (SelectedTab)
                {
                    case Tab.UIAnimator:
                        AnimationProfiles();
                        if (Return) return;
                        else break;
                    case Tab.TextRevealer:
                        TextRevealer();
                        if (Return) return;
                        else break;
                }
            }

            if (EditorGUI.EndChangeCheck()) ApplyChanges();
        }

        protected override void ApplyChanges()
        {
            ApplyChanges(AnimationSettings);
            ApplyChanges(new List<Object>(AnimationSettings.AnimationProfiles));
        }

        void AnimationProfiles()
        {
            if (AnimationSettings.AnimationProfiles.Count > 0)
            {
                EditorGUILayout.Space();

                BeginScrollArea();

                for (int i = 0; i < AnimationSettings.AnimationProfiles.Count; i++)
                {
                    AnimationProfile(i);

                    if (Return)
                    {
                        GUILayout.EndHorizontal();
                        break;
                    }
                }

                EndScrollArea();
            }

            EditorGUILayout.Space();

            if (AddButton(NewAnimationProfileContent)) InitializeAnimationProfileCreator();

            FocusClearArea();
        }
        void AnimationProfile(int index)
        {
            GUILayout.BeginHorizontal();

            if (MoveUpButton) MoveListItem(AnimationSettings.AnimationProfiles, index, -1);

            if (MoveDownButton) MoveListItem(AnimationSettings.AnimationProfiles, index, 1);

            GUILayout.Label(AnimationSettings.AnimationProfiles[index].Name, BoxedLabelStyle, GUILayout.Height(ContentLibrary.RowHeight));

            if (EditButton) InitializeAnimationProfileEditor(index);

            if (DeleteButton)
            {
                if (DeleteDialog(AnimationSettings.AnimationProfiles[index], "animation profile")) DeleteAnimationProfile(index);
            }

            GUILayout.EndHorizontal();
        }
        void DeleteAnimationProfile(int index)
        {
            DestroyImmediate(AnimationSettings.AnimationProfiles[index], true);
            AnimationSettings.AnimationProfiles.RemoveAt(index);

            SetAssetNames(new(AnimationSettings.AnimationProfiles), typeof(AnimationProfile));

            AssetDatabase.SaveAssets();

            Return = true;
        }

        void InitializeAnimationProfileEditor(int index)
        {
            AnimationProfile editedAnimationProfile = AnimationSettings.AnimationProfiles[index];
            EditedAnimationProfileIndex = index;

            EditedName = editedAnimationProfile.Name;
            EditedTransitionTime = editedAnimationProfile.TransitionTime;
            EditedTransitionCurve = new(editedAnimationProfile.TransitionCurve.keys);

            EditedHiddenPositionOffset = editedAnimationProfile.HiddenPositionOffset;
            EditedHiddenAngleOffset = editedAnimationProfile.HiddenAngleOffset;
            EditedHiddenScale = editedAnimationProfile.HiddenScale;
            EditedHiddenAlpha = editedAnimationProfile.HiddenAlpha;

            InitialEditedName = EditedName;
            InitialEditedTransitionTime = EditedTransitionTime;
            InitialEditedTransitionCurve = new(EditedTransitionCurve.keys);

            InitialEditedHiddenPositionOffset = EditedHiddenPositionOffset;
            InitialEditedHiddenAngleOffset = EditedHiddenAngleOffset;
            InitialEditedHiddenScale = EditedHiddenScale;
            InitialEditedHiddenAlpha = EditedHiddenAlpha;
        }
        void InitializeAnimationProfileCreator()
        {
            EditedName = "New Animation Profile";
            EditedTransitionTime = .25f;
            EditedTransitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

            EditedHiddenPositionOffset = new(0, 0);
            EditedHiddenAngleOffset = 0;
            EditedHiddenScale = new(1, 1);
            EditedHiddenAlpha = 1;

            EditingNew = true;
            EditedAnimationProfileIndex = AnimationSettings.AnimationProfiles.Count;
        }
        void AnimationProfileEditor()
        {
            EditedName = EditorGUILayout.TextField(NameContent, EditedName, GUILayout.Height(ContentLibrary.RowHeight));

            EditorGUILayout.Space();

            EditedTransitionTime = EditorGUILayout.FloatField(TransitionTimeContent, EditedTransitionTime, GUILayout.Height(ContentLibrary.RowHeight));

            EditedTransitionCurve = EditorGUILayout.CurveField(TransitionCurveContent, EditedTransitionCurve, GUILayout.Height(ContentLibrary.RowHeight));

            EditorGUILayout.Space();

            BeginScrollArea();

            EditedHiddenPositionOffset = EditorGUILayout.Vector2Field(HiddenPositionOffsetContent, EditedHiddenPositionOffset, GUILayout.Height(ContentLibrary.RowHeight * 2));

            EditedHiddenAngleOffset = EditorGUILayout.FloatField(HiddenAngleOffsetContent, EditedHiddenAngleOffset, GUILayout.Height(ContentLibrary.RowHeight));

            EditedHiddenScale = EditorGUILayout.Vector2Field(HiddenScaleContent, EditedHiddenScale, GUILayout.Height(ContentLibrary.RowHeight * 2));

            EditedHiddenAlpha = EditorGUILayout.Slider(HiddenAlphaContent, EditedHiddenAlpha, 0, 1, GUILayout.Height(ContentLibrary.RowHeight));

            EndScrollArea();

            EditorGUILayout.Space();

            GUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();

            if (EditingNew)
            {
                if (ApplyButton(EditedName, "", new(AnimationSettings.AnimationProfiles), true)) SaveNewAnimationProfile();

                if (GUILayout.Button("Cancel", GUILayout.Height(ContentLibrary.RowHeight))) ClearAnimationProfileEditor();

                GUILayout.EndHorizontal();

                FocusClearArea();
            }
            else
            {
                CheckAnimationProfileEditorDirty();

                GUI.enabled = EditorDirty;

                if (GUILayout.Button("Revert", GUILayout.Height(ContentLibrary.RowHeight))) RevertEditedAnimationProfile();

                if (ApplyButton(EditedName, InitialEditedName, new(AnimationSettings.AnimationProfiles), false))
                    SaveEditedAnimationProfile();

                GUI.enabled = true;

                GUILayout.EndHorizontal();

                FocusClearArea();

                if (EditorBackButton()) ClearAnimationProfileEditor();
            }
        }
        void CheckAnimationProfileEditorDirty()
        {
            if (EditorDirty) return;

            if (EditedName != InitialEditedName)
            {
                EditorDirty = true;
                return;
            }

            if (EditedTransitionTime != InitialEditedTransitionTime)
            {
                EditorDirty = true;
                return;
            }

            if (EditedTransitionCurve.keys.Length != InitialEditedTransitionCurve.keys.Length)
            {
                EditorDirty = true;
                return;
            }

            if (EditedHiddenPositionOffset != InitialEditedHiddenPositionOffset)
            {
                EditorDirty = true;
                return;
            }

            if (EditedHiddenAngleOffset != InitialEditedHiddenAngleOffset)
            {
                EditorDirty = true;
                return;
            }

            if (EditedHiddenScale != InitialEditedHiddenScale)
            {
                EditorDirty = true;
                return;
            }

            if (EditedHiddenAlpha != InitialEditedHiddenAlpha)
            {
                EditorDirty = true;
                return;
            }

            for (int i = 0; i < EditedTransitionCurve.keys.Length; i++)
            {
                if (EditedTransitionCurve.keys[i].value != InitialEditedTransitionCurve.keys[i].value)
                {
                    EditorDirty = true;
                    return;
                }

                if (EditedTransitionCurve.keys[i].time != InitialEditedTransitionCurve.keys[i].time)
                {
                    EditorDirty = true;
                    return;
                }

                if (EditedTransitionCurve.keys[i].inTangent != InitialEditedTransitionCurve.keys[i].inTangent)
                {
                    EditorDirty = true;
                    return;
                }

                if (EditedTransitionCurve.keys[i].inWeight != InitialEditedTransitionCurve.keys[i].inWeight)
                {
                    EditorDirty = true;
                    return;
                }

                if (EditedTransitionCurve.keys[i].outTangent != InitialEditedTransitionCurve.keys[i].outTangent)
                {
                    EditorDirty = true;
                    return;
                }

                if (EditedTransitionCurve.keys[i].outWeight != InitialEditedTransitionCurve.keys[i].outWeight)
                {
                    EditorDirty = true;
                    return;
                }
            }
        }
        void RevertEditedAnimationProfile()
        {
            InitializeAnimationProfileEditor(EditedAnimationProfileIndex);
            GUI.FocusControl(null);
            EditorDirty = false;
        }
        void SaveEditedAnimationProfile()
        {
            EditingNew = false;
            EditorDirty = false;

            AnimationProfile editedAnimationProfile = AnimationSettings.AnimationProfiles[EditedAnimationProfileIndex];

            editedAnimationProfile.Name = EditedName;
            editedAnimationProfile.TransitionTime = EditedTransitionTime;
            editedAnimationProfile.TransitionCurve = EditedTransitionCurve;
            editedAnimationProfile.HiddenPositionOffset = EditedHiddenPositionOffset;
            editedAnimationProfile.HiddenAngleOffset = EditedHiddenAngleOffset;
            editedAnimationProfile.HiddenScale = EditedHiddenScale;
            editedAnimationProfile.HiddenAlpha = EditedHiddenAlpha;

            ApplyChanges();

            GUI.FocusControl(null);

            InitializeAnimationProfileEditor(EditedAnimationProfileIndex);
        }
        void SaveNewAnimationProfile()
        {
            AnimationProfile animationProfile = CreateInstance<AnimationProfile>();
            animationProfile.Name = EditedName;
            animationProfile.TransitionTime = EditedTransitionTime;
            animationProfile.TransitionCurve = EditedTransitionCurve;
            animationProfile.HiddenPositionOffset = EditedHiddenPositionOffset;
            animationProfile.HiddenAngleOffset = EditedHiddenAngleOffset;
            animationProfile.HiddenScale = EditedHiddenScale;
            animationProfile.HiddenAlpha = EditedHiddenAlpha;

            AnimationSettings.AnimationProfiles.Add(animationProfile);

            AssetDatabase.AddObjectToAsset(animationProfile, AnimationSettings);

            SetAssetNames(new(AnimationSettings.AnimationProfiles), typeof(AnimationProfile));

            AssetDatabase.SaveAssets();

            ApplyChanges();
            ClearAnimationProfileEditor();
        }
        void ClearAnimationProfileEditor()
        {
            EditingNew = false;

            EditedName = "";
            EditedTransitionTime = 0;
            EditedTransitionCurve = new();

            EditedHiddenPositionOffset = new(0, 0);
            EditedHiddenAngleOffset = 0;
            EditedHiddenScale = new(0, 0);
            EditedHiddenAlpha = 0;

            EditedAnimationProfileIndex = -1;

            GUI.FocusControl(null);
        }

        void TextRevealer()
        {
            EditorGUILayout.Space();

            BeginScrollArea();

            AnimationSettings.TextRevealerTimePerCharacter = EditorGUILayout.FloatField(TimePerCharacterContent,
                AnimationSettings.TextRevealerTimePerCharacter, GUILayout.Height(ContentLibrary.RowHeight));
            
            AnimationSettings.TextRevealerSkipSpaces = EditorGUILayout.Toggle(SkipSpacesContent,
                AnimationSettings.TextRevealerSkipSpaces, GUILayout.Height(ContentLibrary.RowHeight));

            EditorGUILayout.Space();

            AnimationSettings.TextRevealerPauseCharacters = EditorGUILayout.TextField(PauseCharactersContent,
                AnimationSettings.TextRevealerPauseCharacters, GUILayout.Height(ContentLibrary.RowHeight));
            
            AnimationSettings.TextRevealerRegularPauseTime = EditorGUILayout.FloatField(RegularPauseTimeContent,
                AnimationSettings.TextRevealerRegularPauseTime, GUILayout.Height(ContentLibrary.RowHeight));

            EditorGUILayout.Space();

            AnimationSettings.TextRevealerEllipsisCharacters = EditorGUILayout.TextField(EllipsisCharactersContent,
                AnimationSettings.TextRevealerEllipsisCharacters, GUILayout.Height(ContentLibrary.RowHeight));

            AnimationSettings.TextRevealerEllipsisPauseTime = EditorGUILayout.FloatField(EllipsisPauseTimeContent,
                AnimationSettings.TextRevealerEllipsisPauseTime, GUILayout.Height(ContentLibrary.RowHeight));

            EndScrollArea();

            FocusClearArea();
        }
        #endregion
    }
}