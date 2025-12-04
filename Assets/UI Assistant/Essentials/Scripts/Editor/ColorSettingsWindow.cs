using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace UIAssistant
{
    public class ColorSettingsWindow : SettingsWindow
    {
        #region Variables
        readonly GUIContent ActiveColorProfileContent = new("Active Color Profile", "Determines which color sets will be used to override Graphics' colors.");
        readonly GUIContent NewColorProfileContent = new("New Color Profile");
        readonly GUIContent NewColorCategoryContent = new("New Color Category");
        readonly GUIContent UpdateColorizablesContent = new("Update Graphic Colorizers", "Applies current settings to all relevant Game Objects in the open scene.");
        readonly GUIContent FadeTimeContent = new("Fade Time", "The time it takes to transition from one state to the next.");
        readonly GUIContent FadeCurveContent = new("Fade Curve", "Animation curve used to ease between states.");
        readonly GUIContent EditAlphaContent = new("Edit Alpha", "Show alpha values in the editor (alpha values will be applied regardless).");

        SerializedProperty ActiveColorProfile;

        Tab SelectedTab;

        int EditedColorProfileIndex = -1;

        string EditedName;
        float EditedFadeTime;
        AnimationCurve EditedFadeCurve;
        List<Color> EditedNormalColors = new();
        List<Color> EditedHighlightedColors = new();
        List<Color> EditedPressedColors = new();
        List<Color> EditedSelectedColors = new();
        List<Color> EditedDisabledColors = new();

        string InitialEditedName;
        float InitialEditedFadeTime;
        AnimationCurve InitialEditedFadeCurve;
        List<Color> InitialEditedNormalColors = new();
        List<Color> InitialEditedHighlightedColors = new();
        List<Color> InitialEditedPressedColors = new();
        List<Color> InitialEditedSelectedColors = new();
        List<Color> InitialEditedDisabledColors = new();

        bool ShowAlpha;
        #endregion

        #region Enums
        enum Tab
        {
            Profiles = 0,
            Categories = 1,
        }
        #endregion

        #region Function
        [MenuItem("Tools/UI Assistant/Color Settings", priority = 1)]
        public static void OpenWindow()
        {
            GetWindow<ColorSettingsWindow>("Color Settings");
        }
        protected override void OnGUI()
        {
            base.OnGUI();

            if (ActiveColorProfile == null)
                ActiveColorProfile = new SerializedObject(ColorSettings).FindProperty("ActiveColorProfile");

            if (ColorSettings.ColorCategories.Count == 0) CreateColorCategory(true);
            if (ColorSettings.ColorProfiles.Count == 0) CreateColorProfile(true);

            ValidateColorCategories();

            if (ColorSettings.ActiveColorProfile == null) ColorSettings.SetColorProfile(0);

            EditorGUI.BeginChangeCheck();

            if (EditingLocalizedName) LocalizedNameEditor();
            else if (EditedColorProfileIndex > -1) ColorProfileEditor();
            else
            {
                SelectedTab = (Tab)GUILayout.Toolbar((int)SelectedTab, System.Enum.GetNames(typeof(Tab)));

                switch (SelectedTab)
                {
                    case Tab.Categories:
                        ColorCategories();
                        if (Return) return;
                        else break;
                    case Tab.Profiles:
                        ColorProfiles();
                        if (Return) return;
                        else break;
                }
            }

            if (EditorGUI.EndChangeCheck()) ApplyChanges();
        }

        protected override void ApplyChanges()
        {
            ApplyChanges(ColorSettings);
            ApplyChanges(new List<Object>(ColorSettings.ColorCategories));
            ApplyChanges(new List<Object>(ColorSettings.ColorProfiles));
        }

        void ColorProfiles()
        {
            EditorGUILayout.Space();

            int activeIndex = ColorSettings.ColorProfiles.IndexOf(ColorSettings.ActiveColorProfile);
            int initialActiveIndex = activeIndex;
            activeIndex = EditorGUILayout.Popup(ActiveColorProfileContent, activeIndex, ColorSettings.ProfileNames);

            if (activeIndex != initialActiveIndex)
            {
                ColorSettings.SetColorProfile(activeIndex);
            }

            EditorGUILayout.Space();

            BeginScrollArea();

            for (int i = 0; i < ColorSettings.ColorProfiles.Count; i++)
            {
                ColorProfile(i);

                if (Return)
                {
                    GUILayout.EndHorizontal();
                    break;
                }
            }

            EndScrollArea();

            EditorGUILayout.Space();

            if (AddButton(NewColorProfileContent)) InitializeColorProfileCreator();

            FocusClearArea();

            UpdateGraphicColorizersButton();
        }
        void ColorProfile(int index)
        {
            GUILayout.BeginHorizontal();

            if (MoveUpButton) MoveListItem(ColorSettings.ColorProfiles, index, -1);

            if (MoveDownButton) MoveListItem(ColorSettings.ColorProfiles, index, 1);

            GUILayout.Label(ColorSettings.ColorProfiles[index].Name, BoxedLabelStyle, GUILayout.Height(ContentLibrary.RowHeight));

            if (EditButton) InitializeColorProfileEditor(index);

            if (DeleteButton)
            {
                if (DeleteDialog(ColorSettings.ColorProfiles[index], "color profile")) DeleteColorProfile(index);
            }

            GUILayout.EndHorizontal();
        }
        void CreateColorProfile(bool autoCreate)
        {
            ColorProfile colorProfile = CreateInstance<ColorProfile>();

            colorProfile.Name = "New Color Profile";
            colorProfile.LocalizedName = null;
            colorProfile.FadeTime = 0.25f;
            colorProfile.FadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

            ColorSettings.ColorProfiles.Add(colorProfile);
            ColorSettings.OnColorProfileAdded();

            AssetDatabase.AddObjectToAsset(colorProfile, ColorSettings);

            EditorUtility.SetDirty(ColorSettings);
            EditorUtility.SetDirty(colorProfile);

            SetAssetNames(new(ColorSettings.ColorProfiles), typeof(ColorProfile));

            ValidateColorCategories();
            ApplyChanges();
            AssetDatabase.SaveAssets();
            if (autoCreate) AutoCreateMessage(colorProfile);
        }
        void DeleteColorProfile(int index)
        {
            DestroyImmediate(ColorSettings.ColorProfiles[index], true);
            ColorSettings.ColorProfiles.RemoveAt(index);

            ColorSettings.OnColorProfileRemoved();
            SetAssetNames(new(ColorSettings.ColorProfiles), typeof(ColorProfile));

            AssetDatabase.SaveAssets();

            Return = true;
        }

        void InitializeColorProfileEditor(int index)
        {
            ColorProfile editedColorProfile = ColorSettings.ColorProfiles[index];
            EditedColorProfileIndex = index;

            EditedName = editedColorProfile.Name;
            EditedLocalizedName = editedColorProfile.LocalizedName;
            EditedFadeTime = editedColorProfile.FadeTime;
            EditedFadeCurve = new(editedColorProfile.FadeCurve.keys);

            EditedNormalColors = new();
            EditedHighlightedColors = new();
            EditedPressedColors = new();
            EditedSelectedColors = new();
            EditedDisabledColors = new();

            for (int i = 0; i < ColorSettings.ColorCategories.Count; i++)
            {
                ColorCategory.ProfileColors profileColors = ColorSettings.ColorCategories[i].Colors[index];

                EditedNormalColors.Add(profileColors.NormalColor);
                EditedHighlightedColors.Add(profileColors.HighlightedColor);
                EditedPressedColors.Add(profileColors.PressedColor);
                EditedSelectedColors.Add(profileColors.SelectedColor);
                EditedDisabledColors.Add(profileColors.DisabledColor);
            }

            InitialEditedName = EditedName;
            InitialEditedLocalizedName = EditedLocalizedName;
            InitialEditedFadeTime = EditedFadeTime;
            InitialEditedFadeCurve = new(EditedFadeCurve.keys);

            InitialEditedNormalColors = new(EditedNormalColors);
            InitialEditedHighlightedColors = new(EditedHighlightedColors);
            InitialEditedPressedColors = new(EditedPressedColors);
            InitialEditedSelectedColors = new(EditedSelectedColors);
            InitialEditedDisabledColors = new(EditedDisabledColors);
        }
        void InitializeColorProfileCreator()
        {
            EditedName = "New Color Profile";
            EditedLocalizedName = null;
            EditedFadeTime = .1f;
            EditedFadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

            for (int i = 0; i < ColorSettings.ColorCategories.Count; i++)
            {
                EditedNormalColors.Add(Color.white);
                EditedHighlightedColors.Add(Color.white);
                EditedPressedColors.Add(Color.white);
                EditedSelectedColors.Add(Color.white);
                EditedDisabledColors.Add(Color.white);
            }

            EditingNew = true;
            EditedColorProfileIndex = ColorSettings.ColorProfiles.Count;
        }
        void ColorProfileEditor()
        {
            if (EditedNormalColors.Count == 0) InitializeColorProfileEditor(EditedColorProfileIndex);

            EditedName = EditorGUILayout.TextField(NameContent, EditedName, GUILayout.Height(ContentLibrary.RowHeight));

            LocalizedName();

            EditorGUILayout.Space();

            EditedFadeTime = EditorGUILayout.FloatField(FadeTimeContent, EditedFadeTime, GUILayout.Height(ContentLibrary.RowHeight));

            EditedFadeCurve = EditorGUILayout.CurveField(FadeCurveContent, EditedFadeCurve, GUILayout.Height(ContentLibrary.RowHeight));

            EditorGUILayout.Space();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            ShowAlpha = GUILayout.Toggle(ShowAlpha, EditAlphaContent, EditorStyles.miniButton, GUILayout.Width(ContentLibrary.AddButtonWidth));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            EditorGUILayout.Space();

            BeginScrollArea();

            EditorGUILayout.Space();

            GUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();

            GUILayout.BeginVertical();
            EditorGUILayout.Space(ContentLibrary.RowHeight);
            for (int i = 0; i < ColorSettings.ColorCategories.Count; i++)
            {
                EditorGUILayout.Space();
                if (GUILayout.Button(ColorSettings.ColorCategories[i].Name, MiddleRightStyle,
                    GUILayout.ExpandWidth(true), GUILayout.Height(ContentLibrary.CellHeight))) GUI.FocusControl(null);
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            GUILayout.Label(ContentLibrary.NormalContent, MiddleCenterStyle, GUILayout.Height(ContentLibrary.IconHeight));
            for (int i = 0; i < EditedNormalColors.Count; i++)
            {
                EditorGUILayout.Space();
                EditedNormalColors[i] = EditorGUILayout.ColorField(GUIContent.none, EditedNormalColors[i],
                    showEyedropper: false, showAlpha: ShowAlpha, hdr: false, GUILayout.Width(ContentLibrary.CellWidth), GUILayout.Height(ContentLibrary.CellHeight));
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            GUILayout.Label(ContentLibrary.HighlightedContent, MiddleCenterStyle, GUILayout.Height(ContentLibrary.IconHeight));
            for (int i = 0; i < EditedHighlightedColors.Count; i++)
            {
                EditorGUILayout.Space();
                EditedHighlightedColors[i] = EditorGUILayout.ColorField(GUIContent.none, EditedHighlightedColors[i],
                    showEyedropper: false, showAlpha: ShowAlpha, hdr: false, GUILayout.Width(ContentLibrary.CellWidth), GUILayout.Height(ContentLibrary.CellHeight));
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            GUILayout.Label(ContentLibrary.PressedContent, MiddleCenterStyle, GUILayout.Height(ContentLibrary.IconHeight));
            for (int i = 0; i < EditedPressedColors.Count; i++)
            {
                EditorGUILayout.Space();
                EditedPressedColors[i] = EditorGUILayout.ColorField(GUIContent.none, EditedPressedColors[i],
                    showEyedropper: false, showAlpha: ShowAlpha, hdr: false, GUILayout.Width(ContentLibrary.CellWidth), GUILayout.Height(ContentLibrary.CellHeight));
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            GUILayout.Label(ContentLibrary.SelectedContent, MiddleCenterStyle, GUILayout.Height(ContentLibrary.IconHeight));
            for (int i = 0; i < EditedSelectedColors.Count; i++)
            {
                EditorGUILayout.Space();
                EditedSelectedColors[i] = EditorGUILayout.ColorField(GUIContent.none, EditedSelectedColors[i],
                    showEyedropper: false, showAlpha: ShowAlpha, hdr: false, GUILayout.Width(ContentLibrary.CellWidth), GUILayout.Height(ContentLibrary.CellHeight));
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            GUILayout.Label(ContentLibrary.DisabledContent, MiddleCenterStyle, GUILayout.Height(ContentLibrary.IconHeight));
            for (int i = 0; i < EditedDisabledColors.Count; i++)
            {
                EditorGUILayout.Space();
                EditedDisabledColors[i] = EditorGUILayout.ColorField(GUIContent.none, EditedDisabledColors[i],
                    showEyedropper: false, showAlpha: ShowAlpha, hdr: false, GUILayout.Width(ContentLibrary.CellWidth), GUILayout.Height(ContentLibrary.CellHeight));
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            GUI.contentColor = Color.clear;
            EditorGUILayout.Space(ContentLibrary.RowHeight);
            for (int i = 0; i < ColorSettings.ColorCategories.Count; i++)
            {
                EditorGUILayout.Space();
                if (GUILayout.Button(ColorSettings.ColorCategories[i].Name, MiddleRightStyle,
                    GUILayout.ExpandWidth(true), GUILayout.Height(ContentLibrary.CellHeight))) GUI.FocusControl(null);
            }
            GUI.contentColor = Color.white;
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            GUILayout.EndHorizontal();

            EditorGUILayout.Space();

            EndScrollArea();

            EditorGUILayout.Space();

            GUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();

            if (EditingNew)
            {
                if (ApplyButton(EditedName, "", new(ColorSettings.ColorProfiles), true)) SaveNewColorProfile();

                if (GUILayout.Button("Cancel", GUILayout.Height(ContentLibrary.RowHeight))) ClearColorProfileEditor();
              
                GUILayout.EndHorizontal();

                FocusClearArea();
            }
            else
            {
                CheckColorProfileEditorDirty();

                GUI.enabled = EditorDirty;

                if (GUILayout.Button("Revert", GUILayout.Height(ContentLibrary.RowHeight))) RevertEditedColorProfile();

                if (ApplyButton(EditedName, InitialEditedName, new(ColorSettings.ColorProfiles), false))
                    SaveEditedColorProfile();
               
                GUI.enabled = true;

                GUILayout.EndHorizontal();

                FocusClearArea();
                
                if (EditorBackButton()) ClearColorProfileEditor();
            }
        }
        void CheckColorProfileEditorDirty()
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

            if (EditedFadeTime != InitialEditedFadeTime)
            {
                EditorDirty = true;
                return;
            }

            if (EditedFadeCurve.keys.Length != InitialEditedFadeCurve.keys.Length)
            {
                EditorDirty = true;
                return;
            }

            for (int i = 0; i < EditedNormalColors.Count; i++)
            {
                if (EditedNormalColors[i] != InitialEditedNormalColors[i])
                {
                    EditorDirty = true;
                    return;
                }
            }

            for (int i = 0; i < EditedHighlightedColors.Count; i++)
            {
                if (EditedHighlightedColors[i] != InitialEditedHighlightedColors[i])
                {
                    EditorDirty = true;
                    return;
                }
            }

            for (int i = 0; i < EditedPressedColors.Count; i++)
            {
                if (EditedPressedColors[i] != InitialEditedPressedColors[i])
                {
                    EditorDirty = true;
                    return;
                }
            }

            for (int i = 0; i < EditedSelectedColors.Count; i++)
            {
                if (EditedSelectedColors[i] != InitialEditedSelectedColors[i])
                {
                    EditorDirty = true;
                    return;
                }
            }

            for (int i = 0; i < EditedDisabledColors.Count; i++)
            {
                if (EditedDisabledColors[i] != InitialEditedDisabledColors[i])
                {
                    EditorDirty = true;
                    return;
                }
            }

            for (int i = 0; i < EditedFadeCurve.keys.Length; i++)
            {
                if (EditedFadeCurve.keys[i].value != InitialEditedFadeCurve.keys[i].value)
                {
                    EditorDirty = true;
                    return;
                }

                if (EditedFadeCurve.keys[i].time != InitialEditedFadeCurve.keys[i].time)
                {
                    EditorDirty = true;
                    return;
                }

                if (EditedFadeCurve.keys[i].inTangent != InitialEditedFadeCurve.keys[i].inTangent)
                {
                    EditorDirty = true;
                    return;
                }

                if (EditedFadeCurve.keys[i].inWeight != InitialEditedFadeCurve.keys[i].inWeight)
                {
                    EditorDirty = true;
                    return;
                }

                if (EditedFadeCurve.keys[i].outTangent != InitialEditedFadeCurve.keys[i].outTangent)
                {
                    EditorDirty = true;
                    return;
                }

                if (EditedFadeCurve.keys[i].outWeight != InitialEditedFadeCurve.keys[i].outWeight)
                {
                    EditorDirty = true;
                    return;
                }
            }
        }
        void RevertEditedColorProfile()
        {
            InitializeColorProfileEditor(EditedColorProfileIndex);
            GUI.FocusControl(null);
            EditorDirty = false;
        }
        void SaveEditedColorProfile()
        {
            EditingNew = false;
            EditorDirty = false;

            ColorProfile editedColorProfile = ColorSettings.ColorProfiles[EditedColorProfileIndex];

            editedColorProfile.Name = EditedName;
            editedColorProfile.LocalizedName = EditedLocalizedName;
            editedColorProfile.FadeTime = EditedFadeTime;
            editedColorProfile.FadeCurve = new(EditedFadeCurve.keys);

            for (int i = 0; i < ColorSettings.ColorCategories.Count; i++)
            {
                ColorSettings.ColorCategories[i].Colors[EditedColorProfileIndex].NormalColor = EditedNormalColors[i];
                ColorSettings.ColorCategories[i].Colors[EditedColorProfileIndex].HighlightedColor = EditedHighlightedColors[i];
                ColorSettings.ColorCategories[i].Colors[EditedColorProfileIndex].PressedColor = EditedPressedColors[i];
                ColorSettings.ColorCategories[i].Colors[EditedColorProfileIndex].SelectedColor = EditedSelectedColors[i];
                ColorSettings.ColorCategories[i].Colors[EditedColorProfileIndex].DisabledColor = EditedDisabledColors[i];
            }

            ApplyChanges();

            GUI.FocusControl(null);

            InitializeColorProfileEditor(EditedColorProfileIndex);
        }
        void SaveNewColorProfile()
        {
            ColorProfile colorProfile = CreateInstance<ColorProfile>();
            colorProfile.Name = EditedName;
            colorProfile.LocalizedName = EditedLocalizedName;
            colorProfile.FadeTime = EditedFadeTime;
            colorProfile.FadeCurve = new(EditedFadeCurve.keys);

            ColorSettings.ColorProfiles.Add(colorProfile);
            ColorSettings.OnColorProfileAdded();

            AssetDatabase.AddObjectToAsset(colorProfile, ColorSettings);

            SetAssetNames(new(ColorSettings.ColorProfiles), typeof(ColorProfile));

            AssetDatabase.SaveAssets();

            ValidateColorCategories();

            for (int i = 0; i < ColorSettings.ColorCategories.Count; i++)
            {
                ColorSettings.ColorCategories[i].Colors[EditedColorProfileIndex].NormalColor = EditedNormalColors[i];
                ColorSettings.ColorCategories[i].Colors[EditedColorProfileIndex].HighlightedColor = EditedHighlightedColors[i];
                ColorSettings.ColorCategories[i].Colors[EditedColorProfileIndex].PressedColor = EditedPressedColors[i];
                ColorSettings.ColorCategories[i].Colors[EditedColorProfileIndex].SelectedColor = EditedSelectedColors[i];
                ColorSettings.ColorCategories[i].Colors[EditedColorProfileIndex].DisabledColor = EditedDisabledColors[i];
            }

            ApplyChanges();
            ClearColorProfileEditor();
        }
        void ClearColorProfileEditor()
        {
            EditingNew = false;

            EditedName = "";
            EditedLocalizedName = null;
            EditedFadeTime = 0;
            EditedFadeCurve = new();

            EditedNormalColors.Clear();
            EditedHighlightedColors.Clear();
            EditedPressedColors.Clear();
            EditedSelectedColors.Clear();
            EditedDisabledColors.Clear();

            EditedColorProfileIndex = -1;

            GUI.FocusControl(null);
        }

        void ValidateColorCategories()
        {
            for (int i = 0; i < ColorSettings.ColorCategories.Count; i++)
            {
                ColorSettings.ColorCategories[i].ValidateCategory();
            }
        }
        void ColorCategories()
        {
            EditorGUILayout.Space();

            BeginScrollArea();

            for (int i = 0; i < ColorSettings.ColorCategories.Count; i++)
            {
                ColorCategory(i);

                if (Return)
                {
                    GUILayout.EndHorizontal();
                    break;
                }
            }

            EndScrollArea();

            EditorGUILayout.Space();

            if (AddButton(NewColorCategoryContent)) CreateColorCategory(false);

            FocusClearArea();

            UpdateGraphicColorizersButton();
        }
        void ColorCategory(int index)
        {
            GUILayout.BeginHorizontal();

            if (MoveUpButton) MoveListItem(ColorSettings.ColorCategories, index, -1);

            if (MoveDownButton) MoveListItem(ColorSettings.ColorCategories, index, 1);

            ColorSettings.ColorCategories[index].Name = EditorGUILayout.TextField(ColorSettings.ColorCategories[index].Name, GUILayout.Height(ContentLibrary.RowHeight));

            if (DeleteButton)
            {
                if (DeleteDialog(ColorSettings.ColorCategories[index], "color category")) DeleteColorCategory(index);
            }

            GUILayout.EndHorizontal();
        }
        void CreateColorCategory(bool autoCreate)
        {
            ColorCategory colorCategory = CreateInstance<ColorCategory>();

            colorCategory.Name = "New Color Category";

            ColorSettings.ColorCategories.Add(colorCategory);

            AssetDatabase.AddObjectToAsset(colorCategory, ColorSettings);

            EditorUtility.SetDirty(ColorSettings);
            EditorUtility.SetDirty(colorCategory);

            SetAssetNames(new(ColorSettings.ColorCategories), typeof(ColorCategory));

            ValidateColorCategories();
            ApplyChanges();
            AssetDatabase.SaveAssets();
            if (autoCreate) AutoCreateMessage(colorCategory);
        }
        void DeleteColorCategory(int index)
        {
            DestroyImmediate(ColorSettings.ColorCategories[index], true);
            ColorSettings.ColorCategories.RemoveAt(index);

            SetAssetNames(new(ColorSettings.ColorCategories), typeof(ColorCategory));

            AssetDatabase.SaveAssets();

            Return = true;
        }

        void UpdateGraphicColorizersButton()
        {
            if (GUILayout.Button(UpdateColorizablesContent, GUILayout.Height(ContentLibrary.RowHeight)))
            {
                List<GraphicColorizer> coloredGraphics = new(FindObjectsOfType<GraphicColorizer>(true));

                for (int i = 0; i < coloredGraphics.Count; i++)
                {
                    coloredGraphics[i].SetColor();
                }
            }
        }
        #endregion
    }
}