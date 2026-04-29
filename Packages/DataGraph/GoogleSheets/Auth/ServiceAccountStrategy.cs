using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DataGraph.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace DataGraph.GoogleSheets.Auth
{
    /// <summary>
    /// Authentication via Google Cloud service account JSON key file.
    /// Creates a signed JWT, exchanges it for an access token at
    /// Google's token endpoint, and caches the token until expiry.
    /// No interactive login required — suitable for CI/CD pipelines
    /// and headless environments.
    /// The target spreadsheet must be shared with the service account's
    /// <c>client_email</c> address.
    /// </summary>
    internal sealed class ServiceAccountStrategy : IAuthStrategy
    {
        private const string PrefsKeyPath = "DataGraph_Google_ServiceAccountKeyPath";
        private const string Scope = "https://www.googleapis.com/auth/spreadsheets.readonly";
        private const int TokenLifetimeSeconds = 3600;
        private const int TokenRefreshMarginSeconds = 60;

        private ServiceAccountKey _cachedKey;
        private string _cachedAccessToken;
        private DateTime _tokenExpiry = DateTime.MinValue;

        public string MethodId => "ServiceAccount";
        public string DisplayName => "Service Account (CI/CD)";
        public bool RequiresInteractiveAuth => false;

        public bool IsConfigured
        {
            get
            {
                var path = GetKeyFilePath();
                if (string.IsNullOrEmpty(path))
                    return false;
                return System.IO.File.Exists(path);
            }
        }

        /// <summary>
        /// Stores the file system path to the JSON key file.
        /// The file itself is never copied into EditorPrefs —
        /// only the path is persisted.
        /// </summary>
        public void SetKeyFilePath(string path)
        {
            EditorPrefs.SetString(PrefsKeyPath, path ?? "");
            _cachedKey = null;
            _cachedAccessToken = null;
            _tokenExpiry = DateTime.MinValue;
        }

        /// <summary>
        /// Returns the stored JSON key file path.
        /// </summary>
        public string GetKeyFilePath()
        {
            return EditorPrefs.GetString(PrefsKeyPath, "");
        }

        public Task<Result<bool>> AuthenticateAsync(
            CancellationToken cancellationToken = default)
        {
            var loadResult = LoadKey();
            if (loadResult.IsFailure)
                return Task.FromResult(Result<bool>.Failure(loadResult.Error));

            return Task.FromResult(Result<bool>.Success(true));
        }

        public async Task<Result<AuthCredentials>> GetCredentialsAsync(
            CancellationToken cancellationToken = default)
        {
            if (!IsTokenExpired())
                return Result<AuthCredentials>.Success(
                    AuthCredentials.FromBearerToken(_cachedAccessToken));

            var loadResult = LoadKey();
            if (loadResult.IsFailure)
                return Result<AuthCredentials>.Failure(loadResult.Error);

            var tokenResult = await ExchangeJwtForTokenAsync(
                loadResult.Value, cancellationToken);
            if (tokenResult.IsFailure)
                return Result<AuthCredentials>.Failure(tokenResult.Error);

            _cachedAccessToken = tokenResult.Value;
            _tokenExpiry = DateTime.UtcNow.AddSeconds(
                TokenLifetimeSeconds - TokenRefreshMarginSeconds);

            return Result<AuthCredentials>.Success(
                AuthCredentials.FromBearerToken(_cachedAccessToken));
        }

        public void SignOut()
        {
            EditorPrefs.DeleteKey(PrefsKeyPath);
            _cachedKey = null;
            _cachedAccessToken = null;
            _tokenExpiry = DateTime.MinValue;
        }

        /// <summary>
        /// Returns the client_email from the loaded key,
        /// or null if the key is not loaded.
        /// Used by Settings UI to display which account is configured.
        /// </summary>
        public string GetClientEmail()
        {
            var loadResult = LoadKey();
            return loadResult.IsSuccess ? loadResult.Value.ClientEmail : null;
        }

        private bool IsTokenExpired()
        {
            return string.IsNullOrEmpty(_cachedAccessToken)
                   || DateTime.UtcNow >= _tokenExpiry;
        }

        private Result<ServiceAccountKey> LoadKey()
        {
            if (_cachedKey != null && _cachedKey.IsValid)
                return Result<ServiceAccountKey>.Success(_cachedKey);

            var path = GetKeyFilePath();
            if (string.IsNullOrEmpty(path))
                return Result<ServiceAccountKey>.Failure(
                    "Service account key file path is not configured. " +
                    "Set it in Project Settings > DataGraph.");

            if (!System.IO.File.Exists(path))
                return Result<ServiceAccountKey>.Failure(
                    $"Service account key file not found: {path}");

            try
            {
                var json = System.IO.File.ReadAllText(path);
                var key = JsonUtility.FromJson<ServiceAccountKey>(json);

                if (key == null || !key.IsValid)
                    return Result<ServiceAccountKey>.Failure(
                        "Invalid service account key file. " +
                        "Ensure it contains private_key, client_email, " +
                        "and private_key_id fields.");

                _cachedKey = key;
                return Result<ServiceAccountKey>.Success(key);
            }
            catch (Exception ex)
            {
                return Result<ServiceAccountKey>.Failure(
                    $"Failed to read service account key: {ex.Message}");
            }
        }

        private static async Task<Result<string>> ExchangeJwtForTokenAsync(
            ServiceAccountKey key, CancellationToken cancellationToken)
        {
            var jwtResult = CreateSignedJwt(key);
            if (jwtResult.IsFailure)
                return Result<string>.Failure(jwtResult.Error);

            var form = new WWWForm();
            form.AddField("grant_type",
                "urn:ietf:params:oauth:grant-type:jwt-bearer");
            form.AddField("assertion", jwtResult.Value);

            try
            {
                using var request = UnityWebRequest.Post(key.TokenUri, form);
                var operation = request.SendWebRequest();

                while (!operation.isDone)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Yield();
                }

                if (request.result != UnityWebRequest.Result.Success)
                    return Result<string>.Failure(
                        $"Token exchange failed (HTTP {request.responseCode}): " +
                        request.downloadHandler.text);

                var response = JsonUtility.FromJson<TokenResponse>(
                    request.downloadHandler.text);

                if (string.IsNullOrEmpty(response.access_token))
                    return Result<string>.Failure(
                        "Token response contains no access_token.");

                return Result<string>.Success(response.access_token);
            }
            catch (OperationCanceledException)
            {
                return Result<string>.Failure("Token exchange was cancelled.");
            }
            catch (Exception ex)
            {
                return Result<string>.Failure(
                    $"Token exchange failed: {ex.Message}");
            }
        }

        private static Result<string> CreateSignedJwt(ServiceAccountKey key)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var header = "{" +
                $"\"alg\":\"RS256\"," +
                $"\"typ\":\"JWT\"," +
                $"\"kid\":\"{EscapeJsonString(key.PrivateKeyId)}\"" +
                "}";

            var claims = "{" +
                $"\"iss\":\"{EscapeJsonString(key.ClientEmail)}\"," +
                $"\"scope\":\"{Scope}\"," +
                $"\"aud\":\"{EscapeJsonString(key.TokenUri)}\"," +
                $"\"iat\":{now}," +
                $"\"exp\":{now + TokenLifetimeSeconds}" +
                "}";

            var headerB64 = Base64Url.Encode(Encoding.UTF8.GetBytes(header));
            var claimsB64 = Base64Url.Encode(Encoding.UTF8.GetBytes(claims));
            var unsignedToken = $"{headerB64}.{claimsB64}";

            var rsaResult = ImportRsaKey(key.PrivateKeyPem);
            if (rsaResult.IsFailure)
                return Result<string>.Failure(rsaResult.Error);

            try
            {
                using var rsa = rsaResult.Value;
                var signature = rsa.SignData(
                    Encoding.UTF8.GetBytes(unsignedToken),
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1);

                var signatureB64 = Base64Url.Encode(signature);
                return Result<string>.Success(
                    $"{unsignedToken}.{signatureB64}");
            }
            catch (Exception ex)
            {
                return Result<string>.Failure(
                    $"JWT signing failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Imports an RSA private key from PEM-encoded PKCS#8 format.
        /// Google service account JSON keys use PKCS#8 with
        /// "BEGIN PRIVATE KEY" markers.
        /// Supports both CoreCLR (ImportPkcs8PrivateKey) and
        /// Mono (manual ASN.1/DER parsing) runtimes.
        /// </summary>
        private static Result<RSA> ImportRsaKey(string pem)
        {
            if (string.IsNullOrEmpty(pem))
                return Result<RSA>.Failure("Private key PEM is empty.");

            try
            {
                var pemText = pem.Replace("\\n", "\n");

                var base64 = pemText
                    .Replace("-----BEGIN PRIVATE KEY-----", "")
                    .Replace("-----END PRIVATE KEY-----", "")
                    .Replace("\n", "")
                    .Replace("\r", "")
                    .Trim();

                var keyBytes = Convert.FromBase64String(base64);

                var rsa = RSA.Create();

                try
                {
                    rsa.ImportPkcs8PrivateKey(keyBytes, out _);
                    return Result<RSA>.Success(rsa);
                }
                catch (PlatformNotSupportedException)
                {
                    rsa.Dispose();
                }
                catch (NotImplementedException)
                {
                    rsa.Dispose();
                }

                return ImportRsaKeyFromDer(keyBytes);
            }
            catch (Exception ex)
            {
                return Result<RSA>.Failure(
                    $"Failed to import RSA key from PEM: {ex.Message}. " +
                    "Ensure the key file is a valid Google service account JSON key.");
            }
        }

        /// <summary>
        /// Manually parses a PKCS#8 DER-encoded private key into
        /// RSAParameters. Works on Mono where ImportPkcs8PrivateKey
        /// is not available.
        /// PKCS#8 structure:
        ///   SEQUENCE {
        ///     INTEGER (version = 0)
        ///     SEQUENCE { OID rsaEncryption, NULL }
        ///     OCTET STRING { PKCS#1 RSAPrivateKey }
        ///   }
        /// PKCS#1 RSAPrivateKey:
        ///   SEQUENCE {
        ///     INTEGER version
        ///     INTEGER modulus (n)
        ///     INTEGER publicExponent (e)
        ///     INTEGER privateExponent (d)
        ///     INTEGER prime1 (p)
        ///     INTEGER prime2 (q)
        ///     INTEGER exponent1 (dp)
        ///     INTEGER exponent2 (dq)
        ///     INTEGER coefficient (iq)
        ///   }
        /// </summary>
        private static Result<RSA> ImportRsaKeyFromDer(byte[] pkcs8Bytes)
        {
            try
            {
                int offset = 0;

                // Outer SEQUENCE (PKCS#8 wrapper)
                ExpectTag(pkcs8Bytes, ref offset, 0x30);
                ReadLength(pkcs8Bytes, ref offset);

                // Version INTEGER (should be 0)
                ExpectTag(pkcs8Bytes, ref offset, 0x02);
                var versionLen = ReadLength(pkcs8Bytes, ref offset);
                offset += versionLen;

                // AlgorithmIdentifier SEQUENCE
                ExpectTag(pkcs8Bytes, ref offset, 0x30);
                var algLen = ReadLength(pkcs8Bytes, ref offset);
                offset += algLen;

                // PrivateKey OCTET STRING (contains PKCS#1)
                ExpectTag(pkcs8Bytes, ref offset, 0x04);
                ReadLength(pkcs8Bytes, ref offset);

                // Now we're at the PKCS#1 RSAPrivateKey SEQUENCE
                ExpectTag(pkcs8Bytes, ref offset, 0x30);
                ReadLength(pkcs8Bytes, ref offset);

                // PKCS#1 version
                var rsaVersion = ReadIntegerRaw(pkcs8Bytes, ref offset);

                // RSA parameters in order
                var modulus = ReadIntegerRaw(pkcs8Bytes, ref offset);
                var exponent = ReadIntegerRaw(pkcs8Bytes, ref offset);
                var d = ReadIntegerRaw(pkcs8Bytes, ref offset);
                var p = ReadIntegerRaw(pkcs8Bytes, ref offset);
                var q = ReadIntegerRaw(pkcs8Bytes, ref offset);
                var dp = ReadIntegerRaw(pkcs8Bytes, ref offset);
                var dq = ReadIntegerRaw(pkcs8Bytes, ref offset);
                var iq = ReadIntegerRaw(pkcs8Bytes, ref offset);

                var rsaParams = new RSAParameters
                {
                    Modulus = modulus,
                    Exponent = exponent,
                    D = PadToLength(d, modulus.Length),
                    P = p,
                    Q = q,
                    DP = PadToLength(dp, p.Length),
                    DQ = PadToLength(dq, q.Length),
                    InverseQ = PadToLength(iq, q.Length)
                };

                var rsa = RSA.Create();
                rsa.ImportParameters(rsaParams);
                return Result<RSA>.Success(rsa);
            }
            catch (Exception ex)
            {
                return Result<RSA>.Failure(
                    $"Manual PKCS#8 parsing failed: {ex.Message}");
            }
        }

        private static void ExpectTag(byte[] data, ref int offset, byte expectedTag)
        {
            if (offset >= data.Length)
                throw new InvalidOperationException(
                    $"Unexpected end of data at offset {offset}.");
            if (data[offset] != expectedTag)
                throw new InvalidOperationException(
                    $"Expected ASN.1 tag 0x{expectedTag:X2} but got " +
                    $"0x{data[offset]:X2} at offset {offset}.");
            offset++;
        }

        private static int ReadLength(byte[] data, ref int offset)
        {
            if (offset >= data.Length)
                throw new InvalidOperationException("Unexpected end of data.");

            var b = data[offset++];
            if (b < 0x80)
                return b;

            int numBytes = b & 0x7F;
            if (numBytes > 4)
                throw new InvalidOperationException(
                    $"ASN.1 length encoding too large: {numBytes} bytes.");

            int length = 0;
            for (int i = 0; i < numBytes; i++)
            {
                if (offset >= data.Length)
                    throw new InvalidOperationException("Unexpected end of data.");
                length = (length << 8) | data[offset++];
            }

            return length;
        }

        /// <summary>
        /// Reads an ASN.1 INTEGER and returns the unsigned magnitude bytes.
        /// Strips leading zero padding byte if present (used for
        /// positive encoding of values whose MSB is set).
        /// </summary>
        private static byte[] ReadIntegerRaw(byte[] data, ref int offset)
        {
            ExpectTag(data, ref offset, 0x02);
            var length = ReadLength(data, ref offset);

            if (offset + length > data.Length)
                throw new InvalidOperationException(
                    $"INTEGER at offset {offset} extends past data length.");

            var value = new byte[length];
            Array.Copy(data, offset, value, 0, length);
            offset += length;

            if (value.Length > 1 && value[0] == 0x00)
            {
                var trimmed = new byte[value.Length - 1];
                Array.Copy(value, 1, trimmed, 0, trimmed.Length);
                return trimmed;
            }

            return value;
        }

        /// <summary>
        /// Left-pads a byte array with zeros to reach the target length.
        /// RSAParameters requires D, DP, DQ, InverseQ to match
        /// the byte length of their corresponding component
        /// (Modulus, P, Q respectively).
        /// </summary>
        private static byte[] PadToLength(byte[] data, int targetLength)
        {
            if (data.Length >= targetLength)
                return data;

            var padded = new byte[targetLength];
            Array.Copy(data, 0, padded, targetLength - data.Length, data.Length);
            return padded;
        }

        private static string EscapeJsonString(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }

        [Serializable]
        private struct TokenResponse
        {
            public string access_token;
            public int expires_in;
        }
    }
}
