using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DataGraph.Editor.IO;
using DataGraph.Editor.Serialization;

namespace DataGraph.Editor.Commands.Pipelines
{
    /// <summary>
    /// Serializes the parsed data tree to JSON plus a JSON Schema sibling
    /// file in &lt;output&gt;/{GraphName}/JSON/. Schema generation failures
    /// log a warning but do not fail the pipeline.
    /// </summary>
    internal sealed class JsonPipeline : IOutputPipeline
    {
        public bool ShouldRun(PipelineContext context) => context.Formats.GenerateJSON;

        public Task<bool> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
        {
            var log = context.Log;
            try
            {
                log.LogInfo("Serialize: writing JSON output...");
                var jsonPath = Path.Combine(context.GraphOutputPath, "JSON");

                var jsonResult = new JsonDataSerializer().Serialize(context.DataTree);
                if (jsonResult.IsFailure)
                {
                    log.LogError($"Serialize failed: {jsonResult.Error}");
                    return Task.FromResult(false);
                }

                var jsonFilePath = Path.Combine(jsonPath, $"{context.GraphName}.json");
                PathUtilities.EnsureDirectory(jsonFilePath);
                File.WriteAllText(jsonFilePath, jsonResult.Value);
                context.GeneratedFiles.Add(jsonFilePath);

                var schemaResult = new JsonSchemaGenerator().Generate(context.Graph);
                if (schemaResult.IsSuccess)
                {
                    var schemaPath = Path.Combine(jsonPath, $"{context.GraphName}.schema.json");
                    File.WriteAllText(schemaPath, schemaResult.Value);
                    context.GeneratedFiles.Add(schemaPath);
                }

                log.LogInfo($"Serialize: JSON/{context.GraphName}.json + schema");
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                log.LogError($"JSON serialize failed: {ex.Message}");
                return Task.FromResult(false);
            }
        }
    }
}
