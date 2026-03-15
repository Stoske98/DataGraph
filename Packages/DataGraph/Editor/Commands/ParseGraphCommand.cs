using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DataGraph.Editor.Adapter;
using DataGraph.Editor.CodeGen;
using DataGraph.Editor.Domain;
using DataGraph.Editor.Nodes;
using DataGraph.Editor.Public;
using DataGraph.Editor.Parsing;
using DataGraph.Editor.Serialization;
using DataGraph.Editor.UI;
using DataGraph.Runtime;
using UnityEditor;

namespace DataGraph.Editor.Commands
{
    /// <summary>
    /// Orchestrates the full parse pipeline for a single graph:
    /// Adapt -> Fetch -> Parse -> Validate -> Generate -> Serialize.
    /// Logs each step to a GraphLogGroup for console display.
    /// </summary>
    internal sealed class ParseGraphCommand
    {
        private readonly GraphModelAdapter _adapter = new();
        private readonly ParserEngine _parserEngine = new();
        private readonly DataValidator _dataValidator = new();

        /// <summary>
        /// Output format selection for a parse run.
        /// </summary>
        internal sealed class FormatSelection
        {
            public bool GenerateSO { get; set; }
            public bool GenerateJSON { get; set; }
        }

        /// <summary>
        /// Runs the full pipeline, logging each step to the provided log group.
        /// </summary>
        public async Task<bool> ExecuteAsync(
            DataGraphAsset graphAsset,
            ISheetProvider provider,
            FormatSelection formats,
            string outputBasePath,
            GraphLogGroup log,
            CancellationToken cancellationToken = default)
        {
            var graphName = !string.IsNullOrEmpty(graphAsset.GraphName)
                ? graphAsset.GraphName
                : graphAsset.name;

            try
            {
                // 1. Adapt
                log.LogInfo("Adapt: reading graph structure...");
                var adaptResult = _adapter.ReadGraph(graphAsset);
                if (adaptResult.IsFailure)
                {
                    log.LogError($"Adapt failed: {adaptResult.Error}");
                    log.Complete(false);
                    return false;
                }

                var graph = adaptResult.Value;
                log.LogInfo($"Adapt: {graph.AllNodes.Count} nodes resolved");

                // 2. Fetch
                log.LogInfo($"Fetch: requesting data from sheet...");
                var sheetRef = new SheetReference(
                    graph.SheetId, graph.HeaderRowOffset, graph.SheetName);
                var fetchResult = await provider.FetchAsync(sheetRef, cancellationToken);
                if (fetchResult.IsFailure)
                {
                    log.LogError($"Fetch failed: {fetchResult.Error}");
                    log.Complete(false);
                    return false;
                }

                var tableData = fetchResult.Value;
                log.LogInfo($"Fetch: {tableData.RowCount} data rows, {tableData.ColumnCount} columns");

                // 3. Parse
                log.LogInfo("Parse: processing table data...");
                var parseResult = _parserEngine.Parse(tableData, graph);
                if (parseResult.IsFailure)
                {
                    log.LogError($"Parse failed: {parseResult.Error}");
                    log.Complete(false);
                    return false;
                }

                var dataTree = parseResult.Value;
                foreach (var warning in dataTree.ParseWarnings)
                {
                    log.LogWarning($"Parse: {warning.Message}");
                }

                log.LogInfo("Parse: completed");

                // 4. Validate
                log.LogInfo("Validate: checking parsed data...");
                var report = _dataValidator.Validate(dataTree);
                foreach (var entry in report.Entries)
                {
                    switch (entry.Severity)
                    {
                        case ValidationSeverity.Error:
                            log.LogError($"Validate: {entry.Message}");
                            break;
                        case ValidationSeverity.Warning:
                            log.LogWarning($"Validate: {entry.Message}");
                            break;
                        case ValidationSeverity.Info:
                            log.LogInfo($"Validate: {entry.Message}");
                            break;
                    }
                }

                if (report.HasErrors)
                {
                    log.LogError("Validate: failed with errors, aborting output generation");
                    log.Complete(false);
                    return false;
                }

                var generatedFiles = new List<string>();

                // 5. Generate C# (if SO)
                if (formats.GenerateSO)
                {
                    log.LogInfo("Generate: creating C# classes...");
                    var codeGen = new CodeGenerator("SO");
                    var codeResult = codeGen.Generate(graph);
                    if (codeResult.IsFailure)
                    {
                        log.LogError($"Generate failed: {codeResult.Error}");
                        log.Complete(false);
                        return false;
                    }

                    var csPath = Path.Combine(outputBasePath, $"{graphName}.cs");
                    EnsureDirectory(csPath);
                    File.WriteAllText(csPath, codeResult.Value);
                    generatedFiles.Add(csPath);
                    log.LogInfo($"Generate: {graphName}.cs");
                }

                // 6. Serialize JSON
                if (formats.GenerateJSON)
                {
                    log.LogInfo("Serialize: writing JSON output...");
                    var jsonSerializer = new JsonDataSerializer();
                    var jsonResult = jsonSerializer.Serialize(dataTree);
                    if (jsonResult.IsFailure)
                    {
                        log.LogError($"Serialize failed: {jsonResult.Error}");
                        log.Complete(false);
                        return false;
                    }

                    var jsonPath = Path.Combine(outputBasePath, $"{graphName}.json");
                    EnsureDirectory(jsonPath);
                    File.WriteAllText(jsonPath, jsonResult.Value);
                    generatedFiles.Add(jsonPath);

                    var schemaGen = new JsonSchemaGenerator();
                    var schemaResult = schemaGen.Generate(graph);
                    if (schemaResult.IsSuccess)
                    {
                        var schemaPath = Path.Combine(outputBasePath, $"{graphName}.schema.json");
                        File.WriteAllText(schemaPath, schemaResult.Value);
                        generatedFiles.Add(schemaPath);
                    }

                    log.LogInfo($"Serialize: {graphName}.json + schema");
                }

                AssetDatabase.Refresh();

                log.LogSuccess($"Done: {generatedFiles.Count} file(s) generated");
                log.Complete(true);
                return true;
            }
            catch (OperationCanceledException)
            {
                log.LogWarning("Cancelled by user");
                log.Complete(false);
                return false;
            }
            catch (Exception ex)
            {
                log.LogError($"Unexpected error: {ex.Message}");
                log.Complete(false);
                return false;
            }
        }

        /// <summary>
        /// Runs only Adapt + Parse using cached data. For JSON Preview.
        /// Pass maxEntries=1 for first-element preview, 0 for full.
        /// </summary>
        public Result<ParsedDataTree> ParseFromCache(
            DataGraphAsset graphAsset,
            RawTableData cachedData,
            int maxEntries = 0)
        {
            var adaptResult = _adapter.ReadGraph(graphAsset);
            if (adaptResult.IsFailure)
                return Result<ParsedDataTree>.Failure(adaptResult.Error);

            return _parserEngine.Parse(cachedData, adaptResult.Value, maxEntries);
        }

        private static void EnsureDirectory(string filePath)
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
    }
}
