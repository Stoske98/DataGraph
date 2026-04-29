using System;

namespace DataGraph.Runtime
{
    /// <summary>
    /// Base64 URL-safe encoding helpers.
    /// Used by OAuth/JWT flows where '+' '/' and '=' are not valid in URLs
    /// or JOSE compact serialization.
    /// </summary>
    internal static class Base64Url
    {
        /// <summary>
        /// Encodes a byte array as a URL-safe Base64 string:
        /// trims '=' padding, replaces '+' with '-' and '/' with '_'.
        /// </summary>
        public static string Encode(byte[] data)
        {
            return Convert.ToBase64String(data)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }
    }
}
