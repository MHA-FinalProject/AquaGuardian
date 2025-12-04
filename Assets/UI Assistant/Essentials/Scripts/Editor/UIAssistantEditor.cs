using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace UIAssistant
{
    public class UIAssistantEditor : EditorWindow
    {
        #region Variables
        protected readonly GUIContent NameContent = new("Name", "The name of the asset.");
        readonly GUIContent FilterMissingContent = new("Isolate Missing", "Hides all complete Localization Sets.");

        protected AnimationSettings _AnimationSettings;
        protected ColorSettings _ColorSettings;
        protected OptionSettings _OptionSettings;
        protected ScaleSettings _ScaleSettings;
        protected TextSettings _TextSettings;
        protected static PrefabList _PrefabList;

        protected bool EditorDirty;
        Vector2 ScrollPosition;

        int ItemsPerPage = ContentLibrary.ItemsPerPageValues[0];
        int PageIndex;
        protected int PageDisplayStartIndex;
        protected int PageDisplayEndIndex;

        protected string SearchString = "";
        protected string LastSearchString = "";
        protected List<LocalizationSet> VisibleLocalizationSets = new();

        int MissingSetCount;
        bool FilterMissingSets = false;
        bool LastMissingFilterState = true;

        bool GUIStylesSet;
        protected GUIStyle EditorBoxStyle;
        protected GUIStyle BoxedLabelStyle;
        protected GUIStyle MiddleCenterStyle;
        protected GUIStyle MiddleRightStyle;
        protected GUIStyle TextAreaStyle;
        protected GUIStyle CenteredTextFieldStyle;
        protected GUIStyle RemoveButtonStyle;
        protected GUIStyle PagerStyle;
        #endregion

        #region Function
        protected virtual void OnGUI()
        {
            SetGUIStyles();

            GUI.contentColor = ContentLibrary.ContentColor;
        }
        void SetGUIStyles()
        {
            if (GUIStylesSet) return;

            EditorBoxStyle = new(GUI.skin.box)
            {
                margin = new(-10, -10, 0, 0),
                padding = new(0, 0, -2, -2),
            };

            BoxedLabelStyle = new(EditorStyles.helpBox)
            {
                fontSize = 12,
            };

            MiddleCenterStyle = new(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
            };

            MiddleRightStyle = new(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleRight,
            };

            TextAreaStyle = new(EditorStyles.textArea)
            {
                wordWrap = true,
            };

            CenteredTextFieldStyle = new(EditorStyles.textField)
            {
                alignment = TextAnchor.UpperCenter,
            };

            RemoveButtonStyle = new(GUI.skin.button);
            RemoveButtonStyle.normal.textColor = Color.white;
            RemoveButtonStyle.hover.textColor = Color.white;
            RemoveButtonStyle.active.textColor = Color.white;
            RemoveButtonStyle.focused.textColor = Color.white;

            PagerStyle = new(EditorStyles.helpBox)
            {
                alignment = TextAnchor.UpperCenter,
                fontSize = 12,
            };

            GUIStylesSet = true;
        }

        protected bool MoveUpButton => GUILayout.Button(ContentLibrary.UpContent, GUILayout.Width(ContentLibrary.ButtonWidth), GUILayout.Height(ContentLibrary.RowHeight));
        protected bool MoveDownButton => GUILayout.Button(ContentLibrary.DownContent, GUILayout.Width(ContentLibrary.ButtonWidth), GUILayout.Height(ContentLibrary.RowHeight));
        protected bool EditButton => GUILayout.Button(ContentLibrary.EditContent, GUILayout.Width(ContentLibrary.ButtonWidth), GUILayout.Height(ContentLibrary.RowHeight));
        protected bool DeleteButton
        {
            get
            {
                GUI.backgroundColor = ContentLibrary.WarningColor;
                GUI.contentColor = Color.white;

                bool delete = GUILayout.Button(ContentLibrary.DeleteContent, GUILayout.Width(ContentLibrary.ButtonWidth), GUILayout.Height(ContentLibrary.RowHeight));

                GUI.backgroundColor = Color.white;
                GUI.contentColor = ContentLibrary.ContentColor;

                return delete;
            }
        }
        protected bool EditorBackButton()
        {
            bool clearEditor = false;

            if (GUILayout.Button("Back", GUILayout.Height(ContentLibrary.RowHeight)))
            {
                if (EditorDirty)
                {
                    if (RevertDialog())
                    {
                        EditorDirty = false;
                        clearEditor = true;
                    }
                }
                else clearEditor = true;
            }

            return clearEditor;
        }
        protected bool AddButton(GUIContent content)
        {
            bool clicked = false;

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(content, GUILayout.Width(ContentLibrary.AddButtonWidth), GUILayout.Height(ContentLibrary.AddButtonHeight))) clicked = true;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            return clicked;
        }
        protected bool RemoveButton(GUIContent content)
        {
            bool clicked = false;

            GUI.backgroundColor = ContentLibrary.WarningColor;

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(content, RemoveButtonStyle, GUILayout.Width(ContentLibrary.AddButtonWidth), GUILayout.Height(ContentLibrary.AddButtonHeight))) clicked = true;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUI.backgroundColor = Color.white;

            return clicked;
        }
        protected bool ApplyButton(string assetName, string initialAssetName, List<NamedAsset> assetList, bool newAsset)
        {
            bool apply = false;

            if (GUILayout.Button(newAsset ? "Create" : "Apply", GUILayout.Height(ContentLibrary.RowHeight)))
            {
                if (assetName != "")
                {
                    if (assetName == initialAssetName)
                        apply = true;
                    else
                    {
                        bool matchingName = false;

                        for (int i = 0; i < assetList.Count; i++)
                        {
                            if (assetList[i].Name == assetName)
                            {
                                matchingName = true;
                                break;
                            }
                        }

                        apply = !matchingName;
                    }

                    if (!apply) EditorUtility.DisplayDialog("Duplicate Name", $"The name '{assetName}' already exists. Please use a different one.", "OK");
                }
                else EditorUtility.DisplayDialog("Missing Name", "All assets must be named.", "OK");
            }

            return apply;
        }

        protected bool DeleteDialog(NamedAsset asset, string assetType)
        {
            GUI.FocusControl(null);
            return EditorUtility.DisplayDialog($"Delete '{asset.Name}' {assetType}?", $"You cannot undo the delete {assetType} action.", "Delete", "Cancel");
        }
        protected bool RevertDialog()
        {
            return EditorUtility.DisplayDialog("Discard unsaved changes?", "Going back will revert any changes you have made since the last save.", "OK", "Cancel");
        }

        protected void BeginScrollArea()
        {
            GUILayout.BeginVertical(EditorBoxStyle);

            ScrollPosition = EditorGUILayout.BeginScrollView(ScrollPosition, GUILayout.ExpandHeight(false));

            GUILayout.Space(4);
        }
        protected void EndScrollArea()
        {
            GUILayout.Space(2);

            EditorGUILayout.EndScrollView();

            GUILayout.EndVertical();
        }

        protected void Warning(string message)
        {
            GUI.contentColor = Color.white;

            GUILayout.Label(ContentLibrary.WarningContent(message), GUILayout.Width(ContentLibrary.RowHeight), GUILayout.Height(ContentLibrary.RowHeight));

            GUI.contentColor = ContentLibrary.ContentColor;
        }
        protected void FocusClearArea()
        {
            if (GUILayout.Button("", EditorStyles.label, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true))) GUI.FocusControl(null);
        }

        protected void Pager(int itemCount)
        {
            int pageCount = Mathf.CeilToInt((float)itemCount / ItemsPerPage);

            if (PageIndex >= pageCount) PageIndex = pageCount - 1;

            GUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();

            GUILayout.BeginVertical();

            if (pageCount > 1)
            {
                GUILayout.BeginHorizontal();

                if (GUILayout.Button(ContentLibrary.LeftContent, GUILayout.Width(ContentLibrary.ButtonWidth), GUILayout.Height(ContentLibrary.RowHeight)))
                {
                    if (PageIndex == 0) PageIndex = pageCount - 1;
                    else PageIndex--;
                }

                GUILayout.Label((PageIndex + 1) + " / " + pageCount, PagerStyle, GUILayout.Height(ContentLibrary.RowHeight));

                if (GUILayout.Button(ContentLibrary.RightContent, GUILayout.Width(ContentLibrary.ButtonWidth), GUILayout.Height(ContentLibrary.RowHeight)))
                {
                    if (PageIndex == pageCount - 1) PageIndex = 0;
                    else PageIndex++;
                }

                GUILayout.EndHorizontal();
            }

            PageDisplayStartIndex = PageIndex * ItemsPerPage;
            PageDisplayEndIndex = Mathf.Clamp(PageDisplayStartIndex + ItemsPerPage, PageDisplayStartIndex, itemCount);

            GUILayout.BeginHorizontal();

            GUILayout.Label("Assets Per Page");

            ItemsPerPage = EditorGUILayout.IntPopup(ItemsPerPage, ContentLibrary.ItemsPerPageNames, ContentLibrary.ItemsPerPageValues, GUILayout.Width(96), GUILayout.Height(ContentLibrary.RowHeight));

            GUILayout.EndHorizontal();

            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            GUILayout.EndHorizontal();
        }
        protected void LocalizationSetSearchBar(List<LocalizationSet> localizationSets, bool showFilter)
        {
            if (MissingSetCount == 0 && FilterMissingSets) FilterMissingSets = false;

            GUILayout.BeginHorizontal();

            GUILayout.Space(4);

            SearchString = EditorGUILayout.TextField(SearchString, EditorStyles.toolbarSearchField);

            string styleName = GUI.skin.FindStyle("ToolbarSeachCancelButton") != null ? "ToolbarSeachCancelButton" : "ToolbarSearchCancelButton";
            if (GUILayout.Button("", GUI.skin.FindStyle(styleName)))
            {
                SearchString = "";
                LastSearchString = "";

                VisibleLocalizationSets = new(localizationSets);

                GUI.FocusControl(null);
            }

            GUILayout.EndHorizontal();

            if (showFilter && MissingSetCount > 0)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                FilterMissingSets = GUILayout.Toggle(FilterMissingSets, FilterMissingContent, EditorStyles.miniButton, GUILayout.Width(ContentLibrary.AddButtonWidth));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            if (SearchString != LastSearchString) SetVisibleLocalizationSets(localizationSets);
            else if (FilterMissingSets != LastMissingFilterState) SetVisibleLocalizationSets(localizationSets);

            LastSearchString = SearchString;
            LastMissingFilterState = FilterMissingSets;
        }
        protected void SetVisibleLocalizationSets(List<LocalizationSet> localizationSets)
        {
            VisibleLocalizationSets.Clear();
            MissingSetCount = 0;

            for (int i = 0; i < localizationSets.Count; i++)
            {
                bool match = localizationSets[i].Name.Contains(SearchString, System.StringComparison.OrdinalIgnoreCase);

                if (!match)
                {
                    for (int j = 0; j < localizationSets[i].Localizations.Count; j++)
                    {
                        if (localizationSets[i].Localizations[j].Text.Contains(SearchString, System.StringComparison.OrdinalIgnoreCase))
                        {
                            match = true;
                            break;
                        }
                    }
                }

                if (match) VisibleLocalizationSets.Add(localizationSets[i]);
            }

            for (int i = VisibleLocalizationSets.Count - 1; i > -1; i--)
            {
                if (!VisibleLocalizationSets[i].MissingLocalization)
                {
                    if (FilterMissingSets)
                        VisibleLocalizationSets.RemoveAt(i);
                }
                else MissingSetCount++;
            }
        }
        #endregion

        #region Helpers
        protected AnimationSettings AnimationSettings
        {
            get
            {
                if (_AnimationSettings == null) _AnimationSettings = ContentLibrary.GetAnimationSettings();
                return _AnimationSettings;
            }
        }
        protected ColorSettings ColorSettings
        {
            get
            {
                if (_ColorSettings == null) _ColorSettings = ContentLibrary.GetColorSettings();
                return _ColorSettings;
            }
        }
        protected OptionSettings OptionSettings
        {
            get
            {
                if (_OptionSettings == null) _OptionSettings = ContentLibrary.GetOptionSettings();
                return _OptionSettings;
            }
        }
        protected ScaleSettings ScaleSettings
        {
            get
            {
                if (_ScaleSettings == null) _ScaleSettings = ContentLibrary.GetScaleSettings();
                return _ScaleSettings;
            }
        }
        protected TextSettings TextSettings
        {
            get
            {
                if (_TextSettings == null) _TextSettings = ContentLibrary.GetTextSettings();
                return _TextSettings;
            }
        }
        protected static PrefabList PrefabList
        {
            get
            {
                if (_PrefabList == null) _PrefabList = ContentLibrary.GetPrefabList();
                return _PrefabList;
            }
        }
        #endregion
    }
    public abstract class SettingsWindow : UIAssistantEditor
    {
        #region Variables
        readonly GUIContent LocalizedNameContent = new("Localized Name", "Localization Set that contains the name of the asset in all Languages.");
        readonly GUIContent ClearContent = new("Clear", "Removes the Localization Set.");

        protected bool Return;
        protected bool EditingNew;

        protected bool EditingLocalizedName;
        protected LocalizationSet EditedLocalizedName;
        protected LocalizationSet InitialEditedLocalizedName;
        #endregion

        #region Function
        protected override void OnGUI()
        {
            base.OnGUI();

            Return = false;

            GUI.contentColor = ContentLibrary.ContentColor;
        }

        protected virtual void ApplyChanges() { }
        protected void ApplyChanges(Object asset)
        {
            EditorUtility.SetDirty(asset);
        }
        protected void ApplyChanges(List<Object> assets)
        {
            for (int i = 0; i < assets.Count; i++)
            {
                EditorUtility.SetDirty(assets[i]);
            }
        }

        protected void LocalizedName()
        {
            GUILayout.BeginHorizontal();

            EditorGUILayout.PrefixLabel(LocalizedNameContent);

            string buttonLabel = EditedLocalizedName == null ? "" : EditedLocalizedName.Name;

            if (GUILayout.Button(buttonLabel, GUILayout.Height(ContentLibrary.RowHeight)))
                EditingLocalizedName = true;

            if (GUILayout.Button(ClearContent, GUILayout.Width(ContentLibrary.ClearButtonWidth), GUILayout.Height(ContentLibrary.RowHeight)))
            {
                if (!EditorDirty && InitialEditedLocalizedName != null) EditorDirty = true;
                EditedLocalizedName = null;
            }

            GUILayout.EndHorizontal();
        }
        protected void LocalizedNameEditor()
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
                    GUI.enabled = VisibleLocalizationSets[i] != EditedLocalizedName;

                    if (GUILayout.Button(VisibleLocalizationSets[i].Name))
                    {
                        EditedLocalizedName = VisibleLocalizationSets[i];

                        EditingLocalizedName = false;

                        EndScrollArea();
                        break;
                    }

                    GUI.enabled = true;
                }

                EndScrollArea();

                FocusClearArea();

                if (GUILayout.Button("Cancel")) EditingLocalizedName = false;
            }

            if (!EditorDirty) EditorDirty = EditingLocalizedName != InitialEditedLocalizedName;
        }

        protected void SetAssetNames(List<NamedAsset> assets, System.Type type)
        {
            for (int i = 0; i < assets.Count; i++)
            {
                assets[i].name = type.ToString()[12..] + (i + 1).ToString();
            }
        }
        protected void MoveListItem<T>(List<T> list, int originIndex, int direction)
        {
            int targetIndex = originIndex + direction;

            if (targetIndex == list.Count)
            {
                T item = list[originIndex];
                list.RemoveAt(originIndex);
                list.Insert(0, item);
            }
            else if (targetIndex < 0)
            {
                T item = list[originIndex];
                list.RemoveAt(originIndex);
                list.Add(item);
            }
            else
            {
                T a = list[originIndex];
                T b = list[targetIndex];

                list[originIndex] = b;
                list[targetIndex] = a;
            }

            GUI.FocusControl(null);
        }

        protected void AutoCreateMessage(NamedAsset asset) { Debug.LogWarning($"A required asset has been created: {asset.Name}"); }
        #endregion
    }
}