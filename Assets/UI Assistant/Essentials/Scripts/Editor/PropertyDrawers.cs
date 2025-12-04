using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

namespace UIAssistant
{
    public class PropertyDrawer : UnityEditor.PropertyDrawer
    {
        #region Variables
        protected AnimationSettings _AnimationSettings;
        protected ColorSettings _ColorSettings;
        protected OptionSettings _OptionSettings;
        protected ScaleSettings _ScaleSettings;
        protected TextSettings _TextSettings;
        #endregion

        #region Function
        protected void CustomPropertyDrawer(Rect position, SerializedProperty property, GUIContent label, string[] names, List<Object> list)
        {
            int index = list.IndexOf(property.objectReferenceValue);

            EditorGUI.BeginProperty(position, GUIContent.none, property);

            position = EditorGUI.PrefixLabel(position, label);

            EditorGUI.BeginChangeCheck();

            index = EditorGUI.Popup(position, index, names);

            if (EditorGUI.EndChangeCheck())
            {
                property.objectReferenceValue = list[index];
            }

            EditorGUI.EndProperty();
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
        #endregion
    }

    [CustomPropertyDrawer(typeof(ColorCategory))]
    public class ColorCategoryDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            CustomPropertyDrawer(position, property, label, ColorSettings.CategoryNames, new(ColorSettings.ColorCategories));
        }
    }

    [CustomPropertyDrawer(typeof(ColorProfile))]
    public class ColorProfileDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            CustomPropertyDrawer(position, property, label, ColorSettings.ProfileNames, new(ColorSettings.ColorProfiles));
        }
    }

    [CustomPropertyDrawer(typeof(OptionEntry))]
    public class OptionEntryDrawer : PropertyDrawer
    {
        readonly GUIContent ClearContent = new("Clear", "Removes the Option Entry.");

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.objectReferenceValue != null)
            {
                float spacing = 2;
                Rect nameButtonRect = new(position.x, position.y, position.width - ContentLibrary.ClearButtonWidth - spacing, position.height);
                Rect clearButtonRect = new(position.x + nameButtonRect.width + spacing, position.y, ContentLibrary.ClearButtonWidth, position.height);

                CustomPropertyDrawer(nameButtonRect, property, label, OptionSettings.EntryNames, new(OptionSettings.OptionEntries));

                if (property.objectReferenceValue != null)
                {
                    if (GUI.Button(clearButtonRect, ClearContent))
                    {
                        property.objectReferenceValue = null;
                        GUI.changed = true;
                    }
                }
            }
            else
            {
                CustomPropertyDrawer(position, property, label, OptionSettings.EntryNames, new(OptionSettings.OptionEntries));
            }
        }
    }

    [CustomPropertyDrawer(typeof(ScaleProfile))]
    public class ScaleProfileDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            CustomPropertyDrawer(position, property, label, ScaleSettings.ProfileNames, new(ScaleSettings.ScaleProfiles));
        }
    }

    [CustomPropertyDrawer(typeof(Language))]
    public class LanguageDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            CustomPropertyDrawer(position, property, label, TextSettings.LanguageNames, new(TextSettings.Languages));
        }
    }

    [CustomPropertyDrawer(typeof(LocalizationSet))]
    public class LocalizationSetDrawer : PropertyDrawer
    {
        readonly GUIContent ClearContent = new("Clear", "Removes the Localization Set.");

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, GUIContent.none, property);

            position = EditorGUI.PrefixLabel(position, label);

            if (property.objectReferenceValue != null)
            {
                float spacing = 2;
                Rect nameButtonRect = new(position.x, position.y, position.width - ContentLibrary.ClearButtonWidth - spacing, position.height);
                Rect clearButtonRect = new(position.x + nameButtonRect.width + spacing, position.y, ContentLibrary.ClearButtonWidth, position.height);

                string name = property.objectReferenceValue == null ? "" :
                    (property.objectReferenceValue as LocalizationSet).Name;

                if (GUI.Button(nameButtonRect, name, EditorStyles.popup))
                {
                    LocalizationSetSelector.OpenLocalizationSetSelector(property);
                }

                if (GUI.Button(clearButtonRect, ClearContent))
                {
                    property.objectReferenceValue = null;
                    GUI.changed = true;
                }
            }
            else
            {
                string name = property.objectReferenceValue == null ? "" :
                    (property.objectReferenceValue as LocalizationSet).Name;

                if (GUI.Button(position, name, EditorStyles.popup))
                {
                    LocalizationSetSelector.OpenLocalizationSetSelector(property);
                }
            }

            EditorGUI.EndProperty();
        }
    }

    public class LocalizationSetSelector : UIAssistantEditor
    {
        #region Variables
        static SerializedProperty SerializedProperty;
        #endregion

        #region Function
        public static void OpenLocalizationSetSelector(SerializedProperty serializedProperty)
        {
            SerializedProperty = serializedProperty;

            LocalizationSetSelector window = CreateInstance<LocalizationSetSelector>();
            window.titleContent = new("Localization Sets");
            window.ShowAuxWindow();
        }
        protected override void OnGUI()
        {
            base.OnGUI();

            EditorGUILayout.Space();

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
                    if (GUILayout.Button(VisibleLocalizationSets[i].Name))
                    {
                        SerializedProperty.objectReferenceValue = VisibleLocalizationSets[i];
                        SerializedProperty.serializedObject.ApplyModifiedProperties();
                        Close();
                        break;
                    }
                }

                EndScrollArea();
            }
        }
        #endregion
    }

    [CustomPropertyDrawer(typeof(TextStyle))]
    public class TextStyleDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            CustomPropertyDrawer(position, property, label, TextSettings.StyleNames, new(TextSettings.TextStyles));
        }
    }

    [CustomPropertyDrawer(typeof(AnimationProfile))]
    public class AnimationProfileDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            CustomPropertyDrawer(position, property, label, AnimationSettings.ProfileNames, new(AnimationSettings.AnimationProfiles));
        }
    }

    [CustomPropertyDrawer(typeof(TextSettings.StringOrLocalizationSet))]
    public class StringOrLocalizationSetDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            EditorGUI.BeginChangeCheck();

            SerializedProperty localized = property.FindPropertyRelative("Localized");
            SerializedProperty stringProp = property.FindPropertyRelative("String");
            SerializedProperty localizationSet = property.FindPropertyRelative("LocalizationSet");

            Rect contentRect = new(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

            if (localized.boolValue) EditorGUI.PropertyField(contentRect, localizationSet, new GUIContent(label.text, label.tooltip));
            else EditorGUI.PropertyField(contentRect, stringProp, new GUIContent(label.text, label.tooltip));

            if (EditorGUI.EndChangeCheck()) property.serializedObject.ApplyModifiedProperties();
            
            EditorGUI.EndProperty();
        }
    }

    [CustomPropertyDrawer(typeof(CreditsBuilder.CreditsElement))]
    public class CreditsElementDrawer : PropertyDrawer
    {
        #region Variables
        bool GUIStyleSet;
        protected GUIStyle LabelStyle;
        #endregion

        #region Function
        void SetGUIStyle()
        {
            if (GUIStyleSet) return;

            LabelStyle = new(EditorStyles.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };

            GUIStyleSet = true;
        }
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SetGUIStyle();

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();

            SerializedProperty type = property.FindPropertyRelative("Type");
            SerializedProperty mainString = property.FindPropertyRelative("MainString");
            SerializedProperty listString = property.FindPropertyRelative("ListString");
            SerializedProperty customTemplate = property.FindPropertyRelative("CustomTemplate");

            Rect rect = new(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

            SerializedProperty localizedProp = mainString.FindPropertyRelative("Localized");
            SerializedProperty stringProp = mainString.FindPropertyRelative("String");
            SerializedProperty localizationSetProp = mainString.FindPropertyRelative("LocalizationSet");

            string properString = "";
            if (type.intValue != 3)
            {
                if (localizedProp.boolValue)
                {
                    LocalizationSet localizationSet = localizationSetProp.objectReferenceValue as LocalizationSet;
                    if (localizationSet != null) properString = localizationSet.LocalizedText;
                }
                else properString = stringProp.stringValue;
            }
            else if (customTemplate.objectReferenceValue != null) properString = customTemplate.objectReferenceValue.name;

            if (properString == "") properString = "Undefined";

            string propertyLabel = "";
            switch (type.intValue)
            {
                case 0:
                    propertyLabel = $"{properString} (Single)";
                    break;
                case 1:
                    propertyLabel = $"{properString} (Vertical)";
                    break;
                case 2:
                    propertyLabel = $"{properString} (Horizontal)";
                    break;
                case 3:
                    propertyLabel = $"{properString} (Custom)";
                    break;
            }
            EditorGUI.LabelField(rect, propertyLabel, LabelStyle);

            rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            rect.height = EditorGUIUtility.singleLineHeight;
            EditorGUI.PropertyField(rect, type, new GUIContent());

            rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            rect.height = EditorGUIUtility.singleLineHeight;
            if (type.intValue == 3) EditorGUI.PropertyField(rect, customTemplate, new GUIContent());
            else
            {
                EditorGUI.PropertyField(rect, mainString, new GUIContent());
                if (type.intValue != 0)
                {
                    rect.y += EditorGUIUtility.standardVerticalSpacing;
                    rect.height = EditorGUIUtility.singleLineHeight * 8;
                    EditorGUI.PropertyField(rect, listString, new GUIContent());
                }
            }

            if (EditorGUI.EndChangeCheck()) property.serializedObject.ApplyModifiedProperties();

            EditorGUI.EndProperty();
        }
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            SerializedProperty type = property.FindPropertyRelative("Type");

            if (type.intValue == 0 || type.intValue == 3) return EditorGUIUtility.singleLineHeight * 3 + EditorGUIUtility.standardVerticalSpacing + 12;
            else return EditorGUIUtility.singleLineHeight * 10 + EditorGUIUtility.standardVerticalSpacing * 2 + 12;
        }
        #endregion
    }
}