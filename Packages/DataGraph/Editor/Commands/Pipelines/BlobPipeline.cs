using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DataGraph.Editor.CodeGen;
using DataGraph.Editor.IO;

namespace DataGraph.Editor.Commands.Pipelines
{
    /// <summary>
    /// Generates {GraphName}Blob.cs, {GraphName}BlobDatabase.cs, and
    /// {GraphName}BlobBuilder.cs into &lt;output&gt;/{GraphName}/Blob/.
    /// The .blob asset itself is built later by
    /// ParseGraphCommand.CreateBlobAssetsAsync once the generated builder
    /// type compiles.
    /// </summary>
    internal sealed class BlobPipeline : IOutputPipeline
    {
        public bool ShouldRun(PipelineContext context) => context.Formats.GenerateBlob;

        public Task<bool> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
        {
            var log = context.Log;
            try
            {
                log.LogInfo("Generate: creating Blob structs...");
                var blobPath = Path.Combine(context.GraphOutputPath, "Blob");
                var gen = new BlobCodeGenerator();

                var entriesResult = gen.GenerateEntries(context.Graph);
                if (entriesResult.IsFailure)
                {
                    log.LogError($"Blob generate failed: {entriesResult.Error}");
                    return Task.FromResult(false);
                }

                var blobCsPath = Path.Combine(blobPath, $"{context.GraphName}Blob.cs");
                PathUtilities.EnsureDirectory(blobCsPath);
                File.WriteAllText(blobCsPath, entriesResult.Value);
                context.GeneratedFiles.Add(blobCsPath);

                var dbResult = gen.GenerateDatabase(context.Graph);
                if (dbResult.IsFailure)
                {
                    log.LogError($"Blob generate failed: {dbResult.Error}");
                    return Task.FromResult(false);
                }

                var dbCsPath = Path.Combine(blobPath, $"{context.GraphName}BlobDatabase.cs");
                File.WriteAllText(dbCsPath, dbResult.Value);
                context.GeneratedFiles.Add(dbCsPath);

                var builderResult = gen.GenerateBuilder(context.Graph);
                if (builderResult.IsFailure)
                {
                    log.LogError($"Blob generate failed: {builderResult.Error}");
                    return Task.FromResult(false);
                }

                var builderCsPath = Path.Combine(blobPath, $"{context.GraphName}BlobBuilder.cs");
                File.WriteAllText(builderCsPath, builderResult.Value);
                context.GeneratedFiles.Add(builderCsPath);

                log.LogInfo($"Generate: Blob/{context.GraphName}Blob.cs + Blob/{context.GraphName}BlobDatabase.cs + Blob/{context.GraphName}BlobBuilder.cs");
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                log.LogError($"Blob generate failed: {ex.Message}");
                return Task.FromResult(false);
            }
        }
    }
}
