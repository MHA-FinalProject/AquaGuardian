using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace UIAssistant
{
#if UNITY_EDITOR

    [HelpURL("https://sites.google.com/view/uiassistant/")]
    public class ContentLibrary : ScriptableObject
    {
        #region Variables
        static ContentLibrary _Asset;
        static ColorSettings _ColorSettings;
        static OptionSettings _OptionSettings;
        static ScaleSettings _ScaleSettings;
        static TextSettings _TextSettings;
        static AnimationSettings _AnimationSettings;
        static PrefabList _PrefabList;

        public Texture2D Logo;
        public Texture2D WarnIcon;
        public Texture2D EditIcon;
        public Texture2D DeleteIcon;
        public Texture2D UpIcon;
        public Texture2D DownIcon;
        public Texture2D LeftIcon;
        public Texture2D RightIcon;
        public Texture2D NormalIcon;
        public Texture2D HighlightedIcon;
        public Texture2D PressedIcon;
        public Texture2D SelectedIcon;
        public Texture2D DisabledIcon;

        public static readonly int[] ItemsPerPageValues = { 25, 50, 75, 100 };
        public static readonly string[] ItemsPerPageNames = { "25", "50", "75", "100" };

        public const float RowHeight = 20;
        public const float IconHeight = 16;
        public const float ButtonWidth = 30;
        public const float AddButtonWidth = 230;
        public const float AddButtonHeight = 24;
        public const float CellWidth = 50;
        public const float CellHeight = 50;
        public const float ClearButtonWidth = 45;

        public static readonly Color LightThemeContentColor = Color.black;
        public static readonly Color DarkThemeContentColor = Color.white;
        public static readonly Color32 LightThemeWarningColor = new(199, 80, 80, 255);
        public static readonly Color32 DarkThemeWarningColor = new(255, 128, 128, 255);
        #endregion

        #region Function
        void OnValidate()
        {
            SetAsset();
        }
        static void SetAsset()
        {
            if (_Asset != null) return;

            string[] paths = AssetDatabase.FindAssets("t:ContentLibrary");
            if (paths.Length > 0)
                _Asset = AssetDatabase.LoadAssetAtPath<ContentLibrary>(AssetDatabase.GUIDToAssetPath(paths[0]));

            if (_Asset == null)
            {
                bool folderExists = AssetDatabase.IsValidFolder("UI Assistant/Data");

                if (folderExists)
                {
                    _Asset = CreateInstance<ContentLibrary>();
                    AssetDatabase.CreateAsset(_Asset, "UI Assistant/Data/ContentLibrary.asset");
                }
                else
                {
                    Debug.LogWarning("Finished importing the UI Assistant package. Please restart Unity.");
                }
            }
        }
        #endregion

        #region Helpers
        static ContentLibrary Asset
        {
            get
            {
                if (_Asset != null) return _Asset;
                else
                {
                    SetAsset();
                    return _Asset;
                }
            }
        }
        public static ColorSettings GetColorSettings()
        {
            if (_ColorSettings != null) return _ColorSettings;
            else
            {
                string[] paths = AssetDatabase.FindAssets("t:ColorSettings");

                if (paths.Length > 0)
                    _ColorSettings = AssetDatabase.LoadAssetAtPath<ColorSettings>(AssetDatabase.GUIDToAssetPath(paths[0]));
                else
                {
                    _ColorSettings = CreateInstance<ColorSettings>();
                    AssetDatabase.CreateAsset(_ColorSettings, GetFolderPath() + "ColorSettings.asset");
                }
                return _ColorSettings;
            }
        }
        public static OptionSettings GetOptionSettings()
        {
            if (_OptionSettings != null) return _OptionSettings;
            else
            {
                string[] paths = AssetDatabase.FindAssets("t:OptionSettings");

                if (paths.Length > 0)
                    _OptionSettings = AssetDatabase.LoadAssetAtPath<OptionSettings>(AssetDatabase.GUIDToAssetPath(paths[0]));
                else
                {
                    _OptionSettings = CreateInstance<OptionSettings>();
                    AssetDatabase.CreateAsset(_OptionSettings, GetFolderPath() + "OptionSettings.asset");
                }
                return _OptionSettings;
            }
        }
        public static ScaleSettings GetScaleSettings()
        {
            if (_ScaleSettings != null) return _ScaleSettings;
            else
            {
                string[] paths = AssetDatabase.FindAssets("t:ScaleSettings");

                if (paths.Length > 0)
                    _ScaleSettings = AssetDatabase.LoadAssetAtPath<ScaleSettings>(AssetDatabase.GUIDToAssetPath(paths[0]));
                else
                {
                    _ScaleSettings = CreateInstance<ScaleSettings>();
                    AssetDatabase.CreateAsset(_ScaleSettings, GetFolderPath() + "ScaleSettings.asset");
                }

                return _ScaleSettings;
            }
        }
        public static TextSettings GetTextSettings()
        {
            if (_TextSettings != null) return _TextSettings;
            else
            {
                string[] paths = AssetDatabase.FindAssets("t:TextSettings");
                if (paths.Length > 0)
                    _TextSettings = AssetDatabase.LoadAssetAtPath<TextSettings>(AssetDatabase.GUIDToAssetPath(paths[0]));
                else
                {
                    _TextSettings = CreateInstance<TextSettings>();
                    AssetDatabase.CreateAsset(_TextSettings, GetFolderPath() + "TextSettings.asset");
                }

                return _TextSettings;
            }
        }
        public static AnimationSettings GetAnimationSettings()
        {
            if (_AnimationSettings != null) return _AnimationSettings;
            else
            {
                string[] paths = AssetDatabase.FindAssets("t:AnimationSettings");

                if (paths.Length > 0)
                    _AnimationSettings = AssetDatabase.LoadAssetAtPath<AnimationSettings>(AssetDatabase.GUIDToAssetPath(paths[0]));
                else
                {
                    _AnimationSettings = CreateInstance<AnimationSettings>();
                    AssetDatabase.CreateAsset(_AnimationSettings, GetFolderPath() + "AnimationSettings.asset");
                }

                return _AnimationSettings;
            }
        }
        public static PrefabList GetPrefabList()
        {
            if (_PrefabList != null) return _PrefabList;
            else
            {
                string[] paths = AssetDatabase.FindAssets("t:PrefabList");

                if (paths.Length > 0)
                    _PrefabList = AssetDatabase.LoadAssetAtPath<PrefabList>(AssetDatabase.GUIDToAssetPath(paths[0]));
                else
                {
                    _PrefabList = CreateInstance<PrefabList>();
                    AssetDatabase.CreateAsset(_PrefabList, GetFolderPath() + "PrefabList.asset");
                }

                return _PrefabList;
            }
        }

        static string GetFolderPath()
        {
            string path = AssetDatabase.GetAssetPath(Asset);
            string assetName = Asset.name + ".asset";
            return path.Substring(0, path.Length - assetName.Length);
        }
        public static Color ContentColor => EditorGUIUtility.isProSkin ? DarkThemeContentColor : LightThemeContentColor;
        public static Color WarningColor => EditorGUIUtility.isProSkin ? DarkThemeWarningColor : LightThemeWarningColor;

        public static Texture2D LogoTexture => Asset.Logo;
        public static GUIContent EditContent => new("", Asset.EditIcon, "Edit");
        public static GUIContent DeleteContent => new("", Asset.DeleteIcon, "Delete");
        public static GUIContent UpContent => new("", Asset.UpIcon, "");
        public static GUIContent DownContent => new("", Asset.DownIcon, "");
        public static GUIContent LeftContent => new(Asset.LeftIcon);
        public static GUIContent RightContent => new(Asset.RightIcon);
        public static GUIContent NormalContent => new("", Asset.NormalIcon, "Normal");
        public static GUIContent HighlightedContent => new("", Asset.HighlightedIcon, "Highlighted");
        public static GUIContent PressedContent => new("", Asset.PressedIcon, "Pressed");
        public static GUIContent SelectedContent => new("", Asset.SelectedIcon, "Selected");
        public static GUIContent DisabledContent => new("", Asset.DisabledIcon, "Disabled");
        public static GUIContent WarningContent(string message) => new("", Asset.WarnIcon, message);
        #endregion
    }

#endif
}