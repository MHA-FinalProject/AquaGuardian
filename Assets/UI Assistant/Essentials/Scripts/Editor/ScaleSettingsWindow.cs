using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace UIAssistant
{
    public class ScaleSettingsWindow : SettingsWindow
    {
        #region Variables
        readonly GUIContent ActiveScaleProfileContent = new("Active Scale Profile", "The active Scale Profile whose values will determine Scaleables' and Text Stylers' SetScale results.");
        readonly GUIContent NewScaleProfileContent = new("New Scale Profile");
        readonly GUIContent MinScaleMultiplierContent = new("Min Scale Multiplier", "The value used to multiply scales when the interpolation value is set to 0. Cannot be lower than 0.01.");
        readonly GUIContent MaxScaleMultiplierContent = new("Max Scale Multiplier", "The value used to multiply scales when the interpolation value is set to 1. This value can exceed 1.");
        readonly GUIContent MinFontSizeIncreaseContent = new("Min Font Size Increase", "The value used to increase font sizes when the interpolation value is set to 0.");
        readonly GUIContent MaxFontSizeIncreaseContent = new("Max Font Size Increase", "The value used to increase font sizes when the interpolation value is set to 1.");
        readonly GUIContent MinFontSizeMultiplierContent = new("Min Font Size Multiplier", "The value used to multiply font sizes when the interpolation value is set to 0. Cannot be lower than 0.01.");
        readonly GUIContent MaxFontSizeMultiplierContent = new("Max Font Size Multiplier", "The value used to multiply font sizes when the interpolation value is set to 1. This value can exceed 1.");
        readonly GUIContent ScaleInterpolationValueContent = new("Scale Interpolation", "The interpolation value between the active Scale Profile's Min and Max Scale Multiplier values.");
        readonly GUIContent FontSizeInterpolationContent = new("Font Size Interpolation", "The interpolation value between the active Scale Profile's Min and Max Font Size Increase, as well as Min and Max Font Size Multiplier values.");

        SerializedProperty ActiveScaleProfile;
        float ScaleInterpolationValue;
        float FontSizeInterpolationValue;

        ScaleProfile EditedScaleProfile;

        string EditedName;
        float EditedMinScaleMultiplier;
        float EditedMaxScaleMultiplier;
        float EditedMinFontSizeIncrease;
        float EditedMaxFontSizeIncrease;
        float EditedMinFontSizeMultiplier;
        float EditedMaxFontSizeMultiplier;

        string InitialEditedName;
        float InitialEditedMinScaleMultiplier;
        float InitialEditedMaxScaleMultiplier;
        float InitialEditedMinFontSizeIncrease;
        float InitialEditedMaxFontSizeIncrease;
        float InitialEditedMinFontSizeMultiplier;
        float InitialEditedMaxFontSizeMultiplier;
        #endregion

        #region Function
        [MenuItem("Tools/UI Assistant/Scale Settings", priority = 3)]
        public static void OpenWindow()
        {
            GetWindow<ScaleSettingsWindow>("Scale Settings");
        }
        protected override void OnGUI()
        {
            base.OnGUI();

            if (ActiveScaleProfile == null)
                ActiveScaleProfile = new SerializedObject(ScaleSettings).FindProperty("ActiveScaleProfile");

            if (ScaleSettings.ScaleProfiles.Count == 0) CreateScaleProfile(true);

            if (ScaleSettings.ActiveScaleProfile == null) ScaleSettings.SetScaleProfile(0);

            EditorGUI.BeginChangeCheck();

            if (EditingLocalizedName) LocalizedNameEditor();
            else if (EditedScaleProfile != null || EditingNew) ScaleProfileEditor();
            else ScaleProfiles();

            if (EditorGUI.EndChangeCheck()) ApplyChanges();
        }
        protected override void ApplyChanges()
        {
            ApplyChanges(ScaleSettings);
            ApplyChanges(new List<Object>(ScaleSettings.ScaleProfiles));
        }

        void ScaleProfiles()
        {
            EditorGUILayout.Space();

            int activeIndex = ScaleSettings.ScaleProfiles.IndexOf(ScaleSettings.ActiveScaleProfile);
            int initialActiveIndex = activeIndex;
            activeIndex = EditorGUILayout.Popup(ActiveScaleProfileContent, activeIndex, ScaleSettings.ProfileNames);

            if (activeIndex != initialActiveIndex) ScaleSettings.SetScaleProfile(activeIndex);

            EditorGUILayout.Space();

            BeginScrollArea();

            for (int i = 0; i < ScaleSettings.ScaleProfiles.Count; i++)
            {
                ScaleProfile(i);

                if (Return)
                {
                    GUILayout.EndHorizontal();
                    break;
                }
            }

            EndScrollArea();

            EditorGUILayout.Space();

            if (AddButton(NewScaleProfileContent)) InitializeScaleProfileCreator();

            FocusClearArea();

            ScaleInterpolationValue = EditorGUILayout.Slider(ScaleInterpolationValueContent, ScaleSettings.ScaleInterpolationValue, 0, 1, GUILayout.Height(ContentLibrary.RowHeight));
            ScaleSettings.ScaleInterpolationValue = ScaleInterpolationValue;

            FontSizeInterpolationValue = EditorGUILayout.Slider(FontSizeInterpolationContent, ScaleSettings.FontSizeInterpolationValue, 0, 1, GUILayout.Height(ContentLibrary.RowHeight));
            ScaleSettings.FontSizeInterpolationValue = FontSizeInterpolationValue;
        }
        void ScaleProfile(int index)
        {
            GUILayout.BeginHorizontal();

            if (MoveUpButton) MoveListItem(ScaleSettings.ScaleProfiles, index, -1);

            if (MoveDownButton) MoveListItem(ScaleSettings.ScaleProfiles, index, 1);

            GUILayout.Label(ScaleSettings.ScaleProfiles[index].Name, BoxedLabelStyle, GUILayout.Height(ContentLibrary.RowHeight));

            if (EditButton) InitializeScaleProfileEditor(ScaleSettings.ScaleProfiles[index]);

            if (DeleteButton)
            {
                if (DeleteDialog(ScaleSettings.ScaleProfiles[index], "scale profile")) DeleteScaleProfile(index);
            }

            GUILayout.EndHorizontal();
        }
        void CreateScaleProfile(bool autoCreate)
        {
            ScaleProfile scaleProfile = CreateInstance<ScaleProfile>();

            scaleProfile.Name = "New Scale Profile";
            scaleProfile.LocalizedName = null;
            scaleProfile.MinScaleMultiplier = .75f;
            scaleProfile.MaxScaleMultiplier = 1;
            scaleProfile.MinFontSizeIncrease = -5;
            scaleProfile.MaxFontSizeIncrease = 5;
            scaleProfile.MinFontSizeMultiplier = .75f;
            scaleProfile.MaxFontSizeMultiplier = 1;

            ScaleSettings.ScaleProfiles.Add(scaleProfile);
            ScaleSettings.OnScaleProfileAdded();

            AssetDatabase.AddObjectToAsset(scaleProfile, ScaleSettings);

            EditorUtility.SetDirty(ScaleSettings);
            EditorUtility.SetDirty(scaleProfile);

            SetAssetNames(new(ScaleSettings.ScaleProfiles), typeof(ScaleProfile));

            ApplyChanges();
            AssetDatabase.SaveAssets();
            if (autoCreate) AutoCreateMessage(scaleProfile);
        }
        void DeleteScaleProfile(int index)
        {
            DestroyImmediate(ScaleSettings.ScaleProfiles[index], true);
            ScaleSettings.ScaleProfiles.RemoveAt(index);

            ScaleSettings.OnScaleProfileRemoved();
            SetAssetNames(new(ScaleSettings.ScaleProfiles), typeof(ScaleProfile));

            AssetDatabase.SaveAssets();

            Return = true;
        }

        void InitializeScaleProfileEditor(ScaleProfile scaleProfile)
        {
            EditedScaleProfile = scaleProfile;

            EditedName = EditedScaleProfile.Name;
            EditedLocalizedName = EditedScaleProfile.LocalizedName;
            EditedMinScaleMultiplier = EditedScaleProfile.MinScaleMultiplier;
            EditedMaxScaleMultiplier = EditedScaleProfile.MaxScaleMultiplier;
            EditedMinFontSizeIncrease = EditedScaleProfile.MinFontSizeIncrease;
            EditedMaxFontSizeIncrease = EditedScaleProfile.MaxFontSizeIncrease;
            EditedMinFontSizeMultiplier = EditedScaleProfile.MinFontSizeMultiplier;
            EditedMaxFontSizeMultiplier = EditedScaleProfile.MaxFontSizeMultiplier;

            InitialEditedName = EditedName;
            InitialEditedLocalizedName = EditedLocalizedName;
            InitialEditedMinScaleMultiplier = EditedMinScaleMultiplier;
            InitialEditedMaxScaleMultiplier = EditedMaxScaleMultiplier;
            InitialEditedMinFontSizeIncrease = EditedMinFontSizeIncrease;
            InitialEditedMaxFontSizeIncrease = EditedMaxFontSizeIncrease;
            InitialEditedMinFontSizeMultiplier = EditedMinFontSizeMultiplier;
            InitialEditedMaxFontSizeMultiplier = EditedMaxFontSizeMultiplier;
        }
        void InitializeScaleProfileCreator()
        {
            EditedName = "New Scale Profile";
            EditedLocalizedName = null;
            EditedMinScaleMultiplier = .75f;
            EditedMaxScaleMultiplier = 1;
            EditedMinFontSizeIncrease = -5;
            EditedMaxFontSizeIncrease = 5;
            EditedMinFontSizeMultiplier = .75f;
            EditedMaxFontSizeMultiplier = 1;

            EditingNew = true;
        }
        void ScaleProfileEditor()
        {
            EditedName = EditorGUILayout.TextField(NameContent, EditedName, GUILayout.Height(ContentLibrary.RowHeight));

            LocalizedName();

            EditorGUILayout.Space();

            BeginScrollArea();

            EditedMinScaleMultiplier = EditorGUILayout.FloatField(MinScaleMultiplierContent, EditedMinScaleMultiplier, GUILayout.Height(ContentLibrary.RowHeight));

            EditedMaxScaleMultiplier = EditorGUILayout.FloatField(MaxScaleMultiplierContent, EditedMaxScaleMultiplier, GUILayout.Height(ContentLibrary.RowHeight));

            EditorGUILayout.MinMaxSlider(ref EditedMinScaleMultiplier, ref EditedMaxScaleMultiplier, 0, 1, GUILayout.Height(ContentLibrary.RowHeight));

            EditedMinScaleMultiplier = Mathf.Round(EditedMinScaleMultiplier * 100) / 100;
            EditedMaxScaleMultiplier = Mathf.Round(EditedMaxScaleMultiplier * 100) / 100;

            if (EditedMaxScaleMultiplier < EditedMinScaleMultiplier) EditedMaxScaleMultiplier = EditedMinScaleMultiplier;
            EditedMinScaleMultiplier = Mathf.Clamp(EditedMinScaleMultiplier, .01f, EditedMaxScaleMultiplier);

            EditorGUILayout.Space();

            EditedMinFontSizeIncrease = EditorGUILayout.FloatField(MinFontSizeIncreaseContent, EditedMinFontSizeIncrease, GUILayout.Height(ContentLibrary.RowHeight));
           
            EditedMaxFontSizeIncrease = EditorGUILayout.FloatField(MaxFontSizeIncreaseContent, EditedMaxFontSizeIncrease, GUILayout.Height(ContentLibrary.RowHeight));

            EditorGUILayout.Space();

            EditedMinFontSizeMultiplier = EditorGUILayout.FloatField(MinFontSizeMultiplierContent, EditedMinFontSizeMultiplier, GUILayout.Height(ContentLibrary.RowHeight));

            EditedMaxFontSizeMultiplier = EditorGUILayout.FloatField(MaxFontSizeMultiplierContent, EditedMaxFontSizeMultiplier, GUILayout.Height(ContentLibrary.RowHeight));

            EditorGUILayout.MinMaxSlider(ref EditedMinFontSizeMultiplier, ref EditedMaxFontSizeMultiplier, 0, 1, GUILayout.Height(ContentLibrary.RowHeight));

            EditedMinFontSizeMultiplier = Mathf.Round(EditedMinFontSizeMultiplier * 100) / 100;
            EditedMaxFontSizeMultiplier = Mathf.Round(EditedMaxFontSizeMultiplier * 100) / 100;

            if (EditedMaxFontSizeMultiplier < EditedMinFontSizeMultiplier) EditedMaxFontSizeMultiplier = EditedMinFontSizeMultiplier;
            EditedMinFontSizeMultiplier = Mathf.Clamp(EditedMinFontSizeMultiplier, .01f, EditedMaxFontSizeMultiplier);
            
            EndScrollArea();

            EditorGUILayout.Space();

            GUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();

            if (EditingNew)
            {
                if (ApplyButton(EditedName, "", new(ScaleSettings.ScaleProfiles), true)) SaveNewScaleProfile();

                if (GUILayout.Button("Cancel", GUILayout.Height(ContentLibrary.RowHeight))) ClearScaleProfileEditor();

                GUILayout.EndHorizontal();

                FocusClearArea();
            }
            else
            {
                CheckScaleProfileEditorDirty();

                GUI.enabled = EditorDirty;

                if (GUILayout.Button("Revert", GUILayout.Height(ContentLibrary.RowHeight))) RevertEditedScaleProfile();

                if (ApplyButton(EditedName, InitialEditedName, new(ScaleSettings.ScaleProfiles), false))
                    SaveEditedScaleProfile();

                GUI.enabled = true;

                GUILayout.EndHorizontal();

                FocusClearArea();

                if (EditorBackButton()) ClearScaleProfileEditor();
            }
        }
        void CheckScaleProfileEditorDirty()
        {
            if (EditorDirty) return;

            if (EditingNew)
            {
                EditorDirty = true;
                return;
            }

            if (EditedName != InitialEditedName)
            {
                EditorDirty = true;
                return;
            }

            if (EditedMinScaleMultiplier != InitialEditedMinScaleMultiplier)
            {
                EditorDirty = true;
                return;
            }

            if (EditedMaxScaleMultiplier != InitialEditedMaxScaleMultiplier)
            {
                EditorDirty = true;
                return;
            }

            if (EditedMinFontSizeIncrease != InitialEditedMinFontSizeIncrease)
            {
                EditorDirty = true;
                return;
            }

            if (EditedMaxFontSizeIncrease != InitialEditedMaxFontSizeIncrease)
            {
                EditorDirty = true;
                return;
            }

            if (EditedMinFontSizeMultiplier != InitialEditedMinFontSizeMultiplier)
            {
                EditorDirty = true;
                return;
            }

            if (EditedMaxFontSizeMultiplier != InitialEditedMaxFontSizeMultiplier)
            {
                EditorDirty = true;
                return;
            }
        }
        void RevertEditedScaleProfile()
        {
            InitializeScaleProfileEditor(EditedScaleProfile);
            GUI.FocusControl(null);
            EditorDirty = false;
        }
        void SaveEditedScaleProfile()
        {
            EditingNew = false;
            EditorDirty = false;

            EditedScaleProfile.Name = EditedName;
            EditedScaleProfile.LocalizedName = EditedLocalizedName;
            EditedScaleProfile.MinScaleMultiplier = EditedMinScaleMultiplier;
            EditedScaleProfile.MaxScaleMultiplier = EditedMaxScaleMultiplier;
            EditedScaleProfile.MinFontSizeIncrease = EditedMinFontSizeIncrease;
            EditedScaleProfile.MaxFontSizeIncrease = EditedMaxFontSizeIncrease;
            EditedScaleProfile.MinFontSizeMultiplier = EditedMinFontSizeMultiplier;
            EditedScaleProfile.MaxFontSizeMultiplier = EditedMaxFontSizeMultiplier;

            ApplyChanges();

            GUI.FocusControl(null);

            InitializeScaleProfileEditor(EditedScaleProfile);
        }
        void SaveNewScaleProfile()
        {
            ScaleProfile scaleProfile = CreateInstance<ScaleProfile>();
            scaleProfile.Name = EditedName;
            scaleProfile.LocalizedName = EditedLocalizedName;
            scaleProfile.MinScaleMultiplier = EditedMinScaleMultiplier;
            scaleProfile.MaxScaleMultiplier = EditedMaxScaleMultiplier;
            scaleProfile.MinFontSizeIncrease = EditedMinFontSizeIncrease;
            scaleProfile.MaxFontSizeIncrease = EditedMaxFontSizeIncrease;
            scaleProfile.MinFontSizeMultiplier = EditedMinFontSizeMultiplier;
            scaleProfile.MaxFontSizeMultiplier = EditedMaxFontSizeMultiplier;

            ScaleSettings.ScaleProfiles.Add(scaleProfile);
            ScaleSettings.OnScaleProfileAdded();

            AssetDatabase.AddObjectToAsset(scaleProfile, ScaleSettings);

            SetAssetNames(new(ScaleSettings.ScaleProfiles), typeof(ScaleProfile));

            AssetDatabase.SaveAssets();

            ApplyChanges();

            ClearScaleProfileEditor();
        }
        void ClearScaleProfileEditor()
        {
            EditingNew = false;

            EditedName = "";
            EditedLocalizedName = null;
            EditedMinScaleMultiplier = .75f;
            EditedMaxScaleMultiplier = 1;
            EditedMinFontSizeIncrease = -5;
            EditedMaxFontSizeIncrease = 5;
            EditedMinFontSizeMultiplier = .75f;
            EditedMaxFontSizeMultiplier = 1;

            EditedScaleProfile = null;

            GUI.FocusControl(null);
        }
        #endregion
    }
}