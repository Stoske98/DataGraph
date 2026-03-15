using System;
using UnityEditor;
using UnityEngine;

namespace DataGraph.Editor.UI
{
    /// <summary>
    /// Editor window for configuring DataGraph settings,
    /// primarily Google Sheets authentication.
    /// </summary>
    internal sealed class DataGraphSettingsPanel : EditorWindow
    {
        private int _authMethodIndex;
        private string _apiKey = "";
        private string _oauthClientId = "";
        private string _oauthClientSecret = "";
        private string _statusMessage = "";
        private MessageType _statusType = MessageType.None;

        private static readonly string[] AuthMethodLabels =
            { "API Key (public sheets)", "OAuth 2.0 (private sheets)" };

        [MenuItem("DataGraph/Settings", false, 200)]
        private static void ShowWindow()
        {
            var window = GetWindow<DataGraphSettingsPanel>("DataGraph Settings");
            window.minSize = new Vector2(400, 250);
        }

        private void OnEnable()
        {
            LoadCurrentSettings();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Google Sheets Authentication", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (!ProviderRegistry.IsGoogleSheetsAvailable())
            {
                EditorGUILayout.HelpBox(
                    "Google Sheets provider is not installed.",
                    MessageType.Warning);
                return;
            }

            _authMethodIndex = GUILayout.Toolbar(_authMethodIndex, AuthMethodLabels);
            EditorGUILayout.Space(8);

            if (_authMethodIndex == 0)
                DrawApiKeySettings();
            else
                DrawOAuthSettings();

            EditorGUILayout.Space(8);
            if (!string.IsNullOrEmpty(_statusMessage))
                EditorGUILayout.HelpBox(_statusMessage, _statusType);

            EditorGUILayout.Space(8);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Sign Out / Clear", GUILayout.Width(160)))
                ClearAll();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawApiKeySettings()
        {
            EditorGUILayout.HelpBox(
                "For sheets that are public or shared via link.\n" +
                "Create an API Key in Google Cloud Console > APIs & Services > Credentials.",
                MessageType.Info);

            _apiKey = EditorGUILayout.TextField("API Key", _apiKey);

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Save API Key", GUILayout.Height(24)))
                SaveApiKey();
        }

        private void DrawOAuthSettings()
        {
            EditorGUILayout.HelpBox(
                "For private sheets. Create OAuth Client ID (Desktop app) " +
                "in Google Cloud Console. Enable Google Sheets API.",
                MessageType.Info);

            _oauthClientId = EditorGUILayout.TextField("Client ID", _oauthClientId);
            _oauthClientSecret = EditorGUILayout.PasswordField("Client Secret", _oauthClientSecret);

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Sign In with Google", GUILayout.Height(24)))
                SignInOAuth();
        }

        private void SaveApiKey()
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                SetStatus("API Key cannot be empty.", MessageType.Error);
                return;
            }

            try
            {
                var provider = ProviderRegistry.CreateGoogleSheetsProvider();
                var configureMethod = provider.GetType().GetMethod("ConfigureApiKey");
                if (configureMethod == null)
                {
                    SetStatus("ConfigureApiKey method not found on provider.", MessageType.Error);
                    return;
                }
                configureMethod.Invoke(provider, new object[] { _apiKey.Trim() });
                SetStatus("API Key saved.", MessageType.Info);
            }
            catch (Exception ex)
            {
                SetStatus($"Failed: {ex.Message}", MessageType.Error);
            }
        }

        private async void SignInOAuth()
        {
            if (string.IsNullOrWhiteSpace(_oauthClientId) ||
                string.IsNullOrWhiteSpace(_oauthClientSecret))
            {
                SetStatus("Client ID and Secret are required.", MessageType.Error);
                return;
            }

            SetStatus("Opening browser...", MessageType.Info);
            Repaint();

            try
            {
                var provider = ProviderRegistry.CreateGoogleSheetsProvider();
                var configureMethod = provider.GetType().GetMethod("ConfigureOAuthAsync");
                if (configureMethod == null)
                {
                    SetStatus("ConfigureOAuthAsync method not found on provider.", MessageType.Error);
                    return;
                }

                var task = (System.Threading.Tasks.Task<Runtime.Result<bool>>)
                    configureMethod.Invoke(provider, new object[]
                    {
                        _oauthClientId.Trim(),
                        _oauthClientSecret.Trim(),
                        System.Threading.CancellationToken.None
                    });

                var result = await task;
                SetStatus(result.IsSuccess ? "Signed in." : result.Error,
                    result.IsSuccess ? MessageType.Info : MessageType.Error);
            }
            catch (Exception ex)
            {
                SetStatus($"Failed: {ex.Message}", MessageType.Error);
            }

            Repaint();
        }

        private void ClearAll()
        {
            try
            {
                var provider = ProviderRegistry.CreateGoogleSheetsProvider();
                var signOutMethod = provider.GetType().GetMethod("SignOut");
                signOutMethod?.Invoke(provider, null);
                _apiKey = "";
                _oauthClientId = "";
                _oauthClientSecret = "";
                SetStatus("Credentials cleared.", MessageType.Info);
            }
            catch (Exception ex)
            {
                SetStatus($"Failed: {ex.Message}", MessageType.Error);
            }
        }

        private void LoadCurrentSettings()
        {
            _apiKey = EditorPrefs.GetString("DataGraph_Google_ApiKey", "");
            _oauthClientId = EditorPrefs.GetString("DataGraph_Google_OAuthClientId", "");
            _oauthClientSecret = EditorPrefs.GetString("DataGraph_Google_OAuthClientSecret", "");
            _authMethodIndex = !string.IsNullOrEmpty(_oauthClientId) ? 1 : 0;
        }

        private void SetStatus(string message, MessageType type)
        {
            _statusMessage = message;
            _statusType = type;
        }
    }
}
