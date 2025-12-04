using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.Networking;
using UnityEngine;
using UnityEditor;
using System.Linq;

namespace UIAssistant
{
    public class TextSettingsWindow : SettingsWindow
    {
        #region Variables
        readonly GUIContent ActiveLanguageContent = new("Active Language", "Determines which localizations will be used to override localized texts.");
        readonly GUIContent NewTextStyleContent = new("New Text Style");
        readonly GUIContent NewLanguageContent = new("New Language");
        readonly GUIContent NewLocalizationSetContent = new("New Localization Set");
        readonly GUIContent CopyLocalizationSetsContent = new("Copy Localization Sets", "Copy all localization data to the clipboard.");
        readonly GUIContent PasteLocalizationSetsContent = new("Paste Localization Sets", "Paste new, or update existing Localization Sets using data copied from a localization sheet.");
        readonly GUIContent ImportLocalizationSetsContent = new("Import Localization Sets", "Import new, or update existing Localization Sets using data in the referenced Google Sheet.");
        readonly GUIContent LocalizationURLContent = new("Localization URL", "URL of the Google Sheets document that contains localization data to be imported.");
        readonly GUIContent RemoveLocalizationSetsContent = new("Remove Localization Sets", "Remove multiple Localization Sets at once, based on your selection.");
        readonly GUIContent KeyContent = new("Key", "Unique identifier of the Localization Set.");
        readonly GUIContent FontAssetContent = new("Font Asset", "The font asset containing the glyphs that can be rendered for the texts.");
        readonly GUIContent FontSizeContent = new("Font Size", "The size the texts will be rendered at in points.");
        readonly GUIContent MinFontSizeContent = new("Min Font Size", "The minimum font size when auto size is enabled for the text.");
        readonly GUIContent MaxFontSizeContent = new("Max Font Size", "The maximum font size when auto size is enabled for the text.");
        readonly GUIContent ApplyToAllContent = new("Apply to All Languages", "Copies this Language's font size values to every other Language.");
        readonly GUIContent UpdateLocalizablesContent = new("Update Localizables", "Applies current settings to all relevant Game Objects in the open scene.");

        static TextSettingsWindow Window;

        Tab SelectedTab;

        TextStyle EditedTextStyle;
        LocalizationSet EditedLocalizationSet;
        bool[] EditorLanguageFoldout;

        string EditedName;
        List<TextStyle.LanguageStyle> EditedStyles = new();
        List<LocalizationSet.Localization> EditedLocalizations = new();

        string InitialEditedName;
        List<TextStyle.LanguageStyle> InitialEditedStyles = new();
        List<LocalizationSet.Localization> InitialEditedLocalizations = new();

        string fetchedGoogleSheetData;
        #endregion

        #region Enums
        enum Tab
        {
            Styles = 0,
            Languages = 1,
            Dictionary = 2,
        }
        #endregion

        #region Function
        [MenuItem("Tools/UI Assistant/Text Settings", priority = 4)]
        public static void OpenWindow()
        {
            GetWindow<TextSettingsWindow>("Text Settings");
        }
        protected override void OnGUI()
        {
            base.OnGUI();

            ValidateTextStyles();
            ValidateLocalizationSets();

            if (Window == null) Window = this;

            if (TextSettings.Languages.Count == 0) CreateLanguage(true);
            if (TextSettings.TextStyles.Count == 0) CreateTextStyle(true);

            if (TextSettings.ActiveLanguage == null) TextSettings.SetLanguage(0);

            EditorGUI.BeginChangeCheck();

            if (EditedTextStyle == null && EditedLocalizationSet == null && !EditingNew)
                SelectedTab = (Tab)GUILayout.Toolbar((int)SelectedTab, System.Enum.GetNames(typeof(Tab)));

            switch (SelectedTab)
            {
                case Tab.Styles:
                    if (EditingNew || EditedTextStyle != null) TextStyleEditor();
                    else TextStyles();
                    if (Return) return;
                    else break;
                case Tab.Languages:
                    Languages();
                    if (Return) return;
                    else break;
                case Tab.Dictionary:
                    if (EditingNew || EditedLocalizationSet != null) LocalizationSetEditor();
                    else LocalizationSets();
                    if (Return) return;
                    else break;
            }

            if (EditorGUI.EndChangeCheck()) ApplyChanges();
        }

        protected override void ApplyChanges()
        {
            ApplyChanges(TextSettings);
            ApplyChanges(new List<Object>(TextSettings.TextStyles));
            ApplyChanges(new List<Object>(TextSettings.Languages));
        }

        void ValidateTextStyles()
        {
            if (TextSettings.Languages.Count == 0) return;

            for (int i = 0; i < TextSettings.TextStyles.Count; i++)
            {
                TextSettings.TextStyles[i].ValidateStyle();
            }
        }
        void TextStyles()
        {
            EditorGUILayout.Space();

            BeginScrollArea();

            for (int i = 0; i < TextSettings.TextStyles.Count; i++)
            {
                TextStyle(i);

                if (Return)
                {
                    GUILayout.EndHorizontal();
                    break;
                }
            }

            EndScrollArea();

            EditorGUILayout.Space();

            if (AddButton(NewTextStyleContent)) InitializeTextStyleCreator();

            FocusClearArea();

            UpdateTextStylersButton();
        }
        void TextStyle(int index)
        {
            GUILayout.BeginHorizontal();

            if (MoveUpButton) MoveListItem(TextSettings.TextStyles, index, -1);

            if (MoveDownButton) MoveListItem(TextSettings.TextStyles, index, 1);

            GUILayout.Label(TextSettings.TextStyles[index].Name, BoxedLabelStyle, GUILayout.Height(ContentLibrary.RowHeight));

            if (EditButton) InitializeTextStyleEditor(TextSettings.TextStyles[index], true);

            if (DeleteButton)
            {
                if (DeleteDialog(TextSettings.TextStyles[index], "text style")) DeleteTextStyle(index);
            }

            GUILayout.EndHorizontal();
        }
        void CreateTextStyle(bool autoCreate)
        {
            TextStyle textStyle = CreateInstance<TextStyle>();

            textStyle.Name = "New Text Style";
            textStyle.LocalizedName = null;
            textStyle.Styles = new();

            TextSettings.TextStyles.Add(textStyle);

            AssetDatabase.AddObjectToAsset(textStyle, TextSettings);

            EditorUtility.SetDirty(TextSettings);
            EditorUtility.SetDirty(textStyle);

            SetAssetNames(new(TextSettings.TextStyles), typeof(TextStyle));

            ValidateTextStyles();
            ApplyChanges();
            AssetDatabase.SaveAssets();
            if (autoCreate) AutoCreateMessage(textStyle);
        }
        void DeleteTextStyle(int index)
        {
            DestroyImmediate(TextSettings.TextStyles[index], true);
            TextSettings.TextStyles.RemoveAt(index);

            SetAssetNames(new(TextSettings.TextStyles), typeof(TextStyle));

            AssetDatabase.SaveAssets();

            Return = true;
        }

        void InitializeTextStyleEditor(TextStyle textStyle, bool resetFoldout)
        {
            if (textStyle == null)
            {
                textStyle = TextSettings.TextStyles.Count > 0 ? TextSettings.TextStyles[0] : null;
                if (textStyle == null) return;
            }

            if (resetFoldout) EditorLanguageFoldout = new bool[TextSettings.Languages.Count];
            if (TextSettings.Languages.Count == 1) EditorLanguageFoldout[0] = true;

            EditedTextStyle = textStyle;

            EditedName = EditedTextStyle.Name;
            EditedStyles = new();
            for (int i = 0; i < EditedTextStyle.Styles.Count; i++)
            {
                TextStyle.LanguageStyle languageStyle = new()
                {
                    Language = EditedTextStyle.Styles[i].Language,
                    FontAsset = EditedTextStyle.Styles[i].FontAsset,
                    Size = EditedTextStyle.Styles[i].Size,
                    MinSize = EditedTextStyle.Styles[i].MinSize,
                    MaxSize = EditedTextStyle.Styles[i].MaxSize,
                };
                EditedStyles.Add(languageStyle);
            }

            InitialEditedName = EditedName;
            InitialEditedStyles = new();
            for (int i = 0; i < EditedStyles.Count; i++)
            {
                TextStyle.LanguageStyle languageStyle = new()
                {
                    Language = EditedStyles[i].Language,
                    FontAsset = EditedStyles[i].FontAsset,
                    Size = EditedStyles[i].Size,
                    MinSize = EditedStyles[i].MinSize,
                    MaxSize = EditedStyles[i].MaxSize,
                };
                InitialEditedStyles.Add(languageStyle);
            }
        }
        void InitializeTextStyleCreator()
        {
            EditorLanguageFoldout = new bool[TextSettings.Languages.Count];
            if (TextSettings.Languages.Count == 1) EditorLanguageFoldout[0] = true;

            EditedName = "New Text Style";
            EditedStyles = new();

            for (int i = 0; i < TextSettings.Languages.Count; i++)
            {
                TextStyle.LanguageStyle languageStyle = new();
                languageStyle.Language = TextSettings.Languages[i];
                EditedStyles.Add(languageStyle);
            }

            EditingNew = true;
        }
        void TextStyleEditor()
        {
            if (EditedStyles.Count == 0) InitializeTextStyleEditor(EditedTextStyle, true);

            bool showFoldout = TextSettings.Languages.Count > 1;

            EditedName = EditorGUILayout.TextField(NameContent, EditedName, GUILayout.Height(ContentLibrary.RowHeight));

            EditorGUILayout.Space();

            BeginScrollArea();

            for (int i = 0; i < EditedStyles.Count; i++)
            {
                if (i > 0) GUILayout.Space(3);

                if (showFoldout)
                    EditorLanguageFoldout[i] = EditorGUILayout.BeginFoldoutHeaderGroup(EditorLanguageFoldout[i], EditedStyles[i].Language.Name);

                if (EditorLanguageFoldout[i])
                {
                    GUI.contentColor = Color.white;
                    EditedStyles[i].FontAsset = EditorGUILayout.ObjectField(FontAssetContent, EditedStyles[i].FontAsset, typeof(TMPro.TMP_FontAsset), false, GUILayout.Height(ContentLibrary.RowHeight)) as TMPro.TMP_FontAsset;
                    GUI.contentColor = ContentLibrary.ContentColor;

                    EditorGUILayout.Space();

                    EditedStyles[i].Size = EditorGUILayout.FloatField(FontSizeContent, EditedStyles[i].Size, GUILayout.Height(ContentLibrary.RowHeight));
                    if (EditedStyles[i].Size < 0) EditedStyles[i].Size = 0;
                    EditedStyles[i].MinSize = EditorGUILayout.FloatField(MinFontSizeContent, EditedStyles[i].MinSize, GUILayout.Height(ContentLibrary.RowHeight));
                    if (EditedStyles[i].MinSize > EditedStyles[i].MaxSize) EditedStyles[i].MinSize = EditedStyles[i].MaxSize;
                    EditedStyles[i].MaxSize = EditorGUILayout.FloatField(MaxFontSizeContent, EditedStyles[i].MaxSize, GUILayout.Height(ContentLibrary.RowHeight));
                    if (EditedStyles[i].MaxSize < EditedStyles[i].MinSize) EditedStyles[i].MaxSize = EditedStyles[i].MinSize;

                    if (showFoldout)
                    {
                        if (GUILayout.Button(ApplyToAllContent, GUILayout.Height(ContentLibrary.RowHeight)))
                        {
                            for (int j = 0; j < EditedStyles.Count; j++)
                            {
                                if (j != i)
                                {
                                    EditedStyles[j].Size = EditedStyles[i].Size;
                                    EditedStyles[j].MinSize = EditedStyles[i].MinSize;
                                    EditedStyles[j].MaxSize = EditedStyles[i].MaxSize;
                                }
                            }
                        }
                    }

                    if (i < EditedStyles.Count - 1) EditorGUILayout.Space();
                }

                if (showFoldout) EditorGUILayout.EndFoldoutHeaderGroup();
            }

            EndScrollArea();

            CheckTextStyleEditorDirty();

            EditorGUILayout.Space();

            GUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();

            if (EditingNew)
            {
                if (ApplyButton(EditedName, "", new(TextSettings.TextStyles), true)) SaveNewTextStyle();

                if (GUILayout.Button("Cancel", GUILayout.Height(ContentLibrary.RowHeight))) ClearTextStyleEditor();
               
                GUILayout.EndHorizontal();

                FocusClearArea();
            }
            else
            {
                GUI.enabled = EditorDirty;

                if (GUILayout.Button("Revert", GUILayout.Height(ContentLibrary.RowHeight))) RevertEditedTextStyle();

                if (ApplyButton(EditedName, InitialEditedName, new(TextSettings.TextStyles), false))
                    SaveEditedTextStyle();

                GUI.enabled = true;

                GUILayout.EndHorizontal();

                FocusClearArea();

                if (EditorBackButton()) ClearTextStyleEditor();
            }
        }
        void CheckTextStyleEditorDirty()
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

            for (int i = 0; i < EditedStyles.Count; i++)
            {
                if (EditedStyles[i].FontAsset != InitialEditedStyles[i].FontAsset)
                {
                    EditorDirty = true;
                    return;
                }
                if (EditedStyles[i].Size != InitialEditedStyles[i].Size)
                {
                    EditorDirty = true;
                    return;
                }
                if (EditedStyles[i].MinSize != InitialEditedStyles[i].MinSize)
                {
                    EditorDirty = true;
                    return;
                }
                if (EditedStyles[i].MaxSize != InitialEditedStyles[i].MaxSize)
                {
                    EditorDirty = true;
                    return;
                }
            }
        }
        void RevertEditedTextStyle()
        {
            InitializeTextStyleEditor(EditedTextStyle, false);
            GUI.FocusControl(null);
            EditorDirty = false;
        }
        void SaveEditedTextStyle()
        {
            EditingNew = false;
            EditorDirty = false;

            EditedTextStyle.Name = EditedName;

            for (int i = 0; i < EditedTextStyle.Styles.Count; i++)
            {
                EditedTextStyle.Styles[i].FontAsset = EditedStyles[i].FontAsset;
                EditedTextStyle.Styles[i].Size = EditedStyles[i].Size;
                EditedTextStyle.Styles[i].MinSize = EditedStyles[i].MinSize;
                EditedTextStyle.Styles[i].MaxSize = EditedStyles[i].MaxSize;
            }

            ApplyChanges();

            GUI.FocusControl(null);

            InitializeTextStyleEditor(EditedTextStyle, false);
        }
        void SaveNewTextStyle()
        {
            TextStyle textStyle = CreateInstance<TextStyle>();
            textStyle.Name = EditedName;
            textStyle.Styles = new(EditedStyles);

            TextSettings.TextStyles.Add(textStyle);

            AssetDatabase.AddObjectToAsset(textStyle, TextSettings);

            ApplyChanges();

            SetAssetNames(new(TextSettings.TextStyles), typeof(TextStyle));

            AssetDatabase.SaveAssets();

            ClearTextStyleEditor();
        }
        void ClearTextStyleEditor()
        {
            EditingNew = false;
            EditorDirty = false;

            EditedName = "";
            EditedStyles.Clear();

            EditedTextStyle = null;

            GUI.FocusControl(null);
        }

        void Languages()
        {
            EditorGUILayout.Space();

            int activeIndex = TextSettings.Languages.IndexOf(TextSettings.ActiveLanguage);
            int initialActiveIndex = activeIndex;
            activeIndex = EditorGUILayout.Popup(ActiveLanguageContent, activeIndex, TextSettings.LanguageNames);

            if (activeIndex != initialActiveIndex)
            {
                TextSettings.SetLanguage(activeIndex);
            }

            EditorGUILayout.Space();

            BeginScrollArea();

            for (int i = 0; i < TextSettings.Languages.Count; i++)
            {
                Language(i);

                if (Return)
                {
                    GUILayout.EndHorizontal();
                    break;
                }
            }

            EndScrollArea();

            GUI.contentColor = Color.white;
            EditorGUILayout.HelpBox("Left column: the name that will appear in the editor.\nRight column: the name of the language in that language, made visible to the user.", MessageType.Info);
            GUI.contentColor = ContentLibrary.ContentColor;

            EditorGUILayout.Space();

            if (AddButton(NewLanguageContent)) CreateLanguage(false);

            FocusClearArea();

            UpdateTextStylersButton();
        }
        void Language(int index)
        {
            GUILayout.BeginHorizontal();

            if (MoveUpButton)
            {
                MoveListItem(TextSettings.Languages, index, -1);
                ValidateTextStyles();
                ValidateLocalizationSets();
            }

            if (MoveDownButton)
            {
                MoveListItem(TextSettings.Languages, index, 1);
                ValidateTextStyles();
                ValidateLocalizationSets();
            }

            TextSettings.Languages[index].Name = EditorGUILayout.TextField(TextSettings.Languages[index].Name,
                GUILayout.Height(ContentLibrary.RowHeight));

            TextSettings.Languages[index].SimpleLocalizedName = EditorGUILayout.TextField(TextSettings.Languages[index].SimpleLocalizedName,
                GUILayout.Height(ContentLibrary.RowHeight));

            if (DeleteButton)
            {
                if (DeleteDialog(TextSettings.Languages[index], "language")) DeleteLanguage(index);
            }

            GUILayout.EndHorizontal();
        }
        void CreateLanguage(bool autoCreate)
        {
            Language language = CreateInstance<Language>();

            language.Name = "New Language";
            language.SimpleLocalizedName = "Localized Language Name";

            TextSettings.Languages.Add(language);
            TextSettings.OnLanguageAdded();

            AssetDatabase.AddObjectToAsset(language, TextSettings);

            SetAssetNames(new(TextSettings.Languages), typeof(Language));

            ValidateTextStyles();
            ValidateLocalizationSets();
            ApplyChanges();
            AssetDatabase.SaveAssets();
            if (autoCreate) AutoCreateMessage(language);
        }
        void DeleteLanguage(int index)
        {
            DestroyImmediate(TextSettings.Languages[index], true);
            TextSettings.Languages.RemoveAt(index);

            TextSettings.OnLanguageRemoved();
            SetAssetNames(new(TextSettings.Languages), typeof(Language));

            AssetDatabase.SaveAssets();

            ValidateTextStyles();
            ValidateLocalizationSets();
            ApplyChanges();

            Return = true;
        }

        void ValidateLocalizationSets()
        {
            for (int i = 0; i < TextSettings.LocalizationSets.Count; i++)
            {
                TextSettings.LocalizationSets[i].ValidateSet();
            }
        }
        void LocalizationSets()
        {
            EditorGUILayout.Space();

            TextSettings.LocalizationURL = EditorGUILayout.TextField(LocalizationURLContent, TextSettings.LocalizationURL, GUILayout.Height(ContentLibrary.RowHeight));

            EditorGUILayout.Space();

            if (TextSettings.LocalizationSets.Count > 0)
            {
                EditorGUILayout.Space();
                LocalizationSetSearchBar(TextSettings.LocalizationSets, true);
            }

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
                    LocalizationSet(i);

                    if (Return)
                    {
                        GUILayout.EndHorizontal();
                        break;
                    }
                }

                EndScrollArea();
            }

            EditorGUILayout.Space();

            if (AddButton(NewLocalizationSetContent)) InitializeLocalizationSetCreator();

            if (AddButton(CopyLocalizationSetsContent)) CopyLocalizations();

            if (AddButton(PasteLocalizationSetsContent)) InitializeLocalizationImport(GUIUtility.systemCopyBuffer, "\t");

            if (AddButton(ImportLocalizationSetsContent))
            {
                FetchGoogleSheetData();
            }

            GUI.backgroundColor = ContentLibrary.WarningColor;
            GUI.contentColor = Color.white;

            GUI.enabled = TextSettings.LocalizationSets.Count > 0;

            if (RemoveButton(RemoveLocalizationSetsContent)) InitializeLocalizationRemoval();

            GUI.enabled = true;

            GUI.backgroundColor = Color.white;
            GUI.contentColor = ContentLibrary.ContentColor;

            FocusClearArea();

            UpdateTextStylersButton();
        }
        void LocalizationSet(int index)
        {
            LocalizationSet localizationSet = VisibleLocalizationSets[index];

            GUILayout.BeginHorizontal();

            if (localizationSet.MissingLocalization) Warning("One or multiple localizations missing.");

            GUILayout.Label(localizationSet.Name, BoxedLabelStyle, GUILayout.Height(ContentLibrary.RowHeight));

            if (EditButton) InitializeLocalizationSetEditor(localizationSet);

            if (DeleteButton)
            {
                if (DeleteDialog(localizationSet, "localization set")) DeleteLocalizationSet(index);
            }

            GUILayout.EndHorizontal();
        }
        void DeleteLocalizationSet(int index)
        {
            LocalizationSet localizationSet = VisibleLocalizationSets[index];

            TextSettings.LocalizationSets.Remove(localizationSet);
            DestroyImmediate(localizationSet, true);

            SetAssetNames(new(TextSettings.LocalizationSets), typeof(LocalizationSet));

            AssetDatabase.SaveAssets();

            SetVisibleLocalizationSets(TextSettings.LocalizationSets);

            Return = true;
        }
        async void FetchGoogleSheetData()
        {
            fetchedGoogleSheetData = await GetLocalizationTSV();
            if (fetchedGoogleSheetData != null)
            {
                InitializeLocalizationImport(fetchedGoogleSheetData, "\t");
                fetchedGoogleSheetData = string.Empty;
            }
        }
        async Task<string> GetLocalizationTSV()
        {
            using UnityWebRequest www = UnityWebRequest.Get(TextSettings.LocalizationURL);
            UnityWebRequestAsyncOperation operation = www.SendWebRequest();

            while (!operation.isDone) await Task.Yield();

            if (www.result != UnityWebRequest.Result.Success)
            {
                EditorUtility.DisplayDialog("Error Fetching Data", www.error, "OK");
                return null;
            }
            return www.downloadHandler.text;
        }
        void SortLocalizationSets()
        {
            TextSettings.LocalizationSets.Sort((A, B) => A.Name.CompareTo(B.Name));
            SetVisibleLocalizationSets(TextSettings.LocalizationSets);
        }

        void InitializeLocalizationSetEditor(LocalizationSet localizationSet)
        {
            EditedLocalizationSet = localizationSet;

            EditedName = EditedLocalizationSet.Name;

            EditedLocalizations = new();

            for (int i = 0; i < EditedLocalizationSet.Localizations.Count; i++)
            {
                LocalizationSet.Localization localization = new()
                {
                    Text = EditedLocalizationSet.Localizations[i].Text,
                };
                EditedLocalizations.Add(localization);
            }

            InitialEditedName = EditedName;
            InitialEditedLocalizations = new();
            for (int i = 0; i < EditedLocalizations.Count; i++)
            {
                LocalizationSet.Localization localization = new()
                {
                    Text = EditedLocalizations[i].Text,
                };
                InitialEditedLocalizations.Add(localization);
            }
        }
        void InitializeLocalizationSetCreator()
        {
            EditedName = "New Localization Set";
            EditedLocalizations = new();

            for (int i = 0; i < TextSettings.Languages.Count; i++)
            {
                LocalizationSet.Localization localization = new();
                localization.Language = TextSettings.Languages[i];
                EditedLocalizations.Add(localization);
            }

            EditingNew = true;
        }
        void LocalizationSetEditor()
        {
            if (EditedLocalizations.Count == 0) InitializeLocalizationSetEditor(EditedLocalizationSet);

            EditedName = EditorGUILayout.TextField(KeyContent, EditedName, GUILayout.Height(ContentLibrary.RowHeight));

            EditorGUILayout.Space();

            BeginScrollArea();

            for (int i = 0; i < TextSettings.Languages.Count; i++)
            {
                if (i > 0) EditorGUILayout.Space();

                GUILayout.Label(TextSettings.Languages[i].Name, MiddleCenterStyle, GUILayout.Height(ContentLibrary.RowHeight));

                EditedLocalizations[i].Text = EditorGUILayout.TextArea(EditedLocalizations[i].Text, TextAreaStyle);
            }

            EndScrollArea();

            EditorGUILayout.Space();

            GUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();

            if (EditingNew)
            {
                if (ApplyButton(EditedName, "", new(TextSettings.LocalizationSets), true)) SaveNewLocalizationSet();

                if (GUILayout.Button("Cancel", GUILayout.Height(ContentLibrary.RowHeight))) ClearLocalizationSetEditor();

                GUILayout.EndHorizontal();

                FocusClearArea();
            }
            else
            {
                CheckLocalizationSetEditorDirty();

                GUI.enabled = EditorDirty;

                if (GUILayout.Button("Revert", GUILayout.Height(ContentLibrary.RowHeight))) RevertEditedLocalizationSet();

                if (ApplyButton(EditedName, InitialEditedName, new(TextSettings.LocalizationSets), false))
                    SaveEditedLocalizationSet();

                GUI.enabled = true;

                GUILayout.EndHorizontal();

                FocusClearArea();

                if (EditorBackButton()) ClearLocalizationSetEditor();
            }
        }
        void CheckLocalizationSetEditorDirty()
        {
            if (EditorDirty) return;

            if (EditedName != InitialEditedName)
            {
                EditorDirty = true;
                return;
            }

            for (int i = 0; i < EditedLocalizations.Count; i++)
            {
                if (EditedLocalizations[i].Text != InitialEditedLocalizations[i].Text)
                {
                    EditorDirty = true;
                    return;
                }
            }
        }
        void RevertEditedLocalizationSet()
        {
            InitializeLocalizationSetEditor(EditedLocalizationSet);
            GUI.FocusControl(null);
            EditorDirty = false;
        }
        void SaveEditedLocalizationSet()
        {
            EditingNew = false;
            EditorDirty = false;

            EditedLocalizationSet.Name = EditedName;

            for (int i = 0; i < EditedLocalizationSet.Localizations.Count; i++)
            {
                EditedLocalizationSet.Localizations[i].Text = EditedLocalizations[i].Text;
            }

            ApplyChanges();

            EditedLocalizationSet.ValidateSet();

            SortLocalizationSets();

            GUI.FocusControl(null);

            InitializeLocalizationSetEditor(EditedLocalizationSet);
        }
        void SaveNewLocalizationSet()
        {
            LocalizationSet localizationSet = CreateInstance<LocalizationSet>();
            localizationSet.Name = EditedName;
            localizationSet.Localizations = new(EditedLocalizations);

            TextSettings.LocalizationSets.Add(localizationSet);

            AssetDatabase.AddObjectToAsset(localizationSet, TextSettings);

            ApplyChanges();

            localizationSet.ValidateSet();

            SetAssetNames(new(TextSettings.LocalizationSets), typeof(LocalizationSet));
            SortLocalizationSets();

            AssetDatabase.SaveAssets();

            ClearLocalizationSetEditor();
        }
        void ClearLocalizationSetEditor()
        {
            EditingNew = false;

            EditedName = "";
            EditedLocalizations.Clear();

            EditedLocalizationSet = null;

            GUI.FocusControl(null);
        }

        void InitializeLocalizationImport(string text, string splitChar)
        {
            int activeIndex = TextSettings.Languages.IndexOf(TextSettings.ActiveLanguage);

            List<string> rows = new(text.Split(System.Environment.NewLine));

            int lastRowIndex = rows.Count - 1;
            if (rows[lastRowIndex] == "") rows.RemoveAt(lastRowIndex);

            List<string> firstRowContent = new(rows[0].Split(splitChar));
            firstRowContent.RemoveAt(0);
            if (TextSettings.Languages.Count != firstRowContent.Count)
            {
                EditorUtility.DisplayDialog("Format Error", "Unable to import localization data. Please refer to the template for the correct format.", "OK");
                return;
            }
            bool exclueFirstRow = true;
            for (int i = 0; i < firstRowContent.Count; i++)
            {
                if (firstRowContent[i] != TextSettings.Languages[i].Name)
                {
                    exclueFirstRow = false;
                    break;
                }
            }
            if (exclueFirstRow) rows.RemoveAt(0);

            if (rows.Count == 0)
            {
                EditorUtility.DisplayDialog("Import Failed", "No localization data was found.", "OK");
                return;
            }

            List<LocalizationImporter.ImportedLocalizationSet> importList = new();

            int skippedRowCount = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                List<string> localizations = new(rows[i].Split(splitChar));

                if (localizations[0] == string.Empty)
                {
                    skippedRowCount++;
                    continue;
                }

                LocalizationImporter.ImportedLocalizationSet importData = new()
                {
                    Selected = true,
                    Name = localizations[0],
                    Localizations = new(),
                    ActiveIndex = activeIndex,
                };

                for (int j = 0; j < TextSettings.Languages.Count; j++)
                {
                    LocalizationSet.Localization localization = new()
                    {
                        Language = TextSettings.Languages[j],
                        Text = localizations[j],
                    };

                    importData.Localizations.Add(localization);
                }

                importList.Add(importData);
            }

            if (skippedRowCount == rows.Count)
            {
                EditorUtility.DisplayDialog("Import Failed", "No localization data was found.", "OK");
                return;
            }

            importList.Sort((A, B) => A.Name.CompareTo(B.Name));

            LocalizationImporter.OpenImportWindow(importList);
        }
        public static void ImportLocalizationSets(List<LocalizationImporter.ImportedLocalizationSet> importList)
        {
            for (int i = importList.Count - 1; i >= 0; i--)
            {
                LocalizationSet localizationSet = Window.TextSettings.GetLocalizationSet(importList[i].Name);

                if (localizationSet != null)
                {
                    localizationSet.Localizations = new(importList[i].Localizations);
                    localizationSet.ValidateSet();
                    importList.RemoveAt(i);
                }
            }

            for (int i = 0; i < importList.Count; i++)
            {
                LocalizationSet localizationSet = CreateInstance<LocalizationSet>();
                localizationSet.Name = importList[i].Name;
                localizationSet.Localizations = new(importList[i].Localizations);

                localizationSet.ValidateSet();

                Window.TextSettings.LocalizationSets.Add(localizationSet);

                AssetDatabase.AddObjectToAsset(localizationSet, Window.TextSettings);
            }

            Window.ApplyChanges();

            Window.SortLocalizationSets();
            Window.SetAssetNames(new(Window.TextSettings.LocalizationSets), typeof(LocalizationSet));

            AssetDatabase.SaveAssets();
        }
        void InitializeLocalizationRemoval()
        {
            List<LocalizationImporter.RemovableLocalizationSet> removeList = new();

            for (int i = 0; i < TextSettings.LocalizationSets.Count; i++)
            {
                LocalizationImporter.RemovableLocalizationSet removeData = new()
                {
                    Selected = false,
                    LocalizationSet = TextSettings.LocalizationSets[i],
                };

                removeList.Add(removeData);
            }

            LocalizationImporter.OpenRemoveWindow(removeList);
        }
        public static void RemoveLocalizationSets(List<LocalizationImporter.RemovableLocalizationSet> removeList)
        {
            for (int i = 0; i < removeList.Count; i++)
            {
                if (removeList[i].Selected) DestroyImmediate(Window.TextSettings.LocalizationSets[i], true);
            }

            for (int i = Window.TextSettings.LocalizationSets.Count - 1; i >= 0; i--)
            {
                if (Window.TextSettings.LocalizationSets[i] == null) Window.TextSettings.LocalizationSets.RemoveAt(i);
            }

            Window.SetAssetNames(new(Window.TextSettings.LocalizationSets), typeof(LocalizationSet));

            AssetDatabase.SaveAssets();

            Window.SetVisibleLocalizationSets(Window.TextSettings.LocalizationSets);

            Window.Return = true;
        }
        void CopyLocalizations()
        {
            string localizations = "";

            for (int i = 0; i < TextSettings.Languages.Count; i++)
            {
                localizations += "\t" + TextSettings.Languages[i].Name;
            }

            for (int i = 0; i < TextSettings.LocalizationSets.Count; i++)
            {
                LocalizationSet localizationSet = TextSettings.LocalizationSets[i];

                localizations += "\n" + localizationSet.Name;

                for (int j = 0; j < localizationSet.Localizations.Count; j++)
                {
                    localizations += "\t" + localizationSet.Localizations[j].Text;
                }
            }

            EditorGUIUtility.systemCopyBuffer = localizations;
        }

        void UpdateTextStylersButton()
        {
            if (GUILayout.Button(UpdateLocalizablesContent, GUILayout.Height(ContentLibrary.RowHeight)))
            {
                List<ILocalizable> localizers = new(FindObjectsOfType<MonoBehaviour>().OfType<ILocalizable>());

                for (int i = 0; i < localizers.Count; i++)
                {
                    localizers[i].OnLanguageChanged();
                }
            }
        }
        public static void OpenSettingsButton()
        {
            GUILayout.BeginHorizontal();

            GUILayout.Space(-15);

            if (GUILayout.Button("Open Text Settings")) OpenWindow();

            GUILayout.EndHorizontal();
        }
        #endregion
    }
}