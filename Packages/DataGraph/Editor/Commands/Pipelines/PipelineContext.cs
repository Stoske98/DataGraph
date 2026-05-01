using System.Collections.Generic;
using DataGraph.Editor.Domain;
using DataGraph.Editor.UI;

namespace DataGraph.Editor.Commands.Pipelines
{
    /// <summary>
    /// Shared inputs for all output pipelines (SO / Blob / Quantum / JSON
    /// / Enum). Constructed once by ParseGraphCommand after Adapt + Fetch
    /// + Parse + Validate succeed; each pipeline reads what it needs.
    /// </summary>
    internal sealed class PipelineContext
    {
        /// <summary>User-facing graph name (file basename minus extension).</summary>
        public string GraphName { get; }

        /// <summary>Adapter output — read by codegen pipelines.</summary>
        public ParseableGraph Graph { get; }

        /// <summary>
        /// Parser output. Wraps ParsedEnumDefinition for enum graphs.
        /// May be null only for the rare branch where a pipeline is invoked
        /// purely for codegen without prior parse — current pipelines always
        /// receive a non-null tree.
        /// </summary>
        public ParsedDataTree DataTree { get; }

        /// <summary>Root output folder configured in DataGraphSettings.</summary>
        public string OutputBasePath { get; }

        /// <summary>OutputBasePath / GraphName — convenience for codegen pipelines.</summary>
        public string GraphOutputPath { get; }

        /// <summary>Format flags (GenerateSO / GenerateJSON / etc.).</summary>
        public ParseGraphCommand.FormatSelection Formats { get; }

        /// <summary>Console log group; pipelines append progress + errors here.</summary>
        public GraphLogGroup Log { get; }

        /// <summary>
        /// Files written by pipelines this run. Each pipeline appends its
        /// outputs so the orchestrator can report a final count.
        /// </summary>
        public List<string> GeneratedFiles { get; }

        public PipelineContext(
            string graphName,
            ParseableGraph graph,
            ParsedDataTree dataTree,
            string outputBasePath,
            string graphOutputPath,
            ParseGraphCommand.FormatSelection formats,
            GraphLogGroup log,
            List<string> generatedFiles)
        {
            GraphName = graphName;
            Graph = graph;
            DataTree = dataTree;
            OutputBasePath = outputBasePath;
            GraphOutputPath = graphOutputPath;
            Formats = formats;
            Log = log;
            GeneratedFiles = generatedFiles;
        }
    }
}
