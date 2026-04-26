using UnityEditor;

namespace DataGraph.OneDrive
{
    /// <summary>
    /// Per-user OneDrive credential storage backed by EditorPrefs.
    /// Lives in the OneDrive assembly to avoid cross-referencing Editor.
    /// Uses fixed EditorPrefs keys shared with DataGraphSettingsProvider
    /// in the Editor assembly (both read/write the same keys).
    /// </summary>
    internal static class OneDriveCredentials
    {
        private const string Prefix = "DataGraph.Credentials.";

        private const string ClientIdKey = Prefix + "OneDrive.ClientId";
        private const string TenantIdKey = Prefix + "OneDrive.TenantId";
        private const string RefreshTokenKey = Prefix + "OneDrive.RefreshToken";
        private const string ClientSecretKey = Prefix + "OneDrive.ClientSecret";
        private const string AuthModeKey = Prefix + "OneDrive.AuthMode";

        public static string ClientId
        {
            get => EditorPrefs.GetString(ClientIdKey, "");
            set => EditorPrefs.SetString(ClientIdKey, value ?? "");
        }

        public static string TenantId
        {
            get => EditorPrefs.GetString(TenantIdKey, "common");
            set => EditorPrefs.SetString(TenantIdKey,
                string.IsNullOrWhiteSpace(value) ? "common" : value);
        }

        public static string RefreshToken
        {
            get => EditorPrefs.GetString(RefreshTokenKey, "");
            set => EditorPrefs.SetString(RefreshTokenKey, value ?? "");
        }

        /// <summary>
        /// Client secret for App-Only (client credentials) flow.
        /// Not used for PKCE flow.
        /// </summary>
        public static string ClientSecret
        {
            get => EditorPrefs.GetString(ClientSecretKey, "");
            set => EditorPrefs.SetString(ClientSecretKey, value ?? "");
        }

        /// <summary>
        /// 0 = OAuth 2.0 PKCE (interactive, user-level),
        /// 1 = App-Only (client credentials, headless).
        /// </summary>
        public static int AuthMode
        {
            get => EditorPrefs.GetInt(AuthModeKey, 0);
            set => EditorPrefs.SetInt(AuthModeKey, value);
        }

        /// <summary>
        /// Whether PKCE auth is configured (has refresh token).
        /// </summary>
        public static bool IsConfigured => !string.IsNullOrEmpty(ClientId)
                                           && !string.IsNullOrEmpty(RefreshToken);

        /// <summary>
        /// Whether App-Only auth is configured (has client secret).
        /// </summary>
        public static bool IsAppOnlyConfigured => !string.IsNullOrEmpty(ClientId)
                                                   && !string.IsNullOrEmpty(ClientSecret)
                                                   && !string.IsNullOrEmpty(TenantId)
                                                   && TenantId != "common";

        public static bool HasClientId => !string.IsNullOrEmpty(ClientId);

        /// <summary>
        /// Whether the currently selected auth mode is configured.
        /// </summary>
        public static bool IsActiveAuthConfigured =>
            AuthMode == 1 ? IsAppOnlyConfigured : IsConfigured;
    }
}
