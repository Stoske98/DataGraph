using System;
using System.IO;
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
        public bool IsAuthenticated => OneDriveCredentials.IsConfigured;

        public async Task<Result<RawTableData>> FetchAsync(
            SheetReference reference,
            CancellationToken cancellationToken = default)
        {
            var descriptor = NormalizeDescriptor(reference.SheetId);
            var worksheetName = !string.IsNullOrEmpty(reference.Range)
                ? reference.Range
                : "Sheet1";

            if (OneDriveCredentials.IsConfigured)
            {
                var fetcher = new OneDriveFetcher(
                    OneDriveCredentials.ClientId,
                    OneDriveCredentials.TenantId);

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
                return ParseXlsxBytes(bytes, worksheetName);
            }
            catch (Exception ex)
            {
                return Result<RawTableData>.Failure(
                    $"Failed to parse Excel file: {ex.Message}");
            }
        }

        private static Result<RawTableData> ParseXlsxBytes(
            byte[] xlsxBytes, string worksheetName)
        {
            using var stream = new MemoryStream(xlsxBytes);
            using var archive = new System.IO.Compression.ZipArchive(
                stream, System.IO.Compression.ZipArchiveMode.Read);

            var sharedStrings = ReadSharedStrings(archive);
            var sheetEntry = FindWorksheetEntry(archive, worksheetName);
            if (sheetEntry == null)
                return Result<RawTableData>.Failure(
                    $"Worksheet '{worksheetName}' not found in the Excel file.");

            using var wsStream = sheetEntry.Open();
            var wsDoc = System.Xml.Linq.XDocument.Load(wsStream);
            var ns = wsDoc.Root?.Name.Namespace;
            if (ns == null)
                return Result<RawTableData>.Failure("Invalid worksheet XML.");

            var allRows = new System.Collections.Generic.SortedDictionary<
                int, System.Collections.Generic.Dictionary<int, string>>();
            int maxCol = 0;

            foreach (var row in wsDoc.Descendants(ns + "row"))
            {
                if (!int.TryParse(row.Attribute("r")?.Value, out var rowIdx))
                    continue;
                rowIdx--;

                var cells = new System.Collections.Generic.Dictionary<int, string>();
                foreach (var cell in row.Elements(ns + "c"))
                {
                    var cellRef = cell.Attribute("r")?.Value ?? "";
                    var colIdx = CellRefToColumnIndex(cellRef);
                    var cellType = cell.Attribute("t")?.Value;
                    var vElem = cell.Element(ns + "v");
                    var value = vElem?.Value ?? "";

                    if (cellType == "s" && int.TryParse(value, out var ssIdx)
                        && ssIdx < sharedStrings.Count)
                        value = sharedStrings[ssIdx];

                    cells[colIdx] = value;
                    if (colIdx >= maxCol) maxCol = colIdx + 1;
                }

                allRows[rowIdx] = cells;
            }

            if (allRows.Count == 0)
                return Result<RawTableData>.Failure("Worksheet has no data.");

            string[] headers = null;
            var dataRows = new System.Collections.Generic.List<string[]>();
            bool isFirst = true;

            foreach (var kvp in allRows)
            {
                var row = new string[maxCol];
                foreach (var cell in kvp.Value)
                    if (cell.Key < maxCol)
                        row[cell.Key] = cell.Value ?? "";

                for (int i = 0; i < row.Length; i++)
                    row[i] ??= "";

                if (isFirst) { headers = row; isFirst = false; continue; }
                dataRows.Add(row);
            }

            if (headers == null)
                return Result<RawTableData>.Failure("No header row found.");

            return Result<RawTableData>.Success(
                new RawTableData(dataRows.ToArray(), headers));
        }

        private static System.Collections.Generic.List<string> ReadSharedStrings(
            System.IO.Compression.ZipArchive archive)
        {
            var strings = new System.Collections.Generic.List<string>();
            var sst = archive.GetEntry("xl/sharedStrings.xml");
            if (sst == null) return strings;

            using var sstStream = sst.Open();
            var doc = System.Xml.Linq.XDocument.Load(sstStream);
            var ns = doc.Root?.Name.Namespace;
            if (ns == null) return strings;

            foreach (var si in doc.Descendants(ns + "si"))
            {
                var t = si.Element(ns + "t");
                if (t != null) { strings.Add(t.Value); continue; }

                var text = "";
                foreach (var r in si.Elements(ns + "r"))
                {
                    var rt = r.Element(ns + "t");
                    if (rt != null) text += rt.Value;
                }
                strings.Add(text);
            }

            return strings;
        }

        private static System.IO.Compression.ZipArchiveEntry FindWorksheetEntry(
            System.IO.Compression.ZipArchive archive, string worksheetName)
        {
            var wb = archive.GetEntry("xl/workbook.xml");
            if (wb != null)
            {
                using var wbStream = wb.Open();
                var wbDoc = System.Xml.Linq.XDocument.Load(wbStream);
                var ns = wbDoc.Root?.Name.Namespace;
                if (ns != null)
                {
                    int idx = 0;
                    foreach (var sheet in wbDoc.Descendants(ns + "sheet"))
                    {
                        idx++;
                        var name = sheet.Attribute("name")?.Value;
                        if (string.Equals(name, worksheetName,
                            StringComparison.OrdinalIgnoreCase))
                        {
                            var entry = archive.GetEntry(
                                $"xl/worksheets/sheet{idx}.xml");
                            if (entry != null) return entry;
                        }
                    }
                }
            }

            return archive.GetEntry("xl/worksheets/sheet1.xml");
        }

        private static int CellRefToColumnIndex(string cellRef)
        {
            int col = 0;
            foreach (var c in cellRef)
            {
                if (c < 'A' || c > 'Z') break;
                col = col * 26 + (c - 'A' + 1);
            }
            return col - 1;
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
