using UnityEditor;

namespace DataGraph.Editor
{
    /// <summary>
    /// Per-user credential storage backed by EditorPrefs.
    /// Never serialized to project assets — keeps OAuth tokens and
    /// API configuration out of source control.
    /// Google Sheets credentials are managed by GoogleSheetsProvider through
    /// its own IAuthStrategy system — this class handles OneDrive and
    /// cross-assembly credential metadata (e.g. service account display info).
    /// </summary>
    internal static class DataGraphCredentials
    {
        private const string Prefix = "DataGraph.Credentials.";

        private const string OneDriveClientIdKey = Prefix + "OneDrive.ClientId";
        private const string OneDriveTenantIdKey = Prefix + "OneDrive.TenantId";
        private const string OneDriveRefreshTokenKey = Prefix + "OneDrive.RefreshToken";

        private const string GoogleServiceAccountKeyPathKey =
            Prefix + "Google.ServiceAccountKeyPath";

        public static OneDriveCredentialStore OneDrive { get; } = new();
        public static GoogleServiceAccountStore GoogleServiceAccount { get; } = new();

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
        /// Clears the stored Google service account key file path.
        /// </summary>
        public static void ClearGoogleServiceAccount()
        {
            EditorPrefs.DeleteKey(GoogleServiceAccountKeyPathKey);
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

        /// <summary>
        /// Google service account key file path storage.
        /// The actual key content lives in the JSON file on disk;
        /// only the path is stored in EditorPrefs.
        /// The key file is read by <c>ServiceAccountStrategy</c> in the
        /// GoogleSheets assembly. This store exists in the Editor assembly
        /// so that <c>DataGraphSettingsProvider</c> can read/write the path
        /// without cross-referencing the GoogleSheets assembly.
        /// Both assemblies use the same EditorPrefs key.
        /// </summary>
        internal sealed class GoogleServiceAccountStore
        {
            public string KeyFilePath
            {
                get => EditorPrefs.GetString(GoogleServiceAccountKeyPathKey, "");
                set => EditorPrefs.SetString(GoogleServiceAccountKeyPathKey, value ?? "");
            }

            public bool HasKeyFile =>
                !string.IsNullOrEmpty(KeyFilePath)
                && System.IO.File.Exists(KeyFilePath);
        }
    }
}
