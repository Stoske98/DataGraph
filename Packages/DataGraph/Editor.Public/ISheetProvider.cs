using System.Threading;
using System.Threading.Tasks;
using DataGraph.Runtime;

namespace DataGraph.Editor.Public
{
    /// <summary>
    /// Contract for data source providers that fetch raw tabular data
    /// from external sources. V1 ships with Google Sheets; V2 adds
    /// Excel, CSV, and custom providers.
    /// </summary>
    public interface ISheetProvider
    {
        /// <summary>
        /// Unique identifier for this provider type (e.g. "GoogleSheets").
        /// </summary>
        string ProviderId { get; }

        /// <summary>
        /// Human-readable name shown in the editor UI.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Whether this provider is currently authenticated and ready to fetch.
        /// </summary>
        bool IsAuthenticated { get; }

        /// <summary>
        /// Fetches raw table data from the given sheet reference.
        /// Returns a string matrix where each row is an array of cell values.
        /// </summary>
        Task<Result<RawTableData>> FetchAsync(
            SheetReference reference,
            CancellationToken cancellationToken = default);
    }
}
