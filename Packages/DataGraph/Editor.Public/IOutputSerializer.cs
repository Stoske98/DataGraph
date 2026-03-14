using DataGraph.Runtime;

namespace DataGraph.Editor.Public
{
    /// <summary>
    /// Contract for output format serializers. Each format (SO, JSON, Blob, QSO)
    /// implements this interface to transform parsed data into output files.
    /// </summary>
    public interface IOutputSerializer
    {
        /// <summary>
        /// Unique identifier for this format (e.g. "SO", "JSON").
        /// </summary>
        string FormatId { get; }

        /// <summary>
        /// Human-readable name shown in Parse Runner UI.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Whether this format generates C# class files.
        /// </summary>
        bool GeneratesCode { get; }

        /// <summary>
        /// Serializes parsed data into output files at the given base path.
        /// </summary>
        Result<SerializationOutput> Serialize(
            SerializationContext context);
    }
}
