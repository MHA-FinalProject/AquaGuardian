using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace UIAssistant
{
    public class GameObjectMenu : UIAssistantEditor
    {
        #region Function
        [MenuItem("GameObject/UI Assistant/Prefab...", priority = -99)]
        static void OpenCustomPrefabList()
        {
            GameObjectMenu window = CreateInstance<GameObjectMenu>();
            window.titleContent = new("Instantiate Custom Prefab");
            window.ShowAuxWindow();
        }

        protected override void OnGUI()
        {
            base.OnGUI();

            GUI.contentColor = Color.white;

            if (PrefabList.CustomPrefabs.Count == 0)
            {
                EditorGUILayout.HelpBox("There are no custom prefabs to instantiate. Open Tools/UI Assistant/Prefab List to add one!", MessageType.Error);

                if (GUILayout.Button("Open Prefab List"))
                {
                    PrefabListWindow.OpenWindow();
                    Close();
                }
            }
            else
            {
                BeginScrollArea();

                for (int i = 0; i < PrefabList.CustomPrefabs.Count; i++)
                {
                    if (GUILayout.Button(PrefabList.CustomPrefabs[i].Name))
                    {
                        CreatePrefab(PrefabList.CustomPrefabs[i]);
                        Close();
                    }
                }

                EndScrollArea();
            }
        }

        [MenuItem("GameObject/UI Assistant/Button", priority = 0)]
        static void CreateButton() { CreatePrefab(PrefabList.EssentialPrefabs[0]); }

        [MenuItem("GameObject/UI Assistant/Dropdown", priority = 1)]
        static void CreateDropdown() { CreatePrefab(PrefabList.EssentialPrefabs[1]); }

        [MenuItem("GameObject/UI Assistant/Input Field", priority = 2)]
        static void CreateInputField() { CreatePrefab(PrefabList.EssentialPrefabs[2]); }

        [MenuItem("GameObject/UI Assistant/Option Cycler", priority = 3)]
        static void CreateOptionCycler() { CreatePrefab(PrefabList.EssentialPrefabs[3]); }

        [MenuItem("GameObject/UI Assistant/Scrollbar", priority = 4)]
        static void CreateScrollbar() { CreatePrefab(PrefabList.EssentialPrefabs[4]); }

        [MenuItem("GameObject/UI Assistant/Slider", priority = 5)]
        static void CreateSlider() { CreatePrefab(PrefabList.EssentialPrefabs[5]); }

        [MenuItem("GameObject/UI Assistant/Toggle", priority = 6)]
        static void CreateToggle() { CreatePrefab(PrefabList.EssentialPrefabs[6]); }

        static void CreatePrefab(PrefabList.Prefab prefab)
        {
            if (prefab.GameObject == null)
            {
                Debug.LogWarning($"The game object for {prefab.Name} is missing. Please update your list under Tools/UI Assistant/Prefab List!");
                return;
            }

            Transform parent = Selection.activeTransform;

            if (PrefabList.AutoCreateCanvas)
            {
                Canvas canvas = parent == null ? null : parent.GetComponentInParent<Canvas>();

                if (canvas == null)
                {
                    GameObject newGameObject = new("Canvas");
                    canvas = newGameObject.AddComponent<Canvas>();
                    newGameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
                    newGameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                    newGameObject.transform.SetParent(parent);

                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    RectTransform canvasRectTransform = canvas.GetComponent<RectTransform>();
                    canvasRectTransform.sizeDelta = new(1920, 1080);
                    canvasRectTransform.anchoredPosition = new(960, 540);

                    parent = newGameObject.transform;
                }
            }

            Object newObject = PrefabUtility.InstantiatePrefab(prefab.GameObject, parent);
            Undo.RegisterCreatedObjectUndo(newObject, "Create " + prefab.Name);
            Selection.activeObject = newObject;
        }
        #endregion
    }
}