using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace UIAssistant
{
    public class AssetEditor : Editor
    {
        #region Variables
        SerializedProperty Name;
        #endregion

        #region Function
        void OnEnable()
        {
            Name = serializedObject.FindProperty("Name");    
        }
        public override void OnInspectorGUI()
        {
            bool helpBox = Name == null;

            GUILayout.BeginHorizontal();

            GUILayout.Space(-15);

            if (helpBox)
            {
                EditorGUILayout.HelpBox("This is a Settings asset. Open its settings window to edit its contents by using the Tools/UI Assistant menu, or click on the button below.", MessageType.Info);
            }
            else
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label(Name.stringValue);
                GUILayout.FlexibleSpace();
            }

            GUILayout.EndHorizontal();

            if (helpBox) EditorGUILayout.Space();
        }
        #endregion
    }
    public class ColorAssetEditor : AssetEditor
    {
        #region Function
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            GUILayout.BeginHorizontal();

            GUILayout.Space(-15);

            if (GUILayout.Button("Open Color Settings")) ColorSettingsWindow.OpenWindow();

            GUILayout.EndHorizontal();
        }
        #endregion
    }
    public class OptionAssetEditor : AssetEditor
    {
        #region Function
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            GUILayout.BeginHorizontal();

            GUILayout.Space(-15);

            if (GUILayout.Button("Open Option Settings")) OptionSettingsWindow.OpenWindow();

            GUILayout.EndHorizontal();
        }
        #endregion
    }
    public class ScaleAssetEditor : AssetEditor
    {
        #region Function
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            GUILayout.BeginHorizontal();

            GUILayout.Space(-15);

            if (GUILayout.Button("Open Scale Settings")) ScaleSettingsWindow.OpenWindow();

            GUILayout.EndHorizontal();
        }
        #endregion
    }
    public class TextAssetEditor : AssetEditor
    {
        #region Function
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            GUILayout.BeginHorizontal();

            GUILayout.Space(-15);

            if (GUILayout.Button("Open Text Settings")) TextSettingsWindow.OpenWindow();

            GUILayout.EndHorizontal();
        }
        #endregion
    }
    public class AnimationAssetEditor : AssetEditor
    {
        #region Function
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            GUILayout.BeginHorizontal();

            GUILayout.Space(-15);

            if (GUILayout.Button("Open Animation Settings")) AnimationSettingsWindow.OpenWindow();

            GUILayout.EndHorizontal();
        }
        #endregion
    }

    [CustomEditor(typeof(PrefabList)), CanEditMultipleObjects]
    public class PrefabListEditor : Editor
    {
        #region Function
        public override void OnInspectorGUI()
        {
            GUILayout.BeginHorizontal();

            GUILayout.Space(-15);

            if (GUILayout.Button("Open Prefab List")) PrefabListWindow.OpenWindow();

            GUILayout.EndHorizontal();
        }
        #endregion
    }

    [CustomEditor(typeof(ContentLibrary))]
    public class ContentLibraryEditor : Editor
    {
        #region Variables
        readonly GUIContent EditContent = new("Customize Icons", "Allows you to change settings windows' icons.");
     
        bool Edit;

        SerializedProperty Logo;
        SerializedProperty WarnIcon;
        SerializedProperty EditIcon;
        SerializedProperty DeleteIcon;
        SerializedProperty UpIcon;
        SerializedProperty DownIcon;
        SerializedProperty LeftIcon;
        SerializedProperty RightIcon;
        SerializedProperty NormalIcon;
        SerializedProperty HighlightedIcon;
        SerializedProperty PressedIcon;
        SerializedProperty SelectedIcon;
        SerializedProperty DisabledIcon;
        #endregion

        #region Function
        void OnEnable()
        {
            Logo = serializedObject.FindProperty("Logo");
            WarnIcon = serializedObject.FindProperty("WarnIcon");
            EditIcon = serializedObject.FindProperty("EditIcon");
            DeleteIcon = serializedObject.FindProperty("DeleteIcon");
            UpIcon = serializedObject.FindProperty("UpIcon");
            DownIcon = serializedObject.FindProperty("DownIcon");
            LeftIcon = serializedObject.FindProperty("LeftIcon");
            RightIcon = serializedObject.FindProperty("RightIcon");

            NormalIcon = serializedObject.FindProperty("NormalIcon");
            HighlightedIcon = serializedObject.FindProperty("HighlightedIcon");
            PressedIcon = serializedObject.FindProperty("PressedIcon");
            SelectedIcon = serializedObject.FindProperty("SelectedIcon");
            DisabledIcon = serializedObject.FindProperty("DisabledIcon");
        }
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();

            EditorGUI.indentLevel--;

            EditorGUILayout.HelpBox("The UI Assistant's components and settings windows require this asset to function.\nDo not delete it.", MessageType.Warning, true);

            EditorGUILayout.Space();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            Edit = GUILayout.Toggle(Edit, EditContent, EditorStyles.miniButton, GUILayout.Width(ContentLibrary.AddButtonWidth));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            EditorGUILayout.Space();

            if (Edit)
            {
                EditorGUI.indentLevel++;

                GUILayout.Label("Shared Settings Icons", EditorStyles.boldLabel);

                EditorGUILayout.PropertyField(Logo);
                EditorGUILayout.PropertyField(WarnIcon);
                EditorGUILayout.PropertyField(EditIcon);
                EditorGUILayout.PropertyField(DeleteIcon);
                EditorGUILayout.PropertyField(UpIcon);
                EditorGUILayout.PropertyField(DownIcon);
                EditorGUILayout.PropertyField(LeftIcon);
                EditorGUILayout.PropertyField(RightIcon);

                EditorGUILayout.Space();

                GUILayout.Label("Color Settings Icons", EditorStyles.boldLabel);

                EditorGUILayout.PropertyField(NormalIcon);
                EditorGUILayout.PropertyField(HighlightedIcon);
                EditorGUILayout.PropertyField(PressedIcon);
                EditorGUILayout.PropertyField(SelectedIcon);
                EditorGUILayout.PropertyField(DisabledIcon);
            }

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(target);
                serializedObject.ApplyModifiedProperties();
            }
        }
        #endregion
    }

    [CustomEditor(typeof(ColorCategory)), CanEditMultipleObjects]
    public class ColorCategoryEditor : ColorAssetEditor { }

    [CustomEditor(typeof(ColorProfile)), CanEditMultipleObjects]
    public class ColorProfileEditor : ColorAssetEditor { }

    [CustomEditor(typeof(ColorSettings)), CanEditMultipleObjects]
    public class ColorSettingsEditor : ColorAssetEditor { }

    [CustomEditor(typeof(OptionEntry)), CanEditMultipleObjects]
    public class OptionEntryEditor : OptionAssetEditor { }

    [CustomEditor(typeof(OptionSettings)), CanEditMultipleObjects]
    public class OptionSettingsEditor : OptionAssetEditor { }

    [CustomEditor(typeof(ScaleProfile)), CanEditMultipleObjects]
    public class ScaleProfileEditor : ScaleAssetEditor { }

    [CustomEditor(typeof(ScaleSettings)), CanEditMultipleObjects]
    public class ScaleSettingsEditor : ScaleAssetEditor { }

    [CustomEditor(typeof(Language)), CanEditMultipleObjects]
    public class LanguageEditor : TextAssetEditor { }

    [CustomEditor(typeof(LocalizationSet)), CanEditMultipleObjects]
    public class LocalizationSetEditor : TextAssetEditor { }

    [CustomEditor(typeof(TextSettings)), CanEditMultipleObjects]
    public class TextSettingsEditor : TextAssetEditor { }
   
    [CustomEditor(typeof(TextStyle)), CanEditMultipleObjects]
    public class TextStyleEditor : TextAssetEditor { }

    [CustomEditor(typeof(AnimationProfile)), CanEditMultipleObjects]
    public class AnimationProfileEditor : AnimationAssetEditor { }

    [CustomEditor(typeof(AnimationSettings)), CanEditMultipleObjects]
    public class AnimationSettingsEditor : AnimationAssetEditor { }

    [CustomEditor(typeof(DocumentationOpener)), CanEditMultipleObjects]
    public class DocumentationOpenerEditor : Editor
    {
        #region Variables
        readonly GUIContent HelpContent = new("UI Assistant Resources", "Tools/UI Assistant/Help");
        readonly GUIContent DocumentationContent = new("Open Documentation", HelpURL);
        const string HelpURL = "https://sites.google.com/view/uiassistant/";
        #endregion

        #region Function
        public override void OnInspectorGUI()
        {
            EditorGUILayout.Space();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button(DocumentationContent, GUILayout.Width(ContentLibrary.AddButtonWidth), GUILayout.Height(ContentLibrary.AddButtonHeight)))
                Application.OpenURL(HelpURL);

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button(HelpContent, GUILayout.Width(ContentLibrary.AddButtonWidth), GUILayout.Height(ContentLibrary.AddButtonHeight)))
                HelpWindow.OpenWindow();

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
        #endregion
    }
}