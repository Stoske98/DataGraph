using System;
using UnityEngine;

namespace DataGraph.GoogleSheets.Auth
{
    /// <summary>
    /// Deserialized Google Cloud service account JSON key file.
    /// Field names match the JSON structure exactly for
    /// <see cref="JsonUtility"/> deserialization.
    /// Only the fields required for JWT-based token exchange are included.
    /// </summary>
    [Serializable]
    internal sealed class ServiceAccountKey
    {
        /// <summary>
        /// RSA private key in PEM format (PKCS#8).
        /// Contains literal "\n" escape sequences that must be
        /// converted to actual newlines before PEM parsing.
        /// </summary>
        [SerializeField] private string private_key;

        /// <summary>
        /// Service account email address.
        /// The target spreadsheet must be shared with this email.
        /// </summary>
        [SerializeField] private string client_email;

        /// <summary>
        /// Unique identifier for the private key.
        /// Included in JWT header as "kid" for key identification.
        /// </summary>
        [SerializeField] private string private_key_id;

        /// <summary>
        /// Google token endpoint URL.
        /// Typically "https://oauth2.googleapis.com/token".
        /// </summary>
        [SerializeField] private string token_uri;

        /// <summary>
        /// Google Cloud project identifier.
        /// </summary>
        [SerializeField] private string project_id;

        public string PrivateKeyPem => private_key;
        public string ClientEmail => client_email;
        public string PrivateKeyId => private_key_id;
        public string TokenUri => string.IsNullOrEmpty(token_uri)
            ? "https://oauth2.googleapis.com/token"
            : token_uri;
        public string ProjectId => project_id;

        /// <summary>
        /// Whether this key contains the minimum required fields
        /// for JWT creation and token exchange.
        /// </summary>
        public bool IsValid =>
            !string.IsNullOrEmpty(private_key)
            && !string.IsNullOrEmpty(client_email)
            && !string.IsNullOrEmpty(private_key_id);
    }
}
