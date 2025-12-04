using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace UIAssistant
{
    public class LocalizationImporter : UIAssistantEditor
    {
        #region Variables
        static bool Import;
        static List<ImportedLocalizationSet> ImportList;
        static List<RemovableLocalizationSet> RemoveList;
        static List<DuplicateKey> DuplicateKeys;
        static int DuplicateCount;
        int SelectionCount;
        #endregion

        #region Classes
        public class ImportedLocalizationSet
        {
            public bool Selected;
            public string Name;
            public List<LocalizationSet.Localization> Localizations;
            public int ActiveIndex;
        }
        public class RemovableLocalizationSet
        {
            public bool Selected;
            public LocalizationSet LocalizationSet;
        }
        class DuplicateKey
        {
            public string Key;
            public int Count;

            public DuplicateKey(string key)
            {
                Key = key;
                Count = 1;
            }
        }
        #endregion

        #region Function
        public static void OpenImportWindow(List<ImportedLocalizationSet> importList)
        {
            Import = true;

            DuplicateKeys = new();
            DuplicateCount = 0;
            ImportList = new();

            List<string> addedNames = new();
            for (int i = 0; i < importList.Count; i++)
            {
                string setName = importList[i].Name;

                if (!addedNames.Contains(setName))
                {
                    ImportList.Add(importList[i]);
                    addedNames.Add(setName);
                }
                else
                {
                    DuplicateKey duplicateKey = RegisteredDuplicate(setName);

                    if (duplicateKey == null) DuplicateKeys.Add(new(setName));
                    else duplicateKey.Count++;

                    DuplicateCount++;
                }
            }

            OpenWindow(new("Localization Importer"));
        }
        public static void OpenRemoveWindow(List<RemovableLocalizationSet> removeList)
        {
            Import = false;
            RemoveList = removeList;

            OpenWindow(new("Localization Remover"));
        }
        static void OpenWindow(GUIContent titleContent)
        {
            LocalizationImporter window = CreateInstance<LocalizationImporter>();
            window.titleContent = titleContent;
            window.ShowAuxWindow();
        }

        protected override void OnGUI()
        {
            base.OnGUI();

            if (Import && DuplicateKeys.Count > 0)
            {
                GUI.contentColor = Color.white;

                string list = "";

                for (int i = 0; i < DuplicateKeys.Count; i++)
                {
                    list += $"\n - {DuplicateKeys[i].Key} ({DuplicateKeys[i].Count})";
                }

                EditorGUILayout.HelpBox($"Import data contains multiple instances of the following keys:{list}\n\n{DuplicateCount} localization sets have been discarded.\nPlease rename duplicate keys before re-importing.", MessageType.Error);

                GUI.contentColor = ContentLibrary.ContentColor;
            }

            int dataCount = Import ? ImportList.Count : RemoveList.Count;

            if (dataCount > ContentLibrary.ItemsPerPageValues[0])
            {
                EditorGUILayout.Space();

                Pager(dataCount);
            }
            else
            {
                PageDisplayStartIndex = 0;
                PageDisplayEndIndex = dataCount;
            }

            BeginScrollArea();

            SelectionCount = 0;

            if (Import)
            {
                for (int i = PageDisplayStartIndex; i < PageDisplayEndIndex; i++)
                {
                    ImportList[i].Selected = EditorGUILayout.ToggleLeft(ImportList[i].Name, ImportList[i].Selected, GUILayout.Height(ContentLibrary.RowHeight));
                    if (ImportList[i].Selected) SelectionCount++;
                }
            }
            else
            {
                for (int i = PageDisplayStartIndex; i < PageDisplayEndIndex; i++)
                {
                    RemoveList[i].Selected = EditorGUILayout.ToggleLeft(RemoveList[i].LocalizationSet.Name, RemoveList[i].Selected, GUILayout.Height(ContentLibrary.RowHeight));
                    if (RemoveList[i].Selected) SelectionCount++;
                }
            }

            EndScrollArea();

            EditorGUILayout.Space();

            SelectionButtons();

            FocusClearArea();

            if (Import)
            {
                GUI.enabled = SelectionCount > 0;

                if (GUILayout.Button("Import Selected"))
                {
                    List<ImportedLocalizationSet> importList = new();

                    for (int i = 0; i < ImportList.Count; i++)
                    {
                        if (ImportList[i].Selected) importList.Add(ImportList[i]);
                    }

                    TextSettingsWindow.ImportLocalizationSets(importList);
                    Close();
                }
            
                GUI.enabled = true;
            }
            else
            {
                GUI.contentColor = Color.white;
                EditorGUILayout.HelpBox("You cannot undo the delete localization set action.", MessageType.Warning);

                GUI.enabled = SelectionCount > 0;

                GUI.backgroundColor = ContentLibrary.WarningColor;
                GUI.contentColor = Color.white;

                if (GUILayout.Button("Remove Selected", RemoveButtonStyle))
                {
                    TextSettingsWindow.RemoveLocalizationSets(RemoveList);
                    Close();
                }

                GUI.backgroundColor = Color.white;
                GUI.contentColor = ContentLibrary.ContentColor;

                GUI.enabled = true;
            }
        }
        void SelectionButtons()
        {
            GUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();

            int maxCount = Import ? ImportList.Count : RemoveList.Count;

            GUI.enabled = SelectionCount < maxCount;

            if (GUILayout.Button("Select All", GUILayout.ExpandWidth(false), GUILayout.Height(ContentLibrary.RowHeight)))
            {
                if (Import)
                {
                    for (int i = 0; i < ImportList.Count; i++)
                    {
                        ImportList[i].Selected = true;
                    }
                }
                else
                {
                    for (int i = 0; i < RemoveList.Count; i++)
                    {
                        RemoveList[i].Selected = true;
                    }
                }
            }

            GUI.enabled = SelectionCount > 0;

            if (GUILayout.Button("Select None", GUILayout.ExpandWidth(false), GUILayout.Height(ContentLibrary.RowHeight)))
            {
                if (Import)
                {
                    for (int i = 0; i < ImportList.Count; i++)
                    {
                        ImportList[i].Selected = false;
                    }
                }
                else
                {
                    for (int i = 0; i < RemoveList.Count; i++)
                    {
                        RemoveList[i].Selected = false;
                    }
                }
            }

            GUILayout.FlexibleSpace();

            GUILayout.EndHorizontal();

            GUI.enabled = false;

            GUILayout.Label($"{SelectionCount} Selected; {maxCount} Total", MiddleCenterStyle, GUILayout.Height(ContentLibrary.RowHeight));

            GUI.enabled = true;
        }
        static DuplicateKey RegisteredDuplicate(string setName)
        {
            DuplicateKey duplicateKey = null;

            for (int i = 0; i < DuplicateKeys.Count; i++)
            {
                if (setName == DuplicateKeys[i].Key)
                {
                    duplicateKey = DuplicateKeys[i];
                    break;
                }
            }

            return duplicateKey;
        }
        #endregion
    }
}