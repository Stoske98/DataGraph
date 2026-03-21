using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;

namespace DataGraph.LocalFile
{
    /// <summary>
    /// Parses .xlsx files into a jagged string array.
    /// XLSX is a ZIP archive containing XML files. This parser reads
    /// shared strings, workbook sheet names, and worksheet cell data
    /// using only System.IO.Compression and System.Xml.Linq — no
    /// external dependencies required.
    /// </summary>
    internal static class XlsxParser
    {
        private static readonly XNamespace SpreadsheetNs =
            "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace RelationshipNs =
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static readonly XNamespace PackageRelNs =
            "http://schemas.openxmlformats.org/package/2006/relationships";

        /// <summary>
        /// Parses a worksheet from an .xlsx file by sheet name.
        /// Returns all rows as string arrays. If sheetName is null,
        /// reads the first sheet.
        /// </summary>
        public static string[][] Parse(string filePath, string sheetName = null)
        {
            using var stream = File.OpenRead(filePath);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

            var sharedStrings = ReadSharedStrings(archive);
            var sheetPath = ResolveSheetPath(archive, sheetName);

            if (string.IsNullOrEmpty(sheetPath))
                throw new InvalidOperationException(
                    $"Sheet '{sheetName ?? "(first)"}' not found in '{Path.GetFileName(filePath)}'.");

            return ReadSheet(archive, sheetPath, sharedStrings);
        }

        /// <summary>
        /// Returns all sheet names in the workbook.
        /// </summary>
        public static List<string> GetSheetNames(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

            var names = new List<string>();
            var workbookEntry = archive.GetEntry("xl/workbook.xml");
            if (workbookEntry == null) return names;

            using var reader = workbookEntry.Open();
            var doc = XDocument.Load(reader);
            var sheets = doc.Root?.Element(SpreadsheetNs + "sheets");
            if (sheets == null) return names;

            foreach (var sheet in sheets.Elements(SpreadsheetNs + "sheet"))
            {
                var name = sheet.Attribute("name")?.Value;
                if (!string.IsNullOrEmpty(name))
                    names.Add(name);
            }

            return names;
        }

        private static List<string> ReadSharedStrings(ZipArchive archive)
        {
            var strings = new List<string>();
            var entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry == null) return strings;

            using var reader = entry.Open();
            var doc = XDocument.Load(reader);

            foreach (var si in doc.Root.Elements(SpreadsheetNs + "si"))
            {
                var tElement = si.Element(SpreadsheetNs + "t");
                if (tElement != null)
                {
                    strings.Add(tElement.Value);
                    continue;
                }

                var richText = new System.Text.StringBuilder();
                foreach (var r in si.Elements(SpreadsheetNs + "r"))
                {
                    var rt = r.Element(SpreadsheetNs + "t");
                    if (rt != null)
                        richText.Append(rt.Value);
                }
                strings.Add(richText.ToString());
            }

            return strings;
        }

        private static string ResolveSheetPath(ZipArchive archive, string sheetName)
        {
            var workbookEntry = archive.GetEntry("xl/workbook.xml");
            if (workbookEntry == null) return null;

            using var wbReader = workbookEntry.Open();
            var workbook = XDocument.Load(wbReader);
            var sheets = workbook.Root?.Element(SpreadsheetNs + "sheets");
            if (sheets == null) return null;

            string targetRId = null;

            if (string.IsNullOrEmpty(sheetName))
            {
                var firstSheet = sheets.Element(SpreadsheetNs + "sheet");
                targetRId = firstSheet?.Attribute(RelationshipNs + "id")?.Value;
            }
            else
            {
                foreach (var sheet in sheets.Elements(SpreadsheetNs + "sheet"))
                {
                    if (string.Equals(sheet.Attribute("name")?.Value, sheetName, StringComparison.OrdinalIgnoreCase))
                    {
                        targetRId = sheet.Attribute(RelationshipNs + "id")?.Value;
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(targetRId)) return null;

            var relsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");
            if (relsEntry == null) return null;

            using var relsReader = relsEntry.Open();
            var rels = XDocument.Load(relsReader);

            foreach (var rel in rels.Root.Elements(PackageRelNs + "Relationship"))
            {
                if (rel.Attribute("Id")?.Value == targetRId)
                {
                    var target = rel.Attribute("Target")?.Value;
                    if (target != null && !target.StartsWith("xl/"))
                        target = "xl/" + target;
                    return target;
                }
            }

            return null;
        }

        private static string[][] ReadSheet(ZipArchive archive, string sheetPath, List<string> sharedStrings)
        {
            var entry = archive.GetEntry(sheetPath);
            if (entry == null) return Array.Empty<string[]>();

            using var reader = entry.Open();
            var doc = XDocument.Load(reader);
            var sheetData = doc.Root?.Element(SpreadsheetNs + "sheetData");
            if (sheetData == null) return Array.Empty<string[]>();

            var rows = new List<string[]>();
            int maxCol = 0;

            foreach (var rowEl in sheetData.Elements(SpreadsheetNs + "row"))
            {
                var cells = new SortedDictionary<int, string>();

                foreach (var cellEl in rowEl.Elements(SpreadsheetNs + "c"))
                {
                    var cellRef = cellEl.Attribute("r")?.Value;
                    if (string.IsNullOrEmpty(cellRef)) continue;

                    int colIndex = CellRefToColumnIndex(cellRef);
                    var cellType = cellEl.Attribute("t")?.Value;
                    var valueEl = cellEl.Element(SpreadsheetNs + "v");
                    var inlineStr = cellEl.Element(SpreadsheetNs + "is");

                    string cellValue = "";

                    if (cellType == "s" && valueEl != null)
                    {
                        if (int.TryParse(valueEl.Value, out int ssIndex) &&
                            ssIndex >= 0 && ssIndex < sharedStrings.Count)
                            cellValue = sharedStrings[ssIndex];
                    }
                    else if (cellType == "inlineStr" && inlineStr != null)
                    {
                        var t = inlineStr.Element(SpreadsheetNs + "t");
                        cellValue = t?.Value ?? "";
                    }
                    else if (valueEl != null)
                    {
                        cellValue = valueEl.Value;
                    }

                    cells[colIndex] = cellValue;
                    if (colIndex > maxCol) maxCol = colIndex;
                }

                var row = new string[maxCol + 1];
                for (int c = 0; c <= maxCol; c++)
                    row[c] = cells.TryGetValue(c, out var v) ? v : "";

                rows.Add(row);
            }

            NormalizeRowWidths(rows, maxCol + 1);
            return rows.ToArray();
        }

        private static void NormalizeRowWidths(List<string[]> rows, int width)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Length < width)
                {
                    var padded = new string[width];
                    Array.Copy(rows[i], padded, rows[i].Length);
                    for (int c = rows[i].Length; c < width; c++)
                        padded[c] = "";
                    rows[i] = padded;
                }
            }
        }

        private static int CellRefToColumnIndex(string cellRef)
        {
            int col = 0;
            for (int i = 0; i < cellRef.Length; i++)
            {
                char c = cellRef[i];
                if (c >= 'A' && c <= 'Z')
                    col = col * 26 + (c - 'A' + 1);
                else
                    break;
            }
            return col - 1;
        }
    }
}
