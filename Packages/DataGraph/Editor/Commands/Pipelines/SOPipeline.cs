using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DataGraph.Editor.CodeGen;
using DataGraph.Editor.IO;

namespace DataGraph.Editor.Commands.Pipelines
{
    /// <summary>
    /// Generates {GraphName}.cs and {GraphName}Database.cs (entry classes
    /// + database asset wrapper) into &lt;output&gt;/{GraphName}/SO/.
    /// Asset population is a separate phase (ParseGraphCommand
    /// .CreateSOAssetsAsync) that runs after Unity recompiles.
    /// </summary>
    internal sealed class SOPipeline : IOutputPipeline
    {
        public bool ShouldRun(PipelineContext context) => context.Formats.GenerateSO;

        public Task<bool> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
        {
            var log = context.Log;
            try
            {
                log.LogInfo("Generate: creating C# classes...");
                var soPath = Path.Combine(context.GraphOutputPath, "SO");
                var codeGen = new CodeGenerator();

                var entriesResult = codeGen.GenerateEntries(context.Graph);
                if (entriesResult.IsFailure)
                {
                    log.LogError($"Generate failed: {entriesResult.Error}");
                    return Task.FromResult(false);
                }

                var csPath = Path.Combine(soPath, $"{context.GraphName}.cs");
                PathUtilities.EnsureDirectory(csPath);
                File.WriteAllText(csPath, entriesResult.Value);
                context.GeneratedFiles.Add(csPath);

                var dbResult = codeGen.GenerateDatabase(context.Graph);
                if (dbResult.IsFailure)
                {
                    log.LogError($"Generate failed: {dbResult.Error}");
                    return Task.FromResult(false);
                }

                var dbCsPath = Path.Combine(soPath, $"{context.GraphName}Database.cs");
                File.WriteAllText(dbCsPath, dbResult.Value);
                context.GeneratedFiles.Add(dbCsPath);

                log.LogInfo($"Generate: SO/{context.GraphName}.cs + SO/{context.GraphName}Database.cs");
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                log.LogError($"SO generate failed: {ex.Message}");
                return Task.FromResult(false);
            }
        }
    }
}
