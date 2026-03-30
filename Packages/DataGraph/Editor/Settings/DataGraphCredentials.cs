using UnityEditor;

namespace DataGraph.Editor
{
    /// <summary>
    /// Per-user credential storage backed by EditorPrefs.
    /// Never serialized to project assets — keeps OAuth tokens and
    /// API configuration out of source control.
    /// Google Sheets credentials are managed by GoogleSheetsProvider through
    /// its own IAuthStrategy system — this class handles OneDrive only.
    /// </summary>
    internal static class DataGraphCredentials
    {
        private const string Prefix = "DataGraph.Credentials.";

        private const string OneDriveClientIdKey = Prefix + "OneDrive.ClientId";
        private const string OneDriveTenantIdKey = Prefix + "OneDrive.TenantId";
        private const string OneDriveRefreshTokenKey = Prefix + "OneDrive.RefreshToken";

        public static OneDriveCredentialStore OneDrive { get; } = new();

        /// <summary>
        /// Clears all stored OneDrive credentials from EditorPrefs.
        /// </summary>
        public static void ClearOneDrive()
        {
            EditorPrefs.DeleteKey(OneDriveClientIdKey);
            EditorPrefs.DeleteKey(OneDriveTenantIdKey);
            EditorPrefs.DeleteKey(OneDriveRefreshTokenKey);
        }

        /// <summary>
        /// OneDrive / Microsoft Graph credential accessors.
        /// Uses OAuth 2.0 with PKCE — no client secret needed
        /// (public client model, appropriate for desktop/editor apps).
        /// </summary>
        internal sealed class OneDriveCredentialStore
        {
            public string ClientId
            {
                get => EditorPrefs.GetString(OneDriveClientIdKey, "");
                set => EditorPrefs.SetString(OneDriveClientIdKey, value ?? "");
            }

            public string TenantId
            {
                get => EditorPrefs.GetString(OneDriveTenantIdKey, "common");
                set => EditorPrefs.SetString(OneDriveTenantIdKey,
                    string.IsNullOrWhiteSpace(value) ? "common" : value);
            }

            public string RefreshToken
            {
                get => EditorPrefs.GetString(OneDriveRefreshTokenKey, "");
                set => EditorPrefs.SetString(OneDriveRefreshTokenKey, value ?? "");
            }

            public bool IsConfigured => !string.IsNullOrEmpty(ClientId)
                                        && !string.IsNullOrEmpty(RefreshToken);

            public bool HasClientId => !string.IsNullOrEmpty(ClientId);
        }
    }
}
