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

        public static bool IsConfigured => !string.IsNullOrEmpty(ClientId)
                                           && !string.IsNullOrEmpty(RefreshToken);

        public static bool HasClientId => !string.IsNullOrEmpty(ClientId);
    }
}
