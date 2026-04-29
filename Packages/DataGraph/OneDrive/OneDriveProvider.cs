using System;
using System.Threading;
using System.Threading.Tasks;
using DataGraph.Editor.Public;
using DataGraph.OneDrive.Auth;
using DataGraph.OneDrive.Fetch;
using DataGraph.Runtime;
using UnityEngine;
using UnityEngine.Networking;

namespace DataGraph.OneDrive
{
    /// <summary>
    /// ISheetProvider implementation for OneDrive/SharePoint Excel files.
    /// Two modes:
    ///   1. Authenticated (OAuth) — full Graph API access for any file
    ///   2. Share link fallback — appends download=1 to the share URL,
    ///      downloads .xlsx directly, parses locally without auth
    /// Must be public for ProviderRegistry cross-assembly Type.GetType().
    /// </summary>
    public sealed class OneDriveProvider : ISheetProvider
    {
        public string ProviderId => "OneDrive";
        public string DisplayName => "OneDrive";
        public bool IsAuthenticated => OneDriveCredentials.IsActiveAuthConfigured;

        public async Task<Result<RawTableData>> FetchAsync(
            SheetReference reference,
            CancellationToken cancellationToken = default)
        {
            var descriptor = NormalizeDescriptor(reference.SheetId);
            var worksheetName = !string.IsNullOrEmpty(reference.Range)
                ? reference.Range
                : "Sheet1";

            if (OneDriveCredentials.IsAppOnlyConfigured
                && OneDriveCredentials.AuthMode == 1)
            {
                var fetcher = new OneDriveFetcher(
                    OneDriveCredentials.ClientId,
                    OneDriveCredentials.TenantId,
                    isAppOnly: true);

                return await fetcher.FetchAsync(
                    descriptor, worksheetName, cancellationToken,
                    reference.Columns);
            }

            if (OneDriveCredentials.IsConfigured)
            {
                var fetcher = new OneDriveFetcher(
                    OneDriveCredentials.ClientId,
                    OneDriveCredentials.TenantId,
                    isAppOnly: false);

                return await fetcher.FetchAsync(
                    descriptor, worksheetName, cancellationToken,
                    reference.Columns);
            }

            if (IsShareLink(reference.SheetId))
            {
                return await FetchViaDirectDownloadAsync(
                    reference.SheetId, worksheetName, cancellationToken);
            }

            return Result<RawTableData>.Failure(
                "OneDrive is not configured and the SheetId is not a share link. " +
                "Sign in through Project Settings > DataGraph, " +
                "or use a public OneDrive/SharePoint share link.");
        }

        /// <summary>
        /// Downloads .xlsx by appending download=1 to the share link.
        /// The 1drv.ms shortener follows redirects and returns the raw file.
        /// No authentication required for publicly shared files.
        /// </summary>
        private static async Task<Result<RawTableData>> FetchViaDirectDownloadAsync(
            string shareUrl, string worksheetName, CancellationToken ct)
        {
            Debug.Log($"[DataGraph] OneDrive direct download: {shareUrl}");
            var downloadUrl = shareUrl +
                (shareUrl.Contains("?") ? "&" : "?") + "download=1";

            using var request = UnityWebRequest.Get(downloadUrl);
            request.redirectLimit = 10;
            request.timeout = 30;

            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Delay(50, ct);
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                return Result<RawTableData>.Failure(
                    $"Failed to download file (HTTP {request.responseCode}). " +
                    "Ensure the share link is set to " +
                    "'Anyone with the link can view'.");
            }

            var bytes = request.downloadHandler.data;
            if (bytes == null || bytes.Length == 0)
                return Result<RawTableData>.Failure("Downloaded file is empty.");

            // Verify it's actually an XLSX (ZIP) file, not an HTML page
            if (bytes.Length < 4 || bytes[0] != 0x50 || bytes[1] != 0x4B)
            {
                var contentType = request.GetResponseHeader("Content-Type") ?? "";
                if (contentType.Contains("text/html"))
                {
                    return Result<RawTableData>.Failure(
                        "Share link returned an HTML page instead of an Excel file. " +
                        "The file may not be publicly shared, or the link format " +
                        "is not supported for direct download.");
                }

                return Result<RawTableData>.Failure(
                    $"Downloaded content is not a valid XLSX file " +
                    $"(Content-Type: {contentType}, Size: {bytes.Length} bytes).");
            }

            try
            {
                var rows = XlsxParser.Parse(bytes, worksheetName);
                if (rows.Length == 0)
                    return Result<RawTableData>.Failure("Worksheet has no data.");

                var headers = rows[0];
                var dataRows = new string[rows.Length - 1][];
                Array.Copy(rows, 1, dataRows, 0, rows.Length - 1);

                return Result<RawTableData>.Success(
                    new RawTableData(dataRows, headers));
            }
            catch (InvalidOperationException)
            {
                return Result<RawTableData>.Failure(
                    $"Worksheet '{worksheetName}' not found in the Excel file.");
            }
            catch (Exception ex)
            {
                return Result<RawTableData>.Failure(
                    $"Failed to parse Excel file: {ex.Message}");
            }
        }

        private static bool IsShareLink(string sheetId)
        {
            if (string.IsNullOrEmpty(sheetId)) return false;
            return sheetId.Contains("1drv.ms")
                   || sheetId.Contains("sharepoint.com")
                   || sheetId.Contains("onedrive.live.com");
        }

        private static string NormalizeDescriptor(string sheetId)
        {
            if (string.IsNullOrEmpty(sheetId)) return sheetId;
            if (sheetId.StartsWith("onedrive:///"))
                return sheetId.Substring("onedrive://".Length);
            if (sheetId.StartsWith("onedrive://"))
                return sheetId.Substring("onedrive://".Length);
            return sheetId;
        }
    }
}
