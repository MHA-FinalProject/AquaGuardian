using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace UIAssistant
{
    public class PrefabListWindow : SettingsWindow
    {
        #region Variables
        readonly GUIContent AutoCreateCanvasContent = new("Auto-create Canvas", "If a prefab is instantiated without a parent Canvas, one will be created for it automatically.");
        readonly GUIContent NewPrefabContent = new("Add Custom Prefab");
        #endregion

        #region Function
        [MenuItem("Tools/UI Assistant/Prefab List", priority = 5)]
        public static void OpenWindow()
        {
            GetWindow<PrefabListWindow>("Prefab List");
        }
        protected override void OnGUI()
        {
            base.OnGUI();

            EditorGUI.BeginChangeCheck();

            Undo.RecordObject(PrefabList, "Prefab List Changes");

            Prefabs();

            if (EditorGUI.EndChangeCheck()) ApplyChanges();
        }
        protected override void ApplyChanges()
        {
            EditorUtility.SetDirty(PrefabList);
        }

        void Prefabs()
        {
            PrefabList.AutoCreateCanvas = EditorGUILayout.ToggleLeft(AutoCreateCanvasContent,
                PrefabList.AutoCreateCanvas, GUILayout.Height(ContentLibrary.RowHeight));

            EditorGUILayout.Space();

            BeginScrollArea();

            for (int i = 0; i < PrefabList.EssentialPrefabs.Count; i++)
            {
                EssentialPrefab(i);
            }

            if (PrefabList.CustomPrefabs.Count > 0) EditorGUILayout.Space();

            GUI.contentColor = ContentLibrary.ContentColor;

            for (int i = 0; i < PrefabList.CustomPrefabs.Count; i++)
            {
                CustomPrefab(i);

                if (Return)
                {
                    GUILayout.EndHorizontal();
                    break;
                }
            }

            EndScrollArea();

            EditorGUILayout.Space();

            if (AddButton(NewPrefabContent)) PrefabList.CustomPrefabs.Add(new());

            FocusClearArea();
        }
        void EssentialPrefab(int index)
        {
            GUILayout.BeginHorizontal();

            GUI.enabled = false;

            if (MoveUpButton) { }
            if (MoveDownButton) { }

            PrefabList.EssentialPrefabs[index].Name = EditorGUILayout.TextField(PrefabList.EssentialPrefabs[index].Name, GUILayout.Height(ContentLibrary.RowHeight));

            GUI.enabled = true;

            GUI.contentColor = Color.white;

            PrefabList.EssentialPrefabs[index].GameObject = EditorGUILayout.ObjectField(PrefabList.EssentialPrefabs[index].GameObject, typeof(GameObject), false) as GameObject;

            GUI.enabled = false;

            if (DeleteButton) { }

            GUI.enabled = true;

            GUILayout.EndHorizontal();
        }
        void CustomPrefab(int index)
        {
            GUILayout.BeginHorizontal();

            if (MoveUpButton) MoveListItem(PrefabList.CustomPrefabs, index, -1);

            if (MoveDownButton) MoveListItem(PrefabList.CustomPrefabs, index, 1);

            PrefabList.CustomPrefabs[index].Name = EditorGUILayout.TextField(PrefabList.CustomPrefabs[index].Name, GUILayout.Height(ContentLibrary.RowHeight));

            GUI.contentColor = Color.white;

            PrefabList.CustomPrefabs[index].GameObject = EditorGUILayout.ObjectField(PrefabList.CustomPrefabs[index].GameObject, typeof(GameObject), false) as GameObject;

            if (DeleteButton)
            {
                PrefabList.CustomPrefabs.RemoveAt(index);
                Return = true;
            }

            GUILayout.EndHorizontal();
        }
        #endregion
    }
}
