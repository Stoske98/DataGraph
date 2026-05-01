using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DataGraph.Editor.CodeGen;
using DataGraph.Editor.IO;

namespace DataGraph.Editor.Commands.Pipelines
{
    /// <summary>
    /// Generates {GraphName}QuantumDatabase.cs (entry + AssetObject) into
    /// Assets/QuantumUser/Simulation/DataGraph/{GraphName}/. The .asset
    /// itself is built later by ParseGraphCommand.CreateQuantumAssetsAsync
    /// once the generated types compile.
    /// </summary>
    internal sealed class QuantumPipeline : IOutputPipeline
    {
        public bool ShouldRun(PipelineContext context) => context.Formats.GenerateQuantum;

        public Task<bool> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
        {
            var log = context.Log;
            try
            {
                log.LogInfo("Generate: creating Quantum AssetObject classes...");
                var gen = new QuantumCodeGenerator();
                var result = gen.Generate(context.Graph);
                if (result.IsFailure)
                {
                    log.LogError($"Quantum generate failed: {result.Error}");
                    return Task.FromResult(false);
                }

                var quantumPath = $"Assets/QuantumUser/Simulation/DataGraph/{context.GraphName}";
                var csPath = Path.Combine(quantumPath, $"{context.GraphName}QuantumDatabase.cs");
                PathUtilities.EnsureDirectory(csPath);
                File.WriteAllText(csPath, result.Value);
                context.GeneratedFiles.Add(csPath);

                log.LogInfo($"Generate: QuantumUser/Simulation/DataGraph/{context.GraphName}/{context.GraphName}QuantumDatabase.cs");
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                log.LogError($"Quantum generate failed: {ex.Message}");
                return Task.FromResult(false);
            }
        }
    }
}
