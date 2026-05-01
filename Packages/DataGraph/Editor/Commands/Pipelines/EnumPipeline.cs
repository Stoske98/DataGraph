using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DataGraph.Editor.CodeGen;
using DataGraph.Editor.Domain;
using DataGraph.Editor.IO;

namespace DataGraph.Editor.Commands.Pipelines
{
    /// <summary>
    /// Generates a single enum/flag .cs file from a graph whose root is
    /// a ParseableEnumRoot or ParseableFlagRoot. Routed by ParseGraphCommand
    /// in place of the SO/Blob/Quantum/JSON pipelines — enum graphs do not
    /// flow through DataValidator and never produce data files.
    /// </summary>
    internal sealed class EnumPipeline : IOutputPipeline
    {
        public bool ShouldRun(PipelineContext context)
            => context.Graph.Root is ParseableEnumRoot
            || context.Graph.Root is ParseableFlagRoot;

        public Task<bool> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
        {
            var log = context.Log;
            var isFlag = context.Graph.Root is ParseableFlagRoot;
            var kind = isFlag ? "Flag" : "Enum";

            try
            {
                if (!(context.DataTree?.Root is ParsedEnumDefinition enumDef))
                {
                    log.LogError($"{kind} pipeline received unexpected parse result.");
                    return Task.FromResult(false);
                }

                log.LogInfo($"Parse: {enumDef.Members.Count} members found");
                log.LogInfo($"Generate: creating {kind} source...");

                var genResult = new CodeGenerator().GenerateEnum(enumDef);
                if (genResult.IsFailure)
                {
                    log.LogError($"Generate failed: {genResult.Error}");
                    return Task.FromResult(false);
                }

                var subfolder = isFlag ? "Flags" : "Enums";
                string csPath;
                if (ProviderRegistry.IsQuantumAvailable())
                    csPath = $"Assets/QuantumUser/Simulation/DataGraph/{subfolder}/{enumDef.TypeName}.cs";
                else
                    csPath = Path.Combine(context.OutputBasePath, subfolder, $"{enumDef.TypeName}.cs");

                PathUtilities.EnsureDirectory(csPath);
                File.WriteAllText(csPath, genResult.Value);
                context.GeneratedFiles.Add(csPath);
                log.LogSuccess($"Generated: {csPath} ({enumDef.Members.Count} members)");
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                log.LogError($"{kind} pipeline failed: {ex.Message}");
                return Task.FromResult(false);
            }
        }
    }
}
