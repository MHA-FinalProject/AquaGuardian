using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace UIAssistant
{
    public class OptionSettingsWindow : SettingsWindow
    {
        #region Variables
        readonly GUIContent LocalizedContent = new("Localize Options", "When set to true, Option Syncers' label texts will be set based on Option Entries' localized names.");
        readonly GUIContent NewOptionEntryContent = new("New Option Entry");
        readonly GUIContent OptionTypeContent = new("Type", "The type of the Option Entry. Determines which Selectables can control it.");
        readonly GUIContent PrefabContent = new("Prefab", "Selectable associated with this Option Entry, used to set this Option Entry's value.");
        readonly GUIContent ValueFormatContent = new("Value Format", "The format in which the Option Entry's value is displayed.");
        readonly GUIContent ClampContent = new("Clamp", "Determines whether the Option Entry has limits.");
        readonly GUIContent ClampMinContent = new("Min Value", "The minimum value of the clamped Option Entry.");
        readonly GUIContent ClampMaxContent = new("Max Value", "The maximum value of the clamped Option Entry.");
        readonly GUIContent DefaultValueContent = new("Default Value", "Value referenced when RestoreDefaults() is called.");
        readonly GUIContent AddOptionContent = new("Add Option");

        readonly string[] OptionTypeNames =
            { "Custom Float", "Custom Bool", "Custom List", "[Color] Color Profile", "[Scaling] Scale Profile", "[Scaling] Scale Interpolaion", "[Scaling] Font Size Interpolation", "[Text] Language" };
        OptionEntry EditedOptionEntry;

        string EditedName;
        OptionType EditedType;
        OptionSyncer EditedPrefab;
        string EditedValueFormat;
        bool EditedClamp;
        float EditedClampMin;
        float EditedClampMax;
        List<string> EditedOptions;
        List<LocalizationSet> EditedLocalizedOptions;
        float EditedDefaultFloatValue;
        bool EditedDefaultBoolValue;
        int EditedDefaultIntValue;

        string InitialEditedName;
        OptionType InitialEditedType;
        OptionSyncer InitialEditedPrefab;
        string InitialEditedValueFormat;
        bool InitialEditedClamp;
        float InitialEditedClampMin;
        float InitialEditedClampMax;
        List<string> InitialEditedOptions;
        List<LocalizationSet> InitialEditedLocalizedOptions;
        float InitialEditedDefaultFloatValue;
        bool InitialEditedDefaultBoolValue;
        int InitialEditedDefaultIntValue;

        int SelectedLocalizationSetIndex;
        bool SelectingLocalizationSet;
        #endregion

        #region Function
        [MenuItem("Tools/UI Assistant/Option Settings", priority = 2)]
        public static void OpenWindow()
        {
            GetWindow<OptionSettingsWindow>("Option Settings");
        }
        protected override void OnGUI()
        {
            base.OnGUI();

            EditorGUI.BeginChangeCheck();

            if (EditingLocalizedName) LocalizedNameEditor();
            else if (SelectingLocalizationSet) LocalizationSetSelector();
            else if (EditingNew || EditedOptionEntry != null) OptionEntryEditor();
            else
            {
                OptionEntries();
                if (Return) return;
            }

            if (EditorGUI.EndChangeCheck()) ApplyChanges();
        }

        protected override void ApplyChanges()
        {
            ApplyChanges(OptionSettings);
            ApplyChanges(new List<Object>(OptionSettings.OptionEntries));
        }

        void OptionEntries()
        {
            OptionSettings.Localized = EditorGUILayout.ToggleLeft(LocalizedContent, OptionSettings.Localized, GUILayout.Height(ContentLibrary.RowHeight));

            if (OptionSettings.OptionEntries.Count > 0)
            {
                EditorGUILayout.Space();

                BeginScrollArea();

                for (int i = 0; i < OptionSettings.OptionEntries.Count; i++)
                {
                    OptionEntry(i);

                    if (Return)
                    {
                        GUILayout.EndHorizontal();
                        break;
                    }
                }

                EndScrollArea();
            }

            EditorGUILayout.Space();

            if (AddButton(NewOptionEntryContent)) InitializeOptionEntryCreator();

            FocusClearArea();
        }
        void OptionEntry(int index)
        {
            GUILayout.BeginHorizontal();

            if (MoveUpButton) MoveListItem(OptionSettings.OptionEntries, index, -1);

            if (MoveDownButton) MoveListItem(OptionSettings.OptionEntries, index, 1);

            GUILayout.Label(OptionSettings.OptionEntries[index].Name, BoxedLabelStyle, GUILayout.Height(ContentLibrary.RowHeight));

            if (EditButton) InitializeOptionEntryEditor(OptionSettings.OptionEntries[index]);

            if (DeleteButton)
            {
                if (DeleteDialog(OptionSettings.OptionEntries[index], "option entry")) DeleteOptionEntry(index);
            }

            GUILayout.EndHorizontal();
        }
        void DeleteOptionEntry(int index)
        {
            DestroyImmediate(OptionSettings.OptionEntries[index], true);
            OptionSettings.OptionEntries.RemoveAt(index);

            SetAssetNames(new(OptionSettings.OptionEntries), typeof(OptionEntry));

            AssetDatabase.SaveAssets();

            Return = true;
        }

        void InitializeOptionEntryEditor(OptionEntry optionEntry)
        {
            EditedOptionEntry = optionEntry;

            EditedName = EditedOptionEntry.Name;
            EditedLocalizedName = EditedOptionEntry.LocalizedName;
            EditedType = EditedOptionEntry.Type;
            EditedPrefab = EditedOptionEntry.Prefab;
            EditedValueFormat = EditedOptionEntry.ValueFormat;
            EditedClamp = EditedOptionEntry.Clamp;
            EditedClampMin = EditedOptionEntry.ClampMin;
            EditedClampMax = EditedOptionEntry.ClampMax;
            EditedOptions = new(EditedOptionEntry.Options);
            EditedLocalizedOptions = new(EditedOptionEntry.LocalizedOptions);
            EditedDefaultFloatValue = EditedOptionEntry.DefaultFloatValue;
            EditedDefaultBoolValue = EditedOptionEntry.DefaultBoolValue;
            EditedDefaultIntValue = EditedOptionEntry.DefaultIntValue;

            InitialEditedName = EditedName;
            InitialEditedLocalizedName = EditedLocalizedName;
            InitialEditedType = EditedType;
            InitialEditedPrefab = EditedPrefab;
            InitialEditedValueFormat = EditedValueFormat;
            InitialEditedClamp = EditedClamp;
            InitialEditedClampMin = EditedClampMin;
            InitialEditedClampMax = EditedClampMax;
            InitialEditedOptions = new(EditedOptions);
            InitialEditedLocalizedOptions = new(EditedLocalizedOptions);
            InitialEditedDefaultFloatValue = EditedDefaultFloatValue;
            InitialEditedDefaultBoolValue = EditedDefaultBoolValue;
            InitialEditedDefaultIntValue = EditedDefaultIntValue;
        }
        void InitializeOptionEntryCreator()
        {
            EditedName = "New Option Entry";
            EditedLocalizedName = null;
            EditedType = OptionType.Float;
            EditedValueFormat = "0.0%";
            EditedClamp = true;
            EditedClampMin = 0;
            EditedClampMax = 100;
            EditedOptions = new();
            EditedLocalizedOptions = new();

            EditingNew = true;
        }
        void OptionEntryEditor()
        {
            EditedName = EditorGUILayout.TextField(NameContent, EditedName, GUILayout.Height(ContentLibrary.RowHeight));

            if (OptionSettings.Localized) LocalizedName();

            EditedType = (OptionType)EditorGUILayout.Popup(OptionTypeContent,
                (int)EditedType, OptionTypeNames);

            EditedPrefab = EditorGUILayout.ObjectField(PrefabContent, EditedPrefab, typeof(OptionSyncer), false) as OptionSyncer;

            if (EditedType == OptionType.Float)
            {
                if (EditedClamp)
                {
                    EditedDefaultFloatValue = EditorGUILayout.Slider(DefaultValueContent, EditedDefaultFloatValue, EditedClampMin, EditedClampMax, GUILayout.Height(ContentLibrary.RowHeight));
                }
                else
                {
                    EditedDefaultFloatValue = EditorGUILayout.FloatField(DefaultValueContent, EditedDefaultFloatValue, GUILayout.Height(ContentLibrary.RowHeight));
                }

                EditorGUILayout.Space();

                BeginScrollArea();

                EditedValueFormat = EditorGUILayout.TextField(ValueFormatContent, EditedValueFormat, GUILayout.Height(ContentLibrary.RowHeight));

                EditedClamp = EditorGUILayout.Toggle(ClampContent, EditedClamp, GUILayout.Height(ContentLibrary.RowHeight));

                if (EditedClamp)
                {
                    EditedClampMin = EditorGUILayout.FloatField(ClampMinContent, EditedClampMin, GUILayout.Height(ContentLibrary.RowHeight));
                    EditedClampMax = EditorGUILayout.FloatField(ClampMaxContent, EditedClampMax, GUILayout.Height(ContentLibrary.RowHeight));
                }


                EndScrollArea();
            }
            else if (EditedType == OptionType.Bool)
            {
                EditedDefaultBoolValue = EditorGUILayout.Toggle(DefaultValueContent, EditedDefaultBoolValue, GUILayout.Height(ContentLibrary.RowHeight));
            }
            else if (EditedType == OptionType.List)
            {
                EditorGUILayout.Space();

                if (OptionSettings.Localized)
                {
                    if (EditedLocalizedOptions.Count > 1)
                    {
                        EditorGUILayout.Space();
                        string[] defaultArray = new string[EditedLocalizedOptions.Count];
                        for (int i = 0; i < EditedLocalizedOptions.Count; i++)
                        {
                            defaultArray[i] = EditedLocalizedOptions[i] != null ? EditedLocalizedOptions[i].LocalizedText : $"Option {i + 1} (null)";
                        }
                        EditedDefaultIntValue = EditorGUILayout.Popup(DefaultValueContent, EditedDefaultIntValue, defaultArray);
                    }
                    else EditedDefaultIntValue = 0;
                    
                    bool scrollArea = EditedLocalizedOptions.Count > 0;

                    if (scrollArea) BeginScrollArea();

                    for (int i = 0; i < EditedLocalizedOptions.Count; i++)
                    {
                        GUILayout.BeginHorizontal();

                        if (MoveUpButton) MoveListItem(EditedLocalizedOptions, i, -1);

                        if (MoveDownButton) MoveListItem(EditedLocalizedOptions, i, 1);

                        string localizationSetName = "";
                        if (EditedLocalizedOptions[i] != null) localizationSetName = EditedLocalizedOptions[i].Name;

                        if (GUILayout.Button(localizationSetName))
                        {
                            SelectedLocalizationSetIndex = i;
                            SelectingLocalizationSet = true;
                        }

                        if (DeleteButton)
                        {
                            EditedLocalizedOptions.RemoveAt(i);
                            GUILayout.EndHorizontal();
                            break;
                        }

                        GUILayout.EndHorizontal();
                    }

                    if (scrollArea) EndScrollArea();

                    EditorGUILayout.Space();

                    if (AddButton(AddOptionContent)) EditedLocalizedOptions.Add(null);
                }
                else
                {
                    if (EditedOptions.Count > 1)
                    {
                        EditorGUILayout.Space();
                        string[] defaultArray = new string[EditedOptions.Count];
                        for (int i = 0; i < EditedOptions.Count; i++)
                        {
                            defaultArray[i] = EditedOptions[i].Length > 0 ? EditedOptions[i] : $"Option {i + 1} (null)";
                        }
                        EditedDefaultIntValue = EditorGUILayout.Popup(DefaultValueContent, EditedDefaultIntValue, defaultArray);
                    }
                    else EditedDefaultIntValue = 0;

                    bool scrollArea = EditedOptions.Count > 0;

                    if (scrollArea) BeginScrollArea();

                    for (int i = 0; i < EditedOptions.Count; i++)
                    {
                        GUILayout.BeginHorizontal();

                        if (MoveUpButton) MoveListItem(EditedOptions, i, -1);

                        if (MoveDownButton) MoveListItem(EditedOptions, i, 1);

                        EditedOptions[i] = EditorGUILayout.TextField(EditedOptions[i], GUILayout.Height(ContentLibrary.RowHeight));

                        if (DeleteButton)
                        {
                            EditedOptions.RemoveAt(i);
                            GUILayout.EndHorizontal();
                            break;
                        }

                        GUILayout.EndHorizontal();
                    }

                    if (scrollArea) EndScrollArea();

                    EditorGUILayout.Space();

                    if (AddButton(AddOptionContent)) EditedOptions.Add("");
                }
            }
            else if (EditedType == OptionType.ColorProfile)
            {
                if (ColorSettings.ColorProfiles.Count > 0)
                {
                    EditedDefaultIntValue = EditorGUILayout.Popup(DefaultValueContent, EditedDefaultIntValue, ColorSettings.ProfileNames);
                }
                EditedDefaultIntValue = Mathf.Clamp(EditedDefaultIntValue, 0, ColorSettings.ColorProfiles.Count - 1);
            }
            else if (EditedType == OptionType.ScaleProfile)
            {
                if (ScaleSettings.ScaleProfiles.Count > 0)
                {
                    EditedDefaultIntValue = EditorGUILayout.Popup(DefaultValueContent, EditedDefaultIntValue, ScaleSettings.ProfileNames);
                }
                EditedDefaultIntValue = Mathf.Clamp(EditedDefaultIntValue, 0, ScaleSettings.ScaleProfiles.Count - 1);
            }
            else if (EditedType == OptionType.ScaleInterpolation || EditedType == OptionType.FontSizeInterpolation)
            {
                EditorGUILayout.Space();

                EditedDefaultFloatValue = EditorGUILayout.Slider(DefaultValueContent, EditedDefaultFloatValue, EditedClampMin, EditedClampMax, GUILayout.Height(ContentLibrary.RowHeight));

                BeginScrollArea();

                EditedValueFormat = EditorGUILayout.TextField(ValueFormatContent, EditedValueFormat, GUILayout.Height(ContentLibrary.RowHeight));

                GUI.enabled = false;

                EditedClamp = EditorGUILayout.Toggle(ClampContent, true, GUILayout.Height(ContentLibrary.RowHeight));

                if (EditedClamp)
                {
                    EditedClampMin = EditorGUILayout.FloatField(ClampMinContent, 0, GUILayout.Height(ContentLibrary.RowHeight));
                    EditedClampMax = EditorGUILayout.FloatField(ClampMaxContent, 1, GUILayout.Height(ContentLibrary.RowHeight));
                }

                GUI.enabled = true;

                EndScrollArea();
            }
            else if (EditedType == OptionType.Language)
            {
                if (TextSettings.Languages.Count > 0)
                {
                    EditedDefaultIntValue = EditorGUILayout.Popup(DefaultValueContent, EditedDefaultIntValue, TextSettings.LanguageNames);
                }
                EditedDefaultIntValue = Mathf.Clamp(EditedDefaultIntValue, 0, TextSettings.Languages.Count - 1);
            }

            CheckOptionEntryEditorDirty();

            EditorGUILayout.Space();

            GUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();

            if (EditingNew)
            {
                if (ApplyButton(EditedName, "", new(OptionSettings.OptionEntries), true)) SaveNewOptionEntry();

                if (GUILayout.Button("Cancel", GUILayout.Height(ContentLibrary.RowHeight))) ClearOptionEntryEditor();

                GUILayout.EndHorizontal();

                FocusClearArea();
            }
            else
            {
                GUI.enabled = EditorDirty;

                if (GUILayout.Button("Revert", GUILayout.Height(ContentLibrary.RowHeight))) RevertEditedOptionEntry();

                if (ApplyButton(EditedName, InitialEditedName, new(OptionSettings.OptionEntries), false))
                    SaveEditedOptionEntry();

                GUI.enabled = true;

                GUILayout.EndHorizontal();

                FocusClearArea();

                if (EditorBackButton()) ClearOptionEntryEditor();
            }
        }
        void LocalizationSetSelector()
        {
            LocalizationSetSearchBar(TextSettings.LocalizationSets, false);

            if (VisibleLocalizationSets.Count > 0)
            {
                if (VisibleLocalizationSets.Count > ContentLibrary.ItemsPerPageValues[0]) Pager(VisibleLocalizationSets.Count);
                else
                {
                    EditorGUILayout.Space();

                    PageDisplayStartIndex = 0;
                    PageDisplayEndIndex = VisibleLocalizationSets.Count;
                }

                BeginScrollArea();

                for (int i = PageDisplayStartIndex; i < PageDisplayEndIndex; i++)
                {
                    GUI.enabled = VisibleLocalizationSets[i] != EditedLocalizedOptions[SelectedLocalizationSetIndex];

                    if (GUILayout.Button(VisibleLocalizationSets[i].Name))
                    {
                        EditedLocalizedOptions[SelectedLocalizationSetIndex] = VisibleLocalizationSets[i];

                        SelectingLocalizationSet = false;

                        EndScrollArea();
                        break;
                    }

                    GUI.enabled = true;
                }

                EndScrollArea();

                FocusClearArea();

                if (GUILayout.Button("Cancel")) SelectingLocalizationSet = false;
            }
        }
        void CheckOptionEntryEditorDirty()
        {
            if (EditorDirty) return;

            if (EditedName != InitialEditedName)
            {
                EditorDirty = true;
                return;
            }

            if (EditedType != InitialEditedType)
            {
                EditorDirty = true;
                return;
            }

            if (EditedPrefab != InitialEditedPrefab)
            {
                EditorDirty = true;
                return;
            }

            if (EditedValueFormat != InitialEditedValueFormat)
            {
                EditorDirty = true;
                return;
            }

            if (EditedDefaultFloatValue != InitialEditedDefaultFloatValue)
            {
                EditorDirty = true;
                return;
            }

            if (EditedDefaultBoolValue != InitialEditedDefaultBoolValue)
            {
                EditorDirty = true;
                return;
            }

            if (EditedDefaultIntValue != InitialEditedDefaultIntValue)
            {
                EditorDirty = true;
                return;
            }

            if (EditedType == OptionType.Float)
            {
                if (EditedClamp != InitialEditedClamp)
                {
                    EditorDirty = true;
                    return;
                }
                if (EditedClampMin != InitialEditedClampMin)
                {
                    EditorDirty = true;
                    return;
                }
                if (EditedClampMax != InitialEditedClampMax)
                {
                    EditorDirty = true;
                    return;
                }
            }
            else if (EditedType == OptionType.List)
            {
                if (EditedOptions.Count != InitialEditedOptions.Count)
                {
                    EditorDirty = true;
                    return;
                }
                if (EditedLocalizedOptions.Count != InitialEditedLocalizedOptions.Count)
                {
                    EditorDirty = true;
                    return;
                }
                if (OptionSettings.Localized)
                {
                    for (int i = 0; i < EditedLocalizedOptions.Count; i++)
                    {
                        if (EditedLocalizedOptions[i] != InitialEditedLocalizedOptions[i])
                        {
                            EditorDirty = true;
                            return;
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < EditedOptions.Count; i++)
                    {
                        if (EditedOptions[i] != InitialEditedOptions[i])
                        {
                            EditorDirty = true;
                            return;
                        }
                    }
                }
            }
        }
        void RevertEditedOptionEntry()
        {
            InitializeOptionEntryEditor(EditedOptionEntry);
            GUI.FocusControl(null);
            EditorDirty = false;
        }
        void SaveEditedOptionEntry()
        {
            EditingNew = false;
            EditorDirty = false;

            EditedOptionEntry.Name = EditedName;
            EditedOptionEntry.LocalizedName = EditedLocalizedName;
            EditedOptionEntry.Type = EditedType;
            EditedOptionEntry.Prefab = EditedPrefab;
            EditedOptionEntry.ValueFormat = EditedValueFormat;
            EditedOptionEntry.Clamp = EditedClamp;
            EditedOptionEntry.ClampMin = EditedClampMin;
            EditedOptionEntry.ClampMax = EditedClampMax;
            EditedOptionEntry.Options = new(EditedOptions);
            EditedOptionEntry.LocalizedOptions = new(EditedLocalizedOptions);
            EditedOptionEntry.DefaultFloatValue = EditedDefaultFloatValue;
            EditedOptionEntry.DefaultBoolValue = EditedDefaultBoolValue;
            EditedOptionEntry.DefaultIntValue = EditedDefaultIntValue;
            switch (EditedOptionEntry.Type)
            {
                case OptionType.ColorProfile:
                    if (ColorSettings.ColorProfiles.Count > 0) EditedOptionEntry.DefaultNamedAssetValue = ColorSettings.ColorProfiles[EditedDefaultIntValue];
                    break;
                case OptionType.ScaleProfile:
                    if (ScaleSettings.ScaleProfiles.Count > 0) EditedOptionEntry.DefaultNamedAssetValue = ScaleSettings.ScaleProfiles[EditedDefaultIntValue];
                    break;
                case OptionType.Language:
                    if (TextSettings.Languages.Count > 0) EditedOptionEntry.DefaultNamedAssetValue = TextSettings.Languages[EditedDefaultIntValue];
                    break;
            }

            ApplyChanges();

            GUI.FocusControl(null);

            InitializeOptionEntryEditor(EditedOptionEntry);
        }
        void SaveNewOptionEntry()
        {
            OptionEntry optionEntry = CreateInstance<OptionEntry>();
            optionEntry.Name = EditedName;
            optionEntry.LocalizedName = EditedLocalizedName;
            optionEntry.Type = EditedType;
            optionEntry.Prefab = null;
            optionEntry.ValueFormat = EditedValueFormat;
            optionEntry.Clamp = EditedClamp;
            optionEntry.ClampMin = EditedClampMin;
            optionEntry.ClampMax = EditedClampMax;
            optionEntry.Options = new(EditedOptions);
            optionEntry.LocalizedOptions = EditedLocalizedOptions;
            optionEntry.DefaultFloatValue = EditedDefaultFloatValue;
            optionEntry.DefaultBoolValue = EditedDefaultBoolValue;
            optionEntry.DefaultIntValue = EditedDefaultIntValue;
            switch (optionEntry.Type)
            {
                case OptionType.ColorProfile:
                    if (ColorSettings.ColorProfiles.Count > 0) optionEntry.DefaultNamedAssetValue = ColorSettings.ColorProfiles[EditedDefaultIntValue];
                    break;
                case OptionType.ScaleProfile:
                    if (ScaleSettings.ScaleProfiles.Count > 0) optionEntry.DefaultNamedAssetValue = ScaleSettings.ScaleProfiles[EditedDefaultIntValue];
                    break;
                case OptionType.Language:
                    if (TextSettings.Languages.Count > 0) optionEntry.DefaultNamedAssetValue = TextSettings.Languages[EditedDefaultIntValue];
                    break;
            }

            OptionSettings.OptionEntries.Add(optionEntry);

            AssetDatabase.AddObjectToAsset(optionEntry, OptionSettings);

            ApplyChanges();

            SetAssetNames(new(OptionSettings.OptionEntries), typeof(OptionEntry));

            AssetDatabase.SaveAssets();

            ClearOptionEntryEditor();
        }
        void ClearOptionEntryEditor()
        {
            EditingNew = false;
            EditorDirty = false;

            EditedName = "";
            EditedLocalizedName = null;
            EditedType = OptionType.Float;
            EditedPrefab = null;
            EditedValueFormat = "";
            EditedClamp = false;
            EditedClampMin = 0;
            EditedClampMax = 0;
            EditedOptions = new();
            EditedLocalizedOptions = new();
            EditedDefaultFloatValue = 0;
            EditedDefaultBoolValue = false;
            EditedDefaultIntValue = 0;

            EditedOptionEntry = null;

            GUI.FocusControl(null);
        }
        #endregion
    }
}