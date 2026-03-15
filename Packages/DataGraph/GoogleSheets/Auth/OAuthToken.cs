using System;
using UnityEngine;

namespace DataGraph.GoogleSheets.Auth
{
    /// <summary>
    /// Serializable OAuth 2.0 token data stored locally in EditorPrefs.
    /// </summary>
    [Serializable]
    internal class OAuthToken
    {
        [SerializeField] private string _accessToken;
        [SerializeField] private string _refreshToken;
        [SerializeField] private long _expiryTimeTicks;

        public string AccessToken
        {
            get => _accessToken;
            set => _accessToken = value;
        }

        public string RefreshToken
        {
            get => _refreshToken;
            set => _refreshToken = value;
        }

        public DateTime ExpiryTime
        {
            get => new DateTime(_expiryTimeTicks, DateTimeKind.Utc);
            set => _expiryTimeTicks = value.Ticks;
        }

        public bool IsExpired => DateTime.UtcNow >= ExpiryTime.AddSeconds(-60);
        public bool CanRefresh => !string.IsNullOrEmpty(_refreshToken);
    }
}
