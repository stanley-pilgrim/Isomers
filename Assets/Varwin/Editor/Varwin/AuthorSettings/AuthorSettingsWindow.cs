using System;
using UnityEditor;
using UnityEngine;

namespace Varwin.Editor
{
    public class AuthorSettingsWindow : EditorWindow
    {
        private static AuthorSettingsWindow _window;
        
        private static readonly Vector2 WindowSize = new Vector2(350, 180);
        
        public static void OpenWindow()
        {
            _window = GetWindow<AuthorSettingsWindow>(false, SdkTexts.DefaultAuthorWindowTitle, true);
            
            _window.minSize = WindowSize;
            _window.maxSize = WindowSize;
            
            _window.Show();
            
            AuthorSettings.Initialize();
        }
        
        private void OnGUI()
        {
            if (string.IsNullOrWhiteSpace(AuthorSettings.SavedName))
            {
                AuthorSettings.Initialize();
            }

            GUILayout.BeginVertical(EditorStyles.inspectorFullWidthMargins);
            DrawFields();
            EditorGUILayout.Space();
            DrawControls();
            DrawNameCanNotBeEmptyHelpBox();
            GUILayout.EndVertical();
        }

        private void OnDestroy()
        {
            if (AuthorSettings.IsChanged() && !string.IsNullOrWhiteSpace(AuthorSettings.Name))
            {
                if (EditorUtility.DisplayDialog(SdkTexts.DefaultAuthorWindowTitle, SdkTexts.SaveDefaultAuthorInfoQuestion, "Yes", "No"))
                {
                    AuthorSettings.SaveAll();
                }
            }
        }

        private void DrawFields()
        {
            GUILayout.BeginVertical();

            AuthorSettings.Name = DrawField("Name", AuthorSettings.Name);
            AuthorSettings.Email = DrawField("E-Mail", AuthorSettings.Email);
            AuthorSettings.Url = DrawField("URL", AuthorSettings.Url);
            
            GUILayout.EndVertical();
        }

        private void DrawControls()
        {
            GUILayout.BeginHorizontal();

            DrawSaveButton();
            //DrawReloadButton();
            //DrawRevertToDefaultValuesButton();
            
            GUILayout.EndHorizontal();
        }

        private void DrawSaveButton()
        {
            bool enabled = AuthorSettings.IsChanged() && !string.IsNullOrWhiteSpace(AuthorSettings.Name);
            EditorGUI.BeginDisabledGroup(!enabled);
            if (GUILayout.Button("Save"))
            {
                AuthorSettings.SaveAll();
                AuthorSettings.Initialize();
                Debug.Log(SdkTexts.DefaultAuthorInfoWasUpdatedMessage);
            }
            EditorGUI.EndDisabledGroup();
        }

        private void DrawReloadButton()
        {
            bool enabled = AuthorSettings.IsChanged();
            EditorGUI.BeginDisabledGroup(!enabled);
            if (GUILayout.Button("Reload"))
            {
                AuthorSettings.Initialize();
                Debug.Log(SdkTexts.DefaultAuthorInfoWasReloadedMessage);
            }
            EditorGUI.EndDisabledGroup();
        }

        private void DrawRevertToDefaultValuesButton()
        {
            if (GUILayout.Button("Clear"))
            {
                if (EditorUtility.DisplayDialog(SdkTexts.DefaultAuthorWindowTitle, SdkTexts.DefaultAuthorInfoWillRevertQuestion, "OK", "Cancel"))
                {
                    AuthorSettings.Clear();
                    AuthorSettings.Initialize();
                    Debug.Log(SdkTexts.DefaultAuthorInfoWasRevertMessage);
                }
            }
        }

        private void DrawNameCanNotBeEmptyHelpBox()
        {
            if (string.IsNullOrWhiteSpace(AuthorSettings.Name))
            {
                EditorGUILayout.HelpBox(SdkTexts.AuthorNameEmptyWarning, MessageType.Error);
            }
        }
        
        private string DrawField(string label, string value)
        {
            GUILayout.Label($"{label}:");
            return EditorGUILayout.TextField(value).Trim();
        }
    }
}