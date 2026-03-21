using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DataGraph.Editor.Public;
using DataGraph.Runtime;

namespace DataGraph.LocalFile
{
    /// <summary>
    /// ISheetProvider implementation for local CSV and XLSX files.
    /// Auto-detects format by file extension. SheetReference.SheetId
    /// is the file path (absolute or project-relative).
    /// SheetReference.Range is the sheet/tab name for XLSX files.
    /// </summary>
    internal sealed class LocalFileProvider : ISheetProvider
    {
        public string ProviderId => "LocalFile";

        public string DisplayName => "Local File (CSV / Excel)";

        public bool IsAuthenticated => true;

        /// <summary>
        /// Reads tabular data from a local CSV or XLSX file.
        /// </summary>
        public Task<Result<RawTableData>> FetchAsync(
            SheetReference reference,
            CancellationToken cancellationToken = default)
        {
            if (reference == null)
                return Task.FromResult(Result<RawTableData>.Failure("Sheet reference is null."));

            if (string.IsNullOrEmpty(reference.SheetId))
                return Task.FromResult(Result<RawTableData>.Failure("File path is empty."));

            try
            {
                var filePath = reference.SheetId;

                if (!File.Exists(filePath))
                    return Task.FromResult(Result<RawTableData>.Failure(
                        $"File not found: {filePath}"));

                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                string[][] allRows;

                switch (extension)
                {
                    case ".csv":
                        allRows = CsvParser.Parse(filePath);
                        break;
                    case ".tsv":
                        allRows = CsvParser.Parse(filePath, '\t');
                        break;
                    case ".xlsx":
                        allRows = XlsxParser.Parse(filePath, reference.Range);
                        break;
                    default:
                        return Task.FromResult(Result<RawTableData>.Failure(
                            $"Unsupported file format: {extension}. Supported: .csv, .tsv, .xlsx"));
                }

                if (allRows.Length == 0)
                    return Task.FromResult(Result<RawTableData>.Failure(
                        "File contains no data."));

                var headerOffset = reference.HeaderRowOffset;
                if (headerOffset < 1) headerOffset = 1;

                if (allRows.Length < headerOffset)
                    return Task.FromResult(Result<RawTableData>.Failure(
                        $"File has {allRows.Length} rows but header offset is {headerOffset}."));

                var headers = allRows[headerOffset - 1];

                var dataRowCount = allRows.Length - headerOffset;
                var dataRows = new string[dataRowCount][];
                Array.Copy(allRows, headerOffset, dataRows, 0, dataRowCount);

                var tableData = new RawTableData(dataRows, headers);
                return Task.FromResult(Result<RawTableData>.Success(tableData));
            }
            catch (Exception ex)
            {
                return Task.FromResult(Result<RawTableData>.Failure(
                    $"Failed to read file: {ex.Message}"));
            }
        }
    }
}
