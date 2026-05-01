using System.Threading;
using System.Threading.Tasks;

namespace DataGraph.Editor.Commands.Pipelines
{
    /// <summary>
    /// One stage of the parse-time output pipeline (SO codegen, Blob codegen,
    /// Quantum codegen, JSON serialize, Enum codegen). Pipelines run in
    /// sequence but are independent — failure of one short-circuits the run.
    /// </summary>
    internal interface IOutputPipeline
    {
        /// <summary>
        /// True if this pipeline should execute for the given context.
        /// Format pipelines check their format flag; the EnumPipeline checks
        /// whether the graph root is an enum/flag definition.
        /// </summary>
        bool ShouldRun(PipelineContext context);

        /// <summary>
        /// Runs the pipeline. Returns true on success or skip, false on
        /// failure (in which case the orchestrator aborts). Pipelines are
        /// expected to log their own progress and errors via context.Log.
        /// </summary>
        Task<bool> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken);
    }
}
