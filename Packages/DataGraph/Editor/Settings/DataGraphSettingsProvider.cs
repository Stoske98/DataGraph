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
        private const string Version = "2.0";
        private const string DocUrl = "https://docs.datagraph.dev";

        private const string OneDriveClientIdKey = "DataGraph.Credentials.OneDrive.ClientId";
        private const string OneDriveTenantIdKey = "DataGraph.Credentials.OneDrive.TenantId";
        private const string OneDriveRefreshTokenKey = "DataGraph.Credentials.OneDrive.RefreshToken";

        private const string GsApiKeyPref = "DataGraph_Google_ApiKey";
        private const string GsClientIdPref = "DataGraph_Google_OAuthClientId";
        private const string GsClientSecretPref = "DataGraph_Google_OAuthClientSecret";
        private const string GsOAuthTokenPref = "DataGraph_Google_OAuthToken";
        private const string GsAuthMethodPref = "DataGraph.GoogleSheets.AuthMethod";
        private const string GsServiceAccountKeyPathPref =
            "DataGraph_Google_ServiceAccountKeyPath";

        private static readonly string[] ColumnDisplayModeLabels =
        {
            "Column Letters",
            "Header Names"
        };

        private DataGraphSettings _settings;

        private bool _connectionsFoldout = true;
        private bool _pathsFoldout = true;
        private bool _codeGenFoldout = true;
        private bool _editorFoldout = true;
        private bool _aboutFoldout;
        private bool _googleFoldout = true;
        private bool _oneDriveFoldout = true;
        private int _gsAuthMethod;

        private static readonly string[] GoogleAuthMethodLabels =
        {
            "API Key",
            "OAuth 2.0",
            "Service Account"
        };

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
                    "Parse", "JSON", "Preview", "OAuth", "API Key",
                    "Service Account"
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
                _gsAuthMethod, GoogleAuthMethodLabels);
            if (newMethod != _gsAuthMethod)
            {
                _gsAuthMethod = newMethod;
                EditorPrefs.SetInt(GsAuthMethodPref, newMethod);
            }

            EditorGUILayout.Space(4);

            switch (_gsAuthMethod)
            {
                case 0:
                    DrawGoogleApiKeyAuth();
                    break;
                case 1:
                    DrawGoogleOAuthAuth();
                    break;
                case 2:
                    DrawGoogleServiceAccountAuth();
                    break;
            }

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

            var hasToken = EditorPrefs.HasKey(GsOAuthTokenPref)
                           && !string.IsNullOrEmpty(
                               EditorPrefs.GetString(GsOAuthTokenPref, ""));

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (hasToken)
            {
                if (GUILayout.Button("Sign Out", GUILayout.Width(80)))
                {
                    EditorPrefs.DeleteKey(GsOAuthTokenPref);
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
            DrawStatusDot(hasToken);
        }

        private void DrawGoogleServiceAccountAuth()
        {
            var keyPath = EditorPrefs.GetString(GsServiceAccountKeyPathPref, "");

            EditorGUILayout.BeginHorizontal();
            var newPath = EditorGUILayout.TextField("JSON Key File", keyPath);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                var selected = EditorUtility.OpenFilePanel(
                    "Select Service Account JSON Key",
                    string.IsNullOrEmpty(keyPath) ? "" : System.IO.Path.GetDirectoryName(keyPath),
                    "json");
                if (!string.IsNullOrEmpty(selected))
                    newPath = selected;
            }
            EditorGUILayout.EndHorizontal();

            if (newPath != keyPath)
            {
                EditorPrefs.SetString(GsServiceAccountKeyPathPref, newPath);
                ConfigureGoogleServiceAccount(newPath);
            }

            var fileExists = !string.IsNullOrEmpty(newPath)
                             && System.IO.File.Exists(newPath);

            if (fileExists)
            {
                var email = ReadServiceAccountEmail(newPath);
                if (!string.IsNullOrEmpty(email))
                {
                    EditorGUILayout.Space(2);
                    EditorGUILayout.LabelField("Account", email);
                    EditorGUILayout.HelpBox(
                        "Share your spreadsheet with this email address " +
                        "to grant the service account read access.",
                        MessageType.Info);
                }
            }
            else if (!string.IsNullOrEmpty(newPath))
            {
                EditorGUILayout.HelpBox(
                    "Key file not found at the specified path.",
                    MessageType.Warning);
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(newPath));
            if (GUILayout.Button("Clear", GUILayout.Width(80)))
            {
                EditorPrefs.SetString(GsServiceAccountKeyPathPref, "");
                ClearGoogleServiceAccountViaReflection();
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            DrawStatusDot(fileExists);
        }

        private static string ReadServiceAccountEmail(string path)
        {
            try
            {
                var json = System.IO.File.ReadAllText(path);
                var emailKey = "\"client_email\"";
                var idx = json.IndexOf(emailKey, StringComparison.Ordinal);
                if (idx < 0) return null;

                var colonIdx = json.IndexOf(':', idx + emailKey.Length);
                if (colonIdx < 0) return null;

                var quoteStart = json.IndexOf('"', colonIdx + 1);
                if (quoteStart < 0) return null;

                var quoteEnd = json.IndexOf('"', quoteStart + 1);
                if (quoteEnd < 0) return null;

                return json.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
            }
            catch
            {
                return null;
            }
        }

        private static void ConfigureGoogleServiceAccount(string keyFilePath)
        {
            try
            {
                var provider = ProviderRegistry.CreateGoogleSheetsProvider();
                provider.GetType()
                    .GetMethod("ConfigureServiceAccount",
                        BindingFlags.Public | BindingFlags.Instance)
                    ?.Invoke(provider, new object[] { keyFilePath });
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"[DataGraph] Configure Service Account failed: {ex.Message}");
            }
        }

        private static void ClearGoogleServiceAccountViaReflection()
        {
            try
            {
                var provider = ProviderRegistry.CreateGoogleSheetsProvider();
                var strategyProp = provider.GetType()
                    .GetProperty("Strategies",
                        BindingFlags.Public | BindingFlags.Instance);
                if (strategyProp == null) return;

                var strategies = strategyProp.GetValue(provider);
                var indexer = strategies.GetType().GetProperty("Item");
                if (indexer == null) return;

                var strategy = indexer.GetValue(strategies, new object[] { "ServiceAccount" });
                strategy?.GetType()
                    .GetMethod("SignOut", BindingFlags.Public | BindingFlags.Instance)
                    ?.Invoke(strategy, null);
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"[DataGraph] Clear Service Account failed: {ex.Message}");
            }
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
            EditorPrefs.DeleteKey(GsOAuthTokenPref);
            EditorPrefs.DeleteKey(GsAuthMethodPref);
            EditorPrefs.DeleteKey(GsServiceAccountKeyPathPref);
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
                var currentMode = (int)_settings.Editor.ColumnDisplayMode;
                var newMode = EditorGUILayout.Popup("Column Display",
                    currentMode, ColumnDisplayModeLabels);
                if (newMode != currentMode)
                {
                    var from = (ColumnDisplayMode)currentMode;
                    var to = (ColumnDisplayMode)newMode;
                    _settings.Editor.ColumnDisplayMode = to;
                    MigrateAllGraphsColumnMode(from, to);
                }

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        /// <summary>
        /// Finds all DataGraphAsset instances in the project and migrates
        /// their node column properties from one display mode to another.
        /// Marks each modified asset dirty and refreshes the open editor window.
        /// </summary>
        private static void MigrateAllGraphsColumnMode(
            ColumnDisplayMode from, ColumnDisplayMode to)
        {
            var guids = AssetDatabase.FindAssets("t:DataGraphAsset");
            int migrated = 0;

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<DataGraphAsset>(path);
                if (asset == null) continue;

                Undo.RecordObject(asset, "Change Column Display Mode");
                asset.MigrateColumnDisplayMode(from, to);
                EditorUtility.SetDirty(asset);
                migrated++;
            }

            if (migrated > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log(
                    $"[DataGraph] Migrated column display mode in {migrated} graph(s).");

                var windows = Resources.FindObjectsOfTypeAll<DataGraphWindow>();
                foreach (var w in windows)
                    w.ReloadActiveGraph();
            }
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
