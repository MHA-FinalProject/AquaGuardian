using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace UIAssistant
{
    public class HelpWindow : SettingsWindow
    {
        #region Variables
        readonly GUIContent WebsiteContent = new("Website", WebsiteLink);
        const string WebsiteLink = "https://sites.google.com/view/uiassistant/";
        readonly GUIContent YouTubeContent = new("YouTube", YouTubeLink);
        const string YouTubeLink = "https://www.youtube.com/@UIAssistant";
        readonly GUIContent EmailContent = new("Email", $"Copy to clipboard:\n{Email}");
        const string Email = "user.interface.assistant@gmail.com";

        const string SuppressHelpEditorPref = "UIAssistantSuppressHelp";
        const string HelpSeenSessionState = "UIAssistantHelpSeen";

        GUIStyle TextStyle;
        #endregion

        #region Function
        [MenuItem("Tools/UI Assistant/Help", priority = -9999)]
        public static void OpenWindow()
        {
            GetWindow<HelpWindow>("UI Assistant Help");
        }
        [InitializeOnLoadMethod]
        static void OnEditorLoad()
        {
            if (!SessionState.GetBool(HelpSeenSessionState, false))
            {
                if (!EditorPrefs.GetBool(SuppressHelpEditorPref, false)) EditorApplication.delayCall += OpenWindow;
                SessionState.SetBool(HelpSeenSessionState, true);
            }
        }

        override protected void OnGUI()
        {
            base.OnGUI();

            TextStyle = new()
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
            };

            EditorGUILayout.Space();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            float logoSize = 100;
            GUI.contentColor = Color.white;
            GUILayout.Label(ContentLibrary.LogoTexture, GUILayout.Width(logoSize), GUILayout.Height(logoSize));
            GUI.contentColor = ContentLibrary.ContentColor;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            EditorGUILayout.Space();
            GUILayout.Label("Thank you for using the UI Assistant!", TextStyle);
            EditorGUILayout.Space();

            BeginScrollArea();
            EditorGUILayout.Space();

            TextStyle.alignment = TextAnchor.MiddleRight;
            URLButton(WebsiteContent, WebsiteLink, true);
            EditorGUILayout.Space();
            URLButton(YouTubeContent, YouTubeLink, true);
            EditorGUILayout.Space();
            EmailContent.text = GUIUtility.systemCopyBuffer == Email ? "Email (Copied)" : "Email";
            URLButton(EmailContent, Email, false);

            EditorGUILayout.Space();
            EndScrollArea();
            GUILayout.FlexibleSpace();

            bool suppressHelp = EditorPrefs.GetBool(SuppressHelpEditorPref, false);
            bool suppressHelpPreference = GUILayout.Toggle(suppressHelp, "Don't show at startup");
            if (suppressHelp != suppressHelpPreference) EditorPrefs.SetBool(SuppressHelpEditorPref, suppressHelpPreference);
        }
        void URLButton(GUIContent content, string url, bool openLink)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button(content, GUILayout.Width(150), GUILayout.Height(ContentLibrary.AddButtonHeight)))
            {
                if (openLink) Application.OpenURL(url);
                else GUIUtility.systemCopyBuffer = url;
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
        #endregion
    }
}