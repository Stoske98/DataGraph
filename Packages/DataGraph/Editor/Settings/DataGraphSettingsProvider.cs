using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using DataGraph.Editor.Public;
using UnityEditor;
using UnityEngine;

namespace DataGraph.Editor
{
    /// <summary>
    /// Registers DataGraph in Unity's Project Settings under "Project/DataGraph".
    /// Google Sheets and OneDrive credentials are accessed through reflection
    /// and EditorPrefs respectively to avoid cross-assembly dependencies.
    /// </summary>
    internal sealed class DataGraphSettingsProvider : SettingsProvider
    {
        private const string SettingsPath = "Project/DataGraph";
        private const string Version = "1.5";
        private const string DocUrl = "https://docs.datagraph.dev";

        private const string OneDriveClientIdKey = "DataGraph.Credentials.OneDrive.ClientId";
        private const string OneDriveTenantIdKey = "DataGraph.Credentials.OneDrive.TenantId";
        private const string OneDriveRefreshTokenKey = "DataGraph.Credentials.OneDrive.RefreshToken";

        private const string GsApiKeyPref = "DataGraph.GoogleSheets.ApiKey";
        private const string GsClientIdPref = "DataGraph.GoogleSheets.OAuth.ClientId";
        private const string GsClientSecretPref = "DataGraph.GoogleSheets.OAuth.ClientSecret";
        private const string GsRefreshTokenPref = "DataGraph.GoogleSheets.OAuth.RefreshToken";
        private const string GsAuthMethodPref = "DataGraph.GoogleSheets.AuthMethod";

        private DataGraphSettings _settings;

        private bool _connectionsFoldout = true;
        private bool _pathsFoldout = true;
        private bool _codeGenFoldout = true;
        private bool _editorFoldout = true;
        private bool _aboutFoldout;
        private bool _googleFoldout = true;
        private bool _oneDriveFoldout = true;
        private int _gsAuthMethod;

        private DataGraphSettingsProvider()
            : base(SettingsPath, SettingsScope.Project) { }

        [SettingsProvider]
        internal static SettingsProvider CreateProvider()
        {
            return new DataGraphSettingsProvider
            {
                keywords = new HashSet<string>
                {
                    "DataGraph", "Google Sheets", "OneDrive", "Namespace",
                    "Parse", "JSON", "Preview", "OAuth", "API Key"
                }
            };
        }

        public override void OnActivate(string searchContext,
            UnityEngine.UIElements.VisualElement rootElement)
        {
            _settings = DataGraphSettings.Instance;
            _gsAuthMethod = EditorPrefs.GetInt(GsAuthMethodPref, 0);
        }

        public override void OnGUI(string searchContext)
        {
            if (_settings == null) return;
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.Space(4);
            DrawConnectionsSection();
            DrawPathsSection();
            DrawCodeGenerationSection();
            DrawEditorSection();
            DrawAboutSection();
            if (EditorGUI.EndChangeCheck())
                _settings.Save();
        }

        // ==================== CONNECTIONS ====================

        private void DrawConnectionsSection()
        {
            _connectionsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(
                _connectionsFoldout, "Connections");
            if (_connectionsFoldout)
            {
                EditorGUI.indentLevel++;
                DrawGoogleSheetsConnection();
                EditorGUILayout.Space(8);
                DrawOneDriveConnection();
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // ==================== GOOGLE SHEETS ====================

        private void DrawGoogleSheetsConnection()
        {
            _googleFoldout = EditorGUILayout.Foldout(
                _googleFoldout, "Google Sheets", toggleOnLabelClick: true);
            if (!_googleFoldout) return;

            EditorGUI.indentLevel++;

            if (!ProviderRegistry.IsGoogleSheetsAvailable())
            {
                EditorGUILayout.HelpBox(
                    "DataGraph.GoogleSheets package is not installed.\n" +
                    "Install it to enable Google Sheets as a data source.",
                    MessageType.Info);
                EditorGUI.indentLevel--;
                return;
            }

            var newMethod = EditorGUILayout.Popup("Auth Method",
                _gsAuthMethod, new[] { "API Key", "OAuth 2.0" });
            if (newMethod != _gsAuthMethod)
            {
                _gsAuthMethod = newMethod;
                EditorPrefs.SetInt(GsAuthMethodPref, newMethod);
            }

            EditorGUILayout.Space(4);

            if (_gsAuthMethod == 0)
                DrawGoogleApiKeyAuth();
            else
                DrawGoogleOAuthAuth();

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Clear All Google Credentials", GUILayout.Width(200)))
            {
                if (EditorUtility.DisplayDialog("Clear Credentials",
                    "Remove all stored Google Sheets credentials?",
                    "Clear", "Cancel"))
                {
                    ClearGoogleCredentials();
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel--;
        }

        private void DrawGoogleApiKeyAuth()
        {
            var apiKey = EditorPrefs.GetString(GsApiKeyPref, "");
            var newKey = EditorGUILayout.PasswordField("API Key", apiKey);
            if (newKey != apiKey)
            {
                EditorPrefs.SetString(GsApiKeyPref, newKey);
                ConfigureGoogleApiKey(newKey);
            }

            DrawStatusDot(!string.IsNullOrEmpty(newKey));
        }

        private void DrawGoogleOAuthAuth()
        {
            var clientId = EditorPrefs.GetString(GsClientIdPref, "");
            var newClientId = EditorGUILayout.TextField("Client ID", clientId);
            if (newClientId != clientId)
                EditorPrefs.SetString(GsClientIdPref, newClientId);

            var clientSecret = EditorPrefs.GetString(GsClientSecretPref, "");
            var newSecret = EditorGUILayout.PasswordField("Client Secret", clientSecret);
            if (newSecret != clientSecret)
                EditorPrefs.SetString(GsClientSecretPref, newSecret);

            var hasRefresh = !string.IsNullOrEmpty(
                EditorPrefs.GetString(GsRefreshTokenPref, ""));

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (hasRefresh)
            {
                if (GUILayout.Button("Sign Out", GUILayout.Width(80)))
                {
                    EditorPrefs.SetString(GsRefreshTokenPref, "");
                    SignOutGoogle();
                }
            }
            else
            {
                var canSignIn = !string.IsNullOrEmpty(newClientId)
                                && !string.IsNullOrEmpty(newSecret);
                EditorGUI.BeginDisabledGroup(!canSignIn);
                if (GUILayout.Button("Sign In", GUILayout.Width(80)))
                    ConfigureGoogleOAuth(newClientId, newSecret);
                EditorGUI.EndDisabledGroup();
            }

            EditorGUILayout.EndHorizontal();
            DrawStatusDot(hasRefresh);
        }

        private static void ConfigureGoogleApiKey(string apiKey)
        {
            try
            {
                var provider = ProviderRegistry.CreateGoogleSheetsProvider();
                provider.GetType()
                    .GetMethod("ConfigureApiKey", BindingFlags.Public | BindingFlags.Instance)
                    ?.Invoke(provider, new object[] { apiKey });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataGraph] Configure API Key failed: {ex.Message}");
            }
        }

        private static async void ConfigureGoogleOAuth(string clientId, string clientSecret)
        {
            try
            {
                var provider = ProviderRegistry.CreateGoogleSheetsProvider();
                var method = provider.GetType().GetMethod("ConfigureOAuthAsync",
                    BindingFlags.Public | BindingFlags.Instance);
                if (method == null) return;

                var task = method.Invoke(provider,
                    new object[] { clientId, clientSecret, CancellationToken.None });
                if (task is System.Threading.Tasks.Task t)
                    await t;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataGraph] OAuth sign-in failed: {ex.Message}");
            }
        }

        private static void SignOutGoogle()
        {
            try
            {
                var provider = ProviderRegistry.CreateGoogleSheetsProvider();
                provider.GetType()
                    .GetMethod("SignOut", BindingFlags.Public | BindingFlags.Instance)
                    ?.Invoke(provider, null);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataGraph] Google sign-out failed: {ex.Message}");
            }
        }

        private static void ClearGoogleCredentials()
        {
            EditorPrefs.DeleteKey(GsApiKeyPref);
            EditorPrefs.DeleteKey(GsClientIdPref);
            EditorPrefs.DeleteKey(GsClientSecretPref);
            EditorPrefs.DeleteKey(GsRefreshTokenPref);
            EditorPrefs.DeleteKey(GsAuthMethodPref);
            SignOutGoogle();
        }

        // ==================== ONEDRIVE ====================

        private void DrawOneDriveConnection()
        {
            _oneDriveFoldout = EditorGUILayout.Foldout(
                _oneDriveFoldout, "OneDrive", toggleOnLabelClick: true);
            if (!_oneDriveFoldout) return;

            EditorGUI.indentLevel++;

            if (!ProviderRegistry.IsOneDriveAvailable())
            {
                EditorGUILayout.HelpBox(
                    "DataGraph.OneDrive package is not installed.\n" +
                    "Install it to enable OneDrive/SharePoint as a data source.",
                    MessageType.Info);
                EditorGUI.indentLevel--;
                return;
            }

            var clientId = EditorPrefs.GetString(OneDriveClientIdKey, "");
            var newClientId = EditorGUILayout.TextField("Client ID", clientId);
            if (newClientId != clientId)
                EditorPrefs.SetString(OneDriveClientIdKey, newClientId);

            var tenantId = EditorPrefs.GetString(OneDriveTenantIdKey, "common");
            var newTenantId = EditorGUILayout.TextField("Tenant ID", tenantId);
            if (newTenantId != tenantId)
                EditorPrefs.SetString(OneDriveTenantIdKey, newTenantId);

            var hasRefresh = !string.IsNullOrEmpty(
                EditorPrefs.GetString(OneDriveRefreshTokenKey, ""));

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (hasRefresh)
            {
                if (GUILayout.Button("Sign Out", GUILayout.Width(80)))
                    EditorPrefs.SetString(OneDriveRefreshTokenKey, "");
            }
            else
            {
                EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(newClientId));
                if (GUILayout.Button("Sign In", GUILayout.Width(80)))
                    InvokeOneDriveSignIn(newClientId, newTenantId);
                EditorGUI.EndDisabledGroup();
            }

            EditorGUILayout.EndHorizontal();

            var isConfigured = !string.IsNullOrEmpty(newClientId) && hasRefresh;
            DrawStatusDot(isConfigured);

            EditorGUI.indentLevel--;
        }

        private static void InvokeOneDriveSignIn(string clientId, string tenantId)
        {
            try
            {
                var providerType = ProviderRegistry.CreateOneDriveProvider().GetType();
                var oauthType = providerType.Assembly.GetType(
                    "DataGraph.OneDrive.Auth.OneDriveOAuthFlow");
                oauthType?.GetMethod("Start",
                        BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                    ?.Invoke(null, new object[] { clientId, tenantId });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataGraph] OneDrive sign-in failed: {ex.Message}");
            }
        }

        // ==================== PATHS ====================

        private void DrawPathsSection()
        {
            _pathsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_pathsFoldout, "Paths");
            if (_pathsFoldout)
            {
                EditorGUI.indentLevel++;
                DrawFolderField("Graphs Folder", _settings.Paths.GraphsFolder,
                    v => _settings.Paths.GraphsFolder = v);
                DrawFolderField("Generated Output", _settings.Paths.GeneratedFolder,
                    v => _settings.Paths.GeneratedFolder = v);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // ==================== CODE GENERATION ====================

        private void DrawCodeGenerationSection()
        {
            _codeGenFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(
                _codeGenFoldout, "Code Generation");
            if (_codeGenFoldout)
            {
                EditorGUI.indentLevel++;
                var ns = EditorGUILayout.TextField("Namespace",
                    _settings.CodeGeneration.Namespace);
                if (ns != _settings.CodeGeneration.Namespace)
                    _settings.CodeGeneration.Namespace = ns;

                var ap = EditorGUILayout.Toggle("Auto-Parse on Save",
                    _settings.CodeGeneration.AutoParseOnSave);
                if (ap != _settings.CodeGeneration.AutoParseOnSave)
                    _settings.CodeGeneration.AutoParseOnSave = ap;
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // ==================== EDITOR ====================

        private void DrawEditorSection()
        {
            _editorFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(
                _editorFoldout, "Editor");
            if (_editorFoldout)
            {
                EditorGUI.indentLevel++;
                var ar = EditorGUILayout.Toggle("Auto-Refresh JSON Preview",
                    _settings.Editor.AutoRefreshJsonPreview);
                if (ar != _settings.Editor.AutoRefreshJsonPreview)
                    _settings.Editor.AutoRefreshJsonPreview = ar;
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // ==================== ABOUT ====================

        private void DrawAboutSection()
        {
            _aboutFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(
                _aboutFoldout, "About");
            if (_aboutFoldout)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Version", Version);
                EditorGUILayout.LabelField("Unity", Application.unityVersion);
                EditorGUILayout.Space(4);
                if (GUILayout.Button("Open Documentation", GUILayout.Width(200)))
                    Application.OpenURL(DocUrl);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // ==================== HELPERS ====================

        private static void DrawStatusDot(bool connected)
        {
            var rect = EditorGUILayout.GetControlRect(false, 20);
            rect.x += EditorGUI.indentLevel * 15;
            var dotRect = new Rect(rect.x, rect.y + 4, 12, 12);
            EditorGUI.DrawRect(dotRect, connected
                ? new Color(0.1f, 0.7f, 0.4f)
                : new Color(0.5f, 0.5f, 0.5f));
            EditorGUI.LabelField(
                new Rect(dotRect.xMax + 6, rect.y, rect.width - 18, rect.height),
                connected ? "Connected" : "Not connected");
        }

        private static void DrawFolderField(string label, string current,
            Action<string> setter)
        {
            EditorGUILayout.BeginHorizontal();
            var val = EditorGUILayout.TextField(label, current);
            if (val != current) setter(val);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                var sel = EditorUtility.OpenFolderPanel(label, current, "");
                if (!string.IsNullOrEmpty(sel))
                {
                    var root = System.IO.Path.GetFullPath(".");
                    if (sel.StartsWith(root))
                        sel = sel.Substring(root.Length + 1);
                    setter(sel);
                    GUI.changed = true;
                }
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
